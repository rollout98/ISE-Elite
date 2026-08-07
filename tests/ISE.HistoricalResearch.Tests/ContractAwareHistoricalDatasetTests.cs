using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class ContractAwareHistoricalDatasetTests
    {
        [Fact]
        public void ValidateAndOrder_accepts_one_way_contract_rollover()
        {
            var validator = new ContractAwareHistoricalDatasetValidator(TimeZoneInfo.Utc);
            var bars = new[]
            {
                Bar("09-26", "2026-06-15T06:00:00+00:00"),
                Bar("06-26", "2026-06-12T06:01:00+00:00"),
                Bar("06-26", "2026-06-12T06:00:00+00:00"),
                Bar("09-26", "2026-06-15T06:01:00+00:00")
            };

            var ordered = validator.ValidateAndOrder(bars);

            Assert.Equal(4, ordered.Count);
            Assert.Equal("06-26", ordered[0].Contract);
            Assert.Equal("09-26", ordered[3].Contract);
        }

        [Fact]
        public void ValidateAndOrder_rejects_contract_reentry()
        {
            var validator = new ContractAwareHistoricalDatasetValidator(TimeZoneInfo.Utc);
            var bars = new[]
            {
                Bar("06-26", "2026-06-12T06:00:00+00:00"),
                Bar("09-26", "2026-06-15T06:00:00+00:00"),
                Bar("06-26", "2026-06-16T06:00:00+00:00")
            };

            Assert.Throws<InvalidOperationException>(() => validator.ValidateAndOrder(bars));
        }

        [Fact]
        public void BuildCoverageReport_reports_complete_sessions_and_segments()
        {
            var validator = new ContractAwareHistoricalDatasetValidator(TimeZoneInfo.Utc);
            var bars = new List<HistoricalBar>();
            AddSession(bars, "06-26", new DateTimeOffset(2026, 6, 12, 6, 0, 0, TimeSpan.Zero), 5);
            AddSession(bars, "09-26", new DateTimeOffset(2026, 6, 15, 6, 0, 0, TimeSpan.Zero), 5);

            var report = validator.BuildCoverageReport(bars, TimeSpan.FromHours(6), TimeSpan.FromHours(6) + TimeSpan.FromMinutes(5));

            Assert.Equal("MNQ", report.Instrument);
            Assert.Equal(10, report.BarCount);
            Assert.Equal(2, report.SessionCount);
            Assert.Equal(2, report.CompleteSessionCount);
            Assert.Equal(0, report.PartialSessionCount);
            Assert.Equal(2, report.ContractSegments.Count);
            Assert.Equal("06-26", report.ContractSegments[0].Contract);
            Assert.Equal("09-26", report.ContractSegments[1].Contract);
        }

        [Fact]
        public void HistoricalDataFileStore_ReadContractAware_loads_multi_contract_tsv()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tsv");
            try
            {
                File.WriteAllText(path,
                    "instrument\tcontract\ttimestampUtc\ttradingDay\tintervalSeconds\topen\thigh\tlow\tclose\tvolume\tsourceKind\tsourceName\tbid\task\n" +
                    Row("06-26", "2026-06-12T11:00:00.0000000+00:00") + "\n" +
                    Row("09-26", "2026-06-15T11:00:00.0000000+00:00") + "\n");

                var store = new HistoricalDataFileStore();
                var bars = store.ReadContractAware(path);

                Assert.Equal(2, bars.Count);
                Assert.Equal("06-26", bars[0].Contract);
                Assert.Equal("09-26", bars[1].Contract);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static void AddSession(List<HistoricalBar> bars, string contract, DateTimeOffset start, int count)
        {
            for (var i = 0; i < count; i++)
                bars.Add(Bar(contract, start.AddMinutes(i).ToString("O")));
        }

        private static HistoricalBar Bar(string contract, string timestamp)
        {
            var ts = DateTimeOffset.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture);
            return new HistoricalBar("MNQ", contract, ts, ts.UtcDateTime.Date, 60, 100m, 101m, 99m, 100m, 1, HistoricalDataSourceKind.NinjaTraderRepository, "Repository");
        }

        private static string Row(string contract, string timestamp)
        {
            return "MNQ\t" + contract + "\t" + timestamp + "\t2026-06-12\t60\t100\t101\t99\t100\t1\t2\tNinjaTrader BarsRequest Repository\t\t";
        }
    }
}
