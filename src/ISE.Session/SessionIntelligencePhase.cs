namespace ISE.Session;

/// <summary>Identifies the detailed market-time context used by the trading brain.</summary>
public enum SessionIntelligencePhase
{
    Closed,
    Maintenance,
    Asia,
    London,
    NewYorkPremarket,
    OpeningAuction,
    OpeningReversalWindow,
    MidMorning,
    SecondaryMoveWindow,
    RegularTrading,
    Lunch,
    ClosingHour,
    EarlyClose
}
