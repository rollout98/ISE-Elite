using System;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningExtendedWalkForwardResearchTests
    {
        [Fact]
        public void BuildsMonthlyAndHalfMonthWindowsFromObservedDates()
        {
            var analyzer = new MorningExtendedWalkForwardAnalyzer();
            var windows = analyzer.BuildWindows(new[]
            {
                new DateTime(2026, 1, 5),
                new DateTime(2026, 1, 20),
                new DateTime(2026, 2, 3),
                new DateTime(2026, 2, 25)
            });

            Assert.Contains(windows, x => x.Label == "2026-01" && x.Cadence == "Monthly");
            Assert.Contains(windows, x => x.Label == "2026-01-H1" && x.Cadence == "HalfMonth");
            Assert.Contains(windows, x => x.Label == "2026-01-H2" && x.Cadence == "HalfMonth");
            Assert.Contains(windows, x => x.Label == "2026-02" && x.Cadence == "Monthly");
            Assert.Contains(windows, x => x.Label == "2026-02-H1" && x.Cadence == "HalfMonth");
            Assert.Contains(windows, x => x.Label == "2026-02-H2" && x.Cadence == "HalfMonth");
        }

        [Fact]
        public void DoesNotInventWindowsForMonthsWithoutObservedSessions()
        {
            var analyzer = new MorningExtendedWalkForwardAnalyzer();
            var windows = analyzer.BuildWindows(new[]
            {
                new DateTime(2025, 12, 20),
                new DateTime(2026, 2, 5)
            });

            Assert.DoesNotContain(windows, x => x.Label.StartsWith("2026-01", StringComparison.Ordinal));
        }

        [Fact]
        public void UpperTierThresholdIsFrozenAtEighty()
        {
            Assert.Equal("Below80", MorningExtendedWalkForwardAnalyzer.TierForScore(79.999m));
            Assert.Equal("Upper80Plus", MorningExtendedWalkForwardAnalyzer.TierForScore(80m));
            Assert.Equal("Upper80Plus", MorningExtendedWalkForwardAnalyzer.TierForScore(100m));
        }

        [Fact]
        public void EmptyDatesProduceNoWindows()
        {
            var analyzer = new MorningExtendedWalkForwardAnalyzer();
            Assert.Empty(analyzer.BuildWindows(Array.Empty<DateTime>()));
        }

        [Fact]
        public void WindowOrderingIsChronologicalWithMonthlyBeforeHalfMonth()
        {
            var analyzer = new MorningExtendedWalkForwardAnalyzer();
            var windows = analyzer.BuildWindows(new[]
            {
                new DateTime(2026, 3, 20),
                new DateTime(2026, 3, 5)
            }).ToList();

            Assert.Equal("2026-03", windows[0].Label);
            Assert.Equal("2026-03-H1", windows[1].Label);
            Assert.Equal("2026-03-H2", windows[2].Label);
        }
    }
}
