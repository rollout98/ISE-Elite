using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningEntryEfficiencyResearchTests
    {
        [Fact]
        public void FutureOutcomeFieldsDoNotChangeEntryEfficiencyScore()
        {
            var bars = BuildBars();
            var setupUtc = bars[bars.Count - 2].TimestampUtc;
            var entryUtc = bars[bars.Count - 1].TimestampUtc;
            var analyzer = new MorningEntryEfficiencyAnalyzer();
            var potentialFeatures = PotentialFeatures();

            var strong = new MorningOpportunityPotentialObservation(
                Trade(setupUtc, entryUtc, 500m, 600m, 20m), potentialFeatures, 82m);
            var weak = new MorningOpportunityPotentialObservation(
                Trade(setupUtc, entryUtc, -200m, 20m, 180m), potentialFeatures, 82m);

            var a = Assert.Single(analyzer.Analyze(bars, new[] { strong }));
            var b = Assert.Single(analyzer.Analyze(bars, new[] { weak }));

            Assert.Equal(a.EntryEfficiencyScore, b.EntryEfficiencyScore);
            AssertFeaturesEqual(a.Features, b.Features);
        }

        [Fact]
        public void StateAndSetupLabelsDoNotChangeEntryEfficiencyScore()
        {
            var bars = BuildBars();
            var setupUtc = bars[bars.Count - 2].TimestampUtc;
            var entryUtc = bars[bars.Count - 1].TimestampUtc;
            var analyzer = new MorningEntryEfficiencyAnalyzer();
            var potentialFeatures = PotentialFeatures();

            var a = new MorningOpportunityPotentialObservation(
                Trade(setupUtc, entryUtc, 100m, 150m, 30m, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation),
                potentialFeatures, 82m);
            var b = new MorningOpportunityPotentialObservation(
                Trade(setupUtc, entryUtc, 100m, 150m, 30m, MorningMarketState.Range, MorningAdaptiveSetupType.RangeResolution),
                potentialFeatures, 82m);

            var rows = analyzer.Analyze(bars, new[] { a, b });
            Assert.Equal(2, rows.Count);
            Assert.Equal(rows[0].EntryEfficiencyScore, rows[1].EntryEfficiencyScore);
            AssertFeaturesEqual(rows[0].Features, rows[1].Features);
        }

        [Fact]
        public void BarsAfterEntryCannotChangeEntryEfficiencyFeaturesOrScore()
        {
            var bars = BuildBars();
            var setupUtc = bars[bars.Count - 2].TimestampUtc;
            var entryUtc = bars[bars.Count - 1].TimestampUtc;
            var potential = new MorningOpportunityPotentialObservation(
                Trade(setupUtc, entryUtc, 100m, 150m, 30m), PotentialFeatures(), 82m);

            var withFuture = bars.ToList();
            var price = bars[bars.Count - 1].Close;
            for (var i = 1; i <= 8; i++)
            {
                var open = price;
                var close = price + 20m + i;
                withFuture.Add(Bar(entryUtc.AddMinutes(i), open, Math.Max(open, close) + 1m, Math.Min(open, close) - 1m, close));
                price = close;
            }

            var analyzer = new MorningEntryEfficiencyAnalyzer();
            var a = Assert.Single(analyzer.Analyze(bars, new[] { potential }));
            var b = Assert.Single(analyzer.Analyze(withFuture, new[] { potential }));

            Assert.Equal(a.EntryEfficiencyScore, b.EntryEfficiencyScore);
            AssertFeaturesEqual(a.Features, b.Features);
        }

        [Fact]
        public void EfficientStructureScoresHigherThanChasedStructure()
        {
            var analyzer = new MorningEntryEfficiencyAnalyzer();
            var efficient = new MorningEntryEfficiencyFeatures(
                80m, 240m, 0.33m, 0.35m, 0.45m, 2, 1, 24m, 0.30m, 2m);
            var chased = new MorningEntryEfficiencyFeatures(
                220m, 240m, 0.92m, 0.05m, 0.95m, 0, 9, 100m, 0.90m, 35m);

            Assert.True(analyzer.Score(efficient) > analyzer.Score(chased));
            Assert.True(analyzer.Score(efficient) >= 70m);
            Assert.True(analyzer.Score(chased) < 40m);
        }

        [Fact]
        public void DecisionMatrixKeepsPotentialAndEntryEfficiencyAsSeparateAxes()
        {
            Assert.Equal(MorningOpportunityDecisionClass.Prime, MorningEntryEfficiencyAnalyzer.DecisionFor("High", "High"));
            Assert.Equal(MorningOpportunityDecisionClass.Wait, MorningEntryEfficiencyAnalyzer.DecisionFor("High", "Low"));
            Assert.Equal(MorningOpportunityDecisionClass.Good, MorningEntryEfficiencyAnalyzer.DecisionFor("Medium", "High"));
            Assert.Equal(MorningOpportunityDecisionClass.Scalp, MorningEntryEfficiencyAnalyzer.DecisionFor("Low", "High"));
            Assert.Equal(MorningOpportunityDecisionClass.Reject, MorningEntryEfficiencyAnalyzer.DecisionFor("Low", "Low"));
        }

        private static MorningOpportunityPotentialFeatures PotentialFeatures()
            => new MorningOpportunityPotentialFeatures(6, 80m, 0.35m, 0.55m, 0.12m, 0.30m, 2, 1.8m, 1.35m, 0m);

        private static List<HistoricalBar> BuildBars()
        {
            var bars = new List<HistoricalBar>();
            var start = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
            var price = 20000m;
            for (var i = 0; i < 24; i++)
            {
                var open = price;
                var pullback = i == 7 || i == 14 || i == 20;
                var close = pullback ? open - 1.25m : open + 1.75m;
                var high = Math.Max(open, close) + 0.50m;
                var low = Math.Min(open, close) - 0.50m;
                bars.Add(Bar(start.AddMinutes(i), open, high, low, close));
                price = close;
            }
            return bars;
        }

        private static HistoricalBar Bar(DateTimeOffset timestampUtc, decimal open, decimal high, decimal low, decimal close)
        {
            return new HistoricalBar("MNQ", "MNQ 09-26", timestampUtc, timestampUtc.UtcDateTime.Date, 60,
                open, high, low, close, 1000, HistoricalDataSourceKind.NinjaTraderRepository, "test");
        }

        private static MorningAdaptiveTradeOutcome Trade(
            DateTimeOffset setupUtc,
            DateTimeOffset entryUtc,
            decimal realized,
            decimal mfe,
            decimal mae,
            MorningMarketState state = MorningMarketState.Trending,
            MorningAdaptiveSetupType setup = MorningAdaptiveSetupType.TrendContinuation)
        {
            return new MorningAdaptiveTradeOutcome(entryUtc.UtcDateTime.Date, state, setup,
                NewYorkResearchDirection.Long, setupUtc, entryUtc, 20035m, 20005m, 120m, 0.50m, 80m,
                MorningAdaptiveManagementMode.Core, MorningAdaptiveExitReason.CoreCapture,
                entryUtc.AddMinutes(10), 20055m, realized, realized, mfe, mae);
        }

        private static void AssertFeaturesEqual(MorningEntryEfficiencyFeatures a, MorningEntryEfficiencyFeatures b)
        {
            Assert.Equal(a.InitialRiskTicks, b.InitialRiskTicks);
            Assert.Equal(a.ContextRangeTicks, b.ContextRangeTicks);
            Assert.Equal(a.StructuralRiskFraction, b.StructuralRiskFraction);
            Assert.Equal(a.PullbackDepthFraction, b.PullbackDepthFraction);
            Assert.Equal(a.EntryLocationFraction, b.EntryLocationFraction);
            Assert.Equal(a.ResetCount, b.ResetCount);
            Assert.Equal(a.BarsSinceLastReset, b.BarsSinceLastReset);
            Assert.Equal(a.ReclaimTicks, b.ReclaimTicks);
            Assert.Equal(a.ShortRangeFraction, b.ShortRangeFraction);
            Assert.Equal(a.SetupToEntryMinutes, b.SetupToEntryMinutes);
        }
    }
}
