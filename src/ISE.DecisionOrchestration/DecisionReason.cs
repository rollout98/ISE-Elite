namespace ISE.DecisionOrchestration;

/// <summary>Explains the final orchestration decision.</summary>
public enum DecisionReason
{
    /// <summary>All gates approved an Elite opportunity.</summary>
    EliteOpportunityApproved,
    /// <summary>All gates approved a normal opportunity.</summary>
    OpportunityApproved,
    /// <summary>The opportunity was approved only at reduced size.</summary>
    ReducedRiskRequired,
    /// <summary>The upstream strategy did not qualify.</summary>
    StrategyRejected,
    /// <summary>The opportunity score was not eligible.</summary>
    OpportunityRejected,
    /// <summary>Daily controls stopped new trading.</summary>
    DailyControlsStoppedTrading,
    /// <summary>Daily controls require immediate flattening.</summary>
    DailyControlsRequireFlat,
    /// <summary>The Risk Engine rejected the candidate.</summary>
    RiskRejected,
    /// <summary>The Trade Planning Engine did not produce an approved plan.</summary>
    TradePlanRejected,
    /// <summary>The candidate requires more confirmation.</summary>
    MoreConfirmationRequired
}
