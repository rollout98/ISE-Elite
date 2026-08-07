using System;
using System.Collections.Generic;
using System.IO;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkResearchDatasetGeneratorTests
    {
        [Fact]
        public void Generate_AcquiresExtractsPersistsAndReportsCoverage()
        {
            var bars = new[]
            {
                Bar("2026-01-05T13:29:00Z", 100m),
                Bar("2026-01-05T13:30:00Z", 101m),
                Bar("2026-01-05T14:00:00Z", 102m),
                Bar("2026-01-05T16:00:00Z", 103m),
                Bar("2026-01-06T13:30:00Z", 104m),
                Bar("2026-01-06T14:00:00Z", 105m)
            };
            var source = new StubSource(bars);
            var request = Request();
            var window = new NewYorkResearchWindow(TimeSpan.FromHours(7.5), TimeSpan.FromHours(10));
            var path = TempFile();

            try
            {
                var manifest = new NewYorkResearchDatasetGenerator().Generate(source, request, window, path);
                var persisted = new HistoricalDataFileStore().Read(path);

                Assert.Equal(6, manifest.SourceBarCount);
                Assert.Equal(4, manifest.SelectedBarCount);
                Assert.Equal(2, manifest.SessionCount);
                Assert.Equal(new DateTime(2026, 1, 5), manifest.FirstSessionDateCentral);
                Assert.Equal(new DateTime(2026, 1, 6), manifest.LastSessionDateCentral);
                Assert.Equal(4, persisted.Count);
                Assert.Equal(101m, persisted[0].Close);
                Assert.Equal(105m, persisted[3].Close);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Generate_WritesHeaderOnlyDatasetWhenWindowHasNoBars()
        {
            var source = new StubSource(new[] { Bar("2026-01-05T13:30:00Z", 100m) });
            var request = Request();
            var window = new NewYorkResearchWindow(TimeSpan.FromHours(11), TimeSpan.FromHours(12));
            var path = TempFile();

            try
            {
                var manifest = new NewYorkResearchDatasetGenerator().Generate(source, request, window, path);
                var persisted = new HistoricalDataFileStore().Read(path);

                Assert.Equal(0, manifest.SelectedBarCount);
                Assert.Equal(0, manifest.SessionCount);
                Assert.Null(manifest.FirstSessionDateCentral);
                Assert.Null(manifest.LastSessionDateCentral);
                Assert.Empty(persisted);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Generate_UsesAcquisitionValidationBeforeWriting()
        {
            var wrongContract = new HistoricalBar(
                "MNQ", "12-26", DateTimeOffset.Parse("2026-01-05T13:30:00Z"), new DateTime(2026, 1, 5), 60,
                100m, 101m, 99m, 100m, 10, HistoricalDataSourceKind.NinjaTraderRepository, "test");
            var source = new StubSource(new[] { wrongContract });
            var path = TempFile();

            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    new NewYorkResearchDatasetGenerator().Generate(
                        source,
                        Request(),
                        new NewYorkResearchWindow(TimeSpan.FromHours(7.5), TimeSpan.FromHours(10)),
                        path));
                Assert.False(File.Exists(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void Generate_RejectsMissingOutputPath()
        {
            Assert.Throws<ArgumentException>(() =>
                new NewYorkResearchDatasetGenerator().Generate(
                    new StubSource(Array.Empty<HistoricalBar>()),
                    Request(),
                    new NewYorkResearchWindow(TimeSpan.FromHours(7.5), TimeSpan.FromHours(10)),
                    " "));
        }

        private static HistoricalDataAcquisitionRequest Request()
        {
            return new HistoricalDataAcquisitionRequest(
                "MNQ",
                "09-26",
                DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-02-01T00:00:00Z"),
                60,
                HistoricalDataSourceKind.NinjaTraderRepository);
        }

        private static HistoricalBar Bar(string timestampUtc, decimal close)
        {
            var timestamp = DateTimeOffset.Parse(timestampUtc);
            return new HistoricalBar(
                "MNQ",
                "09-26",
                timestamp,
                timestamp.UtcDateTime.Date,
                60,
                close,
                close + 1m,
                close - 1m,
                close,
                10,
                HistoricalDataSourceKind.NinjaTraderRepository,
                "test");
        }

        private static string TempFile()
        {
            return Path.Combine(Path.GetTempPath(), "ise-ny-dataset-" + Guid.NewGuid().ToString("N") + ".tsv");
        }

        private sealed class StubSource : IHistoricalDataSource
        {
            private readonly IReadOnlyList<HistoricalBar> _bars;

            public StubSource(IReadOnlyList<HistoricalBar> bars)
            {
                _bars = bars;
            }

            public IReadOnlyList<HistoricalBar> Acquire(HistoricalDataAcquisitionRequest request)
            {
                return _bars;
            }
        }
    }
}
