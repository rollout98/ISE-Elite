namespace ISE.OpportunityScoring;

/// <summary>Represents the final quality assessment for a potential trade.</summary>
public sealed class OpportunityScoreSnapshot
{
    /// <summary>Initializes a new opportunity score snapshot.</summary>
    public OpportunityScoreSnapshot(decimal score, OpportunityGrade grade, decimal sizeMultiplier, bool eligible, string reason)
    {
        Score = score;
        Grade = grade;
        SizeMultiplier = sizeMultiplier;
        Eligible = eligible;
        Reason = reason;
    }

    /// <summary>Gets the weighted score from zero through one hundred.</summary>
    public decimal Score { get; }
    /// <summary>Gets the assigned opportunity grade.</summary>
    public OpportunityGrade Grade { get; }
    /// <summary>Gets the recommended position-size multiplier.</summary>
    public decimal SizeMultiplier { get; }
    /// <summary>Gets whether the opportunity may proceed.</summary>
    public bool Eligible { get; }
    /// <summary>Gets the primary explanation for the assessment.</summary>
    public string Reason { get; }
}
