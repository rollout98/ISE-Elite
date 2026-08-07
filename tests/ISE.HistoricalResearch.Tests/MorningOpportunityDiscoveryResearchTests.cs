using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningOpportunityDiscoveryResearchTests
    {
        [Fact]
        public void ContinuationUsesNextBarOpenAndCompletedStructure()
        {
            var date = new DateTime(2026, 7, 6);
            var bars = BuildContinuationBars(date);
            var config = new MorningOpportunityDiscoveryConfig(trendLookbackBars: 30, compressionLookbackBars: 5,
                structuralLookbackBars: 5, minimumTrendEfficiency: 0.35m, compressionRangeFraction: 0.40m,
                cooldownMinutes: 20, maximumOutcomeMinutes: 30, intermediateObjective: 20m, lowerObjective: 40m, upperObjective: 80m);

            var outcomes = new MorningOpportunityDiscoveryAnalyzer(config).Analyze(bars);
            Assert.NotEmpty(outcomes);
            var first = outcomes[0];
            Assert.Equal(MorningOpportunityType.ContinuationResumption, first.Type);
            Assert.True(first.EntryUtc > first.SetupUtc);
            Assert.True(first.InitialRiskTicks > 0m);
        }

        [Fact]
        public void DiscoveryDoesNotEmitBeforeEnoughCausalHistory()
        {
            var date = new DateTime(2026, 7, 7);
            var bars = BuildSimpleTrend(date, 25);
            var config = new MorningOpportunityDiscoveryConfig(trendLookbackBars: 30, compressionLookbackBars: 5);
            Assert.Empty(new MorningOpportunityDiscoveryAnalyzer(config).Analyze(bars));
        }

        [Fact]
        public void EstimatedOriginNeverOccursAfterSetup()
        {
            var date = new DateTime(2026, 7, 8);
            var bars = BuildContinuationBars(date);
            var config = new MorningOpportunityDiscoveryConfig(trendLookbackBars: 30, compressionLookbackBars: 5,
                structuralLookbackBars: 5, minimumTrendEfficiency: 0.35m, compressionRangeFraction: 0.40m,
                intermediateObjective: 20m, lowerObjective: 40m, upperObjective: 80m);
            foreach (var outcome in new MorningOpportunityDiscoveryAnalyzer(config).Analyze(bars))
                Assert.True(outcome.EstimatedOriginUtc <= outcome.SetupUtc);
        }

        private static List<HistoricalBar> BuildContinuationBars(DateTime date)
        {
            var rows = new List<(decimal open, decimal high, decimal low, decimal close)>();
            decimal p = 100m;
            for (var i = 0; i < 30; i++)
            {
                var open = p;
                p += 1m;
                rows.Add((open, p + 0.25m, open - 0.25m, p));
            }
            for (var i = 0; i < 5; i++)
            {
                var open = p;
                var close = p + (i % 2 == 0 ? 0.10m : -0.10m);
                rows.Add((open, Math.Max(open, close) + 0.15m, Math.Min(open, close) - 0.15m, close));
                p = close;
            }
            rows.Add((p, p + 2.0m, p - 0.10m, p + 1.75m));
            p += 1.75m;
            for (var i = 0; i < 20; i++)
            {
                var open = p;
                p += 0.75m;
                rows.Add((open, p + 0.20m, open - 0.20m, p));
            }
            return Build(date, rows);
        }

        private static List<HistoricalBar> BuildSimpleTrend(DateTime date, int count)
        {
            var rows = new List<(decimal open, decimal high, decimal low, decimal close)>();
            decimal p = 100m;
            for (var i = 0; i < count; i++)
            {
                var open = p; p += 1m;
                rows.Add((open, p + 0.25m, open - 0.25m, p));
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
