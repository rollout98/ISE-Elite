// NinjaTrader 8 runtime bridge for ISE Elite Historical Research.
// This source is intentionally kept outside the cross-platform solution build because it references NinjaTrader assemblies directly.
// Compile/deploy only inside the NinjaTrader 8 custom environment after the corresponding ISE host assemblies are available.

using System;
using System.Collections.Generic;
using System.Threading;
using ISE.NinjaTraderHost.HistoricalData;
using NinjaTrader.Cbi;
using NinjaTrader.Data;

namespace ISE.NinjaTraderRuntime.HistoricalData
{
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
