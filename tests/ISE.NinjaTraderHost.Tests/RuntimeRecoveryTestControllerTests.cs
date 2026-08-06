using System;
using ISE.NinjaTraderHost;
using ISE.PositionManager;
using Xunit;

namespace ISE.NinjaTraderHost.Tests;

public sealed class RuntimeRecoveryTestControllerTests
{
    [Fact]
    public void Arm_requires_fully_protected_position()
    {
        var controller = new RuntimeRecoveryTestController();

        Assert.Throws<InvalidOperationException>(() => controller.Arm(
            Snapshot(PositionManagerStatus.OpenUnprotected, null, null)));
    }

    [Fact]
    public void Same_position_and_order_ids_pass_recovery()
    {
        var controller = new RuntimeRecoveryTestController();
        var before = Snapshot(PositionManagerStatus.Protected, "STOP-1", "TARGET-1");

        controller.Arm(before);
        controller.BeginRestart(before);
        var passed = controller.ValidateRecovered(
            Snapshot(PositionManagerStatus.Protected, "STOP-1", "TARGET-1"));

        Assert.True(passed);
        Assert.Equal(RuntimeRecoveryTestState.Passed, controller.State);
    }

    [Fact]
    public void Changed_protective_ids_fail_recovery()
    {
        var controller = new RuntimeRecoveryTestController();
        var before = Snapshot(PositionManagerStatus.Protected, "STOP-1", "TARGET-1");

        controller.Arm(before);
        controller.BeginRestart(before);
        var passed = controller.ValidateRecovered(
            Snapshot(PositionManagerStatus.Protected, "STOP-2", "TARGET-2"));

        Assert.False(passed);
        Assert.Equal(RuntimeRecoveryTestState.Failed, controller.State);
        Assert.Contains("stop ID changed", controller.LastMessage, StringComparison.Ordinal);
        Assert.Contains("target ID changed", controller.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Position_change_after_arming_blocks_restart()
    {
        var controller = new RuntimeRecoveryTestController();
        var before = Snapshot(PositionManagerStatus.Protected, "STOP-1", "TARGET-1");
        controller.Arm(before);

        var changed = new PositionManagerSnapshot(
            "Sim101", "MNQ 09-26", PositionSide.Short, 2, -2, 29445.75m,
            PositionManagerStatus.Protected, null, "STOP-1", "TARGET-1");

        Assert.Throws<InvalidOperationException>(() => controller.BeginRestart(changed));
        Assert.Equal(RuntimeRecoveryTestState.Armed, controller.State);
    }

    private static PositionManagerSnapshot Snapshot(PositionManagerStatus status,
        string? stopOrderId, string? targetOrderId) =>
        new PositionManagerSnapshot(
            "Sim101",
            "MNQ 09-26",
            PositionSide.Short,
            1,
            -1,
            29445.75m,
            status,
            null,
            stopOrderId,
            targetOrderId);
}
