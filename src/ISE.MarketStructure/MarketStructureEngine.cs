using System;
using System.Collections.Generic;
using ISE.Core.Engines;
using ISE.MarketData;

namespace ISE.MarketStructure;

/// <summary>Detects confirmed swings and derives deterministic market structure.</summary>
public sealed class MarketStructureEngine : IEngine<MarketStructureInput, MarketStructureSnapshot>
{
    private const string EngineVersion = "0.1.0";
    private const string ConfigurationVersion = "market-structure-v1";

    /// <summary>Processes a chronological candle sequence into a market structure snapshot.</summary>
    public MarketStructureSnapshot Process(MarketStructureInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var swings = DetectSwings(input.Candles, input.PivotStrength);
        var direction = ResolveDirection(swings);
        var latestClose = input.Candles[input.Candles.Count - 1].Close;
        var latestHigh = FindLatest(swings, SwingType.High);
        var latestLow = FindLatest(swings, SwingType.Low);

        return new MarketStructureSnapshot(
            Guid.NewGuid(),
            input.CorrelationId,
            input.TradingDayId,
            input.TimestampUtc,
            EngineVersion,
            ConfigurationVersion,
            swings.AsReadOnly(),
            direction,
            latestHigh is not null && latestClose > latestHigh.Price,
            latestLow is not null && latestClose < latestLow.Price);
    }

    private static List<SwingPoint> DetectSwings(IReadOnlyList<Candle> candles, int strength)
    {
        var swings = new List<SwingPoint>();
        SwingPoint? previousHigh = null;
        SwingPoint? previousLow = null;

        for (var index = strength; index < candles.Count - strength; index++)
        {
            var candidate = candles[index];
            var isHigh = true;
            var isLow = true;

            for (var offset = 1; offset <= strength; offset++)
            {
                if (candidate.High <= candles[index - offset].High ||
                    candidate.High <= candles[index + offset].High)
                {
                    isHigh = false;
                }

                if (candidate.Low >= candles[index - offset].Low ||
                    candidate.Low >= candles[index + offset].Low)
                {
                    isLow = false;
                }
            }

            if (isHigh)
            {
                var classification = ClassifyHigh(candidate.High, previousHigh);
                previousHigh = new SwingPoint(index, candidate.CloseTimeUtc, candidate.High, SwingType.High, classification);
                swings.Add(previousHigh);
            }

            if (isLow)
            {
                var classification = ClassifyLow(candidate.Low, previousLow);
                previousLow = new SwingPoint(index, candidate.CloseTimeUtc, candidate.Low, SwingType.Low, classification);
                swings.Add(previousLow);
            }
        }

        swings.Sort((left, right) => left.CandleIndex.CompareTo(right.CandleIndex));
        return swings;
    }

    private static StructureClassification ClassifyHigh(decimal price, SwingPoint? previous)
    {
        if (previous is null) return StructureClassification.Unclassified;
        if (price > previous.Price) return StructureClassification.HigherHigh;
        if (price < previous.Price) return StructureClassification.LowerHigh;
        return StructureClassification.Equal;
    }

    private static StructureClassification ClassifyLow(decimal price, SwingPoint? previous)
    {
        if (previous is null) return StructureClassification.Unclassified;
        if (price > previous.Price) return StructureClassification.HigherLow;
        if (price < previous.Price) return StructureClassification.LowerLow;
        return StructureClassification.Equal;
    }

    private static StructureDirection ResolveDirection(IReadOnlyList<SwingPoint> swings)
    {
        var high = FindLatest(swings, SwingType.High);
        var low = FindLatest(swings, SwingType.Low);

        if (high?.Classification == StructureClassification.HigherHigh &&
            low?.Classification == StructureClassification.HigherLow)
        {
            return StructureDirection.Bullish;
        }

        if (high?.Classification == StructureClassification.LowerHigh &&
            low?.Classification == StructureClassification.LowerLow)
        {
            return StructureDirection.Bearish;
        }

        return StructureDirection.Neutral;
    }

    private static SwingPoint? FindLatest(IReadOnlyList<SwingPoint> swings, SwingType type)
    {
        for (var index = swings.Count - 1; index >= 0; index--)
        {
            if (swings[index].Type == type) return swings[index];
        }

        return null;
    }
}
