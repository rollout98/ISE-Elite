using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ISE.BrokerExecution;
using ISE.ExecutionCoordinator;
using ISE.NinjaTraderHost;
using NinjaTrader.Cbi;

namespace ISE.Elite.NinjaTrader8;

public sealed class NinjaTraderExecutionSnapshot
{
    public NinjaTraderExecutionSnapshot(string executionId, string platformOrderId, string instrument,
        int quantity, decimal price, string marketPosition, DateTime occurredAt)
    {
        ExecutionId = executionId ?? string.Empty;
        PlatformOrderId = platformOrderId ?? string.Empty;
        Instrument = instrument ?? string.Empty;
        Quantity = quantity;
        Price = price;
        MarketPosition = marketPosition ?? string.Empty;
        OccurredAt = occurredAt;
    }

    public string ExecutionId { get; }
    public string PlatformOrderId { get; }
    public string Instrument { get; }
    public int Quantity { get; }
    public decimal Price { get; }
    public string MarketPosition { get; }
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

        var instrumentName = _options.ResolveInstrument(command.Instrument);
        var instrument = Instrument.GetInstrument(instrumentName)
            ?? throw new InvalidOperationException(
                $"NinjaTrader could not resolve '{instrumentName}'. Add the exact contract to the Instruments database first.");

        var orderType = MapOrderType(command.OrderType);
        var limitPrice = orderType == OrderType.Limit
            ? Convert.ToDouble(command.EntryPrice ?? throw new InvalidOperationException("A limit order requires an entry price."))
            : 0d;
        var stopPrice = orderType == OrderType.StopMarket
            ? Convert.ToDouble(command.EntryPrice ?? throw new InvalidOperationException("A stop-market order requires an entry price."))
            : 0d;

        var order = _account!.CreateOrder(
            instrument,
            MapOrderAction(command.Side),
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

        var platformOrderId = order.OrderId;
        if (string.IsNullOrWhiteSpace(platformOrderId))
            throw new InvalidOperationException("NinjaTrader did not assign an order ID after submission.");

        lock (_sync)
        {
            if (_ordersByPlatformId.ContainsKey(platformOrderId))
                throw new InvalidOperationException("NinjaTrader returned a duplicate order ID.");
            _ordersByPlatformId.Add(platformOrderId, order);
        }

        Diagnostic?.Invoke($"Submitted {command.Side} {command.Quantity} {instrument.FullName} to Sim101 as {platformOrderId}.");
        return platformOrderId;
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

        _account!.Cancel(new[] { order });
        Diagnostic?.Invoke($"Cancellation requested for NinjaTrader order {platformOrderId}.");
    }

    public void Dispose() => Stop();

    private void OnOrderUpdate(object sender, OrderEventArgs e)
    {
        if (!TryMapOrderState(e.Order.OrderState, out var state))
            return;

        var platformOrderId = e.Order.OrderId;
        if (string.IsNullOrWhiteSpace(platformOrderId))
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

    private static OrderAction MapOrderAction(ExecutionSide side) => side switch
    {
        ExecutionSide.Buy => OrderAction.Buy,
        ExecutionSide.Sell => OrderAction.SellShort,
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
