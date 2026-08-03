using System;

namespace ISE.MarketState;

/// <summary>Describes the dominant market regime and its supporting evidence.</summary>
public sealed class MarketStateSnapshot
{
    /// <summary>Initializes a classified market-state result.</summary>
    public MarketStateSnapshot(MarketStateClassification classification, decimal confidence, string reason)
    {
        if (confidence < 0m || confidence > 1m)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        Classification = classification;
        Confidence = confidence;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>Gets the dominant market-state classification.</summary>
    public MarketStateClassification Classification { get; }
    /// <summary>Gets normalized confidence in the classification.</summary>
    public decimal Confidence { get; }
    /// <summary>Gets an explainable reason for the classification.</summary>
    public string Reason { get; }
}
