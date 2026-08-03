namespace ISE.DecisionOrchestration;

/// <summary>Identifies the authoritative stage responsible for a final decision.</summary>
public enum DecisionPipelineStage
{
    /// <summary>Strategy qualification.</summary>
    Strategy,
    /// <summary>Confirmation completeness.</summary>
    Confirmation,
    /// <summary>Opportunity quality.</summary>
    Opportunity,
    /// <summary>Daily account controls.</summary>
    DailyControls,
    /// <summary>Risk approval.</summary>
    Risk,
    /// <summary>Trade planning.</summary>
    TradePlanning,
    /// <summary>Final orchestration.</summary>
    Orchestration
}
