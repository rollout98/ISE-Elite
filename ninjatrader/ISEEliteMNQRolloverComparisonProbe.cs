// Supervised, read-only NinjaTrader 8 rollover comparison probe for ISE Elite Historical Research.
// Compares MNQ 06-26 and MNQ 09-26 over the June 2026 rollover overlap using Repository data only.
// No order-entry, position-management, account, or live-trading behavior.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ISE.NinjaTraderHost.HistoricalData;
using ISE.NinjaTraderRuntime.HistoricalData;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Indicators
{
    public sealed class ISEEliteMNQRolloverComparisonProbe : Indicator
    {
        private bool started;

        private static readonly DateTime FromCentral = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime ToCentral = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan WindowStart = new TimeSpan(6, 0, 0);
        private static readonly TimeSpan WindowEnd = new TimeSpan(11, 0, 0);
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only supervised MNQ June 2026 rollover comparison probe.";
                Name = "ISEEliteMNQRolloverComparisonProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-ROLLOVER LOADED");
                Task.Run(RunComparison);
            }
        }

        private void RunComparison()
        {
            try
            {
                Print("ISE-ROLLOVER START fromCentral=" + FromCentral.ToString("yyyy-MM-dd")
                    + " toCentral=" + ToCentral.ToString("yyyy-MM-dd")
                    + " window=06:00-11:00 interval=60s source=Repository");

                var june = RequestContract("MNQ 06-26");
                var september = RequestContract("MNQ 09-26");

                var juneByDay = Summarize(june);
                var septemberByDay = Summarize(september);
                var dates = juneByDay.Keys.Union(septemberByDay.Keys).OrderBy(x => x).ToList();

                Print("ISE-ROLLOVER SUMMARY contract06Bars=" + june.Count
                    + " contract09Bars=" + september.Count
                    + " comparedDays=" + dates.Count);

                DateTime? firstSeptemberVolumeLead = null;
                foreach (var date in dates)
                {
                    DailySummary j;
                    DailySummary s;
                    juneByDay.TryGetValue(date, out j);
                    septemberByDay.TryGetValue(date, out s);

                    var jBars = j == null ? 0 : j.Bars;
                    var jVolume = j == null ? 0L : j.Volume;
                    var sBars = s == null ? 0 : s.Bars;
                    var sVolume = s == null ? 0L : s.Volume;

                    if (!firstSeptemberVolumeLead.HasValue && sVolume > jVolume && sBars > 0)
                        firstSeptemberVolumeLead = date;

                    Print("ISE-ROLLOVER DAY date=" + date.ToString("yyyy-MM-dd")
                        + " 06bars=" + jBars
                        + " 06volume=" + jVolume
                        + " 09bars=" + sBars
                        + " 09volume=" + sVolume
                        + " volumeLeader=" + (sVolume > jVolume ? "09-26" : jVolume > sVolume ? "06-26" : "TIE"));
                }

                Print("ISE-ROLLOVER CROSSOVER first09VolumeLead="
                    + (firstSeptemberVolumeLead.HasValue ? firstSeptemberVolumeLead.Value.ToString("yyyy-MM-dd") : "NONE"));
                Print("ISE-ROLLOVER COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-ROLLOVER ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static IReadOnlyList<NinjaTraderHistoricalBarRecord> RequestContract(string instrumentFullName)
        {
            var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
            var request = new NinjaTraderHistoricalBarsRequest(
                instrumentFullName,
                FromCentral,
                ToCentral,
                IntervalSeconds,
                NinjaTraderHistoricalLookupPolicy.Repository,
                TradingHoursTemplate);

            var records = client.Request(request);
            if (records == null)
                throw new InvalidOperationException("NinjaTrader historical client returned null for " + instrumentFullName + ".");

            return records
                .Where(x => x.TimestampLocal >= FromCentral && x.TimestampLocal < ToCentral)
                .Where(x => x.TimestampLocal.TimeOfDay >= WindowStart && x.TimestampLocal.TimeOfDay < WindowEnd)
                .OrderBy(x => x.TimestampLocal)
                .ToList();
        }

        private static Dictionary<DateTime, DailySummary> Summarize(IReadOnlyList<NinjaTraderHistoricalBarRecord> records)
        {
            return records
                .GroupBy(x => x.TimestampLocal.Date)
                .ToDictionary(
                    x => x.Key,
                    x => new DailySummary(x.Count(), x.Sum(y => y.Volume)));
        }

        private sealed class DailySummary
        {
            public DailySummary(int bars, long volume)
            {
                Bars = bars;
                Volume = volume;
            }

            public int Bars { get; private set; }
            public long Volume { get; private set; }
        }
    }
}
