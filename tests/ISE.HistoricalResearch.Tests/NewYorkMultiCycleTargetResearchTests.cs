using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkMultiCycleTargetResearchTests
    {
        [Fact]
        public void Analyzer_UsesThreeNonOverlappingDefaultCycles()
        {
            var windows = NewYorkMultiCycleTargetAnalyzer.DefaultWindows();
            Assert.Equal(3, windows.Count);
            Assert.Equal(new TimeSpan(8, 30, 0), windows[0].StartCentral);
            Assert.Equal(new TimeSpan(8, 45, 0), windows[0].EndCentral);
            Assert.Equal(new TimeSpan(8, 45, 0), windows[1].StartCentral);
            Assert.Equal(new TimeSpan(9, 30, 0), windows[1].EndCentral);
            Assert.Equal(new TimeSpan(9, 30, 0), windows[2].StartCentral);
            Assert.Equal(new TimeSpan(10, 30, 0), windows[2].EndCentral);
        }

        [Fact]
        public void Analyzer_ComputesTwoContractTargetAvailabilityAndCumulativeCycles()
        {
            var date = new DateTime(2026, 7, 8);
            var bars = new List<HistoricalBar>();
            bars.AddRange(BuildWindow(date, new TimeSpan(8, 30, 0), 15, i => 100m + i * 3m));
            bars.AddRange(BuildWindow(date, new TimeSpan(8, 45, 0), 45, i => 142m - i * 2m));
            bars.AddRange(BuildWindow(date, new TimeSpan(9, 30, 0), 60, i => 54m + i * 1m));

            var result = new NewYorkMultiCycleTargetAnalyzer().Analyze(bars);
            var session = Assert.Single(result);
            Assert.Equal(3, session.Cycles.Count);
            Assert.True(session.Cycles[0].FavorableDollars > 0m);
            Assert.True(session.Cycles[1].FavorableDollars > 0m);
            Assert.NotNull(session.CyclesToLowerObjective);
            Assert.NotNull(session.CyclesToUpperObjective);
            Assert.InRange(session.CyclesToLowerObjective!.Value, 1, 3);
            Assert.InRange(session.CyclesToUpperObjective!.Value, 1, 3);
        }

        [Fact]
        public void Analyzer_CanTreatPostPullbackWindowAsIndependentSecondOpportunity()
        {
            var date = new DateTime(2026, 7, 9);
            var bars = new List<HistoricalBar>();
            bars.AddRange(BuildWindow(date, new TimeSpan(8, 30, 0), 15, i => 100m + i));
            bars.AddRange(BuildWindow(date, new TimeSpan(8, 45, 0), 45, i => 120m - i * 4m));
            bars.AddRange(BuildWindow(date, new TimeSpan(9, 30, 0), 60, i => 50m + i));

            var session = Assert.Single(new NewYorkMultiCycleTargetAnalyzer().Analyze(bars));
            Assert.Equal(NewYorkResearchDirection.Short, session.Cycles[1].Direction);
            Assert.True(session.Cycles[1].LowerObjectiveAvailable);
        }

        [Fact]
        public void Analyzer_RejectsOverlappingResearchWindows()
        {
            var windows = new[]
            {
                new NewYorkResearchCycleWindow(1, "A", new TimeSpan(8, 30, 0), new TimeSpan(9, 0, 0)),
                new NewYorkResearchCycleWindow(2, "B", new TimeSpan(8, 45, 0), new TimeSpan(9, 30, 0))
            };

            Assert.Throws<ArgumentException>(() => new NewYorkMultiCycleTargetAnalyzer(windows: windows));
        }

        private static IEnumerable<HistoricalBar> BuildWindow(DateTime date, TimeSpan start, int minutes, Func<int, decimal> price)
        {
            var central = ResolveCentralTimeZone();
            for (var i = 0; i < minutes; i++)
            {
                var local = DateTime.SpecifyKind(date.Date.Add(start).AddMinutes(i), DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
                var p = price(i);
                yield return new HistoricalBar(
                    "MNQ",
                    "09-26",
                    new DateTimeOffset(utc, TimeSpan.Zero),
                    date.Date,
                    60,
                    p,
                    p + 1m,
                    p - 1m,
                    p,
                    100,
                    HistoricalDataSourceKind.NinjaTraderRepository,
                    "test");
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
