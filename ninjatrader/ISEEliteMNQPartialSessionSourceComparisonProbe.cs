// Supervised, read-only NinjaTrader 8 diagnostic for ISE Elite Historical Research.
// Compares Provider vs Repository coverage for known partial MNQ New York research sessions.
// It submits no orders and does not modify positions or accounts.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ISE.NinjaTraderHost.HistoricalData;
using ISE.NinjaTraderRuntime.HistoricalData;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Indicators
{
    public sealed class ISEEliteMNQPartialSessionSourceComparisonProbe : Indicator
    {
        private bool started;
        private static readonly TimeSpan WindowStart = new TimeSpan(6, 0, 0);
        private static readonly TimeSpan WindowEnd = new TimeSpan(11, 0, 0);
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        private sealed class DiagnosticDate
        {
            public DiagnosticDate(DateTime date, string contract)
            {
                Date = date.Date;
                Contract = contract;
            }

            public DateTime Date { get; }
            public string Contract { get; }
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only probe comparing NinjaTrader Provider and Repository coverage for partial MNQ NY research sessions.";
                Name = "ISEEliteMNQPartialSessionSourceComparisonProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-PARTIAL-CHECK LOADED");
                Task.Run(RunDiagnostics);
            }
        }

        private void RunDiagnostics()
        {
            try
            {
                var dates = new[]
                {
                    new DiagnosticDate(new DateTime(2026, 6, 19), "MNQ 09-26"),
                    new DiagnosticDate(new DateTime(2026, 7, 3), "MNQ 09-26"),
                    new DiagnosticDate(new DateTime(2026, 7, 9), "MNQ 09-26"),
                    new DiagnosticDate(new DateTime(2026, 7, 22), "MNQ 09-26")
                };

                Print("ISE-PARTIAL-CHECK START dates=4 window=06:00-11:00 interval=60s tradingHours=" + TradingHoursTemplate);

                foreach (var item in dates)
                {
                    var provider = RequestWindow(item.Contract, item.Date, NinjaTraderHistoricalLookupPolicy.Provider);
                    var repository = RequestWindow(item.Contract, item.Date, NinjaTraderHistoricalLookupPolicy.Repository);
                    var classification = Classify(provider.Count, repository.Count);

                    Print("ISE-PARTIAL-CHECK DAY date=" + item.Date.ToString("yyyy-MM-dd")
                        + " contract=" + item.Contract
                        + " providerBars=" + provider.Count
                        + " repositoryBars=" + repository.Count
                        + " providerFirst=" + FormatFirst(provider)
                        + " providerLast=" + FormatLast(provider)
                        + " repositoryFirst=" + FormatFirst(repository)
                        + " repositoryLast=" + FormatLast(repository)
                        + " classification=" + classification);
                }

                Print("ISE-PARTIAL-CHECK COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-PARTIAL-CHECK ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static IReadOnlyList<NinjaTraderHistoricalBarRecord> RequestWindow(
            string contract,
            DateTime date,
            NinjaTraderHistoricalLookupPolicy lookupPolicy)
        {
            var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
            var from = date.Date;
            var to = date.Date.AddDays(1);
            var request = new NinjaTraderHistoricalBarsRequest(
                contract,
                from,
                to,
                IntervalSeconds,
                lookupPolicy,
                TradingHoursTemplate);

            var records = client.Request(request) ?? throw new InvalidOperationException("Historical client returned null for " + contract + ".");
            return records
                .Where(x => x.TimestampLocal.Date == date.Date)
                .Where(x => x.TimestampLocal.TimeOfDay >= WindowStart && x.TimestampLocal.TimeOfDay < WindowEnd)
                .OrderBy(x => x.TimestampLocal)
                .ToList();
        }

        private static string Classify(int providerBars, int repositoryBars)
        {
            const int expected = 300;
            if (providerBars == expected && repositoryBars == expected)
                return "FULL_BOTH";
            if (providerBars == expected && repositoryBars < expected)
                return "REPOSITORY_GAP";
            if (providerBars < expected && repositoryBars == providerBars)
                return "SOURCE_AGREE_PARTIAL";
            if (providerBars < expected && repositoryBars < expected && providerBars != repositoryBars)
                return "SOURCE_MISMATCH_PARTIAL";
            if (providerBars < expected && repositoryBars == expected)
                return "PROVIDER_GAP_OR_TIMING";
            return "OTHER";
        }

        private static string FormatFirst(IReadOnlyList<NinjaTraderHistoricalBarRecord> bars)
        {
            return bars.Count == 0 ? "none" : bars[0].TimestampLocal.ToString("HH:mm:ss");
        }

        private static string FormatLast(IReadOnlyList<NinjaTraderHistoricalBarRecord> bars)
        {
            return bars.Count == 0 ? "none" : bars[bars.Count - 1].TimestampLocal.ToString("HH:mm:ss");
        }
    }
}
