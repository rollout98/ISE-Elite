using System;
using System.Collections.Generic;
using ISE.Runtime;

namespace ISE.ExecutionCoordinator;

public enum ExecutionSide { Buy, Sell }
public enum ExecutionOrderType { Market, Limit, StopMarket }
public enum ExecutionState { Blocked, Validated, Queued, Submitted, Accepted, PartiallyFilled, Filled, Rejected, Cancelled, Completed }

public sealed class ExecutionRequest
{
    public ExecutionRequest(string requestId, string strategyId, string symbol, ExecutionSide side, int quantity,
        ExecutionOrderType orderType, decimal? entryPrice, decimal stopPrice, decimal targetPrice,
        DateTime createdAt, string explanationId)
    {
        RequestId = string.IsNullOrWhiteSpace(requestId) ? throw new ArgumentException("Request ID is required.", nameof(requestId)) : requestId;
        StrategyId = string.IsNullOrWhiteSpace(strategyId) ? throw new ArgumentException("Strategy ID is required.", nameof(strategyId)) : strategyId;
        Symbol = string.IsNullOrWhiteSpace(symbol) ? throw new ArgumentException("Symbol is required.", nameof(symbol)) : symbol;
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (stopPrice <= 0m) throw new ArgumentOutOfRangeException(nameof(stopPrice));
        if (targetPrice <= 0m) throw new ArgumentOutOfRangeException(nameof(targetPrice));
        ExplanationId = string.IsNullOrWhiteSpace(explanationId) ? throw new ArgumentException("Explanation ID is required.", nameof(explanationId)) : explanationId;
        RequestId = requestId; StrategyId = strategyId; Symbol = symbol; Side = side; Quantity = quantity;
        OrderType = orderType; EntryPrice = entryPrice; StopPrice = stopPrice; TargetPrice = targetPrice;
        CreatedAt = createdAt; ExplanationId = explanationId;
    }
    public string RequestId { get; }
    public string StrategyId { get; }
    public string Symbol { get; }
    public ExecutionSide Side { get; }
    public int Quantity { get; }
    public ExecutionOrderType OrderType { get; }
    public decimal? EntryPrice { get; }
    public decimal StopPrice { get; }
    public decimal TargetPrice { get; }
    public DateTime CreatedAt { get; }
    public string ExplanationId { get; }
}

public sealed class ExecutionRecord
{
    public ExecutionRecord(ExecutionRequest request, ExecutionState state, string reason, DateTime occurredAt)
    { Request = request; State = state; Reason = reason; OccurredAt = occurredAt; }
    public ExecutionRequest Request { get; }
    public ExecutionState State { get; }
    public string Reason { get; }
    public DateTime OccurredAt { get; }
}

public sealed class ExecutionDecision
{
    public ExecutionDecision(bool accepted, ExecutionState state, ExecutionRequest? request, string reason)
    { Accepted = accepted; State = state; Request = request; Reason = reason; }
    public bool Accepted { get; }
    public ExecutionState State { get; }
    public ExecutionRequest? Request { get; }
    public string Reason { get; }
}

public sealed class ExecutionCoordinator
{
    private readonly Queue<ExecutionRequest> _queue = new Queue<ExecutionRequest>();
    private readonly HashSet<string> _requestIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ExecutionRecord> _history = new List<ExecutionRecord>();

    public IReadOnlyCollection<ExecutionRequest> Queue => _queue.ToArray();
    public IReadOnlyList<ExecutionRecord> History => _history.AsReadOnly();

    public ExecutionDecision Submit(ExecutionRequest request, RuntimeState runtimeState, bool sessionAllowsEntry, bool riskApproved)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (runtimeState != RuntimeState.SeekingEntry)
            return Block(request, "Runtime is not seeking an entry.");
        if (!sessionAllowsEntry)
            return Block(request, "Session controls do not allow a new entry.");
        if (!riskApproved)
            return Block(request, "Authoritative risk approval was denied.");
        if (!_requestIds.Add(request.RequestId))
            return Block(request, "Duplicate execution request was ignored.");

        Record(request, ExecutionState.Validated, "Execution request passed all validation gates.");
        _queue.Enqueue(request);
        Record(request, ExecutionState.Queued, "Execution request entered the dispatch queue.");
        return new ExecutionDecision(true, ExecutionState.Queued, request, "Execution request queued.");
    }

    public ExecutionRequest DispatchNext(DateTime occurredAt)
    {
        if (_queue.Count == 0) throw new InvalidOperationException("No execution request is queued.");
        var request = _queue.Dequeue();
        Record(request, ExecutionState.Submitted, "Execution request was dispatched to the broker adapter.", occurredAt);
        return request;
    }

    public void RecordBrokerUpdate(ExecutionRequest request, ExecutionState state, string reason, DateTime occurredAt)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (state != ExecutionState.Accepted && state != ExecutionState.PartiallyFilled &&
            state != ExecutionState.Filled && state != ExecutionState.Rejected &&
            state != ExecutionState.Cancelled && state != ExecutionState.Completed)
            throw new ArgumentOutOfRangeException(nameof(state));
        Record(request, state, reason, occurredAt);
    }

    private ExecutionDecision Block(ExecutionRequest request, string reason)
    {
        Record(request, ExecutionState.Blocked, reason);
        return new ExecutionDecision(false, ExecutionState.Blocked, request, reason);
    }

    private void Record(ExecutionRequest request, ExecutionState state, string reason, DateTime? occurredAt = null)
        => _history.Add(new ExecutionRecord(request, state, reason, occurredAt ?? request.CreatedAt));
}
