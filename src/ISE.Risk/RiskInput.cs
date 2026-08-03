using System;

namespace ISE.Risk;

/// <summary>Contains account state and trade risk used for approval and sizing.</summary>
public sealed class RiskInput
{
    public RiskInput(PropAccountProfile profile, bool signalEligible, decimal riskPerContract, decimal realizedDailyLoss, decimal remainingDrawdown, int tradesToday)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (riskPerContract <= 0) throw new ArgumentOutOfRangeException(nameof(riskPerContract));
        if (realizedDailyLoss < 0) throw new ArgumentOutOfRangeException(nameof(realizedDailyLoss));
        if (remainingDrawdown < 0) throw new ArgumentOutOfRangeException(nameof(remainingDrawdown));
        if (tradesToday < 0) throw new ArgumentOutOfRangeException(nameof(tradesToday));
        SignalEligible = signalEligible;
        RiskPerContract = riskPerContract;
        RealizedDailyLoss = realizedDailyLoss;
        RemainingDrawdown = remainingDrawdown;
        TradesToday = tradesToday;
    }

    public PropAccountProfile Profile { get; }
    public bool SignalEligible { get; }
    public decimal RiskPerContract { get; }
    public decimal RealizedDailyLoss { get; }
    public decimal RemainingDrawdown { get; }
    public int TradesToday { get; }
}
