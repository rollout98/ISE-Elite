using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkObjectivePathResearchTests
    {
        [Fact]
        public void ContinuesAfterThreeHundredToRecordFiveHundredBeforeStop()
        {
            var date = new DateTime(2026, 7, 17);
            var bars = BuildBars(date, new[]
            {
                (8,56,100m,101m,99m,100m),
                (8,57,100m,180m,99m,170m),
                (8,58,170m,240m,169m,230m),
                (8,59,230m,240m,89m,100m)
            });
            var accepted = new NewYorkRiskQualifiedEntryOutcome(date, NewYorkTradeableEntryType.DirectReversal,
                NewYorkRiskQualifiedDisposition.Accepted, NewYorkResearchDirection.Long, bars[0].TimestampUtc,
                bars[0].TimestampUtc, 100m, 90m, 40m, NewYorkRiskSequenceResult.IntermediateObjectiveFirst,
                bars[1].TimestampUtc, bars[1].TimestampUtc, null, null, null);

            var path = Assert.Single(new NewYorkObjectivePathAnalyzer().Analyze(bars, new[] { accepted }));
            Assert.True(path.Hit300BeforeStop);
            Assert.True(path.Hit500BeforeStop);
            Assert.False(path.Hit1000BeforeStop);
            Assert.True(path.StopOccurred);
            Assert.Equal(bars[3].TimestampUtc, path.StopUtc);
        }

        [Fact]
        public void SameBarStopAndTargetCreditsStopOnly()
        {
            var date = new DateTime(2026, 7, 18);
            var bars = BuildBars(date, new[]
            {
                (8,56,100m,101m,99m,100m),
                (8,57,100m,180m,89m,120m)
            });
            var accepted = new NewYorkRiskQualifiedEntryOutcome(date, NewYorkTradeableEntryType.DirectReversal,
                NewYorkRiskQualifiedDisposition.Accepted, NewYorkResearchDirection.Long, bars[0].TimestampUtc,
                bars[0].TimestampUtc, 100m, 90m, 40m, NewYorkRiskSequenceResult.None,
                null, null, null, null, null);

            var path = Assert.Single(new NewYorkObjectivePathAnalyzer().Analyze(bars, new[] { accepted }));
            Assert.False(path.Hit300BeforeStop);
            Assert.False(path.Hit500BeforeStop);
            Assert.True(path.StopOccurred);
            Assert.Equal(bars[1].TimestampUtc, path.StopUtc);
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
