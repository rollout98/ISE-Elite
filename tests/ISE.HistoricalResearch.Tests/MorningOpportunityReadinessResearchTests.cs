using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningOpportunityReadinessResearchTests
    {
        [Fact]
        public void EarlyTradeableCandidateCanBeDeferredForLaterActionableCandidate()
        {
            var day = new DateTime(2026, 7, 7);
            var early = Build(day, 4, 0, MorningMarketState.Compressing, MorningAdaptiveSetupType.CompressionResumption, 0.35m, 70m, 30m, 40m);
            var later = Build(day, 7, 10, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 0.50m, 120m, 300m, 500m);
            var analyzer = new MorningOpportunityReadinessAnalyzer(MorningOpportunityReadinessConfig.CombineDefault);

            var outcome = Assert.Single(analyzer.Analyze(new[] { early, later }));
            Assert.Single(outcome.SelectedTrades);
            Assert.Equal(later.EntryUtc, outcome.SelectedTrades[0].Source.EntryUtc);
            Assert.Contains(outcome.Decisions, x => x.Candidate.EntryUtc == early.EntryUtc && !x.Selected);
        }

        [Fact]
        public void SelectionNeverUsesFutureOutcomeToBuildReadinessScore()
        {
            var day = new DateTime(2026, 7, 8);
            var a = Build(day, 7, 15, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 0.48m, 110m, -100m, 10m);
            var b = Build(day, 7, 15, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 0.48m, 110m, 900m, 900m);
            var analyzer = new MorningOpportunityReadinessAnalyzer(MorningOpportunityReadinessConfig.FundedDefault);
            Assert.Equal(analyzer.ReadinessScore(a), analyzer.ReadinessScore(b));
        }

        [Fact]
        public void MaximumAttemptsRemainTwo()
        {
            var day = new DateTime(2026, 7, 9);
            var candidates = new List<MorningAdaptiveTradeOutcome>
            {
                Build(day, 7, 0, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 0.55m, 100m, 50m, 200m, holdMinutes: 5),
                Build(day, 7, 20, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 0.55m, 100m, 50m, 200m, holdMinutes: 5),
                Build(day, 7, 40, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 0.55m, 100m, 50m, 200m, holdMinutes: 5)
            };
            var outcome = Assert.Single(new MorningOpportunityReadinessAnalyzer(MorningOpportunityReadinessConfig.CombineDefault).Analyze(candidates));
            Assert.Equal(2, outcome.Attempts);
        }

        private static MorningAdaptiveTradeOutcome Build(DateTime day, int hour, int minute, MorningMarketState state,
            MorningAdaptiveSetupType setup, decimal efficiency, decimal riskTicks, decimal realized, decimal mfeTicks, int holdMinutes = 10)
        {
            var central = ResolveCentralTimeZone();
            var local = DateTime.SpecifyKind(day.Date.AddHours(hour).AddMinutes(minute), DateTimeKind.Unspecified);
            var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, central), TimeSpan.Zero);
            return new MorningAdaptiveTradeOutcome(day, state, setup, NewYorkResearchDirection.Long,
                utc.AddMinutes(-1), utc, 100m, 99m, riskTicks, efficiency, 100m,
                MorningAdaptiveManagementMode.Scalp, MorningAdaptiveExitReason.ScalpCapture, utc.AddMinutes(holdMinutes),
                101m, realized, realized, mfeTicks, 20m);
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
