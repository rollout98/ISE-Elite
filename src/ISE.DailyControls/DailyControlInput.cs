using System;

namespace ISE.DailyControls;

/// <summary>Provides the current account state evaluated by the daily controls engine.</summary>
public sealed class DailyControlInput
{
    /// <summary>Creates a daily control input.</summary>
    public DailyControlInput(
        DailyControlProfile profile,
        decimal realizedProfitLoss,
        int consecutiveLosses,
        int tradesToday,
        bool exceptionalSetup,
        bool accountPaused,
        bool sessionShutdown)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (consecutiveLosses < 0) throw new ArgumentOutOfRangeException(nameof(consecutiveLosses));
        if (tradesToday < 0) throw new ArgumentOutOfRangeException(nameof(tradesToday));

        RealizedProfitLoss = realizedProfitLoss;
        ConsecutiveLosses = consecutiveLosses;
        TradesToday = tradesToday;
        ExceptionalSetup = exceptionalSetup;
        AccountPaused = accountPaused;
        SessionShutdown = sessionShutdown;
    }

    /// <summary>Gets the account control profile.</summary>
    public DailyControlProfile Profile { get; }

    /// <summary>Gets the account's realized profit or loss for the day.</summary>
    public decimal RealizedProfitLoss { get; }

    /// <summary>Gets the current consecutive losing-trade count.</summary>
    public int ConsecutiveLosses { get; }

    /// <summary>Gets the number of completed or initiated trades today.</summary>
    public int TradesToday { get; }

    /// <summary>Gets whether the candidate is classified as exceptional.</summary>
    public bool ExceptionalSetup { get; }

    /// <summary>Gets whether the account has been manually paused.</summary>
    public bool AccountPaused { get; }

    /// <summary>Gets whether session shutdown requires flattening.</summary>
    public bool SessionShutdown { get; }
}
