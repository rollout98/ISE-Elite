using System;

namespace ISE.Playbooks;

/// <summary>Provides normalized evidence used to select an eligible trading playbook.</summary>
public sealed class PlaybookInput
{
    /// <summary>Initializes a playbook evidence snapshot.</summary>
    public PlaybookInput(bool openingWindow, decimal trendEvidence, decimal pullbackEvidence, decimal breakoutEvidence, decimal reversalEvidence, decimal liquiditySweepEvidence, decimal rangeEvidence, decimal extensionEvidence, decimal confirmationEvidence)
    {
        OpeningWindow = openingWindow;
        TrendEvidence = ValidateUnit(trendEvidence, nameof(trendEvidence));
        PullbackEvidence = ValidateUnit(pullbackEvidence, nameof(pullbackEvidence));
        BreakoutEvidence = ValidateUnit(breakoutEvidence, nameof(breakoutEvidence));
        ReversalEvidence = ValidateUnit(reversalEvidence, nameof(reversalEvidence));
        LiquiditySweepEvidence = ValidateUnit(liquiditySweepEvidence, nameof(liquiditySweepEvidence));
        RangeEvidence = ValidateUnit(rangeEvidence, nameof(rangeEvidence));
        ExtensionEvidence = ValidateUnit(extensionEvidence, nameof(extensionEvidence));
        ConfirmationEvidence = ValidateUnit(confirmationEvidence, nameof(confirmationEvidence));
    }

    /// <summary>Gets whether the market is within an approved opening behavior window.</summary>
    public bool OpeningWindow { get; }
    /// <summary>Gets normalized directional-trend evidence.</summary>
    public decimal TrendEvidence { get; }
    /// <summary>Gets normalized pullback-quality evidence.</summary>
    public decimal PullbackEvidence { get; }
    /// <summary>Gets normalized breakout and acceptance evidence.</summary>
    public decimal BreakoutEvidence { get; }
    /// <summary>Gets normalized reversal evidence.</summary>
    public decimal ReversalEvidence { get; }
    /// <summary>Gets normalized liquidity-sweep evidence.</summary>
    public decimal LiquiditySweepEvidence { get; }
    /// <summary>Gets normalized balanced-range evidence.</summary>
    public decimal RangeEvidence { get; }
    /// <summary>Gets normalized extension from fair value.</summary>
    public decimal ExtensionEvidence { get; }
    /// <summary>Gets normalized multi-engine confirmation evidence.</summary>
    public decimal ConfirmationEvidence { get; }

    private static decimal ValidateUnit(decimal value, string name)
    {
        if (value < 0m || value > 1m)
            throw new ArgumentOutOfRangeException(name, "Value must be between zero and one.");
        return value;
    }
}
