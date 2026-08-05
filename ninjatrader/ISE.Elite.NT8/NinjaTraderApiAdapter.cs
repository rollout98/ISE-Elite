using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISE.BrokerExecution;
using ISE.ExecutionCoordinator;
using ISE.NinjaTraderHost;
using ISE.PositionManager;
using NinjaTrader.Cbi;

namespace ISE.Elite.NinjaTrader8;

public sealed class NinjaTraderExecutionSnapshot
{
    public NinjaTraderExecutionSnapshot(string executionId, string platformOrderId, string instrument,
        int quantity, decimal price, string marketPosition, string orderAction, DateTime occurredAt)
    {
        ExecutionId = executionId ?? string.Empty;
        PlatformOrderId = platformOrderId ?? string.Empty;
        Instrument = instrument ?? string.Empty;
        Quantity = quantity;
        Price = price;
        MarketPosition = marketPosition ?? string.Empty;
        OrderAction = orderAction ?? string.Empty;
        OccurredAt = occurredAt;
    }

    public string ExecutionId { get; }
    public string PlatformOrderId { get; }
    public string Instrument { get; }
    public int Quantity { get; }
    public decimal Price { get; }
    public string MarketPosition { get; }
    public string OrderAction { get; }
    public DateTime OccurredAt { get; }
}

public sealed class NinjaTraderPositionSnapshot
{
    public NinjaTraderPositionSnapshot(string instrument, string marketPosition, int quantity,
        decimal averagePrice, DateTime occurredAt)
    {
        Instrument = instrument ?? string.Empty;
        MarketPosition = marketPosition ?? string.Empty;
        Quantity = quantity;
        AveragePrice = averagePrice;
        OccurredAt = occurredAt;
    }

    public string Instrument { get; }
    public string MarketPosition { get; }
    public int Quantity { get; }
    public decimal AveragePrice { get; }
    public DateTime OccurredAt { get; }
}

public sealed class NinjaTraderProtectiveSubmission
{
    public NinjaTraderProtectiveSubmission(string ocoGroup, string stopOrderId, string targetOrderId)
    {
        OcoGroup = ocoGroup;
        StopOrderId = stopOrderId;
        TargetOrderId = targetOrderId;
    }

    public string OcoGroup { get; }
    public string StopOrderId { get; }
    public string TargetOrderId { get; }
}

public sealed class NinjaTraderApiAdapter : INinjaTraderApi, IDisposable
{
    private readonly IseEliteNt8Options _options;
    private readonly object _sync = new object();
    private readonly Dictionary<string, Order> _ordersByPlatformId =
        new Dictionary<string, Order>(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<PlatformOrderUpdate> _pendingOrderUpdates =
        new ConcurrentQueue<PlatformOrderUpdate>();

    private Account? _account;
    private int _flushScheduled;
    private bool _started;

    public NinjaTraderApiAdapter(IseEliteNt8Options options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public event Action<PlatformOrderUpdate>? OrderUpdateReceived;
    public event Action<NinjaTraderExecutionSnapshot>? ExecutionReceived;
    public event Action<NinjaTraderPositionSnapshot>? PositionReceived;
    public event Action<string>? Diagnostic;

    public bool IsConnected => _account?.Connection?.Status == ConnectionStatus.Connected;

    public IReadOnlyCollection<string> AccountNames
    {
        get
        {
            lock (Account.All)
                return Account.All.Select(account => account.Name).ToArray();
        }
    }

    public void Start()
    {
        if (_started)
            throw new InvalidOperationException("The NinjaTrader API adapter is already started.");

        lock (Account.All)
            _account = Account.All.FirstOrDefault(account =>
                string.Equals(account.Name, _options.AccountName, StringComparison.OrdinalIgnoreCase));

        if (_account == null)
            throw new InvalidOperationException("The configured Sim101 account is unavailable.");
        if (_account.Connection == null || _account.Connection.Status != ConnectionStatus.Connected)
            throw new InvalidOperationException("The Sim101 account connection is not connected.");

        _account.OrderUpdate += OnOrderUpdate;
        _account.ExecutionUpdate += OnExecutionUpdate;
        _account.PositionUpdate += OnPositionUpdate;
        TrackExistingIseOrders();
        _started = true;
        Diagnostic?.Invoke("ISE Elite NT8 adapter subscribed to Sim101 order, execution, and position events.");
    }

    public void Stop()
    {
        if (!_started)
            return;

        if (_account != null)
        {
            _account.OrderUpdate -= OnOrderUpdate;
            _account.ExecutionUpdate -= OnExecutionUpdate;
            _account.PositionUpdate -= OnPositionUpdate;
        }

        lock (_sync)
            _ordersByPlatformId.Clear();

        _account = null;
        _started = false;
        Diagnostic?.Invoke("ISE Elite NT8 adapter stopped and unsubscribed from account events.");
    }

    public string Submit(string accountName, BrokerOrderCommand command)
    {
        EnsureStarted();
        if (!string.Equals(accountName, _options.AccountName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This bridge can submit only to Sim101.");
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var instrument = ResolveConfiguredInstrument(command.Instrument);
        var orderType = MapOrderType(command.OrderType);
        var limitPrice = orderType == OrderType.Limit
            ? Convert.ToDouble(command.EntryPrice ?? throw new InvalidOperationException("A limit order requires an entry price."))
            : 0d;
        var stopPrice = orderType == OrderType.StopMarket
            ? Convert.ToDouble(command.EntryPrice ?? throw new InvalidOperationException("A stop-market order requires an entry price."))
            : 0d;

        var order = _account!.CreateOrder(
            instrument,
            MapEntryOrderAction(command.Side),
            orderType,
            OrderEntry.Automated,
            TimeInForce.Day,
            command.Quantity,
            limitPrice,
            stopPrice,
            string.Empty,
            NormalizeSignalName(command.SignalName),
            NinjaTrader.Core.Globals.MaxDate,
            null);

        _account.Submit(new[] { order });
        var platformOrderId = RequireOrderId(order);
        TrackOrder(platformOrderId, order);

        Diagnostic?.Invoke($"Submitted {command.Side} {command.Quantity} {instrument.FullName} to Sim101 as {platformOrderId}.");
        return platformOrderId;
    }

    public NinjaTraderProtectiveSubmission SubmitProtectivePair(ProtectiveOrderPair pair)
    {
        EnsureStarted();
        if (pair == null) throw new ArgumentNullException(nameof(pair));
        if (!string.Equals(pair.Stop.AccountName, _options.AccountName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(pair.Target.AccountName, _options.AccountName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Protective orders can be submitted only to Sim101.");

        var instrument = ResolveConfiguredInstrument(pair.Stop.Instrument);
        if (!string.Equals(pair.Stop.Instrument, pair.Target.Instrument, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stop and target must use the same instrument.");

        var stop = _account!.CreateOrder(
            instrument,
            MapProtectiveOrderAction(pair.Stop.Side),
            OrderType.StopMarket,
            OrderEntry.Automated,
            TimeInForce.Day,
            pair.Stop.Quantity,
            0d,
            Convert.ToDouble(pair.Stop.Price),
            pair.OcoGroup,
            NormalizeSignalName(pair.Stop.SignalName),
            NinjaTrader.Core.Globals.MaxDate,
            null);

        var target = _account.CreateOrder(
            instrument,
            MapProtectiveOrderAction(pair.Target.Side),
            OrderType.Limit,
            OrderEntry.Automated,
            TimeInForce.Day,
            pair.Target.Quantity,
            Convert.ToDouble(pair.Target.Price),
            0d,
            pair.OcoGroup,
            NormalizeSignalName(pair.Target.SignalName),
            NinjaTrader.Core.Globals.MaxDate,
            null);

        _account.Submit(new[] { stop, target });

        var stopOrderId = RequireOrderId(stop);
        var targetOrderId = RequireOrderId(target);
        TrackOrder(stopOrderId, stop);
        TrackOrder(targetOrderId, target);

        Diagnostic?.Invoke(
            $"Submitted protective OCO {pair.OcoGroup}: stop={stopOrderId} at {pair.Stop.Price}; " +
            $"target={targetOrderId} at {pair.Target.Price}.");

        return new NinjaTraderProtectiveSubmission(pair.OcoGroup, stopOrderId, targetOrderId);
    }

    public void Cancel(string accountName, string platformOrderId)
    {
        EnsureStarted();
        if (!string.Equals(accountName, _options.AccountName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This bridge can cancel orders only on Sim101.");

        Order order;
        lock (_sync)
        {
            if (!_ordersByPlatformId.TryGetValue(platformOrderId, out order!))
                throw new KeyNotFoundException("The NinjaTrader order is not tracked by ISE Elite.");
        }

        if (!IsActiveOrder(order.OrderState))
            return;

        _account!.Cancel(new[] { order });
        Diagnostic?.Invoke($"Cancellation requested for NinjaTrader order {platformOrderId}.");
    }

    public void FlattenConfiguredInstrument()
    {
        EnsureStarted();
        var instrument = ResolveConfiguredInstrument(_options.InstrumentFullName);
        _account!.Flatten(new[] { instrument });
        Diagnostic?.Invoke($"Emergency flatten requested for {_options.InstrumentFullName} on Sim101.");
    }

    public decimal GetConfiguredTickSize()
    {
        EnsureStarted();
        return Convert.ToDecimal(ResolveConfiguredInstrument(_options.InstrumentFullName).MasterInstrument.TickSize);
    }

    public NinjaTraderPositionSnapshot GetCurrentPositionSnapshot()
    {
        EnsureStarted();
        var instrument = ResolveConfiguredInstrument(_options.InstrumentFullName);
        lock (_account!.Positions)
        {
            var position = _account.Positions.FirstOrDefault(candidate =>
                string.Equals(candidate.Instrument.FullName, instrument.FullName, StringComparison.OrdinalIgnoreCase));

            if (position == null)
                return new NinjaTraderPositionSnapshot(instrument.FullName, MarketPosition.Flat.ToString(), 0, 0m, DateTime.UtcNow);

            return new NinjaTraderPositionSnapshot(
                position.Instrument.FullName,
                position.MarketPosition.ToString(),
                position.Quantity,
                Convert.ToDecimal(position.AveragePrice),
                DateTime.UtcNow);
        }
    }

    public IReadOnlyCollection<BrokerWorkingOrder> GetWorkingProtectiveOrders()
    {
        EnsureStarted();
        var results = new List<BrokerWorkingOrder>();
        lock (_account!.Orders)
        {
            foreach (var order in _account.Orders)
            {
                if (!IsIseProtectiveOrder(order) || !IsActiveOrder(order.OrderState))
                    continue;

                var kind = order.Name.IndexOf("STOP", StringComparison.OrdinalIgnoreCase) >= 0
                    ? WorkingOrderKind.Stop
                    : WorkingOrderKind.Target;
                var side = order.OrderAction == OrderAction.Buy || order.OrderAction == OrderAction.BuyToCover
                    ? ExecutionSide.Buy
                    : ExecutionSide.Sell;
                var price = kind == WorkingOrderKind.Stop
                    ? Convert.ToDecimal(order.StopPrice)
                    : Convert.ToDecimal(order.LimitPrice);

                results.Add(new BrokerWorkingOrder(order.OrderId, kind, side, order.Quantity, price));
            }
        }

        return results;
    }

    public void Dispose() => Stop();

    private void OnOrderUpdate(object sender, OrderEventArgs e)
    {
        var platformOrderId = e.Order.OrderId;
        if (string.IsNullOrWhiteSpace(platformOrderId))
            return;

        if (IsIseOrder(e.Order))
            TrackOrder(platformOrderId, e.Order);

        if (!TryMapOrderState(e.Order.OrderState, out var state))
            return;

        var update = new PlatformOrderUpdate(
            platformOrderId,
            state,
            e.Order.Filled,
            Convert.ToDecimal(e.Order.AverageFillPrice),
            e.Order.OrderState.ToString(),
            DateTime.UtcNow);

        _pendingOrderUpdates.Enqueue(update);
        ScheduleOrderUpdateFlush();
    }

    private void OnExecutionUpdate(object sender, ExecutionEventArgs e)
    {
        var execution = e.Execution;
        var snapshot = new NinjaTraderExecutionSnapshot(
            execution?.ExecutionId ?? string.Empty,
            execution?.Order?.OrderId ?? string.Empty,
            execution?.Instrument?.FullName ?? string.Empty,
            e.Quantity,
            Convert.ToDecimal(e.Price),
            e.MarketPosition.ToString(),
            execution?.Order?.OrderAction.ToString() ?? string.Empty,
            DateTime.UtcNow);

        Task.Run(() => ExecutionReceived?.Invoke(snapshot));
    }

    private void OnPositionUpdate(object sender, PositionEventArgs e)
    {
        var snapshot = new NinjaTraderPositionSnapshot(
            e.Position.Instrument.FullName,
            e.MarketPosition.ToString(),
            e.Quantity,
            Convert.ToDecimal(e.AveragePrice),
            DateTime.UtcNow);

        Task.Run(() => PositionReceived?.Invoke(snapshot));
    }

    private void ScheduleOrderUpdateFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) != 0)
            return;

        Task.Delay(25).ContinueWith(_ => FlushOrderUpdates(), TaskScheduler.Default);
    }

    private void FlushOrderUpdates()
    {
        try
        {
            while (_pendingOrderUpdates.TryDequeue(out var update))
                OrderUpdateReceived?.Invoke(update);
        }
        finally
        {
            Interlocked.Exchange(ref _flushScheduled, 0);
            if (!_pendingOrderUpdates.IsEmpty)
                ScheduleOrderUpdateFlush();
        }
    }

    private void EnsureStarted()
    {
        if (!_started || _account == null)
            throw new InvalidOperationException("The NinjaTrader API adapter is not started.");
        if (_account.Connection == null || _account.Connection.Status != ConnectionStatus.Connected)
            throw new InvalidOperationException("The Sim101 account connection is no longer connected.");
    }

    private Instrument ResolveConfiguredInstrument(string requestedInstrument)
    {
        var instrumentName = _options.ResolveInstrument(requestedInstrument);
        return Instrument.GetInstrument(instrumentName)
            ?? throw new InvalidOperationException(
                $"NinjaTrader could not resolve '{instrumentName}'. Add the exact contract to the Instruments database first.");
    }

    private void TrackExistingIseOrders()
    {
        if (_account == null) return;
        lock (_account.Orders)
        {
            foreach (var order in _account.Orders.Where(IsIseOrder))
            {
                if (!string.IsNullOrWhiteSpace(order.OrderId))
                    TrackOrder(order.OrderId, order);
            }
        }
    }

    private void TrackOrder(string platformOrderId, Order order)
    {
        lock (_sync)
            _ordersByPlatformId[platformOrderId] = order;
    }

    private static bool IsIseOrder(Order order) =>
        order != null && !string.IsNullOrWhiteSpace(order.Name) &&
        order.Name.StartsWith("ISE-", StringComparison.OrdinalIgnoreCase);

    private static bool IsIseProtectiveOrder(Order order) =>
        IsIseOrder(order) && order.Name.StartsWith("ISE-PROTECT-", StringComparison.OrdinalIgnoreCase);

    private static bool IsActiveOrder(OrderState state) =>
        state == OrderState.Submitted || state == OrderState.Accepted ||
        state == OrderState.Working || state == OrderState.PartFilled;

    private static string RequireOrderId(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.OrderId))
            throw new InvalidOperationException("NinjaTrader did not assign an order ID after submission.");
        return order.OrderId;
    }

    private static OrderAction MapEntryOrderAction(ExecutionSide side) => side switch
    {
        ExecutionSide.Buy => OrderAction.Buy,
        ExecutionSide.Sell => OrderAction.SellShort,
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    private static OrderAction MapProtectiveOrderAction(ExecutionSide side) => side switch
    {
        ExecutionSide.Buy => OrderAction.BuyToCover,
        ExecutionSide.Sell => OrderAction.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    private static OrderType MapOrderType(ExecutionOrderType type) => type switch
    {
        ExecutionOrderType.Market => OrderType.Market,
        ExecutionOrderType.Limit => OrderType.Limit,
        ExecutionOrderType.StopMarket => OrderType.StopMarket,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static bool TryMapOrderState(OrderState state, out PlatformOrderState mapped)
    {
        switch (state)
        {
            case OrderState.Submitted:
                mapped = PlatformOrderState.Submitted;
                return true;
            case OrderState.Accepted:
            case OrderState.Working:
                mapped = PlatformOrderState.Accepted;
                return true;
            case OrderState.PartFilled:
                mapped = PlatformOrderState.PartiallyFilled;
                return true;
            case OrderState.Filled:
                mapped = PlatformOrderState.Filled;
                return true;
            case OrderState.Rejected:
                mapped = PlatformOrderState.Rejected;
                return true;
            case OrderState.Cancelled:
                mapped = PlatformOrderState.Cancelled;
                return true;
            default:
                mapped = default;
                return false;
        }
    }

    private static string NormalizeSignalName(string value)
    {
        var signalName = string.IsNullOrWhiteSpace(value) ? "ISE-Elite" : value.Trim();
        return signalName.Length <= 50 ? signalName : signalName.Substring(0, 50);
    }
}
