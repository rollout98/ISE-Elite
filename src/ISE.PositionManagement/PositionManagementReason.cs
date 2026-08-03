namespace ISE.PositionManagement;

/// <summary>Explains a position-management decision.</summary>
public enum PositionManagementReason
{
    /// <summary>No management change is currently required.</summary>
    NoChange = 0,
    /// <summary>The protective stop should move to break-even or better.</summary>
    BreakEvenActivated = 1,
    /// <summary>A tighter trailing stop was accepted.</summary>
    TrailingStopAccepted = 2,
    /// <summary>A partial exit was requested.</summary>
    PartialExitRequested = 3,
    /// <summary>The remaining position should be closed.</summary>
    PositionClosed = 4,
    /// <summary>The requested stop would increase open risk.</summary>
    RiskIncreaseRejected = 5,
    /// <summary>The supplied position state is invalid.</summary>
    InvalidPosition = 6
}
