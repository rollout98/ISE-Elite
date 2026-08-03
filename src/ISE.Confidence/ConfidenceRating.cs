namespace ISE.Confidence;

/// <summary>Describes the quality band assigned to a confidence score.</summary>
public enum ConfidenceRating
{
    /// <summary>The opportunity must be rejected.</summary>
    Reject = 0,
    /// <summary>The evidence is weak and should not be traded.</summary>
    Weak = 1,
    /// <summary>The evidence is acceptable but requires reduced risk.</summary>
    Acceptable = 2,
    /// <summary>The evidence is good enough for normal consideration.</summary>
    Good = 3,
    /// <summary>The evidence is excellent.</summary>
    Excellent = 4,
    /// <summary>The evidence qualifies as an Elite setup.</summary>
    Elite = 5,
    /// <summary>The evidence is exceptionally aligned.</summary>
    Institutional = 6
}
