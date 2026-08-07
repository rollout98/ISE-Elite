// Supervised, read-only NinjaTrader 8 contract-aware MNQ morning-opportunity dataset probe.
// Extends the research window to 03:00-11:00 Central so timing can be discovered from market structure
// instead of prescribed around the 08:30 cash open. This probe does not submit, change, cancel, or flatten orders.

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
    public sealed class ISEEliteMNQMorningOpportunityDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime RequestedFromCentral = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RequestedToCentral = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RolloverBoundaryCentral = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan WindowStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan WindowEnd = new TimeSpan(11, 0, 0);
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only contract-aware MNQ 03:00-11:00 Central morning-opportunity research dataset probe.";
                Name = "ISEEliteMNQMorningOpportunityDatasetProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-MORNING-DATASET LOADED");
                Task.Run(Run);
            }
        }

        private void Run()
        {
            try
            {
                Print("ISE-MORNING-DATASET START fromCentral=2026-06-01 toCentral=2026-08-01 rolloverBoundary=2026-06-15 window=03:00-11:00 interval=60s source=Repository acquisition=daily-chunks");

                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var juneSelected = RequestSelectedDays(client, "MNQ 06-26", RequestedFromCentral, RolloverBoundaryCentral);
                var septemberSelected = RequestSelectedDays(client, "MNQ 09-26", RolloverBoundaryCentral, RequestedToCentral);

                var combined = juneSelected.Select(x => new ContractBar("MNQ", "06-26", x))
                    .Concat(septemberSelected.Select(x => new ContractBar("MNQ", "09-26", x)))
                    .OrderBy(x => x.Record.TimestampLocal)
                    .ToList();

                if (combined.Count == 0) throw new InvalidOperationException("Morning-opportunity dataset selected zero bars.");
                ValidateUniqueTimestamps(combined);

                var sessions = combined.GroupBy(x => x.Record.TimestampLocal.Date).OrderBy(x => x.Key).ToList();
                var expectedBars = (int)(WindowEnd - WindowStart).TotalMinutes;
                var partial = sessions.Where(x => x.Count() < expectedBars).ToList();

                var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, "morning-MNQ-contract-aware-20260601-20260731-0300-1100-60s-repository.tsv");
                Write(outputPath, combined, ResolveCentralTimeZone());

                Print("ISE-MORNING-DATASET RESULT june06Bars=" + juneSelected.Count
                    + " september09Bars=" + septemberSelected.Count
                    + " selectedBars=" + combined.Count
                    + " sessions=" + sessions.Count
                    + " firstSession=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastSession=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " minBarsPerSession=" + sessions.Min(x => x.Count())
                    + " maxBarsPerSession=" + sessions.Max(x => x.Count())
                    + " partialSessions=" + partial.Count);

                foreach (var p in partial)
                    Print("ISE-MORNING-DATASET PARTIAL date=" + p.Key.ToString("yyyy-MM-dd") + " bars=" + p.Count());

                Print("ISE-MORNING-DATASET FILE " + outputPath);
                Print("ISE-MORNING-DATASET COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-MORNING-DATASET ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static List<NinjaTraderHistoricalBarRecord> RequestSelectedDays(ISEEliteHistoricalBarsRequestClient client, string instrument, DateTime from, DateTime to)
        {
            var selected = new List<NinjaTraderHistoricalBarRecord>();
            for (var day = from.Date; day < to.Date; day = day.AddDays(1))
            {
                var nextDay = day.AddDays(1);
                var records = client.Request(new NinjaTraderHistoricalBarsRequest(instrument, day, nextDay, IntervalSeconds,
                    NinjaTraderHistoricalLookupPolicy.Repository, TradingHoursTemplate));
                selected.AddRange(records.Where(x => x.TimestampLocal >= day && x.TimestampLocal < nextDay)
                    .Where(x => x.TimestampLocal.TimeOfDay >= WindowStart && x.TimestampLocal.TimeOfDay < WindowEnd));
            }
            return selected.OrderBy(x => x.TimestampLocal).ToList();
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
            public ContractBar(string instrument, string contract, NinjaTraderHistoricalBarRecord record) { Instrument = instrument; Contract = contract; Record = record; }
            public string Instrument { get; }
            public string Contract { get; }
            public NinjaTraderHistoricalBarRecord Record { get; }
        }
    }
}
