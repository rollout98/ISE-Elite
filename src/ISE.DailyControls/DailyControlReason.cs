namespace ISE.DailyControls;

/// <summary>Explains why a daily control action was selected.</summary>
public enum DailyControlReason
{
    /// <summary>All configured controls permit normal trading.</summary>
    TradingPermitted,

    /// <summary>The preferred daily target was reached and profit protection is active.</summary>
    PreferredTargetReached,

    /// <summary>An exceptional setup is permitted after the preferred target.</summary>
    ExceptionalSetupPermitted,

    /// <summary>The configured hard daily profit ceiling was reached.</summary>
    MaximumDailyProfitReached,

    /// <summary>The configured daily loss limit was reached.</summary>
    DailyLossLimitReached,

    /// <summary>The configured consecutive-loss limit was reached.</summary>
    ConsecutiveLossLimitReached,

    /// <summary>The configured maximum number of trades was reached.</summary>
    MaximumTradesReached,

    /// <summary>The account was manually paused.</summary>
    AccountPaused,

    /// <summary>The trading session requires shutdown and flattening.</summary>
    SessionShutdown
}
