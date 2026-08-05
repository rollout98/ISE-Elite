using System;
using ISE.BrokerExecution;
using ISE.ExecutionCoordinator;
using Xunit;

namespace ISE.NinjaTraderHost.Tests;

public sealed class ProtectedFillTestControllerTests
{
    private static readonly DateTime Now = new DateTime(2026, 8, 5, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Disabled_test_blocks_arming()
    {
        var controller = new ProtectedFillTestController(new FakeBroker(), false);
        Assert.Throws<InvalidOperationException>(() => controller.Arm(
            ProtectedFillTestController.ConfirmationPhrase, true, true, Now));
    }

    [Fact]
    public void Invalid_confirmation_blocks_arming()
    {
        var controller = Controller();
        Assert.Throws<InvalidOperationException>(() => controller.Arm("WRONG", true, true, Now));
    }

    [Fact]
    public void Open_position_blocks_arming()
    {
        var controller = Controller();
        Assert.Throws<InvalidOperationException>(() => controller.Arm(
            ProtectedFillTestController.ConfirmationPhrase, false, true, Now));
    }

    [Fact]
    public void Protection_must_be_enabled_before_arming()
    {
        var controller = Controller();
        Assert.Throws<InvalidOperationException>(() => controller.Arm(
            ProtectedFillTestController.ConfirmationPhrase, true, false, Now));
    }

    [Fact]
    public void Armed_test_submits_exactly_one_market_buy()
    {
        var broker = new FakeBroker();
        var controller = new ProtectedFillTestController(broker, true);
        controller.Arm(ProtectedFillTestController.ConfirmationPhrase, true, true, Now);

        controller.SubmitMarketBuy(true, Now);

        Assert.Equal(1, broker.SubmitCount);
        Assert.Equal(ExecutionSide.Buy, broker.LastRequest!.Side);
        Assert.Equal(ExecutionOrderType.Market, broker.LastRequest.OrderType);
        Assert.Equal(1, broker.LastRequest.Quantity);
        Assert.Equal(ProtectedFillTestState.Submitted, controller.State);
    }

    [Fact]
    public void Second_submission_is_blocked()
    {
        var controller = Controller();
        controller.Arm(ProtectedFillTestController.ConfirmationPhrase, true, true, Now);
        controller.SubmitMarketBuy(true, Now);

        Assert.Throws<InvalidOperationException>(() => controller.SubmitMarketBuy(true, Now));
    }

    [Fact]
    public void Filled_entry_waits_for_protection_then_becomes_protected()
    {
        var controller = Controller();
        controller.Arm(ProtectedFillTestController.ConfirmationPhrase, true, true, Now);
        var submitted = controller.SubmitMarketBuy(true, Now);

        controller.HandleBrokerEvent(new BrokerOrderEvent(submitted.RequestId, submitted.PlatformOrderId,
            BrokerOrderState.Filled, 1, 29750m, "Filled", Now));
        Assert.Equal(ProtectedFillTestState.FilledAwaitingProtection, controller.State);

        controller.MarkProtected("STOP-1", "TARGET-1", Now);
        Assert.Equal(ProtectedFillTestState.Protected, controller.State);
    }

    [Fact]
    public void Flat_after_protection_completes_test()
    {
        var controller = Controller();
        controller.Arm(ProtectedFillTestController.ConfirmationPhrase, true, true, Now);
        var submitted = controller.SubmitMarketBuy(true, Now);
        controller.HandleBrokerEvent(new BrokerOrderEvent(submitted.RequestId, submitted.PlatformOrderId,
            BrokerOrderState.Filled, 1, 29750m, "Filled", Now));
        controller.MarkProtected("STOP-1", "TARGET-1", Now);

        controller.MarkCompleted(Now);

        Assert.Equal(ProtectedFillTestState.Completed, controller.State);
    }

    private static ProtectedFillTestController Controller() =>
        new ProtectedFillTestController(new FakeBroker(), true);

    private sealed class FakeBroker : IExecutionBroker
    {
        public int SubmitCount { get; private set; }
        public ExecutionRequest? LastRequest { get; private set; }

        public BrokerOrderEvent Submit(ExecutionRequest request, DateTime occurredAt)
        {
            SubmitCount++;
            LastRequest = request;
            return new BrokerOrderEvent(request.RequestId, "ORDER-1", BrokerOrderState.Submitted,
                0, 0m, "Submitted", occurredAt);
        }

        public BrokerOrderEvent Cancel(string requestId, DateTime occurredAt) =>
            new BrokerOrderEvent(requestId, "ORDER-1", BrokerOrderState.Cancelled,
                0, 0m, "Cancelled", occurredAt);
    }
}
