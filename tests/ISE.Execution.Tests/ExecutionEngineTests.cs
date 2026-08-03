using System;
using System.Linq;
using ISE.Execution;
using ISE.TradePlanning;
using Xunit;

namespace ISE.Execution.Tests;

public sealed class ExecutionEngineTests
{
    [Fact]
    public void Approved_long_plan_creates_entry_stop_and_target_orders()
    {
        var engine = new ExecutionEngine();
        var planId = Guid.NewGuid();
        var plan = ApprovedPlan(TradeDirection.Long);

        var result = engine.CreateCommands(planId, plan);

        Assert.True(result.Accepted);
        Assert.Equal(3, result.Orders.Count);
        var entry = result.Orders.Single(x => x.Role == ExecutionOrderRole.Entry);
        var stop = result.Orders.Single(x => x.Role == ExecutionOrderRole.ProtectiveStop);
        Assert.Equal(ExecutionSide.Buy, entry.Side);
        Assert.Equal(ExecutionSide.Sell, stop.Side);
        Assert.Equal(ExecutionOrderState.PendingSubmission, entry.State);
        Assert.Equal(ExecutionOrderState.Held, stop.State);
    }

    [Fact]
    public void Duplicate_trade_plan_is_rejected()
    {
        var engine = new ExecutionEngine();
        var planId = Guid.NewGuid();
        var plan = ApprovedPlan(TradeDirection.Short);

        engine.CreateCommands(planId, plan);
        var duplicate = engine.CreateCommands(planId, plan);

        Assert.False(duplicate.Accepted);
        Assert.Equal(ExecutionResultReason.DuplicateTradePlan, duplicate.Reason);
        Assert.Empty(duplicate.Orders);
    }

    [Fact]
    public void Working_order_tracks_partial_and_complete_fills()
    {
        var engine = new ExecutionEngine();
        var order = engine.CreateCommands(Guid.NewGuid(), ApprovedPlan(TradeDirection.Long)).Orders[0];
        var accepted = engine.Accept(order, "NT-1001");
        var partial = engine.ApplyFill(accepted.Order, 1);
        var complete = engine.ApplyFill(partial.Order, 1);

        Assert.True(accepted.Accepted);
        Assert.Equal(ExecutionOrderState.Working, accepted.Order.State);
        Assert.Equal(ExecutionOrderState.PartiallyFilled, partial.Order.State);
        Assert.Equal(1, partial.Order.FilledQuantity);
        Assert.Equal(ExecutionOrderState.Filled, complete.Order.State);
        Assert.Equal(2, complete.Order.FilledQuantity);
    }

    [Fact]
    public void Platform_rejection_preserves_order_identity_and_reason()
    {
        var engine = new ExecutionEngine();
        var order = engine.CreateCommands(Guid.NewGuid(), ApprovedPlan(TradeDirection.Short)).Orders[0];

        var rejected = engine.Reject(order, "Account connection unavailable.");

        Assert.True(rejected.Accepted);
        Assert.Equal(ExecutionResultReason.PlatformRejected, rejected.Reason);
        Assert.Equal(ExecutionOrderState.Rejected, rejected.Order.State);
        Assert.Equal(order.OrderId, rejected.Order.OrderId);
        Assert.Equal("Account connection unavailable.", rejected.Order.Message);
    }

    private static TradePlan ApprovedPlan(TradeDirection direction) =>
        new TradePlan(true, TradePlanReason.Planned, direction, EntryOrderType.Market, 2, 20000m, direction == TradeDirection.Long ? 19975m : 20025m, direction == TradeDirection.Long ? 20050m : 19950m, 2m);
}
