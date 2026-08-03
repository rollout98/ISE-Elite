namespace ISE.Session;

/// <summary>Describes exchange-calendar availability for the evaluated trading day.</summary>
public enum TradingCalendarStatus
{
    /// <summary>A normal exchange trading day.</summary>
    Normal,

    /// <summary>A full exchange holiday when trading is closed.</summary>
    HolidayClosed,

    /// <summary>An exchange-designated shortened trading day.</summary>
    EarlyClose
}
