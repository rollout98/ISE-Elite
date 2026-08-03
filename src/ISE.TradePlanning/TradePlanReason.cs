namespace ISE.TradePlanning;

/// <summary>Explains the trade-planning outcome.</summary>
public enum TradePlanReason
{
    /// <summary>Plan was created successfully.</summary>
    Planned = 0,
    /// <summary>The upstream strategy was not approved.</summary>
    StrategyNotApproved = 1,
    /// <summary>The upstream risk decision was not approved.</summary>
    RiskNotApproved = 2,
    /// <summary>The account objective does not permit another trade.</summary>
    ObjectiveNotPermitted = 3,
    /// <summary>The direction is not tradable.</summary>
    InvalidDirection = 4,
    /// <summary>The requested contract quantity is invalid.</summary>
    InvalidContracts = 5,
    /// <summary>The stop distance is invalid.</summary>
    InvalidStopDistance = 6,
    /// <summary>The reward multiple is invalid.</summary>
    InvalidRewardMultiple = 7
}
