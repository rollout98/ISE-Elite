// Supervised, read-only NinjaTrader 8 probe for ISE Elite Historical Research.
// Apply to an MNQ 09-26 chart only after ISEEliteHistoricalBarsRequestClient.cs compiles cleanly.

using System;
using System.Threading.Tasks;
using ISE.NinjaTraderHost.HistoricalData;
using ISE.NinjaTraderRuntime.HistoricalData;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Indicators
{
    public sealed class ISEEliteHistoricalBarsRequestProbe : Indicator
    {
        private bool started;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only supervised probe for ISE Elite historical BarsRequest validation.";
                Name = "ISEEliteHistoricalBarsRequestProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-HIST-PROBE LOADED");
                StartProbe();
            }
        }

        private void StartProbe()
        {
            var instrumentFullName = Instrument == null ? null : Instrument.FullName;
            if (string.IsNullOrWhiteSpace(instrumentFullName))
            {
                Print("ISE-HIST-PROBE ERROR instrument is unavailable.");
                return;
            }

            // Use the immediately preceding full local calendar day so the request is small and deterministic.
            var todayLocal = DateTime.Today;
            var fromLocal = todayLocal.AddDays(-1);
            var toLocal = todayLocal;

            Print("ISE-HIST-PROBE START instrument=" + instrumentFullName
                + " fromLocal=" + fromLocal.ToString("yyyy-MM-dd HH:mm:ss")
                + " toLocal=" + toLocal.ToString("yyyy-MM-dd HH:mm:ss")
                + " interval=60s source=Provider tradingHours=CME US Index Futures ETH");

            Task.Run(() => RunRequest(instrumentFullName, fromLocal, toLocal));
        }

        private void RunRequest(string instrumentFullName, DateTime fromLocal, DateTime toLocal)
        {
            try
            {
                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(60));
                var request = new NinjaTraderHistoricalBarsRequest(
                    instrumentFullName,
                    fromLocal,
                    toLocal,
                    60,
                    NinjaTraderHistoricalLookupPolicy.Provider,
                    "CME US Index Futures ETH");

                var records = client.Request(request);
                if (records == null || records.Count == 0)
                {
                    Print("ISE-HIST-PROBE RESULT count=0 source=Provider");
                    return;
                }

                var first = records[0];
                var last = records[records.Count - 1];
                Print("ISE-HIST-PROBE RESULT count=" + records.Count
                    + " source=Provider first=" + first.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss")
                    + " last=" + last.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss")
                    + " firstClose=" + first.Close
                    + " lastClose=" + last.Close
                    + " firstTradingDay=" + first.TradingDay.ToString("yyyy-MM-dd")
                    + " lastTradingDay=" + last.TradingDay.ToString("yyyy-MM-dd"));
            }
            catch (Exception ex)
            {
                Print("ISE-HIST-PROBE ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
