namespace ISE.ExecutionIntelligence;

/// <summary>Describes how an approved trade should be submitted.</summary>
public enum ExecutionMode
{
    /// <summary>No order may be submitted.</summary>
    Reject = 0,
    /// <summary>Wait for execution conditions to improve.</summary>
    Wait = 1,
    /// <summary>Submit a passive limit order away from immediate market impact.</summary>
    PassiveLimit = 2,
    /// <summary>Submit an aggressive limit order near the opposite quote.</summary>
    AggressiveLimit = 3,
    /// <summary>Submit a market order for immediate execution.</summary>
    Market = 4
}
