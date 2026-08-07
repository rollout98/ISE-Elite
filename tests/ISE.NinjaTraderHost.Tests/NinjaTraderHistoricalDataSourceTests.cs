using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using ISE.NinjaTraderHost.HistoricalData;
using Xunit;

namespace ISE.NinjaTraderHost.Tests
{
    public sealed class NinjaTraderHistoricalDataSourceTests
    {
        [Fact]
        public void Acquire_maps_provider_records_and_filters_to_exact_utc_window()
        {
            var client = new FakeClient(new[]
            {
                Bar(new DateTime(2026, 8, 6, 13, 59, 0)),
                Bar(new DateTime(2026, 8, 6, 14, 0, 0)),
                Bar(new DateTime(2026, 8, 6, 14, 1, 0)),
                Bar(new DateTime(2026, 8, 6, 14, 2, 0))
            });
            var source = new NinjaTraderHistoricalDataSource(client, TimeZoneInfo.Utc, "CME US Index Futures ETH");
            var request = new HistoricalDataAcquisitionRequest(
                "MNQ",
                "09-26",
                Utc(2026, 8, 6, 14, 0, 0),
                Utc(2026, 8, 6, 14, 2, 0),
                60,
                HistoricalDataSourceKind.NinjaTraderProvider);

            var result = source.Acquire(request);

            Assert.Equal(2, result.Count);
            Assert.All(result, x => Assert.Equal(HistoricalDataSourceKind.NinjaTraderProvider, x.SourceKind));
            Assert.Equal("MNQ", result[0].Instrument);
            Assert.Equal("09-26", result[0].Contract);
            Assert.Equal(Utc(2026, 8, 6, 14, 0, 0), result[0].TimestampUtc);
            Assert.Equal("MNQ 09-26", client.LastRequest!.InstrumentFullName);
            Assert.Equal(NinjaTraderHistoricalLookupPolicy.Provider, client.LastRequest.LookupPolicy);
            Assert.Equal(60, client.LastRequest.IntervalSeconds);
        }

        [Fact]
        public void Acquire_maps_repository_lookup_policy()
        {
            var client = new FakeClient(new[] { Bar(new DateTime(2026, 8, 6, 14, 0, 0)) });
            var source = new NinjaTraderHistoricalDataSource(client, TimeZoneInfo.Utc, "CME US Index Futures ETH");
            var request = new HistoricalDataAcquisitionRequest(
                "MNQ",
                "MNQ 09-26",
                Utc(2026, 8, 6, 14, 0, 0),
                Utc(2026, 8, 6, 14, 1, 0),
                30,
                HistoricalDataSourceKind.NinjaTraderRepository);

            var result = source.Acquire(request);

            Assert.Single(result);
            Assert.Equal(HistoricalDataSourceKind.NinjaTraderRepository, result[0].SourceKind);
            Assert.Equal("MNQ 09-26", client.LastRequest!.InstrumentFullName);
            Assert.Equal(NinjaTraderHistoricalLookupPolicy.Repository, client.LastRequest.LookupPolicy);
            Assert.Equal(30, client.LastRequest.IntervalSeconds);
        }

        [Fact]
        public void Acquire_rejects_non_ninjatrader_source_kind()
        {
            var source = new NinjaTraderHistoricalDataSource(new FakeClient(Array.Empty<NinjaTraderHistoricalBarRecord>()), TimeZoneInfo.Utc, "CME US Index Futures ETH");
            var request = new HistoricalDataAcquisitionRequest(
                "MNQ",
                "09-26",
                Utc(2026, 8, 6, 14, 0, 0),
                Utc(2026, 8, 6, 15, 0, 0),
                60,
                HistoricalDataSourceKind.ImportedFile);

            Assert.Throws<InvalidOperationException>(() => source.Acquire(request));
        }

        [Fact]
        public void Acquire_requests_full_local_days_then_filters_exact_range()
        {
            var client = new FakeClient(Array.Empty<NinjaTraderHistoricalBarRecord>());
            var source = new NinjaTraderHistoricalDataSource(client, TimeZoneInfo.Utc, "CME US Index Futures ETH");
            var request = new HistoricalDataAcquisitionRequest(
                "MNQ",
                "09-26",
                Utc(2026, 8, 6, 14, 30, 0),
                Utc(2026, 8, 7, 15, 15, 0),
                60,
                HistoricalDataSourceKind.NinjaTraderProvider);

            source.Acquire(request);

            Assert.Equal(new DateTime(2026, 8, 6), client.LastRequest!.FromLocal);
            Assert.Equal(new DateTime(2026, 8, 8), client.LastRequest.ToLocal);
            Assert.Equal("CME US Index Futures ETH", client.LastRequest.TradingHoursTemplate);
        }

        private static NinjaTraderHistoricalBarRecord Bar(DateTime time)
        {
            return new NinjaTraderHistoricalBarRecord(
                time,
                time.Date,
                100m,
                102m,
                99m,
                101m,
                50,
                100.75m,
                101.00m);
        }

        private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute, int second)
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
        }

        private sealed class FakeClient : INinjaTraderHistoricalBarsClient
        {
            private readonly IReadOnlyList<NinjaTraderHistoricalBarRecord> _records;

            public FakeClient(IReadOnlyList<NinjaTraderHistoricalBarRecord> records)
            {
                _records = records;
            }

            public NinjaTraderHistoricalBarsRequest? LastRequest { get; private set; }

            public IReadOnlyList<NinjaTraderHistoricalBarRecord> Request(NinjaTraderHistoricalBarsRequest request)
            {
                LastRequest = request;
                return _records;
            }
        }
    }
}
