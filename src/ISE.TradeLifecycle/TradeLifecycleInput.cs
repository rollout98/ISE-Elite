using System;

namespace ISE.TradeLifecycle;

/// <summary>Provides the current trade state and market progress for lifecycle evaluation.</summary>
public sealed class TradeLifecycleInput
{
    /// <summary>Initializes a lifecycle evaluation request.</summary>
    public TradeLifecycleInput(
        TradeLifecycleState state,
        int openQuantity,
        decimal entryPrice,
        decimal currentPrice,
        decimal stopPrice,
        decimal targetPrice,
        bool isLong,
        bool entryFilled = false,
        bool emergencyExit = false,
        bool targetReached = false,
        decimal breakEvenTrigger = 0m,
        decimal breakEvenOffset = 0m,
        decimal trailingDistance = 0m,
        int partialExitQuantity = 0,
        bool partialExitRequested = false)
    {
        if (openQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(openQuantity));
        if (entryPrice <= 0m || currentPrice <= 0m || stopPrice <= 0m || targetPrice <= 0m)
            throw new ArgumentOutOfRangeException(nameof(entryPrice), "Prices must be positive.");
        if (breakEvenTrigger < 0m || breakEvenOffset < 0m || trailingDistance < 0m)
            throw new ArgumentOutOfRangeException(nameof(breakEvenTrigger));
        if (partialExitQuantity < 0 || partialExitQuantity > openQuantity)
            throw new ArgumentOutOfRangeException(nameof(partialExitQuantity));

        State = state;
        OpenQuantity = openQuantity;
        EntryPrice = entryPrice;
        CurrentPrice = currentPrice;
        StopPrice = stopPrice;
        TargetPrice = targetPrice;
        IsLong = isLong;
        EntryFilled = entryFilled;
        EmergencyExit = emergencyExit;
        TargetReached = targetReached;
        BreakEvenTrigger = breakEvenTrigger;
        BreakEvenOffset = breakEvenOffset;
        TrailingDistance = trailingDistance;
        PartialExitQuantity = partialExitQuantity;
        PartialExitRequested = partialExitRequested;
    }

    /// <summary>Gets the current lifecycle state.</summary>
    public TradeLifecycleState State { get; }
    /// <summary>Gets the currently open quantity.</summary>
    public int OpenQuantity { get; }
    /// <summary>Gets the average entry price.</summary>
    public decimal EntryPrice { get; }
    /// <summary>Gets the current market price.</summary>
    public decimal CurrentPrice { get; }
    /// <summary>Gets the current protective stop price.</summary>
    public decimal StopPrice { get; }
    /// <summary>Gets the final target price.</summary>
    public decimal TargetPrice { get; }
    /// <summary>Gets whether the position is long.</summary>
    public bool IsLong { get; }
    /// <summary>Gets whether the pending entry has filled.</summary>
    public bool EntryFilled { get; }
    /// <summary>Gets whether an emergency close is required.</summary>
    public bool EmergencyExit { get; }
    /// <summary>Gets whether the final target has been reached.</summary>
    public bool TargetReached { get; }
    /// <summary>Gets the favorable price distance required before break-even protection.</summary>
    public decimal BreakEvenTrigger { get; }
    /// <summary>Gets the favorable offset beyond entry for break-even protection.</summary>
    public decimal BreakEvenOffset { get; }
    /// <summary>Gets the trailing distance behind current price.</summary>
    public decimal TrailingDistance { get; }
    /// <summary>Gets the requested partial-exit quantity.</summary>
    public int PartialExitQuantity { get; }
    /// <summary>Gets whether a partial exit has been requested.</summary>
    public bool PartialExitRequested { get; }
}
