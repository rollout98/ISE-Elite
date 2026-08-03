namespace ISE.Session;

/// <summary>Describes the expected opportunity quality of the current time window.</summary>
public enum SessionQuality
{
    /// <summary>No trading opportunity because the exchange or account window is closed.</summary>
    Closed,

    /// <summary>A low-quality period where new trades should be heavily restricted.</summary>
    Low,

    /// <summary>A normal-quality period suitable for selective trading.</summary>
    Normal,

    /// <summary>A high-quality period with favorable participation and structure.</summary>
    High,

    /// <summary>A prime proprietary decision window eligible for the strongest setups.</summary>
    Prime
}
