namespace ISE.Signal;

/// <summary>Represents the directional trade decision produced by the Signal Engine.</summary>
public enum SignalDirection
{
    /// <summary>No actionable directional signal exists.</summary>
    None,

    /// <summary>Conditions support a long trade candidate.</summary>
    Long,

    /// <summary>Conditions support a short trade candidate.</summary>
    Short
}
