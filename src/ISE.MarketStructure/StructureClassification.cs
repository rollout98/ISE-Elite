namespace ISE.MarketStructure;

/// <summary>Classifies a swing relative to the previous swing of the same type.</summary>
public enum StructureClassification
{
    /// <summary>No prior comparable swing exists.</summary>
    Unclassified,

    /// <summary>A swing high above the previous swing high.</summary>
    HigherHigh,

    /// <summary>A swing low above the previous swing low.</summary>
    HigherLow,

    /// <summary>A swing high below the previous swing high.</summary>
    LowerHigh,

    /// <summary>A swing low below the previous swing low.</summary>
    LowerLow,

    /// <summary>The swing price equals the previous comparable swing.</summary>
    Equal
}
