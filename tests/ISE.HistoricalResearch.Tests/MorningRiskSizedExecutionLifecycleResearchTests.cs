using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningRiskSizedExecutionLifecycleResearchTests
    {
        [Theory]
        [InlineData(150, 2)]
        [InlineData(151, 1)]
        [InlineData(300, 1)]
        [InlineData(301, 0)]
        public void QuantityUsesFrozenOneHundredFiftyDollarRiskObjective(
            double riskTicks,
            int expected)
        {
            var quantity = MorningPreExtensionRiskAttributionAnalyzer
                .MaximumContractsWithinRisk(
                    (decimal)riskTicks,
                    150m,
                    0.50m,
                    2);

            Assert.Equal(expected, quantity);
        }

        [Fact]
        public void RiskRejectedOpportunityDoesNotConsumeAttempt()
        {
            var day = new DateTime(2026, 7, 27);

            var rejected = Candidate(day, 14, 0, 100m, 350m);
            var tradable1 = Candidate(day, 14, 10, 100m, 100m);
            var tradable2 = Candidate(day, 14, 30, 100m, 100m);

            var bars = FlatBars(
                new DateTimeOffset(2026, 7, 27, 13, 55, 0, TimeSpan.Zero),
                120,
                100m);

            var config = FastManagementConfig();

            var replay = new MorningRiskSizedExecutionLifecycleAnalyzer(
                managementConfig: config)
                .Replay(
                    bars,
                    new[] { rejected, tradable1, tradable2 },
                    maximumAttempts: 2);

            Assert.Equal(1, replay.RejectedRisk);
            Assert.Equal(2, replay.SelectedTrades.Count);
            Assert.DoesNotContain(
                replay.SelectedTrades,
                x => x.Candidate.EntryUtc == rejected.EntryUtc);
        }

        [Fact]
        public void RiskRejectedOpportunityCreatesNoPositionOccupancy()
        {
            var day = new DateTime(2026, 7, 28);

            var rejected = Candidate(day, 14, 0, 100m, 350m);
            var immediateNext = Candidate(day, 14, 1, 100m, 100m);

            var bars = FlatBars(
                new DateTimeOffset(2026, 7, 28, 13, 55, 0, TimeSpan.Zero),
                90,
                100m);

            var replay = new MorningRiskSizedExecutionLifecycleAnalyzer(
                managementConfig: FastManagementConfig())
                .Replay(
                    bars,
                    new[] { rejected, immediateNext });

            Assert.Equal(1, replay.RejectedRisk);
            Assert.Single(replay.SelectedTrades);
            Assert.Equal(immediateNext.EntryUtc, replay.SelectedTrades[0].Candidate.EntryUtc);
            Assert.Equal(0, replay.RejectedPositionOpen);
        }

        [Fact]
        public void OneContractTradeScalesRealizedDollarsWithoutChangingTicks()
        {
            var day = new DateTime(2026, 7, 29);
            var candidate = Candidate(day, 14, 0, 100m, 200m);

            var bars = RisingBars(
                new DateTimeOffset(2026, 7, 29, 14, 0, 0, TimeSpan.Zero),
                20,
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
                scalpTimeoutMinutes: 10,
                nonAlignedBreakevenTriggerTicks: 6,
                extensionProfitFloorTicks: 6,
                runnerThresholdTicks: 20,
                runnerAlignedBars: 2,
                runnerTrailTicks: 10,
                enablePreExtensionAdaptiveBreakeven: false);

            var replay = new MorningRiskSizedExecutionLifecycleAnalyzer(
                managementConfig: config)
                .Replay(bars, new[] { candidate });

            var trade = Assert.Single(replay.SelectedTrades);

            Assert.Equal(1, trade.Quantity);
            Assert.Equal(
                trade.RealizedTicks * 0.50m,
                trade.RealizedDollars);
            Assert.True(trade.PlannedRiskDollars <= 150m);
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
                day.Year, day.Month, day.Day,
                hourUtc, minuteUtc, 0, TimeSpan.Zero);

            var stopPrice = entryPrice - initialRiskTicks * 0.25m;

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
                5, 20m, 0.2m, 0.4m, 0.2m, 0.5m, 1, 1m, 1.2m, 0.1m);

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

            return new MorningDailySequencingCandidate(eo, sw);
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
                    "v7-5-flat-unit-test"));
            }

            return result;
        }

        private static IReadOnlyList<HistoricalBar> RisingBars(
            DateTimeOffset start,
            int count,
            decimal startPrice)
        {
            var result = new List<HistoricalBar>();
            var price = startPrice;

            for (var i = 0; i < count; i++)
            {
                var timestamp = start.AddMinutes(i);
                var open = price;
                var close = price + 1m;

                result.Add(new HistoricalBar(
                    "MNQ",
                    "09-26",
                    timestamp,
                    timestamp.UtcDateTime.Date,
                    60,
                    open,
                    close + 0.25m,
                    open - 0.25m,
                    close,
                    1000L,
                    HistoricalDataSourceKind.ImportedFile,
                    "v7-5-rising-unit-test"));

                price = close;
            }

            return result;
        }
    }
}
