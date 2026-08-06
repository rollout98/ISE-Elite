using System;
using System.Collections.Generic;
using ISE.BrokerExecution;
using ISE.ExecutionCoordinator;

namespace ISE.NinjaTraderHost;

public enum ProtectedFillTestState
{
    Disabled,
    Disarmed,
    Armed,
    Submitted,
    Accepted,
    PartiallyFilled,
    FilledAwaitingProtection,
    Protected,
    Completed,
    Rejected,
    Faulted
}

public sealed class ProtectedFillTestAuditEntry
{
    public ProtectedFillTestAuditEntry(ProtectedFillTestState state, string message, DateTime occurredAt)
    {
        State = state;
        Message = message ?? string.Empty;
        OccurredAt = occurredAt;
    }

    public ProtectedFillTestState State { get; }
    public string Message { get; }
    public DateTime OccurredAt { get; }
}

public sealed class ProtectedFillTestController
{
    public const string ConfirmationPhrase = "ARM-SIM101-MNQ-PROTECTED-FILL-1";

    private readonly IExecutionBroker _broker;
    private readonly string _symbol;
    private readonly List<ProtectedFillTestAuditEntry> _audit = new List<ProtectedFillTestAuditEntry>();
    private ExecutionRequest? _request;
    private bool _submissionAttempted;

    public ProtectedFillTestController(IExecutionBroker broker, bool enabled, string symbol = "MNQ")
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _symbol = string.IsNullOrWhiteSpace(symbol)
            ? throw new ArgumentException("Protected-fill symbol is required.", nameof(symbol))
            : symbol.Trim();
        State = enabled ? ProtectedFillTestState.Disarmed : ProtectedFillTestState.Disabled;
        Record(State, enabled
            ? "Protected-fill test is enabled but disarmed."
            : "Protected-fill test is disabled.", DateTime.UtcNow);
    }

    public ProtectedFillTestState State { get; private set; }
    public ExecutionRequest? Request => _request;
    public IReadOnlyList<ProtectedFillTestAuditEntry> Audit => _audit.AsReadOnly();

    public void Arm(string confirmationPhrase, bool accountFlat, bool protectionEnabled, DateTime occurredAt)
    {
        if (State == ProtectedFillTestState.Disabled)
            throw new InvalidOperationException("The protected-fill test is disabled in configuration.");
        if (State != ProtectedFillTestState.Disarmed)
            throw new InvalidOperationException("The protected-fill test can be armed only once per runtime session.");
        if (!accountFlat)
            throw new InvalidOperationException("Sim101 must be flat before the protected-fill test can be armed.");
        if (!protectionEnabled)
            throw new InvalidOperationException("Position protection must be enabled before arming the protected-fill test.");
        if (!string.Equals(confirmationPhrase, ConfirmationPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException("The protected-fill confirmation phrase is invalid.");

        State = ProtectedFillTestState.Armed;
        Record(State, "Protected-fill test armed for one MNQ market entry.", occurredAt);
    }

    public BrokerOrderEvent SubmitMarketBuy(bool accountFlat, DateTime occurredAt) =>
        SubmitMarket(ExecutionSide.Buy, accountFlat, occurredAt);

    public BrokerOrderEvent SubmitMarketSell(bool accountFlat, DateTime occurredAt) =>
        SubmitMarket(ExecutionSide.Sell, accountFlat, occurredAt);

    public void HandleBrokerEvent(BrokerOrderEvent brokerEvent)
    {
        if (brokerEvent == null) throw new ArgumentNullException(nameof(brokerEvent));
        if (_request == null || !string.Equals(brokerEvent.RequestId, _request.RequestId, StringComparison.Ordinal))
            return;

        switch (brokerEvent.State)
        {
            case BrokerOrderState.Submitted:
                State = ProtectedFillTestState.Submitted;
                break;
            case BrokerOrderState.Accepted:
                State = ProtectedFillTestState.Accepted;
                break;
            case BrokerOrderState.PartiallyFilled:
                State = ProtectedFillTestState.PartiallyFilled;
                break;
            case BrokerOrderState.Filled:
                State = ProtectedFillTestState.FilledAwaitingProtection;
                break;
            case BrokerOrderState.Rejected:
                State = ProtectedFillTestState.Rejected;
                break;
            case BrokerOrderState.Cancelled:
                State = ProtectedFillTestState.Faulted;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(brokerEvent));
        }

        Record(State, "Broker update: " + brokerEvent.State + "; filled=" + brokerEvent.FilledQuantity +
            "; average=" + brokerEvent.AverageFillPrice + ".", brokerEvent.OccurredAt);
    }

    public void MarkProtected(string stopOrderId, string targetOrderId, DateTime occurredAt)
    {
        if (State != ProtectedFillTestState.FilledAwaitingProtection &&
            State != ProtectedFillTestState.Protected)
            return;
        if (string.IsNullOrWhiteSpace(stopOrderId) || string.IsNullOrWhiteSpace(targetOrderId))
            throw new ArgumentException("Both protective order IDs are required.");

        State = ProtectedFillTestState.Protected;
        Record(State, "Stop and target are working: stop=" + stopOrderId + "; target=" + targetOrderId + ".",
            occurredAt);
    }

    public void MarkCompleted(DateTime occurredAt)
    {
        if (State != ProtectedFillTestState.Protected &&
            State != ProtectedFillTestState.FilledAwaitingProtection)
            return;
        State = ProtectedFillTestState.Completed;
        Record(State, "Protected-fill test completed with Sim101 flat.", occurredAt);
    }

    private BrokerOrderEvent SubmitMarket(ExecutionSide side, bool accountFlat, DateTime occurredAt)
    {
        if (side != ExecutionSide.Buy && side != ExecutionSide.Sell)
            throw new ArgumentOutOfRangeException(nameof(side));
        if (State != ProtectedFillTestState.Armed)
            throw new InvalidOperationException("The protected-fill test must be armed before submission.");
        if (!accountFlat)
            throw new InvalidOperationException("Sim101 is no longer flat; protected-fill entry is blocked.");
        if (_submissionAttempted)
            throw new InvalidOperationException("The one-entry protected-fill allowance has already been consumed.");

        _submissionAttempted = true;
        _request = new ExecutionRequest(
            "PROTECTED-FILL-" + occurredAt.ToUniversalTime().ToString("yyyyMMddHHmmssfff") + "-" +
                Guid.NewGuid().ToString("N").Substring(0, 8),
            "SIM101-PROTECTED-FILL",
            _symbol,
            side,
            1,
            ExecutionOrderType.Market,
            null,
            1m,
            2m,
            occurredAt,
            "MANUAL-SIM101-PROTECTED-FILL-TEST");

        try
        {
            var submitted = _broker.Submit(_request, occurredAt);
            State = ProtectedFillTestState.Submitted;
            Record(State, "Submitted one MNQ market-" + SideLabel(side) + " protected-fill test entry.", occurredAt);
            return submitted;
        }
        catch (Exception exception)
        {
            State = ProtectedFillTestState.Faulted;
            Record(State, "Protected-fill submission failed: " + exception.Message, occurredAt);
            throw;
        }
    }

    private static string SideLabel(ExecutionSide side) =>
        side == ExecutionSide.Buy ? "buy" : "sell";

    private void Record(ProtectedFillTestState state, string message, DateTime occurredAt) =>
        _audit.Add(new ProtectedFillTestAuditEntry(state, message, occurredAt));
}
