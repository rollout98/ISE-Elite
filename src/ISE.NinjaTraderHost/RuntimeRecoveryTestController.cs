using System;
using System.Collections.Generic;
using ISE.PositionManager;

namespace ISE.NinjaTraderHost;

public enum RuntimeRecoveryTestState
{
    Disarmed,
    Armed,
    Restarting,
    Passed,
    Failed
}

public sealed class RuntimeRecoveryExpectation
{
    public RuntimeRecoveryExpectation(PositionManagerSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        AccountName = snapshot.AccountName;
        Instrument = snapshot.Instrument;
        Side = snapshot.ExpectedSide;
        Quantity = snapshot.ExpectedQuantity;
        BrokerSignedQuantity = snapshot.BrokerSignedQuantity;
        AveragePrice = snapshot.AveragePrice;
        StopOrderId = snapshot.StopOrderId!;
        TargetOrderId = snapshot.TargetOrderId!;
    }

    public string AccountName { get; }
    public string Instrument { get; }
    public PositionSide Side { get; }
    public int Quantity { get; }
    public int BrokerSignedQuantity { get; }
    public decimal AveragePrice { get; }
    public string StopOrderId { get; }
    public string TargetOrderId { get; }
}

public sealed class RuntimeRecoveryTestController
{
    public RuntimeRecoveryTestState State { get; private set; } = RuntimeRecoveryTestState.Disarmed;
    public RuntimeRecoveryExpectation? Expectation { get; private set; }
    public string LastMessage { get; private set; } = "Runtime recovery test is disarmed.";

    public void Arm(PositionManagerSnapshot snapshot)
    {
        if (State != RuntimeRecoveryTestState.Disarmed)
            throw new InvalidOperationException("The runtime recovery test can be armed only once per NinjaTrader session.");

        ValidateProtectedSnapshot(snapshot, "arming");
        Expectation = new RuntimeRecoveryExpectation(snapshot);
        State = RuntimeRecoveryTestState.Armed;
        LastMessage = "Runtime recovery test armed with the existing protected position and order IDs.";
    }

    public void BeginRestart(PositionManagerSnapshot current)
    {
        if (State != RuntimeRecoveryTestState.Armed || Expectation == null)
            throw new InvalidOperationException("The runtime recovery test must be armed before restart.");

        ValidateProtectedSnapshot(current, "restart");
        var mismatches = Compare(current, Expectation, requireProtectedStatus: true);
        if (mismatches.Count != 0)
            throw new InvalidOperationException(
                "The protected position changed after arming; restart is blocked. " + string.Join(" ", mismatches));

        State = RuntimeRecoveryTestState.Restarting;
        LastMessage = "ISE runtime restart is in progress; broker-held orders must remain unchanged.";
    }

    public bool ValidateRecovered(PositionManagerSnapshot recovered)
    {
        if (State != RuntimeRecoveryTestState.Restarting || Expectation == null)
            throw new InvalidOperationException("No runtime recovery restart is awaiting validation.");
        if (recovered == null) throw new ArgumentNullException(nameof(recovered));

        var mismatches = Compare(recovered, Expectation, requireProtectedStatus: true);
        if (mismatches.Count == 0)
        {
            State = RuntimeRecoveryTestState.Passed;
            LastMessage =
                "Runtime recovery passed: position, quantity, average price, stop ID, and target ID were recovered unchanged.";
            return true;
        }

        State = RuntimeRecoveryTestState.Failed;
        LastMessage = "Runtime recovery failed. " + string.Join(" ", mismatches);
        return false;
    }

    public void Fail(string message)
    {
        State = RuntimeRecoveryTestState.Failed;
        LastMessage = string.IsNullOrWhiteSpace(message)
            ? "Runtime recovery failed without additional detail."
            : message;
    }

    private static void ValidateProtectedSnapshot(PositionManagerSnapshot snapshot, string operation)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        if (snapshot.Status != PositionManagerStatus.Protected || snapshot.ExpectedQuantity <= 0 ||
            snapshot.BrokerSignedQuantity == 0 || string.IsNullOrWhiteSpace(snapshot.StopOrderId) ||
            string.IsNullOrWhiteSpace(snapshot.TargetOrderId))
        {
            throw new InvalidOperationException(
                "A fully protected open Sim101 position is required before " + operation + ".");
        }
    }

    private static List<string> Compare(PositionManagerSnapshot actual,
        RuntimeRecoveryExpectation expected, bool requireProtectedStatus)
    {
        var mismatches = new List<string>();

        if (requireProtectedStatus && actual.Status != PositionManagerStatus.Protected)
            mismatches.Add("status=" + actual.Status + " (expected Protected).");
        if (!string.Equals(actual.AccountName, expected.AccountName, StringComparison.OrdinalIgnoreCase))
            mismatches.Add("account changed.");
        if (!string.Equals(actual.Instrument, expected.Instrument, StringComparison.OrdinalIgnoreCase))
            mismatches.Add("instrument changed.");
        if (actual.ExpectedSide != expected.Side)
            mismatches.Add("side=" + actual.ExpectedSide + " (expected " + expected.Side + ").");
        if (actual.ExpectedQuantity != expected.Quantity)
            mismatches.Add("quantity=" + actual.ExpectedQuantity + " (expected " + expected.Quantity + ").");
        if (actual.BrokerSignedQuantity != expected.BrokerSignedQuantity)
            mismatches.Add("brokerSigned=" + actual.BrokerSignedQuantity +
                           " (expected " + expected.BrokerSignedQuantity + ").");
        if (actual.AveragePrice != expected.AveragePrice)
            mismatches.Add("average=" + actual.AveragePrice + " (expected " + expected.AveragePrice + ").");
        if (!string.Equals(actual.StopOrderId, expected.StopOrderId, StringComparison.OrdinalIgnoreCase))
            mismatches.Add("stop ID changed from " + expected.StopOrderId + " to " +
                           (actual.StopOrderId ?? "missing") + ".");
        if (!string.Equals(actual.TargetOrderId, expected.TargetOrderId, StringComparison.OrdinalIgnoreCase))
            mismatches.Add("target ID changed from " + expected.TargetOrderId + " to " +
                           (actual.TargetOrderId ?? "missing") + ".");

        return mismatches;
    }
}
