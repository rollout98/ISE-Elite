using System;
using System.Collections.Generic;
using System.Linq;
using ISE.MarketData;

namespace ISE.Liquidity;

/// <summary>Detects repeated liquidity levels and sweep/reclaim behavior.</summary>
public sealed class LiquidityEngine
{
    /// <summary>Evaluates a candle sequence for liquidity zones and sweeps.</summary>
    public LiquiditySnapshot Process(LiquidityInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var historical = input.Candles.Take(input.Candles.Count - 1).ToArray();
        var latest = input.Candles[input.Candles.Count - 1];
        var zones = new List<LiquidityZone>();

        zones.AddRange(FindZones(historical, input.Tolerance, LiquiditySide.BuySide));
        zones.AddRange(FindZones(historical, input.Tolerance, LiquiditySide.SellSide));

        var buyZones = zones.Where(zone => zone.Side == LiquiditySide.BuySide).ToArray();
        var sellZones = zones.Where(zone => zone.Side == LiquiditySide.SellSide).ToArray();

        var buySideSweep = buyZones.Any(zone => latest.High > zone.Price);
        var sellSideSweep = sellZones.Any(zone => latest.Low < zone.Price);
        var buySideReclaimed = buyZones.Any(zone => latest.High > zone.Price && latest.Close < zone.Price);
        var sellSideReclaimed = sellZones.Any(zone => latest.Low < zone.Price && latest.Close > zone.Price);

        return new LiquiditySnapshot(
            input.TimestampUtc,
            zones.AsReadOnly(),
            buySideSweep,
            sellSideSweep,
            buySideReclaimed,
            sellSideReclaimed);
    }

    private static IEnumerable<LiquidityZone> FindZones(
        IReadOnlyList<Candle> candles,
        decimal tolerance,
        LiquiditySide side)
    {
        var levels = candles
            .Select(candle => new
            {
                Price = side == LiquiditySide.BuySide ? candle.High : candle.Low,
                Time = candle.CloseTimeUtc
            })
            .OrderBy(level => level.Price)
            .ToArray();

        var used = new bool[levels.Length];
        for (var i = 0; i < levels.Length; i++)
        {
            if (used[i]) continue;

            var matches = new List<int> { i };
            for (var j = i + 1; j < levels.Length; j++)
            {
                if (Math.Abs(levels[j].Price - levels[i].Price) <= tolerance)
                    matches.Add(j);
            }

            if (matches.Count < 2) continue;

            foreach (var index in matches) used[index] = true;
            var prices = matches.Select(index => levels[index].Price).ToArray();
            var times = matches.Select(index => levels[index].Time).ToArray();

            yield return new LiquidityZone(
                side,
                prices.Average(),
                matches.Count,
                times.Min(),
                times.Max());
        }
    }
}
