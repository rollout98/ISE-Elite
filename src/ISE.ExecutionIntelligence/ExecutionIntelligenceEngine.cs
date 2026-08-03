using System;
using System.Collections.Generic;

namespace ISE.ExecutionIntelligence;

/// <summary>Selects an execution mode after a strategy has already been approved.</summary>
public sealed class ExecutionIntelligenceEngine
{
    /// <summary>Evaluates execution conditions using deterministic safety precedence.</summary>
    public ExecutionRecommendation Evaluate(ExecutionIntelligenceInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        if (input.NewsLock)
            return Reject(ExecutionReason.NewsLock, "An authoritative news lock blocks execution.");

        if (input.RiskLock)
            return Reject(ExecutionReason.RiskLock, "An authoritative risk lock blocks execution.");

        var contracts = input.DesiredContracts;
        var volatilityReduced = input.VolatilityScore > 90m;
        if (volatilityReduced)
            contracts = Math.Max(1, (int)Math.Floor(contracts / 2m));

        if (input.SpreadTicks > 4m)
            return Approve(ExecutionMode.PassiveLimit, contracts, ExecutionReason.WideSpread, 2, 1, false,
                "The spread exceeds four ticks, so passive execution is required.", volatilityReduced);

        if (input.LiquidityScore < 40m)
            return Approve(ExecutionMode.PassiveLimit, contracts, ExecutionReason.LowLiquidity, 1, 1, false,
                "Liquidity is weak, so market impact must be minimized.", volatilityReduced);

        if (input.ConfidenceScore > 95m && input.SpreadTicks <= 1m && input.LiquidityScore > 90m)
            return Approve(ExecutionMode.Market, contracts, ExecutionReason.EliteImmediateExecution, 0, 1, true,
                "Elite confidence, tight spread, and deep liquidity support immediate execution.", volatilityReduced);

        return Approve(ExecutionMode.AggressiveLimit, contracts,
            volatilityReduced ? ExecutionReason.ExtremeVolatility : ExecutionReason.StandardExecution,
            0, volatilityReduced ? 1 : 2, !volatilityReduced,
            volatilityReduced
                ? "Extreme volatility reduced size; use controlled aggressive-limit execution."
                : "Normal conditions support aggressive-limit execution.",
            volatilityReduced);
    }

    private static ExecutionRecommendation Reject(ExecutionReason reason, string note)
    {
        return new ExecutionRecommendation(false, ExecutionMode.Reject, 0, reason, 0, 0, false, new[] { note });
    }

    private static ExecutionRecommendation Approve(ExecutionMode mode, int contracts, ExecutionReason reason, int offset, int slippage, bool chase, string note, bool volatilityReduced)
    {
        var notes = new List<string> { note };
        if (volatilityReduced)
            notes.Add("Contract quantity was reduced by fifty percent because volatility exceeded ninety.");
        return new ExecutionRecommendation(true, mode, contracts, reason, offset, slippage, chase, notes);
    }
}
