using System;
using System.Collections.Generic;
using ISE.BrokerExecution;
using ISE.ExecutionCoordinator;

namespace ISE.NinjaTraderHost;

public enum SmokeTestState
{
    Disabled,
    Disarmed,
    Armed,
    Submitted,
    Accepted,
    PartiallyFilled,
    CancelRequested,
    Filled,
    Rejected,
    Cancelled,
    Faulted
}

public sealed class SmokeTestAuditEntry
{
    public SmokeTestAuditEntry(SmokeTestState state, string message, DateTime occurredAt)
    {
        State = state;
        Message = message ?? string.Empty;
        OccurredAt = occurredAt;
    }

    public SmokeTestState State { get; }
    public string Message { get; }
    public DateTime OccurredAt { get; }
}

public sealed class Sim101SmokeTestController
{
    public const string ConfirmationPhrase = "ARM-SIM101-MNQ-1";

    private readonly IExecutionBroker _broker;
    private readonly string _symbol;
    private readonly List<SmokeTestAuditEntry> _audit = new List<SmokeTestAuditEntry>();
    private ExecutionRequest? _request;
    private bool _submissionAttempted;

    public Sim101SmokeTestController(IExecutionBroker broker, bool enabled, string symbol = "MNQ")
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _symbol = string.IsNullOrWhiteSpace(symbol)
            ? throw new ArgumentException("Smoke-test symbol is required.", nameof(symbol))
            : symbol.Trim();

        State = enabled ? SmokeTestState.Disarmed : SmokeTestState.Disabled;
        Record(State, enabled
            ? "Sim101 smoke test is enabled but disarmed."
            : "Sim101 smoke test is disabled.", DateTime.UtcNow);
    }

    public SmokeTestState State { get; private set; }
    public ExecutionRequest? Request => _request;
    public IReadOnlyList<SmokeTestAuditEntry> Audit => _audit.AsReadOnly();

    public void Arm(string confirmationPhrase, DateTime occurredAt)
    {
        if (State == SmokeTestState.Disabled)
            throw new InvalidOperationException("The Sim101 smoke test is disabled in configuration.");
        if (State != SmokeTestState.Disarmed)
            throw new InvalidOperationException("The Sim101 smoke test can be armed only once per runtime session.");
        if (!string.Equals(confirmationPhrase, ConfirmationPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException("The smoke-test confirmation phrase is invalid.");

        State = SmokeTestState.Armed;
        Record(State, "Sim101 smoke test armed for one MNQ buy-limit submission.", occurredAt);
    }

    public BrokerOrderEvent SubmitBuyLimit(decimal limitPrice, DateTime occurredAt)
    {
        if (State != SmokeTestState.Armed)
            throw new InvalidOperationException("The Sim101 smoke test must be armed before submission.");
        if (_submissionAttempted)
            throw new InvalidOperationException("The one-order smoke-test allowance has already been consumed.");
        if (limitPrice <= 100m)
            throw new ArgumentOutOfRangeException(nameof(limitPrice), "A realistic positive limit price is required.");

        _submissionAttempted = true;
        _request = new ExecutionRequest(
            "SMOKE-" + occurredAt.ToUniversalTime().ToString("yyyyMMddHHmmssfff") + "-" +
                Guid.NewGuid().ToString("N").Substring(0, 8),
            "SIM101-SMOKE",
            _symbol,
            ExecutionSide.Buy,
            1,
            ExecutionOrderType.Limit,
            limitPrice,
            limitPrice - 100m,
            limitPrice + 100m,
            occurredAt,
            "MANUAL-SIM101-SMOKE-TEST");

        try
        {
            var submitted = _broker.Submit(_request, occurredAt);
            State = SmokeTestState.Submitted;
            Record(State,
                $"Submitted one {_symbol} buy-limit smoke-test order at {limitPrice}. " +
                "No protective child orders are active.", occurredAt);
            return submitted;
        }
        catch (Exception exception)
        {
            State = SmokeTestState.Faulted;
            Record(State, "Smoke-test submission failed: " + exception.Message, occurredAt);
            throw;
        }
    }

    public BrokerOrderEvent Cancel(DateTime occurredAt)
    {
        if (_request == null)
            throw new InvalidOperationException("No smoke-test order has been submitted.");
        if (State != SmokeTestState.Submitted && State != SmokeTestState.Accepted &&
            State != SmokeTestState.PartiallyFilled)
            throw new InvalidOperationException("The smoke-test order is not cancellable in its current state.");

        var cancelled = _broker.Cancel(_request.RequestId, occurredAt);
        State = SmokeTestState.CancelRequested;
        Record(State, "Cancellation was requested for the smoke-test order.", occurredAt);
        return cancelled;
    }

    public void HandleBrokerEvent(BrokerOrderEvent brokerEvent)
    {
        if (brokerEvent == null)
            throw new ArgumentNullException(nameof(brokerEvent));
        if (_request == null || !string.Equals(
                brokerEvent.RequestId, _request.RequestId, StringComparison.Ordinal))
            return;

        switch (brokerEvent.State)
        {
            case BrokerOrderState.Submitted:
                State = SmokeTestState.Submitted;
                break;
            case BrokerOrderState.Accepted:
                State = SmokeTestState.Accepted;
                break;
            case BrokerOrderState.PartiallyFilled:
                State = SmokeTestState.PartiallyFilled;
                break;
            case BrokerOrderState.Filled:
                State = SmokeTestState.Filled;
                break;
            case BrokerOrderState.Rejected:
                State = SmokeTestState.Rejected;
                break;
            case BrokerOrderState.Cancelled:
                State = SmokeTestState.Cancelled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(brokerEvent));
        }

        Record(State,
            $"Broker update: {brokerEvent.State}; filled={brokerEvent.FilledQuantity}; " +
            $"average={brokerEvent.AverageFillPrice}; {brokerEvent.Message}",
            brokerEvent.OccurredAt);
    }

    private void Record(SmokeTestState state, string message, DateTime occurredAt)
        => _audit.Add(new SmokeTestAuditEntry(state, message, occurredAt));
}
