namespace ISE.Strategy;

/// <summary>Explains why a strategy candidate qualified or was rejected.</summary>
public enum StrategyDecisionReason
{
    /// <summary>The candidate satisfied the selected playbook.</summary>
    Qualified,
    /// <summary>Trading is not permitted in the current session.</summary>
    SessionNotEligible,
    /// <summary>The signal is not eligible for execution.</summary>
    SignalNotEligible,
    /// <summary>The signal confidence is below the profile threshold.</summary>
    ConfidenceBelowMinimum,
    /// <summary>The required liquidity event is absent.</summary>
    LiquidityRequirementNotMet,
    /// <summary>Market structure does not support the playbook.</summary>
    StructureNotAligned,
    /// <summary>Order flow does not support the playbook.</summary>
    OrderFlowNotAligned,
    /// <summary>The reversal playbook requires opposing prior trend evidence.</summary>
    ReversalContextMissing,
    /// <summary>The continuation playbook requires aligned trend evidence.</summary>
    ContinuationContextMissing
}
