namespace ISE.Strategy;

/// <summary>Represents the qualification result for a strategy candidate.</summary>
public sealed class StrategyDecision
{
    /// <summary>Creates a strategy decision.</summary>
    public StrategyDecision(bool qualified, StrategyId strategyId, StrategyDecisionReason reason)
    {
        Qualified = qualified;
        StrategyId = strategyId;
        Reason = reason;
    }

    /// <summary>Gets whether the candidate qualified.</summary>
    public bool Qualified { get; }
    /// <summary>Gets the evaluated strategy identifier.</summary>
    public StrategyId StrategyId { get; }
    /// <summary>Gets the qualification or rejection reason.</summary>
    public StrategyDecisionReason Reason { get; }
}
