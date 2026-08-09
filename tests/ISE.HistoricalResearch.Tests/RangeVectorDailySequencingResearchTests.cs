using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class RangeVectorDailySequencingResearchTests
    {
        [Fact]
        public void V4NeverSpendsMoreThanTwoAttemptsPerDay()
        {
            var bars = BuildReversalSession(new DateTime(2026, 7, 27));
            var config = TestSelectionConfig(MorningResearchAccountStage.Combine, maximumRiskTicks: 500m);
            var days = new RangeVectorDailySequencingAnalyzer(config).Analyze(bars);

            Assert.NotEmpty(days);
            Assert.All(days, x => Assert.True(x.Attempts <= 2));
            Assert.Contains(days, x => x.Attempts > 0);
        }

        [Fact]
        public void V4SelectedEntriesRespectFundedRiskAndActionableReadiness()
        {
            var bars = BuildReversalSession(new DateTime(2026, 7, 28));
            var config = TestSelectionConfig(MorningResearchAccountStage.Funded, maximumRiskTicks: 250m);
            var days = new RangeVectorDailySequencingAnalyzer(config).Analyze(bars);
            var selected = days.SelectMany(x => x.SelectedTrades).ToList();

            Assert.NotEmpty(selected);
            Assert.All(selected, x => Assert.True(x.Source.InitialRiskTicks!.Value <= 250m));
            Assert.All(selected, x => Assert.True(x.Decision.Readiness >= MorningOpportunityReadiness.Actionable));
        }

        [Fact]
        public void VectorFlowBiasCannotChangeV4EntryScore()
        {
            var bars = BuildReversalSession(new DateTime(2026, 7, 29));
            var indicator = TestIndicatorConfig();
            var efficient = new EfficientAdaptiveRangeVectorAnalyzer(new EfficientAdaptiveRangeVectorConfig(
                MorningResearchAccountStage.Combine, indicator, maximumStructuralRiskTicks: 500m)).Analyze(bars)
                .First(x => x.Selected);
            var analyzer = new RangeVectorDailySequencingAnalyzer(TestSelectionConfig(
                MorningResearchAccountStage.Combine, maximumRiskTicks: 500m));

            var oppositeBias = efficient.VectorBiasAtEntry == VectorFlowResearchBias.Bullish
                ? VectorFlowResearchBias.Bearish : VectorFlowResearchBias.Bullish;
            var clone = new EfficientAdaptiveRangeVectorOutcome(efficient.Source, efficient.Stage, efficient.Disposition,
                efficient.Reason, efficient.EntryUtc, efficient.EntryPrice, efficient.InitialRiskTicks,
                efficient.DeferralMinutes, oppositeBias, efficient.ManagedOutcome);

            Assert.Equal(analyzer.Score(bars, efficient), analyzer.Score(bars, clone));
        }

        [Fact]
        public void FutureManagedOutcomeCannotChangeV4EntryScore()
        {
            var bars = BuildReversalSession(new DateTime(2026, 7, 30));
            var indicator = TestIndicatorConfig();
            var efficient = new EfficientAdaptiveRangeVectorAnalyzer(new EfficientAdaptiveRangeVectorConfig(
                MorningResearchAccountStage.Combine, indicator, maximumStructuralRiskTicks: 500m)).Analyze(bars)
                .First(x => x.Selected && x.ManagedOutcome != null);
            var analyzer = new RangeVectorDailySequencingAnalyzer(TestSelectionConfig(
                MorningResearchAccountStage.Combine, maximumRiskTicks: 500m));
            var managed = efficient.ManagedOutcome!;
            var alteredManaged = new EfficientAdaptiveManagedOutcome(managed.FinalMode, managed.ExitReason,
                managed.ExitUtc, managed.ExitPrice, managed.RealizedTicks + 1000m, managed.RealizedDollars + 1000m,
                managed.MaxFavorableTicks + 1000m, managed.MaxAdverseTicks, managed.ExtensionActivated,
                managed.AdaptiveBreakevenActivated, managed.BestProtectedTicks);
            var clone = new EfficientAdaptiveRangeVectorOutcome(efficient.Source, efficient.Stage, efficient.Disposition,
                efficient.Reason, efficient.EntryUtc, efficient.EntryPrice, efficient.InitialRiskTicks,
                efficient.DeferralMinutes, efficient.VectorBiasAtEntry, alteredManaged);

            Assert.Equal(analyzer.Score(bars, efficient), analyzer.Score(bars, clone));
        }

        private static RangeVectorDailySelectionConfig TestSelectionConfig(MorningResearchAccountStage stage,
            decimal maximumRiskTicks)
        {
            return new RangeVectorDailySelectionConfig(stage, TestIndicatorConfig(), maximumAttempts: 2,
                maximumStructuralRiskTicks: maximumRiskTicks,
                tradeableScore: 1m, actionableScore: 2m, exceptionalScore: 3m,
                lowerObjectiveDollars: 500m, upperObjectiveDollars: 1000m,
                greenProtectionThresholdDollars: 300m, protectedGreenFloorDollars: 200m,
                contextBars: 30, shortBars: 8);
        }

        private static RangeEntryVectorFlowHoldConfig TestIndicatorConfig()
        {
            return new RangeEntryVectorFlowHoldConfig(
                rangeTimeframeMinutes: 3,
                vectorTimeframeMinutes: 5,
                rangeSamplingPeriod: 3,
                rangeMultiplier: 0.8m,
                ftcLength: 3,
                ftcAtrLength: 2,
                ftcAtrHighestLookback: 2,
                vidyaLength: 3,
                vidyaMomentum: 2,
                vidyaSmoothingLength: 1,
                vidyaAtrLength: 2,
                vidyaBandDistance: 0.5m,
                structureLookbackRangeBars: 3,
                scalpTargetTicks: 150,
                scalpTimeoutMinutes: 30,
                runnerThresholdTicks: 300,
                runnerAlignedBars: 1);
        }

        private static List<HistoricalBar> BuildReversalSession(DateTime tradingDay)
        {
            var prices = new List<decimal>();
            decimal p = 100m;
            for (var i = 0; i < 1080; i++)
            {
                if (i < 700) p -= 0.05m;
                else if (i < 760) p -= 0.20m;
                else if (i < 920) p += 0.30m;
                else p -= 0.25m;
                prices.Add(p);
            }
            return Build(tradingDay, prices);
        }

        private static List<HistoricalBar> Build(DateTime tradingDay, IReadOnlyList<decimal> closes)
        {
            var result = new List<HistoricalBar>();
            var central = ResolveCentralTimeZone();
            var startLocal = tradingDay.Date.AddDays(-1).AddHours(17);
            decimal previous = closes[0];
            for (var i = 0; i < closes.Count; i++)
            {
                var close = closes[i];
                var open = i == 0 ? close : previous;
                var high = Math.Max(open, close) + 0.05m;
                var low = Math.Min(open, close) - 0.05m;
                var local = DateTime.SpecifyKind(startLocal.AddMinutes(i), DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
                result.Add(new HistoricalBar("MNQ", "09-26", new DateTimeOffset(utc, TimeSpan.Zero), tradingDay.Date, 60,
                    open, high, low, close, 100, HistoricalDataSourceKind.NinjaTraderRepository, "test"));
                previous = close;
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
