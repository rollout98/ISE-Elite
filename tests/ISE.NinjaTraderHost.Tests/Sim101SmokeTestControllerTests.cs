using System;
using ISE.BrokerExecution;
using ISE.ExecutionCoordinator;
using ISE.NinjaTraderHost;
using Xunit;

namespace ISE.NinjaTraderHost.Tests;

public sealed class Sim101SmokeTestControllerTests
{
    [Fact]
    public void Disabled_controller_blocks_arming()
    {
        var controller = new Sim101SmokeTestController(new FakeBroker(), false);
        Assert.Throws<InvalidOperationException>(() =>
            controller.Arm(Sim101SmokeTestController.ConfirmationPhrase, DateTime.UtcNow));
        Assert.Equal(SmokeTestState.Disabled, controller.State);
    }

    [Fact]
    public void Invalid_confirmation_phrase_is_rejected()
    {
        var controller = EnabledController();
        Assert.Throws<InvalidOperationException>(() => controller.Arm("wrong", DateTime.UtcNow));
        Assert.Equal(SmokeTestState.Disarmed, controller.State);
    }

    [Fact]
    public void Correct_confirmation_phrase_arms_once()
    {
        var controller = EnabledController();
        controller.Arm(Sim101SmokeTestController.ConfirmationPhrase, DateTime.UtcNow);
        Assert.Equal(SmokeTestState.Armed, controller.State);
        Assert.Throws<InvalidOperationException>(() =>
            controller.Arm(Sim101SmokeTestController.ConfirmationPhrase, DateTime.UtcNow));
    }

    [Fact]
    public void Submission_requires_armed_state()
    {
        var controller = EnabledController();
        Assert.Throws<InvalidOperationException>(() => controller.SubmitBuyLimit(20000m, DateTime.UtcNow));
    }

    [Fact]
    public void Submission_is_one_MNQ_buy_limit_order()
    {
        var broker = new FakeBroker();
        var controller = EnabledController(broker);
        controller.Arm(Sim101SmokeTestController.ConfirmationPhrase, DateTime.UtcNow);
        controller.SubmitBuyLimit(20000m, DateTime.UtcNow);

        Assert.NotNull(broker.SubmittedRequest);
        Assert.Equal("MNQ", broker.SubmittedRequest!.Symbol);
        Assert.Equal(ExecutionSide.Buy, broker.SubmittedRequest.Side);
        Assert.Equal(ExecutionOrderType.Limit, broker.SubmittedRequest.OrderType);
        Assert.Equal(1, broker.SubmittedRequest.Quantity);
        Assert.Equal(20000m, broker.SubmittedRequest.EntryPrice);
        Assert.Equal(SmokeTestState.Submitted, controller.State);
    }

    [Fact]
    public void Second_submission_is_blocked()
    {
        var controller = EnabledController();
        controller.Arm(Sim101SmokeTestController.ConfirmationPhrase, DateTime.UtcNow);
        controller.SubmitBuyLimit(20000m, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => controller.SubmitBuyLimit(19900m, DateTime.UtcNow));
    }

    [Fact]
    public void Cancellation_routes_submitted_request()
    {
        var broker = new FakeBroker();
        var controller = EnabledController(broker);
        controller.Arm(Sim101SmokeTestController.ConfirmationPhrase, DateTime.UtcNow);
        controller.SubmitBuyLimit(20000m, DateTime.UtcNow);
        controller.Cancel(DateTime.UtcNow);

        Assert.Equal(controller.Request!.RequestId, broker.CancelledRequestId);
        Assert.Equal(SmokeTestState.CancelRequested, controller.State);
    }

    [Fact]
    public void Broker_updates_are_audited_and_terminal_state_is_retained()
    {
        var broker = new FakeBroker();
        var controller = EnabledController(broker);
        controller.Arm(Sim101SmokeTestController.ConfirmationPhrase, DateTime.UtcNow);
        controller.SubmitBuyLimit(20000m, DateTime.UtcNow);
        controller.HandleBrokerEvent(new BrokerOrderEvent(
            controller.Request!.RequestId, "NT-1", BrokerOrderState.Cancelled, 0, 0m,
            "Cancelled by operator.", DateTime.UtcNow));

        Assert.Equal(SmokeTestState.Cancelled, controller.State);
        Assert.Contains(controller.Audit, entry => entry.Message.Contains("Cancelled"));
        Assert.Throws<InvalidOperationException>(() =>
            controller.Arm(Sim101SmokeTestController.ConfirmationPhrase, DateTime.UtcNow));
    }

    private static Sim101SmokeTestController EnabledController(FakeBroker? broker = null)
        => new Sim101SmokeTestController(broker ?? new FakeBroker(), true);

    private sealed class FakeBroker : IExecutionBroker
    {
        public ExecutionRequest? SubmittedRequest { get; private set; }
        public string? CancelledRequestId { get; private set; }

        public BrokerOrderEvent Submit(ExecutionRequest request, DateTime occurredAt)
        {
            SubmittedRequest = request;
            return new BrokerOrderEvent(request.RequestId, "NT-1", BrokerOrderState.Submitted,
                0, 0m, "Submitted.", occurredAt);
        }

        public BrokerOrderEvent Cancel(string requestId, DateTime occurredAt)
        {
            CancelledRequestId = requestId;
            return new BrokerOrderEvent(requestId, "NT-1", BrokerOrderState.Cancelled,
                0, 0m, "Cancellation sent.", occurredAt);
        }
    }
}
