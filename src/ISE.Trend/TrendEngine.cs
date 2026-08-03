using System;
using ISE.Core.Engines;

namespace ISE.Trend;

/// <summary>Produces deterministic directional trend assessments.</summary>
public sealed class TrendEngine : IEngine<TrendInput, TrendSnapshot>
{
    private const string EngineVersion = "0.1.0";
    private const string ConfigurationVersion = "trend-v1";

    /// <summary>Processes validated market measurements into a trend snapshot.</summary>
    public TrendSnapshot Process(TrendInput input)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        var bullishScore = 0;
        var bearishScore = 0;
        var bullishEvidenceCount = 0;
        var bearishEvidenceCount = 0;

        if (input.FastEma > input.SlowEma)
        {
            bullishScore += 30;
            bullishEvidenceCount++;
        }
        else if (input.FastEma < input.SlowEma)
        {
            bearishScore += 30;
            bearishEvidenceCount++;
        }

        if (input.Price > input.Vwap)
        {
            bullishScore += 25;
            bullishEvidenceCount++;
        }
        else if (input.Price < input.Vwap)
        {
            bearishScore += 25;
            bearishEvidenceCount++;
        }

        if (input.HigherTimeframeBias > 0.20m)
        {
            bullishScore += 30;
            bullishEvidenceCount++;
        }
        else if (input.HigherTimeframeBias < -0.20m)
        {
            bearishScore += 30;
            bearishEvidenceCount++;
        }

        var hasConflictingEvidence =
            bullishEvidenceCount > 0 &&
            bearishEvidenceCount > 0;

        var efficiencyPoints = (int)Math.Round(
            input.EfficiencyRatio * 15m,
            MidpointRounding.AwayFromZero);

        if (!hasConflictingEvidence)
        {
            if (bullishScore > bearishScore)
            {
                bullishScore += efficiencyPoints;
            }
            else if (bearishScore > bullishScore)
            {
                bearishScore += efficiencyPoints;
            }
        }

        var confidence = Math.Max(bullishScore, bearishScore);
        var hasLowEfficiency = input.EfficiencyRatio < 0.30m;
        var hasInsufficientSeparation = Math.Abs(bullishScore - bearishScore) < 20;
        var isRanging =
            hasLowEfficiency ||
            hasConflictingEvidence ||
            hasInsufficientSeparation;

        var direction = isRanging
            ? TrendDirection.Neutral
            : bullishScore > bearishScore
                ? TrendDirection.Bullish
                : TrendDirection.Bearish;

        var strength = confidence >= 80
            ? TrendStrength.Strong
            : confidence >= 55
                ? TrendStrength.Moderate
                : confidence >= 30
                    ? TrendStrength.Weak
                    : TrendStrength.None;

        if (direction == TrendDirection.Neutral)
        {
            strength = TrendStrength.None;
        }

        return new TrendSnapshot(
            Guid.NewGuid(),
            input.CorrelationId,
            input.TradingDayId,
            input.TimestampUtc,
            EngineVersion,
            ConfigurationVersion,
            direction,
            strength,
            Math.Min(confidence, 100),
            isRanging);
    }
}
