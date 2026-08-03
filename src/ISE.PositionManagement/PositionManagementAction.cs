namespace ISE.PositionManagement;

/// <summary>Identifies a requested position-management action.</summary>
public enum PositionManagementAction
{
    /// <summary>No order change is required.</summary>
    None = 0,
    /// <summary>Move the protective stop to break-even or better.</summary>
    MoveStop = 1,
    /// <summary>Reduce part of the open position.</summary>
    PartialExit = 2,
    /// <summary>Close the remaining position.</summary>
    ClosePosition = 3
}
