using System;
using System.Collections.Generic;
using ISE.ExecutionCoordinator;
using ISE.PositionManager;
using Xunit;

namespace ISE.PositionManager.Tests;

public sealed class AuthoritativePositionManagerTests
{
    [Fact]
    public void Long_fill_opens_unprotected_position()
    {
        var manager = Manager();

        var result = manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));

        Assert.Equal(PositionSide.Long, result.ExpectedSide);
        Assert.Equal(1, result.ExpectedQuantity);
        Assert.Equal(29750m, result.AveragePrice);
        Assert.Equal(PositionManagerStatus.OpenUnprotected, result.Status);
    }

    [Fact]
    public void Duplicate_fill_is_ignored()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));

        var result = manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));

        Assert.Equal(1, result.ExpectedQuantity);
        Assert.Equal("DuplicateFillIgnored", manager.History[1].EventName);
    }

    [Fact]
    public void Matching_broker_snapshot_reconciles()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));

        var result = manager.Reconcile(Position(PositionSide.Long, 1, 29750m));

        Assert.Equal(1, result.BrokerSignedQuantity);
        Assert.Equal(PositionManagerStatus.OpenUnprotected, result.Status);
    }

    [Fact]
    public void Broker_mismatch_requires_reconciliation()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));

        var result = manager.Reconcile(Position(PositionSide.Long, 2, 29750m));

        Assert.Equal(PositionManagerStatus.ReconciliationRequired, result.Status);
        Assert.Equal(2, result.BrokerSignedQuantity);
    }

    [Fact]
    public void Protective_plan_for_long_position_is_pending()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));
        manager.Reconcile(Position(PositionSide.Long, 1, 29750m));

        var plan = manager.CreateProtectivePlan(29700m, 29850m, Now);

        Assert.Equal(ExecutionSide.Sell, plan.ClosingSide);
        Assert.Equal(1, plan.Quantity);
        Assert.Equal(PositionManagerStatus.ProtectiveOrdersPending, manager.Current.Status);
    }

    [Fact]
    public void Protective_acknowledgements_mark_position_protected()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));
        manager.Reconcile(Position(PositionSide.Long, 1, 29750m));
        manager.CreateProtectivePlan(29700m, 29850m, Now);

        manager.RecordProtectiveOrder(ProtectiveOrderKind.Stop, "STOP-1", Now);
        var result = manager.RecordProtectiveOrder(ProtectiveOrderKind.Target, "TARGET-1", Now);

        Assert.Equal(PositionManagerStatus.Protected, result.Status);
        Assert.Equal("STOP-1", result.StopOrderId);
        Assert.Equal("TARGET-1", result.TargetOrderId);
    }

    [Fact]
    public void Emergency_flatten_uses_broker_quantity()
    {
        var manager = Manager();
        manager.ApplyFill(Fill("E1", FillAction.Buy, 1, 29750m));
        manager.Reconcile(Position(PositionSide.Long, 2, 29750m));

        var instruction = manager.CreateEmergencyFlatten("Broker mismatch safety flatten.", Now);

        Assert.Equal(ExecutionSide.Sell, instruction.Side);
        Assert.Equal(2, instruction.Quantity);
        Assert.Equal(PositionManagerStatus.Flattening, manager.Current.Status);
    }

    [Fact]
    public void Restart_recovery_detects_existing_protection()
    {
        var manager = Manager();
        var orders = new List<BrokerWorkingOrder>
        {
            new BrokerWorkingOrder("STOP-1", WorkingOrderKind.Stop, ExecutionSide.Sell, 1, 29700m),
            new BrokerWorkingOrder("TARGET-1", WorkingOrderKind.Target, ExecutionSide.Sell, 1, 29850m)
        };

        var result = manager.Recover(Position(PositionSide.Long, 1, 29750m), orders);

        Assert.Equal(PositionManagerStatus.Protected, result.Status);
        Assert.Equal("STOP-1", result.StopOrderId);
        Assert.Equal("TARGET-1", result.TargetOrderId);
        Assert.Equal(1, result.ExpectedQuantity);
    }

    private static readonly DateTime Now = new DateTime(2026, 8, 5, 14, 0, 0, DateTimeKind.Utc);

    private static AuthoritativePositionManager Manager() =>
        new AuthoritativePositionManager("Sim101", "MNQ 09-26");

    private static ExecutionFill Fill(string id, FillAction action, int quantity, decimal price) =>
        new ExecutionFill(id, "ORDER-" + id, "Sim101", "MNQ 09-26", action, quantity, price, Now);

    private static BrokerPositionSnapshot Position(PositionSide side, int quantity, decimal averagePrice) =>
        new BrokerPositionSnapshot("Sim101", "MNQ 09-26", side, quantity, averagePrice, Now);
}
