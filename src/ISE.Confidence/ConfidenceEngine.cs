using System;
using System.Collections.Generic;

namespace ISE.Confidence;

/// <summary>Calculates explainable confidence scores from weighted market evidence.</summary>
public sealed class ConfidenceEngine
{
    /// <summary>Evaluates confidence using the default v1 weights and thresholds.</summary>
    public ConfidenceResult Evaluate(ConfidenceInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (input.HardRiskBlock)
            return new ConfidenceResult(0m, ConfidenceRating.Reject, false, 0m, new[] { "Authoritative risk block is active." });

        var score =
            input.MarketState * 20m +
            input.HigherTimeframeBias * 15m +
            input.TrendStrength * 15m +
            input.Liquidity * 10m +
            input.SessionQuality * 10m +
            input.PlaybookQuality * 10m +
            input.RewardToRisk * 10m +
            input.VolatilityQuality * 5m +
            input.TimeOfDay * 5m;

        score = Math.Round(score, 2, MidpointRounding.AwayFromZero);
        var rating = Rate(score);
        var approved = score >= 64m;
        var sizeMultiplier = score >= 92m ? 1m : score >= 78m ? 0.75m : score >= 64m ? 0.5m : 0m;
        var reasons = BuildReasons(input, score, rating);

        return new ConfidenceResult(score, rating, approved, sizeMultiplier, reasons);
    }

    private static ConfidenceRating Rate(decimal score)
    {
        if (score >= 97m) return ConfidenceRating.Institutional;
        if (score >= 92m) return ConfidenceRating.Elite;
        if (score >= 86m) return ConfidenceRating.Excellent;
        if (score >= 78m) return ConfidenceRating.Good;
        if (score >= 64m) return ConfidenceRating.Acceptable;
        if (score >= 49m) return ConfidenceRating.Weak;
        return ConfidenceRating.Reject;
    }

    private static IReadOnlyList<string> BuildReasons(ConfidenceInput input, decimal score, ConfidenceRating rating)
    {
        var reasons = new List<string>
        {
            $"Weighted confidence score is {score:0.##}.",
            $"Confidence rating is {rating}."
        };

        if (input.MarketState >= 0.8m) reasons.Add("Market state is strongly aligned.");
        if (input.HigherTimeframeBias >= 0.8m) reasons.Add("Higher-timeframe bias confirms the setup.");
        if (input.PlaybookQuality >= 0.8m) reasons.Add("Playbook evidence is high quality.");
        if (input.RewardToRisk < 0.5m) reasons.Add("Reward-to-risk quality is a limiting factor.");
        if (input.SessionQuality < 0.5m) reasons.Add("Session quality is a limiting factor.");

        return reasons;
    }
}
