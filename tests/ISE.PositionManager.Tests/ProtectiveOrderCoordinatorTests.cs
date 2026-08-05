using System;
using ISE.ExecutionCoordinator;
using ISE.PositionManager;
using Xunit;

namespace ISE.PositionManager.Tests;

public sealed class ProtectiveOrderCoordinatorTests
{
    [Fact]
    public void Long_plan_creates_sell_stop_and_target_with_one_oco_group()
    {
        var coordinator = OpenLong();

        var pair = coordinator.Plan(29700m, 29850m, Now);

        Assert.Equal("OCO-1", pair.OcoGroup);
        Assert.Equal(ExecutionSide.Sell, pair.Stop.Side);
        Assert.Equal(ExecutionSide.Sell, pair.Target.Side);
        Assert.Equal(29700m, pair.Stop.Price);
        Assert.Equal(29850m, pair.Target.Price);
        Assert.Equal(pair.Stop.OcoGroup, pair.Target.OcoGroup);
    }

    [Fact]
    public void Short_plan_creates_buy_stop_and_target()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.SellShort, 1, 29750m));
        manager.Reconcile(Position(PositionSide.Short, 1, 29750m));
        var coordinator = new ProtectiveOrderCoordinator(manager, _ => "OCO-1");

        var pair = coordinator.Plan(29800m, 29650m, Now);

        Assert.Equal(ExecutionSide.Buy, pair.Stop.Side);
        Assert.Equal(ExecutionSide.Buy, pair.Target.Side);
    }

    [Fact]
    public void Submitted_orders_mark_position_protected()
    {
        var coordinator = OpenLong();
        coordinator.Plan(29700m, 29850m, Now);

        coordinator.RecordSubmitted(ProtectiveOrderKind.Stop, "STOP-1", Now);
        var result = coordinator.RecordSubmitted(ProtectiveOrderKind.Target, "TARGET-1", Now);

        Assert.Equal(PositionManagerStatus.Protected, result.Status);
        Assert.Equal("STOP-1", result.StopOrderId);
        Assert.Equal("TARGET-1", result.TargetOrderId);
    }

    [Fact]
    public void Filled_stop_identifies_target_as_sibling()
    {
        var coordinator = ProtectedLong();

        var transition = coordinator.HandleTransition(
            ProtectiveOrderKind.Stop, ProtectivePlatformOrderState.Filled, "STOP-1");

        Assert.Equal("TARGET-1", transition.SiblingOrderId);
        Assert.False(transition.EmergencyFlattenRequired);
    }

    [Fact]
    public void Rejected_stop_requires_emergency_flatten()
    {
        var coordinator = ProtectedLong();

        var transition = coordinator.HandleTransition(
            ProtectiveOrderKind.Stop, ProtectivePlatformOrderState.Rejected, "STOP-1");

        Assert.True(transition.EmergencyFlattenRequired);
        Assert.Equal("TARGET-1", transition.SiblingOrderId);
    }

    [Fact]
    public void Cancelled_protection_with_open_position_requires_flatten()
    {
        var coordinator = ProtectedLong();

        var transition = coordinator.HandleTransition(
            ProtectiveOrderKind.Target, ProtectivePlatformOrderState.Cancelled, "TARGET-1");

        Assert.True(transition.EmergencyFlattenRequired);
    }

    [Fact]
    public void Emergency_flatten_uses_authoritative_broker_quantity()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));
        manager.Reconcile(Position(PositionSide.Long, 2, 29750m));
        var coordinator = new ProtectiveOrderCoordinator(manager, _ => "OCO-1");

        var flatten = coordinator.CreateEmergencyFlatten("Protection failure", Now);

        Assert.Equal(ExecutionSide.Sell, flatten.Side);
        Assert.Equal(2, flatten.Quantity);
    }

    [Fact]
    public void Recovery_restores_protective_order_ids_for_sibling_handling()
    {
        var manager = Manager();
        var coordinator = new ProtectiveOrderCoordinator(manager, _ => "OCO-1");
        var working = new[]
        {
            new BrokerWorkingOrder("STOP-1", WorkingOrderKind.Stop, ExecutionSide.Sell, 1, 29700m),
            new BrokerWorkingOrder("TARGET-1", WorkingOrderKind.Target, ExecutionSide.Sell, 1, 29850m)
        };
        coordinator.Recover(Position(PositionSide.Long, 1, 29750m), working);

        var transition = coordinator.HandleTransition(
            ProtectiveOrderKind.Target, ProtectivePlatformOrderState.Filled, "TARGET-1");

        Assert.Equal("STOP-1", transition.SiblingOrderId);
        Assert.Equal(PositionManagerStatus.Protected, coordinator.Current.Status);
    }

    private static readonly DateTime Now = new DateTime(2026, 8, 5, 15, 0, 0, DateTimeKind.Utc);

    private static ProtectiveOrderCoordinator OpenLong()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));
        manager.Reconcile(Position(PositionSide.Long, 1, 29750m));
        return new ProtectiveOrderCoordinator(manager, _ => "OCO-1");
    }

    private static ProtectiveOrderCoordinator ProtectedLong()
    {
        var coordinator = OpenLong();
        coordinator.Plan(29700m, 29850m, Now);
        coordinator.RecordSubmitted(ProtectiveOrderKind.Stop, "STOP-1", Now);
        coordinator.RecordSubmitted(ProtectiveOrderKind.Target, "TARGET-1", Now);
        return coordinator;
    }

    private static AuthoritativePositionManager Manager() =>
        new AuthoritativePositionManager("Sim101", "MNQ 09-26");

    private static ExecutionFill Fill(string id, FillAction action, int quantity, decimal price) =>
        new ExecutionFill(id, "ORDER-" + id, "Sim101", "MNQ 09-26", action, quantity, price, Now);

    private static BrokerPositionSnapshot Position(PositionSide side, int quantity, decimal averagePrice) =>
        new BrokerPositionSnapshot("Sim101", "MNQ 09-26", side, quantity, averagePrice, Now);
}
