using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;

namespace ISE.NinjaTraderHost.HistoricalData
{
    public enum NinjaTraderHistoricalLookupPolicy
    {
        Provider = 1,
        Repository = 2
    }

    public sealed class NinjaTraderHistoricalBarsRequest
    {
        public NinjaTraderHistoricalBarsRequest(
            string instrumentFullName,
            DateTime fromLocal,
            DateTime toLocal,
            int intervalSeconds,
            NinjaTraderHistoricalLookupPolicy lookupPolicy,
            string tradingHoursTemplate)
        {
            if (string.IsNullOrWhiteSpace(instrumentFullName)) throw new ArgumentException("Instrument full name is required.", nameof(instrumentFullName));
            if (toLocal <= fromLocal) throw new ArgumentException("End must be after start.", nameof(toLocal));
            if (intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            if (string.IsNullOrWhiteSpace(tradingHoursTemplate)) throw new ArgumentException("Trading-hours template is required.", nameof(tradingHoursTemplate));

            InstrumentFullName = instrumentFullName.Trim();
            FromLocal = DateTime.SpecifyKind(fromLocal, DateTimeKind.Unspecified);
            ToLocal = DateTime.SpecifyKind(toLocal, DateTimeKind.Unspecified);
            IntervalSeconds = intervalSeconds;
            LookupPolicy = lookupPolicy;
            TradingHoursTemplate = tradingHoursTemplate.Trim();
        }

        public string InstrumentFullName { get; }
        public DateTime FromLocal { get; }
        public DateTime ToLocal { get; }
        public int IntervalSeconds { get; }
        public NinjaTraderHistoricalLookupPolicy LookupPolicy { get; }
        public string TradingHoursTemplate { get; }
    }

    public sealed class NinjaTraderHistoricalBarRecord
    {
        public NinjaTraderHistoricalBarRecord(
            DateTime timestampLocal,
            DateTime tradingDay,
            decimal open,
            decimal high,
            decimal low,
            decimal close,
            long volume,
            decimal? bid,
            decimal? ask)
        {
            if (tradingDay.TimeOfDay != TimeSpan.Zero) throw new ArgumentException("Trading day must be date-only.", nameof(tradingDay));

            TimestampLocal = DateTime.SpecifyKind(timestampLocal, DateTimeKind.Unspecified);
            TradingDay = tradingDay.Date;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            Bid = bid;
            Ask = ask;
        }

        public DateTime TimestampLocal { get; }
        public DateTime TradingDay { get; }
        public decimal Open { get; }
        public decimal High { get; }
        public decimal Low { get; }
        public decimal Close { get; }
        public long Volume { get; }
        public decimal? Bid { get; }
        public decimal? Ask { get; }
    }

    public interface INinjaTraderHistoricalBarsClient
    {
        IReadOnlyList<NinjaTraderHistoricalBarRecord> Request(NinjaTraderHistoricalBarsRequest request);
    }

    public sealed class NinjaTraderHistoricalDataSource : IHistoricalDataSource
    {
        private readonly INinjaTraderHistoricalBarsClient _client;
        private readonly TimeZoneInfo _ninjaTraderTimeZone;
        private readonly string _tradingHoursTemplate;

        public NinjaTraderHistoricalDataSource(
            INinjaTraderHistoricalBarsClient client,
            TimeZoneInfo ninjaTraderTimeZone,
            string tradingHoursTemplate)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _ninjaTraderTimeZone = ninjaTraderTimeZone ?? throw new ArgumentNullException(nameof(ninjaTraderTimeZone));
            if (string.IsNullOrWhiteSpace(tradingHoursTemplate)) throw new ArgumentException("Trading-hours template is required.", nameof(tradingHoursTemplate));
            _tradingHoursTemplate = tradingHoursTemplate.Trim();
        }

        public IReadOnlyList<HistoricalBar> Acquire(HistoricalDataAcquisitionRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var lookupPolicy = ResolveLookupPolicy(request.PreferredSource);
            var startLocal = TimeZoneInfo.ConvertTime(request.StartUtc, _ninjaTraderTimeZone).DateTime;
            var endLocal = TimeZoneInfo.ConvertTime(request.EndUtc, _ninjaTraderTimeZone).DateTime;

            // NinjaTrader BarsRequest(fromLocal, toLocal) works in full local trading days.
            // Request a containing day range, then enforce the exact UTC interval below.
            var barsRequest = new NinjaTraderHistoricalBarsRequest(
                BuildInstrumentFullName(request.Instrument, request.Contract),
                startLocal.Date,
                endLocal.Date.AddDays(1),
                request.IntervalSeconds,
                lookupPolicy,
                _tradingHoursTemplate);

            var raw = _client.Request(barsRequest);
            if (raw == null) throw new InvalidOperationException("NinjaTrader historical-bars client returned null.");

            var sourceKind = lookupPolicy == NinjaTraderHistoricalLookupPolicy.Provider
                ? HistoricalDataSourceKind.NinjaTraderProvider
                : HistoricalDataSourceKind.NinjaTraderRepository;
            var sourceName = lookupPolicy == NinjaTraderHistoricalLookupPolicy.Provider
                ? "NinjaTrader BarsRequest Provider"
                : "NinjaTrader BarsRequest Repository";

            var result = new List<HistoricalBar>(raw.Count);
            foreach (var record in raw)
            {
                var utc = ConvertLocalTimestampToUtc(record.TimestampLocal);
                if (utc < request.StartUtc || utc >= request.EndUtc)
                    continue;

                result.Add(new HistoricalBar(
                    request.Instrument,
                    request.Contract,
                    utc,
                    record.TradingDay,
                    request.IntervalSeconds,
                    record.Open,
                    record.High,
                    record.Low,
                    record.Close,
                    record.Volume,
                    sourceKind,
                    sourceName,
                    record.Bid,
                    record.Ask));
            }

            return result;
        }

        private DateTimeOffset ConvertLocalTimestampToUtc(DateTime localTimestamp)
        {
            var unspecified = DateTime.SpecifyKind(localTimestamp, DateTimeKind.Unspecified);
            if (_ninjaTraderTimeZone.IsInvalidTime(unspecified))
                throw new InvalidOperationException("NinjaTrader returned a timestamp that falls in a daylight-saving time gap.");
            if (_ninjaTraderTimeZone.IsAmbiguousTime(unspecified))
                throw new InvalidOperationException("NinjaTrader returned an ambiguous daylight-saving timestamp; explicit disambiguation is required before research use.");

            var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, _ninjaTraderTimeZone);
            return new DateTimeOffset(utc, TimeSpan.Zero);
        }

        private static NinjaTraderHistoricalLookupPolicy ResolveLookupPolicy(HistoricalDataSourceKind source)
        {
            if (source == HistoricalDataSourceKind.NinjaTraderProvider)
                return NinjaTraderHistoricalLookupPolicy.Provider;
            if (source == HistoricalDataSourceKind.NinjaTraderRepository)
                return NinjaTraderHistoricalLookupPolicy.Repository;

            throw new InvalidOperationException("NinjaTrader historical source supports only Provider or Repository lookup policies.");
        }

        private static string BuildInstrumentFullName(string instrument, string contract)
        {
            var trimmedInstrument = instrument.Trim();
            var trimmedContract = contract.Trim();
            if (trimmedContract.StartsWith(trimmedInstrument + " ", StringComparison.OrdinalIgnoreCase))
                return trimmedContract;
            return trimmedInstrument + " " + trimmedContract;
        }
    }
}
