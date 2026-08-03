namespace ISE.Trend;

/// <summary>Identifies the directional bias produced by the Trend Engine.</summary>
public enum TrendDirection
{
    /// <summary>No directional edge is established.</summary>
    Neutral,

    /// <summary>Market evidence favors higher prices.</summary>
    Bullish,

    /// <summary>Market evidence favors lower prices.</summary>
    Bearish
}
