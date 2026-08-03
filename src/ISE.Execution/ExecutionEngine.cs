using System;
using System.Collections.Generic;
using ISE.TradePlanning;

namespace ISE.Execution;

/// <summary>Creates execution commands and applies deterministic order lifecycle transitions.</summary>
public sealed class ExecutionEngine
{
    private readonly HashSet<Guid> _submittedTradePlans = new HashSet<Guid>();

    /// <summary>Creates entry, protective-stop, and profit-target orders for an approved trade plan.</summary>
    public ExecutionCommandSet CreateCommands(Guid tradePlanId, TradePlan tradePlan)
    {
        if (tradePlan is null) throw new ArgumentNullException(nameof(tradePlan));
        if (tradePlanId == Guid.Empty)
            return RejectedSet(tradePlanId, ExecutionResultReason.InvalidTradePlanId);
        if (!tradePlan.Approved)
            return RejectedSet(tradePlanId, ExecutionResultReason.TradePlanNotApproved);
        if (!_submittedTradePlans.Add(tradePlanId))
            return RejectedSet(tradePlanId, ExecutionResultReason.DuplicateTradePlan);

        var entrySide = tradePlan.Direction == TradeDirection.Long ? ExecutionSide.Buy : ExecutionSide.Sell;
        var exitSide = entrySide == ExecutionSide.Buy ? ExecutionSide.Sell : ExecutionSide.Buy;

        var orders = new List<ExecutionOrder>
        {
            NewOrder(tradePlanId, ExecutionOrderRole.Entry, entrySide, tradePlan.Contracts, tradePlan.EntryPrice, ExecutionOrderState.PendingSubmission),
            NewOrder(tradePlanId, ExecutionOrderRole.ProtectiveStop, exitSide, tradePlan.Contracts, tradePlan.StopPrice, ExecutionOrderState.Held),
            NewOrder(tradePlanId, ExecutionOrderRole.ProfitTarget, exitSide, tradePlan.Contracts, tradePlan.TargetPrice, ExecutionOrderState.Held)
        };

        return new ExecutionCommandSet(true, ExecutionResultReason.Accepted, tradePlanId, orders);
    }

    /// <summary>Marks a pending or held order as accepted by an execution platform.</summary>
    public ExecutionTransitionResult Accept(ExecutionOrder order, string platformOrderId)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));
        if (string.IsNullOrWhiteSpace(platformOrderId)) throw new ArgumentException("Platform order ID is required.", nameof(platformOrderId));
        if (order.State != ExecutionOrderState.PendingSubmission && order.State != ExecutionOrderState.Held)
            return RejectedTransition(order, ExecutionResultReason.InvalidStateTransition);

        return AcceptedTransition(Clone(order, ExecutionOrderState.Working, order.FilledQuantity, platformOrderId, null));
    }

    /// <summary>Applies a cumulative fill increment to a working or partially filled order.</summary>
    public ExecutionTransitionResult ApplyFill(ExecutionOrder order, int fillQuantity)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));
        if (order.State != ExecutionOrderState.Working && order.State != ExecutionOrderState.PartiallyFilled)
            return RejectedTransition(order, ExecutionResultReason.InvalidStateTransition);
        if (fillQuantity <= 0 || order.FilledQuantity + fillQuantity > order.Quantity)
            return RejectedTransition(order, ExecutionResultReason.InvalidFillQuantity);

        var cumulative = order.FilledQuantity + fillQuantity;
        var state = cumulative == order.Quantity ? ExecutionOrderState.Filled : ExecutionOrderState.PartiallyFilled;
        return AcceptedTransition(Clone(order, state, cumulative, order.PlatformOrderId, null));
    }

    /// <summary>Marks an active order as cancelled.</summary>
    public ExecutionTransitionResult Cancel(ExecutionOrder order, string? message = null)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));
        if (order.State != ExecutionOrderState.PendingSubmission && order.State != ExecutionOrderState.Held && order.State != ExecutionOrderState.Working && order.State != ExecutionOrderState.PartiallyFilled)
            return RejectedTransition(order, ExecutionResultReason.InvalidStateTransition);

        return AcceptedTransition(Clone(order, ExecutionOrderState.Cancelled, order.FilledQuantity, order.PlatformOrderId, message));
    }

    /// <summary>Marks an order as rejected by the execution platform.</summary>
    public ExecutionTransitionResult Reject(ExecutionOrder order, string? message = null)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));
        if (order.State == ExecutionOrderState.Filled || order.State == ExecutionOrderState.Cancelled)
            return RejectedTransition(order, ExecutionResultReason.InvalidStateTransition);

        var rejected = Clone(order, ExecutionOrderState.Rejected, order.FilledQuantity, order.PlatformOrderId, message);
        return new ExecutionTransitionResult(true, ExecutionResultReason.PlatformRejected, rejected);
    }

    /// <summary>Marks an order as failed because of a platform or transport error.</summary>
    public ExecutionTransitionResult Fail(ExecutionOrder order, string? message = null)
    {
        if (order is null) throw new ArgumentNullException(nameof(order));
        if (order.State == ExecutionOrderState.Filled || order.State == ExecutionOrderState.Cancelled)
            return RejectedTransition(order, ExecutionResultReason.InvalidStateTransition);

        var failed = Clone(order, ExecutionOrderState.Failed, order.FilledQuantity, order.PlatformOrderId, message);
        return new ExecutionTransitionResult(true, ExecutionResultReason.PlatformFailure, failed);
    }

    private static ExecutionOrder NewOrder(Guid tradePlanId, ExecutionOrderRole role, ExecutionSide side, int quantity, decimal price, ExecutionOrderState state) =>
        new ExecutionOrder(Guid.NewGuid(), tradePlanId, role, side, quantity, price, state, 0, null, null);

    private static ExecutionOrder Clone(ExecutionOrder order, ExecutionOrderState state, int filledQuantity, string? platformOrderId, string? message) =>
        new ExecutionOrder(order.OrderId, order.TradePlanId, order.Role, order.Side, order.Quantity, order.Price, state, filledQuantity, platformOrderId, message);

    private static ExecutionCommandSet RejectedSet(Guid tradePlanId, ExecutionResultReason reason) =>
        new ExecutionCommandSet(false, reason, tradePlanId, Array.Empty<ExecutionOrder>());

    private static ExecutionTransitionResult AcceptedTransition(ExecutionOrder order) =>
        new ExecutionTransitionResult(true, ExecutionResultReason.Accepted, order);

    private static ExecutionTransitionResult RejectedTransition(ExecutionOrder order, ExecutionResultReason reason) =>
        new ExecutionTransitionResult(false, reason, order);
}
