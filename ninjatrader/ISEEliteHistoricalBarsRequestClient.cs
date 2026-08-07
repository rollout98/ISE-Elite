// NinjaTrader 8 runtime bridge for ISE Elite Historical Research.
// This source is intentionally kept outside the cross-platform solution build because it references NinjaTrader assemblies directly.
// It is self-contained for NinjaTrader's bin\Custom compilation and does not require ISE.NinjaTraderHost.dll.

using System;
using System.Collections.Generic;
using System.Threading;
using NinjaTrader.Cbi;
using NinjaTrader.Data;

namespace ISE.NinjaTraderHost.HistoricalData
{
    // Runtime copies of the small host-side contract used by the BarsRequest bridge.
    // Keep these definitions wire-compatible with src/ISE.NinjaTraderHost/HistoricalData/NinjaTraderHistoricalDataSource.cs.
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
}

namespace ISE.NinjaTraderRuntime.HistoricalData
{
    using ISE.NinjaTraderHost.HistoricalData;

    public sealed class ISEEliteHistoricalBarsRequestClient : INinjaTraderHistoricalBarsClient
    {
        private readonly TimeSpan _requestTimeout;

        public ISEEliteHistoricalBarsRequestClient()
            : this(TimeSpan.FromSeconds(60))
        {
        }

        public ISEEliteHistoricalBarsRequestClient(TimeSpan requestTimeout)
        {
            if (requestTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(requestTimeout));
            _requestTimeout = requestTimeout;
        }

        public IReadOnlyList<NinjaTraderHistoricalBarRecord> Request(NinjaTraderHistoricalBarsRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var instrument = Instrument.GetInstrument(request.InstrumentFullName);
            if (instrument == null)
                throw new InvalidOperationException("NinjaTrader could not resolve instrument '" + request.InstrumentFullName + "'.");

            using (var completed = new ManualResetEventSlim(false))
            using (var barsRequest = new BarsRequest(instrument, request.FromLocal, request.ToLocal))
            {
                barsRequest.BarsPeriod = BuildBarsPeriod(request.IntervalSeconds);
                barsRequest.TradingHours = TradingHours.Get(request.TradingHoursTemplate);
                if (barsRequest.TradingHours == null)
                    throw new InvalidOperationException("NinjaTrader trading-hours template not found: '" + request.TradingHoursTemplate + "'.");

                barsRequest.LookupPolicy = request.LookupPolicy == NinjaTraderHistoricalLookupPolicy.Provider
                    ? LookupPolicies.Provider
                    : LookupPolicies.Repository;
                barsRequest.MergePolicy = MergePolicy.DoNotMerge;
                barsRequest.IsResetOnNewTradingDay = true;

                ErrorCode callbackErrorCode = ErrorCode.NoError;
                string callbackErrorMessage = null;
                var records = new List<NinjaTraderHistoricalBarRecord>();

                barsRequest.Request(new Action<BarsRequest, ErrorCode, string>((returned, errorCode, errorMessage) =>
                {
                    try
                    {
                        callbackErrorCode = errorCode;
                        callbackErrorMessage = errorMessage;
                        if (errorCode != ErrorCode.NoError)
                            return;

                        var sessionIterator = new SessionIterator(returned.Bars);
                        for (var i = 0; i < returned.Bars.Count; i++)
                        {
                            var timeLocal = returned.Bars.GetTime(i);
                            var tradingDay = sessionIterator.GetTradingDay(timeLocal).Date;
                            var bid = returned.Bars.GetBid(i);
                            var ask = returned.Bars.GetAsk(i);

                            records.Add(new NinjaTraderHistoricalBarRecord(
                                timeLocal,
                                tradingDay,
                                Convert.ToDecimal(returned.Bars.GetOpen(i)),
                                Convert.ToDecimal(returned.Bars.GetHigh(i)),
                                Convert.ToDecimal(returned.Bars.GetLow(i)),
                                Convert.ToDecimal(returned.Bars.GetClose(i)),
                                returned.Bars.GetVolume(i),
                                bid > 0 ? (decimal?)Convert.ToDecimal(bid) : null,
                                ask > 0 ? (decimal?)Convert.ToDecimal(ask) : null));
                        }
                    }
                    finally
                    {
                        completed.Set();
                    }
                }));

                if (!completed.Wait(_requestTimeout))
                    throw new TimeoutException("NinjaTrader BarsRequest did not complete within " + _requestTimeout + ".");

                if (callbackErrorCode != ErrorCode.NoError)
                    throw new InvalidOperationException("NinjaTrader BarsRequest failed: " + callbackErrorCode + ". " + callbackErrorMessage);

                return records;
            }
        }

        private static BarsPeriod BuildBarsPeriod(int intervalSeconds)
        {
            if (intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));

            if (intervalSeconds % 60 == 0)
            {
                return new BarsPeriod
                {
                    BarsPeriodType = BarsPeriodType.Minute,
                    Value = intervalSeconds / 60
                };
            }

            return new BarsPeriod
            {
                BarsPeriodType = BarsPeriodType.Second,
                Value = intervalSeconds
            };
        }
    }
}
