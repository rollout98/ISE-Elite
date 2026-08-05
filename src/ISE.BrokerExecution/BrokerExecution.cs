using System;
using System.Collections.Generic;
using ISE.ExecutionCoordinator;
using ISE.NinjaTraderAdapter;

namespace ISE.BrokerExecution;

public enum BrokerOrderState { Submitted, Accepted, PartiallyFilled, Filled, Rejected, Cancelled }

public sealed class BrokerOrderCommand
{
    public BrokerOrderCommand(string requestId, string instrument, ExecutionSide side, ExecutionOrderType orderType,
        int quantity, decimal? entryPrice, decimal stopPrice, decimal targetPrice, string signalName)
    {
        RequestId = requestId; Instrument = instrument; Side = side; OrderType = orderType; Quantity = quantity;
        EntryPrice = entryPrice; StopPrice = stopPrice; TargetPrice = targetPrice; SignalName = signalName;
    }
    public string RequestId { get; }
    public string Instrument { get; }
    public ExecutionSide Side { get; }
    public ExecutionOrderType OrderType { get; }
    public int Quantity { get; }
    public decimal? EntryPrice { get; }
    public decimal StopPrice { get; }
    public decimal TargetPrice { get; }
    public string SignalName { get; }
}

public sealed class BrokerOrderEvent
{
    public BrokerOrderEvent(string requestId, string platformOrderId, BrokerOrderState state, int filledQuantity,
        decimal averageFillPrice, string message, DateTime occurredAt)
    {
        RequestId = requestId; PlatformOrderId = platformOrderId; State = state; FilledQuantity = filledQuantity;
        AverageFillPrice = averageFillPrice; Message = message; OccurredAt = occurredAt;
    }
    public string RequestId { get; }
    public string PlatformOrderId { get; }
    public BrokerOrderState State { get; }
    public int FilledQuantity { get; }
    public decimal AverageFillPrice { get; }
    public string Message { get; }
    public DateTime OccurredAt { get; }
}

public interface IExecutionBroker
{
    BrokerOrderEvent Submit(ExecutionRequest request, DateTime occurredAt);
    BrokerOrderEvent Cancel(string requestId, DateTime occurredAt);
}

public interface INinjaTraderTransport
{
    string Submit(BrokerOrderCommand command);
    void Cancel(string platformOrderId);
}

public sealed class NinjaTraderExecutionBroker : IExecutionBroker
{
    private readonly INinjaTraderTransport _transport;
    private readonly NinjaTraderInstrumentMapper _mapper;
    private readonly Dictionary<string, string> _platformByRequest = new Dictionary<string, string>(StringComparer.Ordinal);

    public NinjaTraderExecutionBroker(INinjaTraderTransport transport, NinjaTraderInstrumentMapper mapper)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public BrokerOrderEvent Submit(ExecutionRequest request, DateTime occurredAt)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (_platformByRequest.ContainsKey(request.RequestId))
            throw new InvalidOperationException("Execution request has already been submitted.");
        if (!_mapper.TryMap(request.Symbol, out var instrument))
            throw new InvalidOperationException("Unsupported NinjaTrader instrument.");

        var command = new BrokerOrderCommand(request.RequestId, instrument, request.Side, request.OrderType,
            request.Quantity, request.EntryPrice, request.StopPrice, request.TargetPrice, "ISE-" + request.StrategyId);
        var platformOrderId = _transport.Submit(command);
        if (string.IsNullOrWhiteSpace(platformOrderId))
            throw new InvalidOperationException("NinjaTrader did not return an order identifier.");

        _platformByRequest.Add(request.RequestId, platformOrderId);
        return new BrokerOrderEvent(request.RequestId, platformOrderId, BrokerOrderState.Submitted, 0, 0m,
            "Order submitted to NinjaTrader.", occurredAt);
    }

    public BrokerOrderEvent Cancel(string requestId, DateTime occurredAt)
    {
        if (!_platformByRequest.TryGetValue(requestId, out var platformOrderId))
            throw new KeyNotFoundException("Execution request is not correlated to a NinjaTrader order.");
        _transport.Cancel(platformOrderId);
        return new BrokerOrderEvent(requestId, platformOrderId, BrokerOrderState.Cancelled, 0, 0m,
            "Cancellation sent to NinjaTrader.", occurredAt);
    }

    public BrokerOrderEvent Normalize(string requestId, BrokerOrderState state, int filledQuantity,
        decimal averageFillPrice, string message, DateTime occurredAt)
    {
        if (!_platformByRequest.TryGetValue(requestId, out var platformOrderId))
            throw new KeyNotFoundException("Execution request is not correlated to a NinjaTrader order.");
        return new BrokerOrderEvent(requestId, platformOrderId, state, filledQuantity, averageFillPrice,
            message ?? string.Empty, occurredAt);
    }
}
