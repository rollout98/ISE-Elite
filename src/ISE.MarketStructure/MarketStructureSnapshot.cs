using System;
using System.Collections.Generic;
using ISE.Core.Contexts;

namespace ISE.MarketStructure;

/// <summary>Represents the immutable result of evaluating a candle sequence.</summary>
public sealed class MarketStructureSnapshot : EngineContext
{
    /// <summary>Initializes a market structure snapshot.</summary>
    public MarketStructureSnapshot(
        Guid contextId,
        Guid correlationId,
        string tradingDayId,
        DateTime timestampUtc,
        string engineVersion,
        string configurationVersion,
        IReadOnlyList<SwingPoint> swings,
        StructureDirection direction,
        bool bullishBreakOfStructure,
        bool bearishBreakOfStructure)
        : base(contextId, correlationId, tradingDayId, timestampUtc, engineVersion, configurationVersion)
    {
        Swings = swings ?? throw new ArgumentNullException(nameof(swings));
        Direction = direction;
        BullishBreakOfStructure = bullishBreakOfStructure;
        BearishBreakOfStructure = bearishBreakOfStructure;
    }

    /// <summary>Gets all confirmed swings in chronological order.</summary>
    public IReadOnlyList<SwingPoint> Swings { get; }

    /// <summary>Gets the current structural direction.</summary>
    public StructureDirection Direction { get; }

    /// <summary>Gets whether the latest close broke above the most recent confirmed swing high.</summary>
    public bool BullishBreakOfStructure { get; }

    /// <summary>Gets whether the latest close broke below the most recent confirmed swing low.</summary>
    public bool BearishBreakOfStructure { get; }
}
