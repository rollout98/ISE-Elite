using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningMarketStateAdaptiveResearchTests
    {
        [Fact]
        public void BroadAnalyzerEmitsCausalNextBarEntries()
        {
            var bars = BuildTrendingPullbackDay(new DateTime(2026, 7, 13));
            var config = new MorningMarketStateAdaptiveConfig(contextBars: 20, shortBars: 5, structureBars: 4,
                cooldownMinutes: 5, trendEfficiency: 0.25m, strongTrendEfficiency: 0.40m,
                rangeEfficiency: 0.10m, maximumHoldMinutes: 30,
                scalpCheckpointDollars: 10m, coreCheckpointDollars: 20m, runnerCheckpointDollars: 40m,
                scalpGivebackDollars: 6m, coreGivebackDollars: 8m, runnerGivebackDollars: 12m);

            var outcomes = new MorningMarketStateAdaptiveAnalyzer(config).Analyze(bars);
            Assert.NotEmpty(outcomes);
            Assert.All(outcomes, x => Assert.True(x.EntryUtc > x.SetupUtc));
            Assert.All(outcomes, x => Assert.True(x.InitialRiskTicks > 0m));
        }

        [Fact]
        public void AnalyzerCanDiscoverMoreThanOneSetupFamily()
        {
            var bars = BuildMixedStructureDay(new DateTime(2026, 7, 14));
            var config = new MorningMarketStateAdaptiveConfig(contextBars: 20, shortBars: 5, structureBars: 4,
                cooldownMinutes: 4, trendEfficiency: 0.25m, strongTrendEfficiency: 0.40m,
                rangeEfficiency: 0.10m, maximumHoldMinutes: 20,
                scalpCheckpointDollars: 10m, coreCheckpointDollars: 20m, runnerCheckpointDollars: 40m,
                scalpGivebackDollars: 6m, coreGivebackDollars: 8m, runnerGivebackDollars: 12m);

            var outcomes = new MorningMarketStateAdaptiveAnalyzer(config).Analyze(bars);
            Assert.True(outcomes.Select(x => x.SetupType).Distinct().Count() >= 2);
        }

        [Fact]
        public void AdaptiveManagementProducesAuditableExitReason()
        {
            var bars = BuildTrendingPullbackDay(new DateTime(2026, 7, 15));
            var config = new MorningMarketStateAdaptiveConfig(contextBars: 20, shortBars: 5, structureBars: 4,
                cooldownMinutes: 8, trendEfficiency: 0.25m, strongTrendEfficiency: 0.40m,
                rangeEfficiency: 0.10m, maximumHoldMinutes: 30,
                scalpCheckpointDollars: 10m, coreCheckpointDollars: 20m, runnerCheckpointDollars: 40m,
                scalpGivebackDollars: 6m, coreGivebackDollars: 8m, runnerGivebackDollars: 12m);

            var outcome = Assert.Single(new MorningMarketStateAdaptiveAnalyzer(config).Analyze(bars).Take(1));
            Assert.NotEqual(MorningAdaptiveExitReason.None, outcome.ExitReason);
            Assert.True(outcome.ExitUtc >= outcome.EntryUtc);
            Assert.True(outcome.MaxFavorableTicks >= 0m);
            Assert.True(outcome.MaxAdverseTicks >= 0m);
        }

        private static List<HistoricalBar> BuildTrendingPullbackDay(DateTime date)
        {
            var rows = new List<(decimal open, decimal high, decimal low, decimal close)>();
            decimal p = 100m;
            for (var i = 0; i < 24; i++)
            {
                var o = p; p += 0.75m;
                rows.Add((o, p + 0.20m, o - 0.15m, p));
            }
            for (var i = 0; i < 4; i++)
            {
                var o = p; p -= 0.35m;
                rows.Add((o, o + 0.15m, p - 0.15m, p));
            }
            for (var i = 0; i < 35; i++)
            {
                var o = p; p += 0.80m;
                rows.Add((o, p + 0.20m, o - 0.15m, p));
            }
            return Build(date, rows);
        }

        private static List<HistoricalBar> BuildMixedStructureDay(DateTime date)
        {
            var rows = new List<(decimal open, decimal high, decimal low, decimal close)>();
            decimal p = 100m;
            for (var i = 0; i < 22; i++)
            {
                var o = p; p += 0.60m;
                rows.Add((o, p + 0.15m, o - 0.15m, p));
            }
            for (var i = 0; i < 5; i++)
            {
                var o = p;
                var c = p + (i % 2 == 0 ? 0.08m : -0.08m);
                rows.Add((o, Math.Max(o, c) + 0.10m, Math.Min(o, c) - 0.10m, c));
                p = c;
            }
            rows.Add((p, p + 1.80m, p - 0.10m, p + 1.60m)); p += 1.60m;
            for (var i = 0; i < 8; i++)
            {
                var o = p; p += 0.55m;
                rows.Add((o, p + 0.15m, o - 0.15m, p));
            }
            var rangeHigh = p + 0.50m;
            var rangeLow = p - 0.50m;
            for (var i = 0; i < 12; i++)
            {
                var o = p;
                var c = i % 2 == 0 ? rangeLow + 0.15m : rangeHigh - 0.15m;
                rows.Add((o, rangeHigh, rangeLow, c));
                p = c;
            }
            rows.Add((p, rangeHigh + 2.0m, rangeLow, rangeHigh + 1.70m)); p = rangeHigh + 1.70m;
            for (var i = 0; i < 15; i++)
            {
                var o = p; p += 0.45m;
                rows.Add((o, p + 0.10m, o - 0.10m, p));
            }
            return Build(date, rows);
        }

        private static List<HistoricalBar> Build(DateTime date, List<(decimal open, decimal high, decimal low, decimal close)> rows)
        {
            var result = new List<HistoricalBar>();
            var central = ResolveCentralTimeZone();
            for (var i = 0; i < rows.Count; i++)
            {
                var local = DateTime.SpecifyKind(date.Date.AddHours(3).AddMinutes(i), DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
                var row = rows[i];
                result.Add(new HistoricalBar("MNQ", "09-26", new DateTimeOffset(utc, TimeSpan.Zero), date.Date, 60,
                    row.open, row.high, row.low, row.close, 100, HistoricalDataSourceKind.NinjaTraderRepository, "test"));
            }
            return result;
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
