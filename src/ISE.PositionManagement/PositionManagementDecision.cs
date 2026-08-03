namespace ISE.PositionManagement;

/// <summary>Represents a platform-independent position-management instruction.</summary>
public sealed class PositionManagementDecision
{
    /// <summary>Initializes a position-management decision.</summary>
    public PositionManagementDecision(PositionManagementAction action, PositionManagementReason reason, int exitQuantity, int remainingQuantity, decimal? replacementStopPrice)
    {
        Action = action;
        Reason = reason;
        ExitQuantity = exitQuantity;
        RemainingQuantity = remainingQuantity;
        ReplacementStopPrice = replacementStopPrice;
    }

    /// <summary>Gets the requested management action.</summary>
    public PositionManagementAction Action { get; }
    /// <summary>Gets the decision reason.</summary>
    public PositionManagementReason Reason { get; }
    /// <summary>Gets the quantity to exit.</summary>
    public int ExitQuantity { get; }
    /// <summary>Gets the quantity expected to remain after the action.</summary>
    public int RemainingQuantity { get; }
    /// <summary>Gets the requested replacement stop price, when applicable.</summary>
    public decimal? ReplacementStopPrice { get; }
}
