namespace ISE.Risk;

/// <summary>Approves or rejects trade candidates and calculates a safe contract quantity.</summary>
public sealed class RiskEngine
{
    public RiskDecision Evaluate(RiskInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        var profile = input.Profile;

        if (!input.SignalEligible)
            return Reject(RiskDecisionReason.SignalNotEligible);
        if (input.RealizedDailyLoss >= profile.DailyLossLimit)
            return Reject(RiskDecisionReason.DailyLossLimitReached);

        var remainingDailyRisk = profile.DailyLossLimit - input.RealizedDailyLoss;
        var usableDrawdown = input.RemainingDrawdown;
        if (usableDrawdown <= 0)
            return Reject(RiskDecisionReason.InsufficientDrawdownRoom);

        var availableRisk = Math.Min(profile.MaximumRiskPerTrade, Math.Min(remainingDailyRisk, usableDrawdown));
        if (input.RiskPerContract > availableRisk)
            return Reject(RiskDecisionReason.StopRiskExceedsCapacity);

        var contractsByRisk = (int)Math.Floor(availableRisk / input.RiskPerContract);
        var contracts = Math.Min(profile.MaximumContracts, contractsByRisk);
        if (contracts < 1)
            return Reject(RiskDecisionReason.NoContractsAllowed);

        var totalRisk = contracts * input.RiskPerContract;
        return new RiskDecision(true, contracts, totalRisk, RiskDecisionReason.Approved);
    }

    private static RiskDecision Reject(RiskDecisionReason reason) => new(false, 0, 0, reason);
}
