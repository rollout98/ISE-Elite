using System;
using System.Collections.Generic;
using ISE.Playbooks;

namespace ISE.StrategyIntelligence;

/// <summary>Represents the final strategy recommendation before risk and execution orchestration.</summary>
public sealed class StrategyRecommendation
{
    /// <summary>Initializes a strategy recommendation.</summary>
    public StrategyRecommendation(PlaybookType playbook, StrategyPosture posture, decimal confidenceScore, decimal sizeMultiplier, bool approved, IReadOnlyList<string> reasons)
    {
        if (confidenceScore < 0m || confidenceScore > 100m)
            throw new ArgumentOutOfRangeException(nameof(confidenceScore));
        if (sizeMultiplier < 0m || sizeMultiplier > 1m)
            throw new ArgumentOutOfRangeException(nameof(sizeMultiplier));

        Playbook = playbook;
        Posture = posture;
        ConfidenceScore = confidenceScore;
        SizeMultiplier = sizeMultiplier;
        Approved = approved;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    /// <summary>Gets the selected playbook.</summary>
    public PlaybookType Playbook { get; }

    /// <summary>Gets the recommended execution posture.</summary>
    public StrategyPosture Posture { get; }

    /// <summary>Gets the confidence score from zero to one hundred.</summary>
    public decimal ConfidenceScore { get; }

    /// <summary>Gets the final recommended size multiplier.</summary>
    public decimal SizeMultiplier { get; }

    /// <summary>Gets whether the strategy may proceed to later authoritative gates.</summary>
    public bool Approved { get; }

    /// <summary>Gets explainable recommendation reasons.</summary>
    public IReadOnlyList<string> Reasons { get; }
}
