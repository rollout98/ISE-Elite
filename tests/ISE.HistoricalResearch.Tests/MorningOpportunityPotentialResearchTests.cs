using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningOpportunityPotentialResearchTests
    {
        [Fact]
        public void FutureOutcomeFieldsDoNotChangePotentialScore()
        {
            var bars = BuildTrendBars();
            var setupUtc = bars[bars.Count - 1].TimestampUtc;
            var entryUtc = setupUtc.AddMinutes(1);
            var analyzer = new MorningOpportunityPotentialAnalyzer();

            var strongFuture = Trade(setupUtc, entryUtc,
                MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation,
                realizedDollars: 900m,
                mfeTicks: 700m,
                maeTicks: 20m);

            var weakFuture = Trade(setupUtc, entryUtc,
                MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation,
                realizedDollars: -180m,
                mfeTicks: 10m,
                maeTicks: 180m);

            var a = Assert.Single(analyzer.Analyze(bars, new[] { strongFuture }));
            var b = Assert.Single(analyzer.Analyze(bars, new[] { weakFuture }));

            Assert.Equal(a.PotentialScore, b.PotentialScore);
            AssertFeaturesEqual(a.Features, b.Features);
        }

        [Fact]
        public void MarketStateAndSetupLabelsDoNotChangePotentialScore()
        {
            var bars = BuildTrendBars();
            var setupUtc = bars[bars.Count - 1].TimestampUtc;
            var entryUtc = setupUtc.AddMinutes(1);
            var analyzer = new MorningOpportunityPotentialAnalyzer();

            var trendCandidate = Trade(setupUtc, entryUtc,
                MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation,
                realizedDollars: 100m,
                mfeTicks: 120m,
                maeTicks: 20m);

            var reversalCandidate = Trade(setupUtc, entryUtc,
                MorningMarketState.Reversing,
                MorningAdaptiveSetupType.FailedBreakoutReversal,
                realizedDollars: 100m,
                mfeTicks: 120m,
                maeTicks: 20m);

            var observations = analyzer.Analyze(bars, new[] { trendCandidate, reversalCandidate });

            Assert.Equal(2, observations.Count);
            Assert.Equal(observations[0].PotentialScore, observations[1].PotentialScore);
            AssertFeaturesEqual(observations[0].Features, observations[1].Features);
        }

        [Fact]
        public void BarsAfterSetupCannotChangeEntryTimeFeaturesOrScore()
        {
            var causalBars = BuildTrendBars();
            var setupUtc = causalBars[causalBars.Count - 1].TimestampUtc;
            var entryUtc = setupUtc.AddMinutes(1);
            var candidate = Trade(setupUtc, entryUtc,
                MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation,
                realizedDollars: 200m,
                mfeTicks: 250m,
                maeTicks: 30m);

            var futureShockBars = causalBars.ToList();
            var price = causalBars[causalBars.Count - 1].Close;
            for (var i = 1; i <= 12; i++)
            {
                var open = price;
                var close = price - 20m - i;
                var high = Math.Max(open, close) + 1m;
                var low = Math.Min(open, close) - 1m;
                futureShockBars.Add(Bar(setupUtc.AddMinutes(i), open, high, low, close));
                price = close;
            }

            var analyzer = new MorningOpportunityPotentialAnalyzer();
            var causal = Assert.Single(analyzer.Analyze(causalBars, new[] { candidate }));
            var withFuture = Assert.Single(analyzer.Analyze(futureShockBars, new[] { candidate }));

            Assert.Equal(causal.PotentialScore, withFuture.PotentialScore);
            AssertFeaturesEqual(causal.Features, withFuture.Features);
        }

        [Fact]
        public void FreshEfficientResetStructureScoresHigherThanMatureExhaustedStructure()
        {
            var analyzer = new MorningOpportunityPotentialAnalyzer();

            var fresh = new MorningOpportunityPotentialFeatures(
                moveAgeBars: 6,
                consumedDisplacementTicks: 80m,
                consumedDisplacementFraction: 0.35m,
                directionalEfficiency: 0.55m,
                efficiencyDelta: 0.12m,
                compressionRatio: 0.30m,
                pullbackResetCount: 2,
                riskEfficiency: 1.80m,
                accelerationRatio: 1.35m,
                exhaustionRisk: 0.00m);

            var mature = new MorningOpportunityPotentialFeatures(
                moveAgeBars: 28,
                consumedDisplacementTicks: 300m,
                consumedDisplacementFraction: 1.05m,
                directionalEfficiency: 0.16m,
                efficiencyDelta: -0.12m,
                compressionRatio: 0.90m,
                pullbackResetCount: 7,
                riskEfficiency: 0.45m,
                accelerationRatio: 0.45m,
                exhaustionRisk: 0.90m);

            Assert.True(analyzer.Score(fresh) > analyzer.Score(mature));
            Assert.True(analyzer.Score(fresh) >= 70m);
            Assert.True(analyzer.Score(mature) < 40m);
        }

        [Fact]
        public void BucketsUseFutureOutcomesOnlyAsPostScoreDiagnostics()
        {
            var setupUtc = new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero);
            var entryUtc = setupUtc.AddMinutes(1);
            var features = new MorningOpportunityPotentialFeatures(6, 80m, 0.35m, 0.55m,
                0.12m, 0.30m, 2, 1.80m, 1.35m, 0m);

            var observations = new[]
            {
                new MorningOpportunityPotentialObservation(
                    Trade(setupUtc, entryUtc, MorningMarketState.Trending,
                        MorningAdaptiveSetupType.TrendContinuation, 300m, 400m, 30m),
                    features, 88m),
                new MorningOpportunityPotentialObservation(
                    Trade(setupUtc, entryUtc, MorningMarketState.Range,
                        MorningAdaptiveSetupType.PullbackRetest, -100m, 100m, 90m),
                    features, 88m)
            };

            var bucket = new MorningOpportunityPotentialAnalyzer()
                .BuildBuckets(observations)
                .Single(x => x.Label == "85-100");

            Assert.Equal(2, bucket.Count);
            Assert.Equal(250m, bucket.AverageMfeTicks);
            Assert.Equal(100m, bucket.AverageRealizedDollars);
            Assert.Equal(0.5m, bucket.PositiveOutcomeRate);
        }

        private static List<HistoricalBar> BuildTrendBars()
        {
            var bars = new List<HistoricalBar>();
            var start = new DateTimeOffset(2026, 7, 1, 12, 30, 0, TimeSpan.Zero);
            var price = 20000m;

            for (var i = 0; i < 30; i++)
            {
                var open = price;
                var pullback = i == 8 || i == 17 || i == 24;
                var close = pullback ? open - 1m : open + 2m;
                var high = Math.Max(open, close) + 0.50m;
                var low = Math.Min(open, close) - 0.50m;
                bars.Add(Bar(start.AddMinutes(i), open, high, low, close));
                price = close;
            }

            return bars;
        }

        private static HistoricalBar Bar(DateTimeOffset timestampUtc, decimal open, decimal high, decimal low, decimal close)
        {
            return new HistoricalBar(
                "MNQ",
                "MNQ 09-26",
                timestampUtc,
                timestampUtc.UtcDateTime.Date,
                60,
                open,
                high,
                low,
                close,
                1000,
                HistoricalDataSourceKind.NinjaTraderRepository,
                "test");
        }

        private static MorningAdaptiveTradeOutcome Trade(
            DateTimeOffset setupUtc,
            DateTimeOffset entryUtc,
            MorningMarketState state,
            MorningAdaptiveSetupType setup,
            decimal realizedDollars,
            decimal mfeTicks,
            decimal maeTicks)
        {
            return new MorningAdaptiveTradeOutcome(
                entryUtc.UtcDateTime.Date,
                state,
                setup,
                NewYorkResearchDirection.Long,
                setupUtc,
                entryUtc,
                20050m,
                20020m,
                120m,
                0.50m,
                80m,
                MorningAdaptiveManagementMode.Core,
                MorningAdaptiveExitReason.CoreCapture,
                entryUtc.AddMinutes(15),
                20075m,
                realizedDollars,
                realizedDollars,
                mfeTicks,
                maeTicks);
        }

        private static void AssertFeaturesEqual(
            MorningOpportunityPotentialFeatures a,
            MorningOpportunityPotentialFeatures b)
        {
            Assert.Equal(a.MoveAgeBars, b.MoveAgeBars);
            Assert.Equal(a.ConsumedDisplacementTicks, b.ConsumedDisplacementTicks);
            Assert.Equal(a.ConsumedDisplacementFraction, b.ConsumedDisplacementFraction);
            Assert.Equal(a.DirectionalEfficiency, b.DirectionalEfficiency);
            Assert.Equal(a.EfficiencyDelta, b.EfficiencyDelta);
            Assert.Equal(a.CompressionRatio, b.CompressionRatio);
            Assert.Equal(a.PullbackResetCount, b.PullbackResetCount);
            Assert.Equal(a.RiskEfficiency, b.RiskEfficiency);
            Assert.Equal(a.AccelerationRatio, b.AccelerationRatio);
            Assert.Equal(a.ExhaustionRisk, b.ExhaustionRisk);
        }
    }
}
