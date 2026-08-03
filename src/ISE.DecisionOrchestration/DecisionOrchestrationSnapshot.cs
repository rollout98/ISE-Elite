using ISE.TradePlanning;

namespace ISE.DecisionOrchestration;

/// <summary>Represents the final result of the complete trading decision pipeline.</summary>
public sealed class DecisionOrchestrationSnapshot
{
    /// <summary>Creates an orchestration result.</summary>
    public DecisionOrchestrationSnapshot(DecisionAction action, DecisionReason reason, int authorizedContracts, decimal sizeMultiplier, TradePlan? tradePlan)
    {
        Action = action;
        Reason = reason;
        AuthorizedContracts = authorizedContracts;
        SizeMultiplier = sizeMultiplier;
        TradePlan = tradePlan;
    }

    /// <summary>Gets the final pipeline action.</summary>
    public DecisionAction Action { get; }
    /// <summary>Gets the primary reason for the action.</summary>
    public DecisionReason Reason { get; }
    /// <summary>Gets the final authorized contract count.</summary>
    public int AuthorizedContracts { get; }
    /// <summary>Gets the final size multiplier applied to approved risk.</summary>
    public decimal SizeMultiplier { get; }
    /// <summary>Gets the approved trade plan when execution may proceed.</summary>
    public TradePlan? TradePlan { get; }
    /// <summary>Gets whether execution may be authorized.</summary>
    public bool ExecutionAuthorized => Action == DecisionAction.ApproveReducedSize || Action == DecisionAction.ApproveNormalSize || Action == DecisionAction.ApproveFullSize;
}
