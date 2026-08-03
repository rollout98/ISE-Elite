namespace ISE.AccountObjectives;

/// <summary>Explains the Account Objective Engine decision.</summary>
public enum ObjectiveDecisionReason
{
    /// <summary>Trading may continue toward the current objective.</summary>
    TradingPermitted,
    /// <summary>The upstream strategy candidate is not qualified.</summary>
    StrategyNotQualified,
    /// <summary>The Risk Engine did not approve the trade.</summary>
    RiskNotApproved,
    /// <summary>The planned daily objective has been reached.</summary>
    DailyObjectiveReached,
    /// <summary>The firm's configured maximum daily profit has been reached.</summary>
    MaximumDailyProfitReached,
    /// <summary>The evaluation profit target has been reached.</summary>
    EvaluationTargetReached,
    /// <summary>Trading is permitted only for an exceptional setup after the preferred target.</summary>
    ExceptionalSetupRequired
}
