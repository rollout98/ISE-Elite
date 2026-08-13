using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningRiskControlDecompositionResearchTests
    {
        [Fact]
        public void FixedTwoAlwaysReturnsTwoForPositiveRisk()
        {
            var analyzer = new MorningRiskControlDecompositionAnalyzer();

            Assert.Equal(
                2,
                analyzer.ResolveQuantity(
                    600m,
                    MorningRiskControlPolicy.FixedTwo));
        }

        [Fact]
        public void SizeTwoOrOneFallsToOneButNeverRiskRejectsPositiveRisk()
        {
            var analyzer = new MorningRiskControlDecompositionAnalyzer();

            Assert.Equal(
                2,
                analyzer.ResolveQuantity(
                    150m,
                    MorningRiskControlPolicy.SizeTwoOrOne));

            Assert.Equal(
                1,
                analyzer.ResolveQuantity(
                    151m,
                    MorningRiskControlPolicy.SizeTwoOrOne));

            Assert.Equal(
                1,
                analyzer.ResolveQuantity(
                    600m,
                    MorningRiskControlPolicy.SizeTwoOrOne));
        }

        [Fact]
        public void StrictTwoOneZeroPreservesV75QuantityBoundaries()
        {
            var analyzer = new MorningRiskControlDecompositionAnalyzer();

            Assert.Equal(
                2,
                analyzer.ResolveQuantity(
                    150m,
                    MorningRiskControlPolicy.StrictTwoOneZero));

            Assert.Equal(
                1,
                analyzer.ResolveQuantity(
                    300m,
                    MorningRiskControlPolicy.StrictTwoOneZero));

            Assert.Equal(
                0,
                analyzer.ResolveQuantity(
                    301m,
                    MorningRiskControlPolicy.StrictTwoOneZero));
        }

        [Fact]
        public void SizeOnlyHighRiskOpportunityStillConsumesExecutedAttempt()
        {
            var day = new DateTime(2026, 7, 27);
            var highRisk = Candidate(day, 14, 0, 100m, 350m);
            var later = Candidate(day, 14, 20, 100m, 100m);

            var bars = FlatBars(
                new DateTimeOffset(2026, 7, 27, 13, 55, 0, TimeSpan.Zero),
                120,
                100m);

            var replay = new MorningRiskControlDecompositionAnalyzer(
                managementConfig: FastManagementConfig())
                .Replay(
                    bars,
                    new[] { highRisk, later },
                    MorningRiskControlPolicy.SizeTwoOrOne,
                    maximumAttempts: 1);

            var trade = Assert.Single(replay.SelectedTrades);

            Assert.Equal(highRisk.EntryUtc, trade.Candidate.EntryUtc);
            Assert.Equal(1, trade.Quantity);
            Assert.Equal(0, replay.RejectedRisk);
        }

        [Fact]
        public void StrictHighRiskRejectionExposesLaterOpportunity()
        {
            var day = new DateTime(2026, 7, 28);
            var highRisk = Candidate(day, 14, 0, 100m, 350m);
            var later = Candidate(day, 14, 1, 100m, 100m);

            var bars = FlatBars(
                new DateTimeOffset(2026, 7, 28, 13, 55, 0, TimeSpan.Zero),
                120,
                100m);

            var replay = new MorningRiskControlDecompositionAnalyzer(
                managementConfig: FastManagementConfig())
                .Replay(
                    bars,
                    new[] { highRisk, later },
                    MorningRiskControlPolicy.StrictTwoOneZero);

            Assert.Equal(1, replay.RejectedRisk);

            var trade = Assert.Single(replay.SelectedTrades);

            Assert.Equal(later.EntryUtc, trade.Candidate.EntryUtc);
            Assert.Equal(2, trade.Quantity);
        }

        [Fact]
        public void FixedTwoAndSizeOnlyHaveSameTradeIdentityWhenQuantityCannotChangeManagement()
        {
            var day = new DateTime(2026, 7, 29);
            var candidates = new[]
            {
                Candidate(day, 14, 0, 100m, 200m),
                Candidate(day, 14, 20, 100m, 100m)
            };

            var bars = FlatBars(
                new DateTimeOffset(2026, 7, 29, 13, 55, 0, TimeSpan.Zero),
                120,
                100m);

            var analyzer = new MorningRiskControlDecompositionAnalyzer(
                managementConfig: FastManagementConfig());

            var fixedTwo = analyzer.Replay(
                bars,
                candidates,
                MorningRiskControlPolicy.FixedTwo);

            var sizeOnly = analyzer.Replay(
                bars,
                candidates,
                MorningRiskControlPolicy.SizeTwoOrOne);

            Assert.Equal(
                fixedTwo.SelectedTrades.Select(x => x.Candidate.EntryUtc),
                sizeOnly.SelectedTrades.Select(x => x.Candidate.EntryUtc));

            Assert.Equal(
                fixedTwo.SelectedTrades.Select(x => x.ExitUtc),
                sizeOnly.SelectedTrades.Select(x => x.ExitUtc));
        }

        private static MorningProtectedPositionConfig FastManagementConfig()
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
                scalpTimeoutMinutes: 5,
                nonAlignedBreakevenTriggerTicks: 6,
                extensionProfitFloorTicks: 6,
                runnerThresholdTicks: 20,
                runnerAlignedBars: 2,
                runnerTrailTicks: 10,
                enablePreExtensionAdaptiveBreakeven: false);
        }

        private static MorningDailySequencingCandidate Candidate(
            DateTime day,
            int hourUtc,
            int minuteUtc,
            decimal entryPrice,
            decimal initialRiskTicks)
        {
            var entryUtc = new DateTimeOffset(
                day.Year,
                day.Month,
                day.Day,
                hourUtc,
                minuteUtc,
                0,
                TimeSpan.Zero);

            var stopPrice =
                entryPrice - initialRiskTicks * 0.25m;

            var outcome = new MorningAdaptiveTradeOutcome(
                day,
                MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation,
                NewYorkResearchDirection.Long,
                entryUtc.AddMinutes(-1),
                entryUtc,
                entryPrice,
                stopPrice,
                initialRiskTicks,
                0.5m,
                initialRiskTicks,
                MorningAdaptiveManagementMode.Core,
                MorningAdaptiveExitReason.CoreCapture,
                entryUtc.AddMinutes(10),
                entryPrice,
                0m,
                0m,
                0m,
                0m);

            var pf = new MorningOpportunityPotentialFeatures(
                5,
                20m,
                0.2m,
                0.4m,
                0.2m,
                0.5m,
                1,
                1m,
                1.2m,
                0.1m);

            var po = new MorningOpportunityPotentialObservation(
                outcome,
                pf,
                50m);

            var ef = new MorningEntryEfficiencyFeatures(
                initialRiskTicks,
                50m,
                0.4m,
                0.2m,
                0.5m,
                2,
                1,
                5m,
                0.4m,
                1m);

            var eo = new MorningEntryEfficiencyObservation(
                po,
                ef,
                90m);

            var sw = new MorningStabilityWeightedPotentialObservation(
                po,
                90m);

            return new MorningDailySequencingCandidate(
                eo,
                sw);
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
                    "v7-6-risk-control-test"));
            }

            return result;
        }
    }
}
