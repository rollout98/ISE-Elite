using System;
using System.Collections.Generic;

namespace ISE.Confidence;

/// <summary>Represents the final confidence assessment.</summary>
public sealed class ConfidenceResult
{
    /// <summary>Initializes a confidence result.</summary>
    public ConfidenceResult(decimal score, ConfidenceRating rating, bool approved, decimal sizeMultiplier, IReadOnlyList<string> reasons)
    {
        if (score < 0m || score > 100m)
            throw new ArgumentOutOfRangeException(nameof(score));
        Score = score;
        Rating = rating;
        Approved = approved;
        SizeMultiplier = sizeMultiplier;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    /// <summary>Gets the confidence score from zero to one hundred.</summary>
    public decimal Score { get; }
    /// <summary>Gets the quality rating.</summary>
    public ConfidenceRating Rating { get; }
    /// <summary>Gets whether the opportunity passes the confidence gate.</summary>
    public bool Approved { get; }
    /// <summary>Gets the recommended position-size multiplier.</summary>
    public decimal SizeMultiplier { get; }
    /// <summary>Gets explainable scoring reasons.</summary>
    public IReadOnlyList<string> Reasons { get; }
}
