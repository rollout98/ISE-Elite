using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningPositionManagementAttributionResearchTests
    {
        [Fact]
        public void EmptyManagedSetProducesNoAttribution()
        {
            var analyzer = new MorningPositionManagementAttributionAnalyzer();

            var result = analyzer.Analyze(
                Array.Empty<HistoricalBar>(),
                Array.Empty<MorningProtectedManagedTrade>());

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(0, "000-099")]
        [InlineData(99.9, "000-099")]
        [InlineData(100, "100-149")]
        [InlineData(149.9, "100-149")]
        [InlineData(150, "150-299")]
        [InlineData(299.9, "150-299")]
        [InlineData(300, "300-499")]
        [InlineData(499.9, "300-499")]
        [InlineData(500, "500+")]
        public void MfeBandsUseFrozenDiagnosticBoundaries(double ticks, string expected)
        {
            Assert.Equal(
                expected,
                MorningPositionManagementAttributionAnalyzer.MfeBand((decimal)ticks));
        }

        [Fact]
        public void LongAttributionComputesFullAndPostExitMfe()
        {
            var candidate = Candidate(
                new DateTime(2026, 7, 27),
                NewYorkResearchDirection.Long,
                14, 0,
                100m,
                baselineRealizedTicks: 2m);

            var managed = Managed(
                candidate,
                new DateTimeOffset(2026, 7, 27, 14, 2, 0, TimeSpan.Zero),
                100.50m,
                realizedTicks: 2m,
                MorningProtectedPositionExitReason.ScalpCapture);

            var bars = new[]
            {
                Bar(2026, 7, 27, 14, 0, 100m, 100.25m, 99.75m, 100m),
                Bar(2026, 7, 27, 14, 1, 100m, 100.50m, 99.90m, 100.25m),
                Bar(2026, 7, 27, 14, 2, 100.25m, 100.75m, 100m, 100.50m),
                Bar(2026, 7, 27, 14, 3, 100.50m, 101.50m, 100.25m, 101m)
            };

            var observation = new MorningPositionManagementAttributionAnalyzer()
                .Analyze(bars, new[] { managed })
                .Single();

            Assert.Equal(6m, observation.FullPathMfeTicks);
            Assert.Equal(6m, observation.PostExitMfeTicks);
            Assert.Equal("000-099", observation.FullPathMfeBand);
        }

        [Fact]
        public void ShortAttributionUsesLowAsFavorableExcursion()
        {
            var candidate = Candidate(
                new DateTime(2026, 7, 28),
                NewYorkResearchDirection.Short,
                14, 0,
                100m,
                baselineRealizedTicks: 1m);

            var managed = Managed(
                candidate,
                new DateTimeOffset(2026, 7, 28, 14, 1, 0, TimeSpan.Zero),
                99.75m,
                realizedTicks: 1m,
                MorningProtectedPositionExitReason.ScalpCapture);

            var bars = new[]
            {
                Bar(2026, 7, 28, 14, 0, 100m, 100.25m, 99.75m, 100m),
                Bar(2026, 7, 28, 14, 1, 100m, 100.10m, 99.50m, 99.75m),
                Bar(2026, 7, 28, 14, 2, 99.75m, 100m, 98.50m, 99m)
            };

            var observation = new MorningPositionManagementAttributionAnalyzer()
                .Analyze(bars, new[] { managed })
                .Single();

            Assert.Equal(6m, observation.FullPathMfeTicks);
            Assert.Equal(6m, observation.PostExitMfeTicks);
        }

        [Fact]
        public void AttributionReportsManagedDeltaWithoutChangingTrade()
        {
            var candidate = Candidate(
                new DateTime(2026, 7, 29),
                NewYorkResearchDirection.Long,
                14, 0,
                100m,
                baselineRealizedTicks: 10m);

            var managed = Managed(
                candidate,
                new DateTimeOffset(2026, 7, 29, 14, 1, 0, TimeSpan.Zero),
                101.25m,
                realizedTicks: 5m,
                MorningProtectedPositionExitReason.ExtensionFloor);

            var bars = new[]
            {
                Bar(2026, 7, 29, 14, 0, 100m, 101m, 99.75m, 100.50m),
                Bar(2026, 7, 29, 14, 1, 100.50m, 101.25m, 100.25m, 101m)
            };

            var beforeExit = managed.ExitPrice;
            var observation = new MorningPositionManagementAttributionAnalyzer()
                .Analyze(bars, new[] { managed })
                .Single();

            Assert.Equal(managed.RealizedDollars - candidate.Entry.Source.Source.RealizedDollars,
                observation.ManagedDeltaDollars);
            Assert.Equal(beforeExit, managed.ExitPrice);
        }

        [Fact]
        public void PostExitThresholdFlagsAreDiagnosticOnly()
        {
            var candidate = Candidate(
                new DateTime(2026, 7, 30),
                NewYorkResearchDirection.Long,
                14, 0,
                100m,
                baselineRealizedTicks: 0m);

            var managed = Managed(
                candidate,
                new DateTimeOffset(2026, 7, 30, 14, 0, 0, TimeSpan.Zero),
                100m,
                realizedTicks: 0m,
                MorningProtectedPositionExitReason.AdaptiveBreakeven);

            var bars = new[]
            {
                Bar(2026, 7, 30, 14, 0, 100m, 100.25m, 99.75m, 100m),
                Bar(2026, 7, 30, 14, 1, 100m, 140m, 99.75m, 120m),
                Bar(2026, 7, 30, 14, 2, 120m, 230m, 119m, 220m)
            };

            var observation = new MorningPositionManagementAttributionAnalyzer()
                .Analyze(bars, new[] { managed })
                .Single();

            Assert.True(observation.PostExitReached150);
            Assert.True(observation.PostExitReached300);
            Assert.True(observation.PostExitReached500);
            Assert.Equal(MorningProtectedPositionExitReason.AdaptiveBreakeven,
                managed.ExitReason);
        }

        private static MorningDailySequencingCandidate Candidate(
            DateTime day,
            NewYorkResearchDirection direction,
            int hourUtc,
            int minuteUtc,
            decimal entryPrice,
            decimal baselineRealizedTicks)
        {
            var entryUtc = new DateTimeOffset(
                day.Year, day.Month, day.Day,
                hourUtc, minuteUtc, 0, TimeSpan.Zero);

            var stop = direction == NewYorkResearchDirection.Long
                ? entryPrice - 5m
                : entryPrice + 5m;

            var baselineDollars = baselineRealizedTicks;

            var outcome = new MorningAdaptiveTradeOutcome(
                day,
                MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation,
                direction,
                entryUtc.AddMinutes(-1),
                entryUtc,
                entryPrice,
                stop,
                20m,
                0.5m,
                20m,
                MorningAdaptiveManagementMode.Core,
                MorningAdaptiveExitReason.CoreCapture,
                entryUtc.AddMinutes(10),
                entryPrice,
                baselineRealizedTicks,
                baselineDollars,
                100m,
                50m);

            var pf = new MorningOpportunityPotentialFeatures(
                5, 20m, 0.2m, 0.4m, 0.2m, 0.5m, 1, 1m, 1.2m, 0.1m);

            var po = new MorningOpportunityPotentialObservation(outcome, pf, 50m);

            var ef = new MorningEntryEfficiencyFeatures(
                20m, 50m, 0.4m, 0.2m, 0.5m, 2, 1, 5m, 0.4m, 1m);

            var eo = new MorningEntryEfficiencyObservation(po, ef, 90m);
            var sw = new MorningStabilityWeightedPotentialObservation(po, 90m);

            return new MorningDailySequencingCandidate(eo, sw);
        }

        private static MorningProtectedManagedTrade Managed(
            MorningDailySequencingCandidate candidate,
            DateTimeOffset exitUtc,
            decimal exitPrice,
            decimal realizedTicks,
            MorningProtectedPositionExitReason reason)
        {
            return new MorningProtectedManagedTrade(
                candidate,
                MorningProtectedPositionMode.Scalp,
                reason,
                exitUtc,
                exitPrice,
                realizedTicks,
                realizedTicks,
                10m,
                5m,
                false,
                reason == MorningProtectedPositionExitReason.AdaptiveBreakeven,
                0m,
                0);
        }

        private static HistoricalBar Bar(
            int year,
            int month,
            int day,
            int hour,
            int minute,
            decimal open,
            decimal high,
            decimal low,
            decimal close)
        {
            var timestamp = new DateTimeOffset(
                year, month, day, hour, minute, 0, TimeSpan.Zero);

            return new HistoricalBar(
                "MNQ",
                "09-26",
                timestamp,
                timestamp.UtcDateTime.Date,
                60,
                open,
                high,
                low,
                close,
                1000L,
                HistoricalDataSourceKind.ImportedFile,
                "v7-2-attribution-test");
        }
    }
}
