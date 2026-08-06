using System;
using System.IO;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class HistoricalDataAcquisitionTests
    {
        [Fact]
        public void Normalizer_sorts_bars_chronologically()
        {
            var normalizer = new HistoricalDataNormalizer();
            var later = BarAt("2026-06-01T13:31:00Z", 21500m);
            var earlier = BarAt("2026-06-01T13:30:00Z", 21490m);

            var result = normalizer.Normalize(new[] { later, earlier });

            Assert.Equal(2, result.Count);
            Assert.Equal(earlier.TimestampUtc, result[0].TimestampUtc);
            Assert.Equal(later.TimestampUtc, result[1].TimestampUtc);
        }

        [Fact]
        public void Normalizer_rejects_duplicate_timestamp_and_interval()
        {
            var normalizer = new HistoricalDataNormalizer();
            var first = BarAt("2026-06-01T13:30:00Z", 21490m);
            var duplicate = BarAt("2026-06-01T13:30:00Z", 21491m);

            Assert.Throws<InvalidOperationException>(() => normalizer.Normalize(new[] { first, duplicate }));
        }

        [Fact]
        public void Normalizer_rejects_mixed_contracts()
        {
            var normalizer = new HistoricalDataNormalizer();
            var first = BarAt("2026-06-01T13:30:00Z", 21490m);
            var second = new HistoricalBar(
                "MNQ", "MNQ 12-26", DateTimeOffset.Parse("2026-06-01T13:31:00Z"), new DateTime(2026, 6, 1),
                60, 21491m, 21495m, 21489m, 21493m, 150, HistoricalDataSourceKind.NinjaTraderProvider, "test");

            Assert.Throws<InvalidOperationException>(() => normalizer.Normalize(new[] { first, second }));
        }

        [Fact]
        public void File_store_round_trips_normalized_bars()
        {
            var store = new HistoricalDataFileStore();
            var path = Path.Combine(Path.GetTempPath(), "ise-historical-" + Guid.NewGuid().ToString("N") + ".tsv");

            try
            {
                var second = BarAt("2026-06-01T13:31:00Z", 21500m);
                var first = BarAt("2026-06-01T13:30:00Z", 21490m);

                store.Write(path, new[] { second, first });
                var loaded = store.Read(path);

                Assert.Equal(2, loaded.Count);
                Assert.Equal(first.TimestampUtc, loaded[0].TimestampUtc);
                Assert.Equal(first.Close, loaded[0].Close);
                Assert.Equal(first.Bid, loaded[0].Bid);
                Assert.Equal(first.Ask, loaded[0].Ask);
                Assert.Equal(HistoricalDataSourceKind.NinjaTraderProvider, loaded[0].SourceKind);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Bar_requires_utc_timestamp()
        {
            Assert.Throws<ArgumentException>(() => new HistoricalBar(
                "MNQ", "MNQ 09-26", new DateTimeOffset(2026, 6, 1, 8, 30, 0, TimeSpan.FromHours(-5)),
                new DateTime(2026, 6, 1), 60, 21490m, 21495m, 21488m, 21492m, 100,
                HistoricalDataSourceKind.NinjaTraderProvider, "test"));
        }

        private static HistoricalBar BarAt(string timestamp, decimal close)
        {
            return new HistoricalBar(
                "MNQ",
                "MNQ 09-26",
                DateTimeOffset.Parse(timestamp),
                new DateTime(2026, 6, 1),
                60,
                close - 2m,
                close + 3m,
                close - 4m,
                close,
                100,
                HistoricalDataSourceKind.NinjaTraderProvider,
                "NinjaTrader",
                close - 0.25m,
                close + 0.25m);
        }
    }
}
