namespace ISE.StrategyIntelligence;

/// <summary>Describes how aggressively an eligible strategy may be expressed.</summary>
public enum StrategyPosture
{
    /// <summary>No strategy may be executed.</summary>
    Reject = 0,
    /// <summary>The strategy should wait for stronger evidence.</summary>
    Wait = 1,
    /// <summary>The strategy is eligible only at reduced size.</summary>
    Reduced = 2,
    /// <summary>The strategy is eligible at normal size.</summary>
    Normal = 3,
    /// <summary>The strategy is eligible at full Elite size.</summary>
    Elite = 4
}
