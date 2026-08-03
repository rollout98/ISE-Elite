using System;
using System.Collections.Generic;
using ISE.Confidence;
using ISE.Playbooks;

namespace ISE.StrategyIntelligence;

/// <summary>Combines playbook eligibility, confidence, and external controls into one strategy recommendation.</summary>
public sealed class StrategyIntelligenceEngine
{
    /// <summary>Evaluates the final strategy posture and recommended size.</summary>
    public StrategyRecommendation Evaluate(StrategyIntelligenceInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var reasons = new List<string>();

        if (input.AuthoritativeBlock)
        {
            reasons.Add("An authoritative control blocks the strategy.");
            return Reject(input, reasons);
        }

        if (!input.Playbook.IsEligible)
        {
            reasons.Add("No playbook is currently eligible.");
            return Reject(input, reasons);
        }

        if (!input.Confidence.Approved)
        {
            reasons.Add("The opportunity failed the confidence gate.");
            return Reject(input, reasons);
        }

        var finalSize = Math.Min(input.Confidence.SizeMultiplier, input.ExternalSizeMultiplier);
        if (finalSize <= 0m)
        {
            reasons.Add("The most restrictive sizing control allows no position.");
            return Reject(input, reasons);
        }

        var posture = DeterminePosture(input.Confidence.Rating, finalSize);
        reasons.Add($"Selected playbook is {input.Playbook.Playbook}.");
        reasons.Add($"Confidence score is {input.Confidence.Score:0.##} ({input.Confidence.Rating}).");
        reasons.Add($"Final size multiplier is {finalSize:0.##} after restrictive controls.");

        if (finalSize < input.Confidence.SizeMultiplier)
            reasons.Add("External risk or daily controls reduced the confidence-recommended size.");

        return new StrategyRecommendation(
            input.Playbook.Playbook,
            posture,
            input.Confidence.Score,
            finalSize,
            true,
            reasons);
    }

    private static StrategyPosture DeterminePosture(ConfidenceRating rating, decimal finalSize)
    {
        if (finalSize < 0.75m)
            return StrategyPosture.Reduced;
        if (rating == ConfidenceRating.Elite || rating == ConfidenceRating.Institutional)
            return finalSize >= 1m ? StrategyPosture.Elite : StrategyPosture.Normal;
        return StrategyPosture.Normal;
    }

    private static StrategyRecommendation Reject(StrategyIntelligenceInput input, IReadOnlyList<string> reasons)
    {
        return new StrategyRecommendation(
            input.Playbook.Playbook,
            StrategyPosture.Reject,
            input.Confidence.Score,
            0m,
            false,
            reasons);
    }
}
