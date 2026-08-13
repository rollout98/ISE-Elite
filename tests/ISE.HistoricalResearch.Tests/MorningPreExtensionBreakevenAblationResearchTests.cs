using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningPreExtensionBreakevenAblationResearchTests
    {
        [Fact]
        public void DefaultConfigPreservesV71BreakevenBehavior()
        {
            var config = new MorningProtectedPositionConfig();

            Assert.True(config.EnablePreExtensionAdaptiveBreakeven);
            Assert.Equal(100, config.NonAlignedBreakevenTriggerTicks);
        }

        [Fact]
        public void AblationConfigDisablesOnlyPreExtensionBreakevenSwitch()
        {
            var v71 = new MorningProtectedPositionConfig();
            var v73 = new MorningProtectedPositionConfig(
                enablePreExtensionAdaptiveBreakeven: false);

            Assert.True(v71.EnablePreExtensionAdaptiveBreakeven);
            Assert.False(v73.EnablePreExtensionAdaptiveBreakeven);

            Assert.Equal(v71.ScalpTargetTicks, v73.ScalpTargetTicks);
            Assert.Equal(v71.NonAlignedBreakevenTriggerTicks, v73.NonAlignedBreakevenTriggerTicks);
            Assert.Equal(v71.ExtensionProfitFloorTicks, v73.ExtensionProfitFloorTicks);
            Assert.Equal(v71.CoreRetentionFraction, v73.CoreRetentionFraction);
            Assert.Equal(v71.RunnerThresholdTicks, v73.RunnerThresholdTicks);
            Assert.Equal(v71.RunnerAlignedBars, v73.RunnerAlignedBars);
            Assert.Equal(v71.RunnerTrailTicks, v73.RunnerTrailTicks);
        }

        [Fact]
        public void SamePathBreakevensInV71ButKeepsStructuralRiskInV73()
        {
            var day = new DateTime(2026, 7, 31);
            var candidate = Candidate(day, 14, 0, 100m);
            var bars = BeAblationBars();

            var v71 = new MorningProtectedPositionIntelligenceAnalyzer(
                new MorningProtectedPositionConfig());

            var v73 = new MorningProtectedPositionIntelligenceAnalyzer(
                new MorningProtectedPositionConfig(
                    enablePreExtensionAdaptiveBreakeven: false));

            var current = v71.Manage(bars, candidate);
            var ablated = v73.Manage(bars, candidate);

            Assert.NotNull(current);
            Assert.NotNull(ablated);

            Assert.Equal(
                MorningProtectedPositionExitReason.AdaptiveBreakeven,
                current!.ExitReason);

            Assert.Equal(
                MorningProtectedPositionExitReason.StructuralStop,
                ablated!.ExitReason);

            Assert.True(current.AdaptiveBreakevenActivated);
            Assert.False(ablated.AdaptiveBreakevenActivated);

            Assert.Equal(
                MorningProtectedPositionMode.Scalp,
                current.FinalMode);

            Assert.Equal(
                MorningProtectedPositionMode.Scalp,
                ablated.FinalMode);
        }

        private static MorningDailySequencingCandidate Candidate(
            DateTime day,
            int hourUtc,
            int minuteUtc,
            decimal entryPrice)
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
                20m, 50m, 0.4m, 0.2m, 0.5m, 2, 1, 5m, 0.4m, 1m);

            var eo = new MorningEntryEfficiencyObservation(
                po,
                ef,
                90m);

            var sw = new MorningStabilityWeightedPotentialObservation(
                po,
                90m);

            return new MorningDailySequencingCandidate(eo, sw);
        }

        private static IReadOnlyList<HistoricalBar> BeAblationBars()
        {
            return new[]
            {
                Bar(14, 0, 100m, 125m, 100m, 124m),
                Bar(14, 1, 124m, 124m, 100m, 101m),
                Bar(14, 2, 101m, 102m, 94m, 95m)
            };
        }

        private static HistoricalBar Bar(
            int hour,
            int minute,
            decimal open,
            decimal high,
            decimal low,
            decimal close)
        {
            var timestamp = new DateTimeOffset(
                2026, 7, 31, hour, minute, 0, TimeSpan.Zero);

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
                "v7-3-be-ablation-test");
        }
    }
}
