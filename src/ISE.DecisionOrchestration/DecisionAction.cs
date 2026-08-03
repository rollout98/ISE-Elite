namespace ISE.DecisionOrchestration;

/// <summary>Defines the final action selected by the trading decision pipeline.</summary>
public enum DecisionAction
{
    /// <summary>Rejects the candidate.</summary>
    Reject,
    /// <summary>Waits for additional confirmation.</summary>
    Wait,
    /// <summary>Approves a reduced-size trade.</summary>
    ApproveReducedSize,
    /// <summary>Approves a normal-size trade.</summary>
    ApproveNormalSize,
    /// <summary>Approves a full-size Elite trade.</summary>
    ApproveFullSize,
    /// <summary>Stops new trading for the account.</summary>
    StopTrading,
    /// <summary>Requires the account to be flattened.</summary>
    ForceFlat
}
