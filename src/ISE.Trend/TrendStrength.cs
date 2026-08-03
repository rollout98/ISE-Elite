namespace ISE.Trend;

/// <summary>Classifies the strength of the established trend.</summary>
public enum TrendStrength
{
    /// <summary>No reliable trend is present.</summary>
    None,

    /// <summary>Directional evidence is present but limited.</summary>
    Weak,

    /// <summary>Directional evidence is established.</summary>
    Moderate,

    /// <summary>Directional evidence is broad and strongly aligned.</summary>
    Strong
}
