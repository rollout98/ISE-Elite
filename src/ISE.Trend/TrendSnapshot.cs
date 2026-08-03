using System;
using ISE.Core.Contexts;

namespace ISE.Trend;

/// <summary>Immutable result published by the Trend Engine.</summary>
public sealed class TrendSnapshot : EngineContext
{
    /// <summary>Initializes a Trend Engine result.</summary>
    public TrendSnapshot(Guid contextId, Guid correlationId, string tradingDayId, DateTime timestampUtc,
        string engineVersion, string configurationVersion, TrendDirection direction,
        TrendStrength strength, int confidence, bool isRanging)
        : base(contextId, correlationId, tradingDayId, timestampUtc, engineVersion, configurationVersion)
    {
        if (confidence < 0 || confidence > 100) throw new ArgumentOutOfRangeException(nameof(confidence));
        Direction = direction;
        Strength = strength;
        Confidence = confidence;
        IsRanging = isRanging;
    }

    /// <summary>Gets the directional bias.</summary>
    public TrendDirection Direction { get; }
    /// <summary>Gets the trend-strength classification.</summary>
    public TrendStrength Strength { get; }
    /// <summary>Gets confidence from 0 to 100.</summary>
    public int Confidence { get; }
    /// <summary>Gets whether market efficiency indicates ranging conditions.</summary>
    public bool IsRanging { get; }
}
