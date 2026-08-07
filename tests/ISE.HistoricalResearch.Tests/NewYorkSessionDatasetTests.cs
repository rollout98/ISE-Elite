using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkSessionDatasetTests
    {
        [Fact]
        public void Extract_filters_by_central_time_and_groups_by_session_date()
        {
            var extractor = new NewYorkSessionDatasetExtractor(TimeZoneInfo.CreateCustomTimeZone("CT", TimeSpan.FromHours(-5), "CT", "CT"));
            var bars = new List<HistoricalBar>
            {
                Bar(new DateTimeOffset(2026, 8, 5, 12, 59, 0, TimeSpan.Zero)),
                Bar(new DateTimeOffset(2026, 8, 5, 13, 0, 0, TimeSpan.Zero)),
                Bar(new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.Zero)),
                Bar(new DateTimeOffset(2026, 8, 5, 15, 0, 0, TimeSpan.Zero)),
                Bar(new DateTimeOffset(2026, 8, 6, 13, 30, 0, TimeSpan.Zero))
            };

            var result = extractor.Extract(bars, new NewYorkResearchWindow(TimeSpan.FromHours(8), TimeSpan.FromHours(10)));

            Assert.Equal(5, result.SourceBarCount);
            Assert.Equal(3, result.SelectedBarCount);
            Assert.Equal(2, result.SessionCount);
            Assert.Equal(new DateTime(2026, 8, 5), result.Sessions[0].SessionDateCentral);
            Assert.Equal(2, result.Sessions[0].Bars.Count);
            Assert.Equal(new DateTime(2026, 8, 6), result.Sessions[1].SessionDateCentral);
            Assert.Single(result.Sessions[1].Bars);
        }

        [Fact]
        public void Extract_preserves_chronological_order()
        {
            var extractor = new NewYorkSessionDatasetExtractor(TimeZoneInfo.Utc);
            var bars = new List<HistoricalBar>
            {
                Bar(new DateTimeOffset(2026, 8, 5, 9, 2, 0, TimeSpan.Zero)),
                Bar(new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero)),
                Bar(new DateTimeOffset(2026, 8, 5, 9, 1, 0, TimeSpan.Zero))
            };

            var result = extractor.Extract(bars, new NewYorkResearchWindow(TimeSpan.FromHours(9), TimeSpan.FromHours(10)));

            Assert.Single(result.Sessions);
            Assert.Equal(new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero), result.Sessions[0].Bars[0].TimestampUtc);
            Assert.Equal(new DateTimeOffset(2026, 8, 5, 9, 2, 0, TimeSpan.Zero), result.Sessions[0].Bars[2].TimestampUtc);
        }

        [Fact]
        public void Extract_rejects_mixed_contracts()
        {
            var extractor = new NewYorkSessionDatasetExtractor(TimeZoneInfo.Utc);
            var bars = new List<HistoricalBar>
            {
                Bar(new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero), "09-26"),
                Bar(new DateTimeOffset(2026, 8, 5, 9, 1, 0, TimeSpan.Zero), "12-26")
            };

            var error = Assert.Throws<InvalidOperationException>(() =>
                extractor.Extract(bars, new NewYorkResearchWindow(TimeSpan.FromHours(8), TimeSpan.FromHours(10))));

            Assert.Contains("single futures contract", error.Message);
        }

        [Fact]
        public void Window_rejects_overnight_or_empty_ranges()
        {
            Assert.Throws<ArgumentException>(() => new NewYorkResearchWindow(TimeSpan.FromHours(10), TimeSpan.FromHours(10)));
            Assert.Throws<ArgumentException>(() => new NewYorkResearchWindow(TimeSpan.FromHours(11), TimeSpan.FromHours(10)));
        }

        private static HistoricalBar Bar(DateTimeOffset timestampUtc, string contract = "09-26")
        {
            return new HistoricalBar(
                "MNQ",
                contract,
                timestampUtc,
                timestampUtc.Date,
                60,
                100m,
                101m,
                99m,
                100.5m,
                10,
                HistoricalDataSourceKind.NinjaTraderProvider,
                "test");
        }
    }
}
