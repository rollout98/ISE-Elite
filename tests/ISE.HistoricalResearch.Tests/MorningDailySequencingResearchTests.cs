using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningDailySequencingResearchTests
    {
        [Fact]
        public void FundedProfileIsMoreSelectiveThanCombine()
        {
            Assert.True(MorningDailySequencingConfig.FundedDefault.MinimumScore > MorningDailySequencingConfig.CombineDefault.MinimumScore);
            Assert.True(MorningDailySequencingConfig.FundedDefault.MaximumStructuralRiskTicks < MorningDailySequencingConfig.CombineDefault.MaximumStructuralRiskTicks);
        }

        [Fact]
        public void SelectionScoreUsesOnlyEntryTimeFields()
        {
            var analyzer = new MorningDailySequencingAnalyzer(MorningDailySequencingConfig.FundedDefault);
            var a = Trade(new DateTime(2026, 7, 1), 7, MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation, 120m, 0.50m, 600m, 1000m);
            var b = Trade(new DateTime(2026, 7, 1), 7, MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation, 120m, 0.50m, -120m, 10m);

            Assert.Equal(analyzer.Score(a), analyzer.Score(b));
        }

        [Fact]
        public void DailySequenceRejectsOverlapAndStopsAfterTwoAttempts()
        {
            var date = new DateTime(2026, 7, 2);
            var rows = new List<MorningAdaptiveTradeOutcome>
            {
                Trade(date, 7, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 100m, 0.50m, 200m, 400m, 30),
                Trade(date, 7, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 100m, 0.50m, 200m, 400m, 10, minute: 10),
                Trade(date, 8, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 100m, 0.50m, 200m, 400m),
                Trade(date, 9, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 100m, 0.50m, 200m, 400m)
            };

            var day = Assert.Single(new MorningDailySequencingAnalyzer(MorningDailySequencingConfig.CombineDefault).Analyze(rows));
            Assert.Equal(2, day.Attempts);
            Assert.True(day.RejectedWhilePositionOpen >= 1);
            Assert.True(day.RejectedByGovernance >= 1);
        }

        [Fact]
        public void FundedSequenceLocksNewEntriesAfterFiveHundred()
        {
            var date = new DateTime(2026, 7, 3);
            var rows = new List<MorningAdaptiveTradeOutcome>
            {
                Trade(date, 7, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 100m, 0.55m, 550m, 700m),
                Trade(date, 9, MorningMarketState.Trending, MorningAdaptiveSetupType.TrendContinuation, 100m, 0.55m, 200m, 300m)
            };

            var day = Assert.Single(new MorningDailySequencingAnalyzer(MorningDailySequencingConfig.FundedDefault).Analyze(rows));
            Assert.Single(day.SelectedTrades);
            Assert.True(day.ReachedLowerObjective);
            Assert.Equal(1, day.RejectedByGovernance);
        }

        [Fact]
        public void RunnerMissDiagnosticDoesNotAffectSelection()
        {
            var date = new DateTime(2026, 7, 6);
            var row = Trade(date, 8, MorningMarketState.Trending, MorningAdaptiveSetupType.CompressionResumption,
                120m, 0.50m, 330m, 760m);
            var day = Assert.Single(new MorningDailySequencingAnalyzer(MorningDailySequencingConfig.FundedDefault)
                .Analyze(new[] { row }));

            Assert.Single(day.SelectedTrades);
            Assert.Equal(1, day.RunnerCapableButNotRunner);
        }

        private static MorningAdaptiveTradeOutcome Trade(DateTime date, int hour, MorningMarketState state,
            MorningAdaptiveSetupType setup, decimal risk, decimal efficiency, decimal realized, decimal mfe,
            int holdMinutes = 8, int minute = 0)
        {
            var central = ResolveCentralTimeZone();
            var local = DateTime.SpecifyKind(date.Date.AddHours(hour).AddMinutes(minute), DateTimeKind.Unspecified);
            var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, central), TimeSpan.Zero);
            return new MorningAdaptiveTradeOutcome(date, state, setup, NewYorkResearchDirection.Long,
                utc.AddMinutes(-1), utc, 100m, 99m, risk, efficiency, 10m,
                MorningAdaptiveManagementMode.Core, MorningAdaptiveExitReason.CoreCapture,
                utc.AddMinutes(holdMinutes), 101m, realized, realized, mfe, 20m);
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
