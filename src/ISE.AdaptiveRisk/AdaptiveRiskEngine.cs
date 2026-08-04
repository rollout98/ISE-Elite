using System;
using System.Collections.Generic;

namespace ISE.AdaptiveRisk;

public enum AdaptiveRiskAction { FullRisk, ReducedRisk, MinimalRisk, StandAside, Blocked }

public sealed class AdaptiveRiskInput
{
    public double DecisionConfidence { get; set; }
    public double ExecutionQuality { get; set; }
    public double MarketHealth { get; set; }
    public double VolatilityRisk { get; set; }
    public double DrawdownPressure { get; set; }
    public double DailyObjectivePressure { get; set; }
    public bool AuthoritativeRiskBlock { get; set; }
}

public sealed class AdaptiveRiskDecision
{
    public AdaptiveRiskDecision(AdaptiveRiskAction action, double riskMultiplier, IReadOnlyList<string> reasons)
    {
        Action = action;
        RiskMultiplier = riskMultiplier;
        Reasons = reasons;
    }

    public AdaptiveRiskAction Action { get; }
    public double RiskMultiplier { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class AdaptiveRiskEngine
{
    public AdaptiveRiskDecision Evaluate(AdaptiveRiskInput input)
    {
        var reasons = new List<string>();
        if (input.AuthoritativeRiskBlock)
            return new AdaptiveRiskDecision(AdaptiveRiskAction.Blocked, 0, new[] { "Authoritative risk control blocked participation." });

        var quality = Clamp((input.DecisionConfidence * 0.35) + (input.ExecutionQuality * 0.25) + (input.MarketHealth * 0.20)
            + ((100 - input.VolatilityRisk) * 0.10) + ((100 - input.DrawdownPressure) * 0.10));

        var pressurePenalty = Math.Max(input.DrawdownPressure, input.DailyObjectivePressure) * 0.35;
        var adjusted = Clamp(quality - pressurePenalty);

        reasons.Add($"Adjusted risk quality is {adjusted:0.0}.");
        if (input.VolatilityRisk >= 75) reasons.Add("Elevated volatility reduced risk allowance.");
        if (input.DrawdownPressure >= 60) reasons.Add("Drawdown pressure reduced risk allowance.");
        if (input.DailyObjectivePressure >= 70) reasons.Add("Daily objective protection reduced risk allowance.");

        if (adjusted >= 85)
            return new AdaptiveRiskDecision(AdaptiveRiskAction.FullRisk, 1.0, reasons);
        if (adjusted >= 70)
            return new AdaptiveRiskDecision(AdaptiveRiskAction.ReducedRisk, 0.65, reasons);
        if (adjusted >= 55)
            return new AdaptiveRiskDecision(AdaptiveRiskAction.MinimalRisk, 0.35, reasons);
        return new AdaptiveRiskDecision(AdaptiveRiskAction.StandAside, 0, reasons);
    }

    private static double Clamp(double value) => Math.Max(0, Math.Min(100, value));
}
