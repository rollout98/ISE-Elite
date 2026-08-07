using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkCausalEntryResearchTests
    {
        [Fact]
        public void Continue_WaitsForResetAndResumptionBeforeEntry()
        {
            var date = new DateTime(2026, 7, 14);
            var bars = BuildBars(date, new[]
            {
                (8,45,100m,101m,99m,100m),
                (8,46,101m,103m,100m,102m),
                (8,47,102m,104m,96m,97m),
                (8,48,97m,103m,97m,103m),
                (8,49,103m,106m,102m,105m),
                (8,50,105m,110m,104m,109m)
            });
            var transition = new NewYorkEightFortyFiveTransitionOutcome(date, NewYorkEightFortyFiveState.Continue,
                NewYorkResearchDirection.Long, NewYorkResearchDirection.Long, bars[0].TimestampUtc, bars[1].TimestampUtc,
                20m, 10m, 0.5m, 0m, 0m, null, null);

            var outcome = Assert.Single(new NewYorkCausalEntryAnalyzer(new NewYorkCausalEntryConfig(0.20m)).Analyze(bars, new[] { transition }));
            Assert.True(outcome.HasEntry);
            Assert.Equal(NewYorkCausalEntryType.ContinuationAfterReset, outcome.EntryType);
            Assert.Equal(bars[3].TimestampUtc, outcome.SetupCompleteUtc);
            Assert.Equal(bars[4].TimestampUtc, outcome.EntryUtc);
        }

        [Fact]
        public void Reverse_RequiresCompletedBarConfirmationBeforeEntry()
        {
            var date = new DateTime(2026, 7, 15);
            var bars = BuildBars(date, new[]
            {
                (8,50,100m,102m,98m,99m),
                (8,51,99m,100m,94m,95m),
                (8,52,95m,96m,90m,91m),
                (8,53,91m,94m,89m,92m)
            });
            var transition = new NewYorkEightFortyFiveTransitionOutcome(date, NewYorkEightFortyFiveState.Reverse,
                NewYorkResearchDirection.Long, NewYorkResearchDirection.Short, bars[0].TimestampUtc, bars[1].TimestampUtc,
                20m, 10m, 0.5m, 0m, 0m, null, null);

            var outcome = Assert.Single(new NewYorkCausalEntryAnalyzer().Analyze(bars, new[] { transition }));
            Assert.True(outcome.HasEntry);
            Assert.Equal(NewYorkCausalEntryType.ReversalAfterConfirmation, outcome.EntryType);
            Assert.Equal(bars[1].TimestampUtc, outcome.SetupCompleteUtc);
            Assert.Equal(bars[2].TimestampUtc, outcome.EntryUtc);
        }

        [Fact]
        public void StandAside_DoesNotCreateEntry()
        {
            var date = new DateTime(2026, 7, 16);
            var bars = BuildBars(date, new[] { (8,45,100m,101m,99m,100m), (8,46,100m,101m,99m,100m) });
            var transition = new NewYorkEightFortyFiveTransitionOutcome(date, NewYorkEightFortyFiveState.StandAside,
                NewYorkResearchDirection.Long, NewYorkResearchDirection.None, null, null, 20m, 5m, 0.25m, 0m, 0m, null, null);

            var outcome = Assert.Single(new NewYorkCausalEntryAnalyzer().Analyze(bars, new[] { transition }));
            Assert.False(outcome.HasEntry);
            Assert.Equal(NewYorkCausalEntryType.None, outcome.EntryType);
        }

        private static List<HistoricalBar> BuildBars(DateTime date, (int hour, int minute, decimal open, decimal high, decimal low, decimal close)[] rows)
        {
            var result = new List<HistoricalBar>();
            var central = ResolveCentralTimeZone();
            foreach (var row in rows)
            {
                var local = DateTime.SpecifyKind(date.Date.AddHours(row.hour).AddMinutes(row.minute), DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
                result.Add(new HistoricalBar("MNQ", "09-26", new DateTimeOffset(utc, TimeSpan.Zero), date.Date, 60,
                    row.open, row.high, row.low, row.close, 100, HistoricalDataSourceKind.NinjaTraderRepository, "test"));
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
