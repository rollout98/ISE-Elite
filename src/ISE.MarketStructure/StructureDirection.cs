namespace ISE.MarketStructure;

/// <summary>Represents the directional state inferred from confirmed market structure.</summary>
public enum StructureDirection
{
    /// <summary>Insufficient or conflicting structure.</summary>
    Neutral,

    /// <summary>Higher-high and higher-low structure.</summary>
    Bullish,

    /// <summary>Lower-high and lower-low structure.</summary>
    Bearish
}
