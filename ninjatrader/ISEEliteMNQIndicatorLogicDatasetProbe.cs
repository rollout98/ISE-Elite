// Supervised, read-only NinjaTrader 8 contract-aware MNQ full-session dataset probe.
// This dataset exists specifically to warm and evaluate the user's 3-minute Range Filter entry logic and
// 5-minute VectorFlow hold-bias logic before the 03:00-11:00 research window. No orders are submitted,
// changed, cancelled, or flattened by this probe.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ISE.NinjaTraderHost.HistoricalData;
using ISE.NinjaTraderRuntime.HistoricalData;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Indicators
{
    public sealed class ISEEliteMNQIndicatorLogicDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime RequestedFromTradingDayCentral = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RequestedToTradingDayCentral = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RolloverBoundaryTradingDayCentral = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan SessionStartPreviousDay = new TimeSpan(17, 0, 0);
        private static readonly TimeSpan SessionEndTradingDay = new TimeSpan(16, 0, 0);
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only contract-aware MNQ full ETH-session dataset for Range Filter and VectorFlow research.";
                Name = "ISEEliteMNQIndicatorLogicDatasetProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-INDICATOR-DATASET LOADED");
                Task.Run(Run);
            }
        }

        private void Run()
        {
            try
            {
                Print("ISE-INDICATOR-DATASET START tradingDays=2026-06-01..2026-07-31 rolloverTradingDay=2026-06-15 session=17:00-prev-to-16:00 interval=60s source=Repository acquisition=trading-day-chunks");
                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var combined = new List<ContractBar>();

                for (var tradingDay = RequestedFromTradingDayCentral.Date; tradingDay < RequestedToTradingDayCentral.Date; tradingDay = tradingDay.AddDays(1))
                {
                    var contract = tradingDay < RolloverBoundaryTradingDayCentral ? "06-26" : "09-26";
                    var instrument = "MNQ " + contract;
                    var selected = RequestTradingSession(client, instrument, tradingDay);
                    combined.AddRange(selected.Select(x => new ContractBar("MNQ", contract, x)));
                }

                combined = combined.OrderBy(x => x.Record.TimestampLocal).ToList();
                if (combined.Count == 0) throw new InvalidOperationException("Indicator-logic dataset selected zero bars.");
                ValidateUniqueTimestamps(combined);

                var sessions = combined.GroupBy(x => x.Record.TradingDay.Date).OrderBy(x => x.Key).ToList();
                const int expectedBars = 23 * 60;
                var partial = sessions.Where(x => x.Count() < expectedBars).ToList();

                var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, "indicatorlogic-MNQ-contract-aware-20260601-20260731-1700-1600-60s-repository.tsv");
                Write(outputPath, combined, ResolveCentralTimeZone());

                Print("ISE-INDICATOR-DATASET RESULT selectedBars=" + combined.Count
                    + " sessions=" + sessions.Count
                    + " firstTradingDay=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastTradingDay=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " minBarsPerSession=" + sessions.Min(x => x.Count())
                    + " maxBarsPerSession=" + sessions.Max(x => x.Count())
                    + " partialSessions=" + partial.Count);

                foreach (var p in partial)
                    Print("ISE-INDICATOR-DATASET PARTIAL tradingDay=" + p.Key.ToString("yyyy-MM-dd") + " bars=" + p.Count());

                Print("ISE-INDICATOR-DATASET FILE " + outputPath);
                Print("ISE-INDICATOR-DATASET COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-INDICATOR-DATASET ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static List<NinjaTraderHistoricalBarRecord> RequestTradingSession(ISEEliteHistoricalBarsRequestClient client, string instrument, DateTime tradingDay)
        {
            var sessionStart = tradingDay.Date.AddDays(-1).Add(SessionStartPreviousDay);
            var sessionEnd = tradingDay.Date.Add(SessionEndTradingDay);
            var records = new List<NinjaTraderHistoricalBarRecord>();

            for (var day = sessionStart.Date; day <= tradingDay.Date; day = day.AddDays(1))
            {
                var nextDay = day.AddDays(1);
                records.AddRange(client.Request(new NinjaTraderHistoricalBarsRequest(instrument, day, nextDay, IntervalSeconds,
                    NinjaTraderHistoricalLookupPolicy.Repository, TradingHoursTemplate)));
            }

            return records.Where(x => x.TimestampLocal >= sessionStart && x.TimestampLocal < sessionEnd)
                .Where(x => x.TradingDay.Date == tradingDay.Date)
                .GroupBy(x => x.TimestampLocal)
                .Select(x => x.First())
                .OrderBy(x => x.TimestampLocal)
                .ToList();
        }

        private static void ValidateUniqueTimestamps(IReadOnlyList<ContractBar> bars)
        {
            var duplicate = bars.GroupBy(x => x.Record.TimestampLocal).FirstOrDefault(x => x.Count() > 1);
            if (duplicate != null) throw new InvalidOperationException("Duplicate contract-aware timestamp detected: " + duplicate.Key.ToString("O"));
        }

        private static void Write(string path, IReadOnlyList<ContractBar> bars, TimeZoneInfo centralTimeZone)
        {
            const string header = "instrument\tcontract\ttimestampUtc\ttradingDay\tintervalSeconds\topen\thigh\tlow\tclose\tvolume\tsourceKind\tsourceName\tbid\task";
            using (var writer = new StreamWriter(path, false))
            {
                writer.WriteLine(header);
                foreach (var item in bars)
                {
                    var bar = item.Record;
                    var local = DateTime.SpecifyKind(bar.TimestampLocal, DateTimeKind.Unspecified);
                    if (centralTimeZone.IsInvalidTime(local) || centralTimeZone.IsAmbiguousTime(local))
                        throw new InvalidOperationException("Historical timestamp requires explicit DST disambiguation: " + local.ToString("O"));
                    var utc = TimeZoneInfo.ConvertTimeToUtc(local, centralTimeZone);
                    var timestampUtc = new DateTimeOffset(utc, TimeSpan.Zero);
                    writer.WriteLine(string.Join("\t", new[]
                    {
                        item.Instrument, item.Contract, timestampUtc.ToString("O", CultureInfo.InvariantCulture),
                        bar.TradingDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), IntervalSeconds.ToString(CultureInfo.InvariantCulture),
                        bar.Open.ToString(CultureInfo.InvariantCulture), bar.High.ToString(CultureInfo.InvariantCulture), bar.Low.ToString(CultureInfo.InvariantCulture),
                        bar.Close.ToString(CultureInfo.InvariantCulture), bar.Volume.ToString(CultureInfo.InvariantCulture), "2",
                        "NinjaTrader BarsRequest Repository", bar.Bid.HasValue ? bar.Bid.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        bar.Ask.HasValue ? bar.Ask.Value.ToString(CultureInfo.InvariantCulture) : string.Empty
                    }));
                }
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }

        private sealed class ContractBar
        {
            public ContractBar(string instrument, string contract, NinjaTraderHistoricalBarRecord record)
            {
                Instrument = instrument;
                Contract = contract;
                Record = record;
            }
            public string Instrument { get; }
            public string Contract { get; }
            public NinjaTraderHistoricalBarRecord Record { get; }
        }
    }
}
