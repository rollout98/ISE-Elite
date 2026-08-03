using System;
using ISE.TradePlanning;

namespace ISE.PositionManagement;

/// <summary>Contains the current position state and management policy inputs.</summary>
public sealed class PositionManagementInput
{
    /// <summary>Initializes position-management input.</summary>
    public PositionManagementInput(Guid positionId, TradeDirection direction, int openQuantity, decimal averageEntryPrice, decimal currentPrice, decimal currentStopPrice, decimal initialRiskPerContract, decimal breakEvenTriggerMultiple, decimal breakEvenOffset, decimal? trailingStopCandidate, int requestedExitQuantity)
    {
        if (positionId == Guid.Empty) throw new ArgumentException("Position ID is required.", nameof(positionId));
        if (openQuantity < 0) throw new ArgumentOutOfRangeException(nameof(openQuantity));
        if (averageEntryPrice <= 0) throw new ArgumentOutOfRangeException(nameof(averageEntryPrice));
        if (currentPrice <= 0) throw new ArgumentOutOfRangeException(nameof(currentPrice));
        if (currentStopPrice <= 0) throw new ArgumentOutOfRangeException(nameof(currentStopPrice));
        if (initialRiskPerContract <= 0) throw new ArgumentOutOfRangeException(nameof(initialRiskPerContract));
        if (breakEvenTriggerMultiple <= 0) throw new ArgumentOutOfRangeException(nameof(breakEvenTriggerMultiple));
        if (requestedExitQuantity < 0 || requestedExitQuantity > openQuantity) throw new ArgumentOutOfRangeException(nameof(requestedExitQuantity));

        PositionId = positionId;
        Direction = direction;
        OpenQuantity = openQuantity;
        AverageEntryPrice = averageEntryPrice;
        CurrentPrice = currentPrice;
        CurrentStopPrice = currentStopPrice;
        InitialRiskPerContract = initialRiskPerContract;
        BreakEvenTriggerMultiple = breakEvenTriggerMultiple;
        BreakEvenOffset = breakEvenOffset;
        TrailingStopCandidate = trailingStopCandidate;
        RequestedExitQuantity = requestedExitQuantity;
    }

    /// <summary>Gets the position identifier.</summary>
    public Guid PositionId { get; }
    /// <summary>Gets the position direction.</summary>
    public TradeDirection Direction { get; }
    /// <summary>Gets the currently open quantity.</summary>
    public int OpenQuantity { get; }
    /// <summary>Gets the average entry price.</summary>
    public decimal AverageEntryPrice { get; }
    /// <summary>Gets the current market price.</summary>
    public decimal CurrentPrice { get; }
    /// <summary>Gets the current protective-stop price.</summary>
    public decimal CurrentStopPrice { get; }
    /// <summary>Gets the original per-contract price risk.</summary>
    public decimal InitialRiskPerContract { get; }
    /// <summary>Gets the favorable-excursion multiple that activates break-even.</summary>
    public decimal BreakEvenTriggerMultiple { get; }
    /// <summary>Gets the offset applied beyond the entry price at break-even.</summary>
    public decimal BreakEvenOffset { get; }
    /// <summary>Gets an optional trailing-stop candidate.</summary>
    public decimal? TrailingStopCandidate { get; }
    /// <summary>Gets the requested exit quantity.</summary>
    public int RequestedExitQuantity { get; }
}
