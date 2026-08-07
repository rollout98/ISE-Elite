using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkEightFortyFiveTransitionResearchTests
    {
        [Fact]
        public void Analyzer_ClassifiesContinuationUsingOnlyCompletedBarsAndNextBarEntry()
        {
            var date = new DateTime(2026, 7, 13);
            var bars = BuildSession(date, openingStart: 100m, openingStep: 2m, post845: i => 130m + i * 2m);
            var outcome = Assert.Single(new NewYorkEightFortyFiveTransitionAnalyzer().Analyze(bars));
            Assert.Equal(NewYorkEightFortyFiveState.Continue, outcome.State);
            Assert.Equal(NewYorkResearchDirection.Long, outcome.TradeDirection);
            Assert.True(outcome.SignalTimestampUtc.HasValue);
            Assert.True(outcome.ReferenceEntryTimestampUtc.HasValue);
            Assert.True(outcome.ReferenceEntryTimestampUtc.Value > outcome.SignalTimestampUtc.Value);
        }

        [Fact]
        public void Analyzer_ClassifiesReversalWhenOpeningStructureFails()
        {
            var date = new DateTime(2026, 7, 14);
            var bars = BuildSession(date, openingStart: 100m, openingStep: 2m, post845: i => 128m - i * 4m);
            var outcome = Assert.Single(new NewYorkEightFortyFiveTransitionAnalyzer().Analyze(bars));
            Assert.Equal(NewYorkEightFortyFiveState.Reverse, outcome.State);
            Assert.Equal(NewYorkResearchDirection.Short, outcome.TradeDirection);
        }

        [Fact]
        public void Analyzer_StandsAsideWhenOpeningIsNotDirectionalEnough()
        {
            var date = new DateTime(2026, 7, 15);
            var bars = BuildSession(date, openingStart: 100m, openingStep: 0.05m, post845: i => 101m + (i % 2 == 0 ? 0.25m : -0.25m));
            var outcome = Assert.Single(new NewYorkEightFortyFiveTransitionAnalyzer().Analyze(bars));
            Assert.Equal(NewYorkEightFortyFiveState.StandAside, outcome.State);
            Assert.Equal(NewYorkResearchDirection.None, outcome.TradeDirection);
            Assert.Null(outcome.ReferenceEntryTimestampUtc);
        }

        private static List<HistoricalBar> BuildSession(DateTime date, decimal openingStart, decimal openingStep, Func<int, decimal> post845)
        {
            var result = new List<HistoricalBar>();
            var central = ResolveCentralTimeZone();
            for (var i = 0; i < 15; i++)
            {
                var p = openingStart + i * openingStep;
                result.Add(Bar(date, new TimeSpan(8, 30, 0).Add(TimeSpan.FromMinutes(i)), p, central));
            }
            for (var i = 0; i < 45; i++)
            {
                result.Add(Bar(date, new TimeSpan(8, 45, 0).Add(TimeSpan.FromMinutes(i)), post845(i), central));
            }
            return result;
        }

        private static HistoricalBar Bar(DateTime date, TimeSpan time, decimal p, TimeZoneInfo central)
        {
            var local = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
            return new HistoricalBar("MNQ", "09-26", new DateTimeOffset(utc, TimeSpan.Zero), date.Date, 60,
                p, p + 1m, p - 1m, p, 100, HistoricalDataSourceKind.NinjaTraderRepository, "test");
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
