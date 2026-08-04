using System;
using System.Collections.Generic;

namespace ISE.PositionSizingIntelligence;

public enum PositionSizingAction { FullSize, ReducedSize, MinimalSize, NoTrade, Blocked }

public sealed class PositionSizingInput
{
    public int MaximumContracts { get; set; }
    public double ParticipationMultiplier { get; set; }
    public double AdaptiveRiskMultiplier { get; set; }
    public double StopDistanceRisk { get; set; }
    public double LiquidityCapacity { get; set; }
    public double AccountPressure { get; set; }
    public bool AuthoritativeRiskBlock { get; set; }
}

public sealed class PositionSizingDecision
{
    public PositionSizingDecision(PositionSizingAction action, int contracts, double effectiveMultiplier, IReadOnlyList<string> reasons)
    {
        Action = action;
        Contracts = contracts;
        EffectiveMultiplier = effectiveMultiplier;
        Reasons = reasons;
    }

    public PositionSizingAction Action { get; }
    public int Contracts { get; }
    public double EffectiveMultiplier { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class PositionSizingIntelligenceEngine
{
    public PositionSizingDecision Evaluate(PositionSizingInput input)
    {
        if (input.AuthoritativeRiskBlock)
            return new PositionSizingDecision(PositionSizingAction.Blocked, 0, 0, new[] { "Authoritative risk control blocked position sizing." });

        var reasons = new List<string>();
        var maximum = Math.Max(0, input.MaximumContracts);
        if (maximum == 0)
            return new PositionSizingDecision(PositionSizingAction.NoTrade, 0, 0, new[] { "No contract capacity is available." });

        var effective = Clamp01(input.ParticipationMultiplier)
            * Clamp01(input.AdaptiveRiskMultiplier)
            * Clamp01((100 - input.StopDistanceRisk) / 100.0)
            * Clamp01(input.LiquidityCapacity / 100.0)
            * Clamp01((100 - input.AccountPressure) / 100.0);

        if (input.StopDistanceRisk >= 70) reasons.Add("Wide stop distance reduced contract size.");
        if (input.LiquidityCapacity < 60) reasons.Add("Limited liquidity capacity reduced contract size.");
        if (input.AccountPressure >= 60) reasons.Add("Account pressure reduced contract size.");

        var contracts = (int)Math.Floor(maximum * effective + 1e-9);
        if (contracts <= 0)
            return new PositionSizingDecision(PositionSizingAction.NoTrade, 0, effective, reasons.Count == 0 ? new[] { "Combined sizing constraints produced no tradable quantity." } : reasons);

        reasons.Insert(0, $"Effective sizing multiplier is {effective:0.00}.");
        var ratio = (double)contracts / maximum;
        var action = ratio >= 0.85 ? PositionSizingAction.FullSize
            : ratio >= 0.45 ? PositionSizingAction.ReducedSize
            : PositionSizingAction.MinimalSize;

        return new PositionSizingDecision(action, contracts, effective, reasons);
    }

    private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
}
