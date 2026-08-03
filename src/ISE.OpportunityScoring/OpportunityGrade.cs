namespace ISE.OpportunityScoring;

/// <summary>Describes the quality tier assigned to a potential trade.</summary>
public enum OpportunityGrade
{
    /// <summary>The opportunity does not meet the minimum trading threshold.</summary>
    Reject = 0,
    /// <summary>The opportunity qualifies for reduced size.</summary>
    B = 1,
    /// <summary>The opportunity qualifies for normal size.</summary>
    A = 2,
    /// <summary>The opportunity qualifies as an elite setup.</summary>
    Elite = 3
}
