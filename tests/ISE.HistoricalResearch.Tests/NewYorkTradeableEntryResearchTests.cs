using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkTradeableEntryResearchTests
    {
        [Fact]
        public void Continue_RequiresBoundedResetPivotAndResumption()
        {
            var date = new DateTime(2026, 7, 14);
            var bars = BuildBars(date, new[]
            {
                (8,30,100m,102m,99m,101m),(8,31,101m,104m,100m,103m),(8,32,103m,107m,102m,106m),(8,33,106m,110m,105m,109m),
                (8,34,109m,112m,108m,111m),(8,35,111m,114m,110m,113m),(8,36,113m,116m,112m,115m),(8,37,115m,118m,114m,117m),
                (8,38,117m,120m,116m,119m),(8,39,119m,122m,118m,121m),(8,40,121m,124m,120m,123m),(8,41,123m,126m,122m,125m),
                (8,42,125m,128m,124m,127m),(8,43,127m,130m,126m,129m),(8,44,129m,132m,128m,131m),
                (8,45,131m,134m,130m,133m),(8,46,133m,136m,132m,135m),(8,47,135m,137m,128m,130m),(8,48,130m,133m,127m,132m),
                (8,49,132m,136m,131m,135m),(8,50,135m,140m,134m,139m),(8,51,139m,142m,138m,141m),
                (8,52,141m,150m,140m,149m),(8,53,149m,160m,148m,159m)
            });
            var transition = new NewYorkEightFortyFiveTransitionOutcome(date, NewYorkEightFortyFiveState.Continue,
                NewYorkResearchDirection.Long, NewYorkResearchDirection.Long, bars[15].TimestampUtc, bars[16].TimestampUtc,
                33m, 30m, 0.9m, 0m, 0m, null, null);

            var outcome = Assert.Single(new NewYorkTradeableEntryAnalyzer().Analyze(bars, new[] { transition }));
            Assert.True(outcome.HasEntry);
            Assert.Equal(NewYorkTradeableEntryType.ContinuationAfterValidatedReset, outcome.EntryType);
            Assert.NotNull(outcome.PivotUtc);
            Assert.False(outcome.ContinuationInvalidated);
        }

        [Fact]
        public void Continue_HandsOffToReversalWhenResetIsDestructive()
        {
            var date = new DateTime(2026, 7, 15);
            var bars = BuildBars(date, new[]
            {
                (8,30,100m,102m,99m,101m),(8,31,101m,105m,100m,104m),(8,32,104m,108m,103m,107m),(8,33,107m,111m,106m,110m),
                (8,34,110m,114m,109m,113m),(8,35,113m,117m,112m,116m),(8,36,116m,120m,115m,119m),(8,37,119m,123m,118m,122m),
                (8,38,122m,126m,121m,125m),(8,39,125m,129m,124m,128m),(8,40,128m,132m,127m,131m),(8,41,131m,135m,130m,134m),
                (8,42,134m,138m,133m,137m),(8,43,137m,141m,136m,140m),(8,44,140m,144m,139m,143m),
                (8,45,143m,146m,142m,145m),(8,46,145m,147m,143m,146m),(8,47,146m,147m,112m,114m),(8,48,114m,116m,106m,108m),
                (8,49,108m,109m,100m,101m),(8,50,101m,103m,95m,96m),(8,51,96m,97m,90m,91m)
            });
            var transition = new NewYorkEightFortyFiveTransitionOutcome(date, NewYorkEightFortyFiveState.Continue,
                NewYorkResearchDirection.Long, NewYorkResearchDirection.Long, bars[15].TimestampUtc, bars[16].TimestampUtc,
                48m, 42m, 0.875m, 0m, 0m, null, null);

            var outcome = Assert.Single(new NewYorkTradeableEntryAnalyzer().Analyze(bars, new[] { transition }));
            Assert.True(outcome.ContinuationInvalidated);
            Assert.True(outcome.HasEntry);
            Assert.Equal(NewYorkTradeableEntryType.ContinuationFailureReversal, outcome.EntryType);
            Assert.Equal(NewYorkResearchDirection.Short, outcome.Direction);
        }

        [Fact]
        public void Sequence_UsesConservativeStopFirstOnSameBar()
        {
            var date = new DateTime(2026, 7, 16);
            var bars = BuildBars(date, new[]
            {
                (8,30,100m,101m,99m,100m),(8,31,100m,102m,99m,101m),(8,32,101m,103m,100m,102m),(8,33,102m,104m,101m,103m),
                (8,34,103m,105m,102m,104m),(8,35,104m,106m,103m,105m),(8,36,105m,107m,104m,106m),(8,37,106m,108m,105m,107m),
                (8,38,107m,109m,106m,108m),(8,39,108m,110m,107m,109m),(8,40,109m,111m,108m,110m),(8,41,110m,112m,109m,111m),
                (8,42,111m,113m,110m,112m),(8,43,112m,114m,111m,113m),(8,44,113m,115m,112m,114m),
                (8,50,110m,111m,106m,107m),(8,51,107m,108m,101m,102m),(8,52,102m,103m,96m,97m),(8,53,97m,98m,90m,91m),
                (8,54,91m,120m,80m,90m)
            });
            var transition = new NewYorkEightFortyFiveTransitionOutcome(date, NewYorkEightFortyFiveState.Reverse,
                NewYorkResearchDirection.Long, NewYorkResearchDirection.Short, bars[15].TimestampUtc, bars[16].TimestampUtc,
                16m, 12m, 0.75m, 0m, 0m, null, null);

            var outcome = Assert.Single(new NewYorkTradeableEntryAnalyzer(new NewYorkTradeableEntryConfig(lowerObjective: 80m, upperObjective: 160m)).Analyze(bars, new[] { transition }));
            Assert.True(outcome.HasEntry);
            Assert.Equal(NewYorkTradeSequenceResult.StopFirst, outcome.SequenceResult);
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
