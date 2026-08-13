using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningProtectedPositionIntelligenceResearchTests
    {
        [Fact]
        public void DefaultProtectionMatchesFrozenV71Design()
        {
            var config = new MorningProtectedPositionConfig();

            Assert.Equal(150, config.ScalpTargetTicks);
            Assert.Equal(100, config.NonAlignedBreakevenTriggerTicks);
            Assert.Equal(100, config.ExtensionProfitFloorTicks);
            Assert.Equal(0.40m, config.CoreRetentionFraction);
            Assert.Equal(300, config.RunnerThresholdTicks);
            Assert.Equal(2, config.RunnerAlignedBars);
            Assert.Equal(250, config.RunnerTrailTicks);
        }

        [Fact]
        public void ReplayCreatesNoTradeWithoutCandidates()
        {
            var analyzer = new MorningProtectedPositionIntelligenceAnalyzer();
            var result = analyzer.ReplayFrozenStrict(
                Array.Empty<HistoricalBar>(),
                Array.Empty<MorningDailySequencingCandidate>());

            Assert.Empty(result.SelectedTrades);
        }

        [Fact]
        public void SourceOutcomeDoesNotChangeManagedResult()
        {
            var day = new DateTime(2026, 7, 27);
            var first = Candidate(day, 14, 0, 100m, 95m, 90m, 1000m);
            var second = Candidate(day, 14, 0, 100m, 95m, 90m, -1000m);

            var bars = Bars(
                new DateTimeOffset(2026, 7, 27, 13, 0, 0, TimeSpan.Zero),
                120,
                100m,
                0.02m);

            var config = FastConfig();
            var analyzer = new MorningProtectedPositionIntelligenceAnalyzer(config);

            var a = analyzer.Manage(bars, first);
            var b = analyzer.Manage(bars, second);

            Assert.NotNull(a);
            Assert.NotNull(b);
            Assert.Equal(a!.ExitUtc, b!.ExitUtc);
            Assert.Equal(a.ExitPrice, b.ExitPrice);
            Assert.Equal(a.RealizedDollars, b.RealizedDollars);
            Assert.Equal(a.FinalMode, b.FinalMode);
            Assert.Equal(a.ExitReason, b.ExitReason);
        }

        [Fact]
        public void ReplayUsesManagedExitToBlockOverlappingLaterCandidate()
        {
            var day = new DateTime(2026, 7, 28);
            var first = Candidate(day, 14, 0, 100m, 95m, 90m, 10m);
            var second = Candidate(day, 14, 2, 100m, 95m, 90m, 10m);

            var bars = FlatBars(
                new DateTimeOffset(2026, 7, 28, 13, 0, 0, TimeSpan.Zero),
                120,
                100m);

            var analyzer = new MorningProtectedPositionIntelligenceAnalyzer(FastConfig());

            var replay = analyzer.ReplayFrozenStrict(
                bars,
                new[] { first, second });

            Assert.Single(replay.SelectedTrades);
            Assert.True(replay.RejectedPositionOpen >= 1);
        }

        [Fact]
        public void ReplayRejectsPotentialBelowFrozenEightyThreshold()
        {
            var day = new DateTime(2026, 7, 29);
            var candidate = Candidate(day, 14, 0, 100m, 95m, 79.9m, 10m);

            var bars = FlatBars(
                new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero),
                120,
                100m);

            var replay = new MorningProtectedPositionIntelligenceAnalyzer(FastConfig())
                .ReplayFrozenStrict(bars, new[] { candidate });

            Assert.Empty(replay.SelectedTrades);
            Assert.Equal(1, replay.RejectedPotential);
        }

        [Fact]
        public void MaximumTwoAttemptsRemainsAuthoritative()
        {
            var day = new DateTime(2026, 7, 30);
            var candidates = new[]
            {
                Candidate(day, 14, 0, 100m, 95m, 90m, 10m),
                Candidate(day, 14, 20, 100m, 95m, 90m, 10m),
                Candidate(day, 14, 40, 100m, 95m, 90m, 10m)
            };

            var bars = FlatBars(
                new DateTimeOffset(2026, 7, 30, 13, 0, 0, TimeSpan.Zero),
                180,
                100m);

            var config = new MorningProtectedPositionConfig(
                ftcLength: 2,
                ftcAtrLength: 2,
                ftcAtrHighestLookback: 2,
                vidyaLength: 2,
                vidyaMomentum: 2,
                vidyaSmoothingLength: 1,
                vidyaAtrLength: 2,
                scalpTargetTicks: 12,
                scalpTimeoutMinutes: 5,
                nonAlignedBreakevenTriggerTicks: 6,
                extensionProfitFloorTicks: 6,
                runnerThresholdTicks: 20,
                runnerAlignedBars: 2,
                runnerTrailTicks: 10);

            var replay = new MorningProtectedPositionIntelligenceAnalyzer(config)
                .ReplayFrozenStrict(bars, candidates);

            Assert.Equal(2, replay.SelectedTrades.Count);
            Assert.True(replay.RejectedAttemptLimit >= 1);
        }

        private static MorningProtectedPositionConfig FastConfig()
        {
            return new MorningProtectedPositionConfig(
                ftcLength: 2,
                ftcAtrLength: 2,
                ftcAtrHighestLookback: 2,
                vidyaLength: 2,
                vidyaMomentum: 2,
                vidyaSmoothingLength: 1,
                vidyaAtrLength: 2,
                scalpTargetTicks: 12,
                scalpTimeoutMinutes: 10,
                nonAlignedBreakevenTriggerTicks: 6,
                extensionProfitFloorTicks: 6,
                coreRetentionFraction: 0.40m,
                runnerThresholdTicks: 20,
                runnerAlignedBars: 2,
                runnerTrailTicks: 10);
        }

        private static MorningDailySequencingCandidate Candidate(
            DateTime day,
            int hourUtc,
            int minuteUtc,
            decimal entryPrice,
            decimal entryScore,
            decimal potentialScore,
            decimal sourceRealized)
        {
            var entryUtc = new DateTimeOffset(
                day.Year, day.Month, day.Day,
                hourUtc, minuteUtc, 0, TimeSpan.Zero);

            var stop = entryPrice - 5m;

            var outcome = new MorningAdaptiveTradeOutcome(
                day,
                MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation,
                NewYorkResearchDirection.Long,
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
                sourceRealized,
                sourceRealized,
                0m,
                0m);

            var potentialFeatures = new MorningOpportunityPotentialFeatures(
                5, 20m, 0.2m, 0.4m, 0.2m, 0.5m, 1, 1m, 1.2m, 0.1m);

            var potential = new MorningOpportunityPotentialObservation(
                outcome,
                potentialFeatures,
                50m);

            var entryFeatures = new MorningEntryEfficiencyFeatures(
                20m, 50m, 0.4m, 0.2m, 0.5m, 2, 1, 5m, 0.4m, 1m);

            var entry = new MorningEntryEfficiencyObservation(
                potential,
                entryFeatures,
                entryScore);

            var weighted = new MorningStabilityWeightedPotentialObservation(
                potential,
                potentialScore);

            return new MorningDailySequencingCandidate(entry, weighted);
        }

        private static IReadOnlyList<HistoricalBar> Bars(
            DateTimeOffset start,
            int count,
            decimal startPrice,
            decimal step)
        {
            var result = new List<HistoricalBar>();
            var price = startPrice;

            for (var i = 0; i < count; i++)
            {
                var timestamp = start.AddMinutes(i);
                var open = price;
                var close = price + step;
                var high = Math.Max(open, close) + 0.05m;
                var low = Math.Min(open, close) - 0.05m;

                result.Add(new HistoricalBar(
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
                    "v7-1-unit-test"));

                price = close;
            }

            return result;
        }

        private static IReadOnlyList<HistoricalBar> FlatBars(
            DateTimeOffset start,
            int count,
            decimal price)
        {
            var result = new List<HistoricalBar>();

            for (var i = 0; i < count; i++)
            {
                var timestamp = start.AddMinutes(i);

                result.Add(new HistoricalBar(
                    "MNQ",
                    "09-26",
                    timestamp,
                    timestamp.UtcDateTime.Date,
                    60,
                    price,
                    price + 0.05m,
                    price - 0.05m,
                    price,
                    1000L,
                    HistoricalDataSourceKind.ImportedFile,
                    "v7-1-flat-unit-test"));
            }

            return result;
        }
    }
}
