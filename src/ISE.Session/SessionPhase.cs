namespace ISE.Session;

/// <summary>
/// Identifies the logical phase within the ISE Elite trading day.
/// </summary>
public enum SessionPhase
{
    /// <summary>
    /// The 3:00 PM–5:00 PM Central maintenance window.
    /// </summary>
    Maintenance,

    /// <summary>
    /// The evening session beginning at 5:00 PM Central.
    /// </summary>
    Evening,

    /// <summary>
    /// The overnight trading session.
    /// </summary>
    Overnight,

    /// <summary>
    /// The premarket period before the New York open.
    /// </summary>
    Premarket,

    /// <summary>
    /// The New York opening phase beginning at 8:30 AM Central.
    /// </summary>
    NewYorkOpen,

    /// <summary>
    /// The regular intraday trading phase.
    /// </summary>
    RegularTrading,

    /// <summary>
    /// The closing phase before the 3:00 PM Central cutoff.
    /// </summary>
    Closing
}