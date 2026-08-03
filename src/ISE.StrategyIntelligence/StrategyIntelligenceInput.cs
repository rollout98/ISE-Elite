using System;
using ISE.Confidence;
using ISE.Playbooks;

namespace ISE.StrategyIntelligence;

/// <summary>Provides the selected playbook, confidence assessment, and external sizing limits.</summary>
public sealed class StrategyIntelligenceInput
{
    /// <summary>Initializes a strategy-intelligence evaluation request.</summary>
    public StrategyIntelligenceInput(PlaybookSelection playbook, ConfidenceResult confidence, decimal externalSizeMultiplier = 1m, bool authoritativeBlock = false)
    {
        Playbook = playbook ?? throw new ArgumentNullException(nameof(playbook));
        Confidence = confidence ?? throw new ArgumentNullException(nameof(confidence));

        if (externalSizeMultiplier < 0m || externalSizeMultiplier > 1m)
            throw new ArgumentOutOfRangeException(nameof(externalSizeMultiplier), "Size multiplier must be between zero and one.");

        ExternalSizeMultiplier = externalSizeMultiplier;
        AuthoritativeBlock = authoritativeBlock;
    }

    /// <summary>Gets the selected playbook.</summary>
    public PlaybookSelection Playbook { get; }

    /// <summary>Gets the confidence assessment.</summary>
    public ConfidenceResult Confidence { get; }

    /// <summary>Gets the most restrictive multiplier supplied by risk or daily controls.</summary>
    public decimal ExternalSizeMultiplier { get; }

    /// <summary>Gets whether an authoritative downstream control blocks the strategy.</summary>
    public bool AuthoritativeBlock { get; }
}
