// Supervised, read-only NinjaTrader 8 contract-aware MNQ dataset probe for ISE Elite Historical Research.
// Uses observed June 2026 volume crossover to preserve contract identity across the rollover.
// It does not submit, change, cancel, or flatten orders.

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
    public sealed class ISEEliteMNQContractAwareDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime RequestedFromCentral = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RequestedToCentral = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RolloverBoundaryCentral = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan NyWindowStart = new TimeSpan(6, 0, 0);
        private static readonly TimeSpan NyWindowEnd = new TimeSpan(11, 0, 0);
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only contract-aware MNQ New York research dataset probe.";
                Name = "ISEEliteMNQContractAwareDatasetProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-CONTRACT-DATASET LOADED");
                Task.Run(Run);
            }
        }

        private void Run()
        {
            try
            {
                Print("ISE-CONTRACT-DATASET START fromCentral=2026-06-01 toCentral=2026-08-01 rolloverBoundary=2026-06-15 window=06:00-11:00 interval=60s source=Repository");

                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var june = Request(client, "MNQ 06-26", RequestedFromCentral, RolloverBoundaryCentral);
                var september = Request(client, "MNQ 09-26", RolloverBoundaryCentral, RequestedToCentral);

                var juneSelected = SelectWindow(june, RequestedFromCentral, RolloverBoundaryCentral);
                var septemberSelected = SelectWindow(september, RolloverBoundaryCentral, RequestedToCentral);

                var combined = juneSelected
                    .Select(x => new ContractBar("MNQ", "06-26", x))
                    .Concat(septemberSelected.Select(x => new ContractBar("MNQ", "09-26", x)))
                    .OrderBy(x => x.Record.TimestampLocal)
                    .ToList();

                if (combined.Count == 0)
                    throw new InvalidOperationException("Contract-aware dataset selected zero bars.");

                var sessions = combined.GroupBy(x => x.Record.TimestampLocal.Date).OrderBy(x => x.Key).ToList();
                var expectedBars = (int)(NyWindowEnd - NyWindowStart).TotalMinutes;
                var partial = sessions.Where(x => x.Count() < expectedBars).ToList();

                var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, "ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv");
                Write(outputPath, combined, ResolveCentralTimeZone());

                Print("ISE-CONTRACT-DATASET RESULT june06Bars=" + juneSelected.Count
                    + " september09Bars=" + septemberSelected.Count
                    + " selectedBars=" + combined.Count
                    + " sessions=" + sessions.Count
                    + " firstSession=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastSession=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " minBarsPerSession=" + sessions.Min(x => x.Count())
                    + " maxBarsPerSession=" + sessions.Max(x => x.Count())
                    + " partialSessions=" + partial.Count);

                foreach (var p in partial)
                    Print("ISE-CONTRACT-DATASET PARTIAL date=" + p.Key.ToString("yyyy-MM-dd") + " bars=" + p.Count());

                Print("ISE-CONTRACT-DATASET FILE " + outputPath);
                Print("ISE-CONTRACT-DATASET COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-CONTRACT-DATASET ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static IReadOnlyList<NinjaTraderHistoricalBarRecord> Request(ISEEliteHistoricalBarsRequestClient client, string instrument, DateTime from, DateTime to)
        {
            return client.Request(new NinjaTraderHistoricalBarsRequest(
                instrument,
                from,
                to,
                IntervalSeconds,
                NinjaTraderHistoricalLookupPolicy.Repository,
                TradingHoursTemplate));
        }

        private static List<NinjaTraderHistoricalBarRecord> SelectWindow(IReadOnlyList<NinjaTraderHistoricalBarRecord> records, DateTime from, DateTime to)
        {
            return records
                .Where(x => x.TimestampLocal >= from && x.TimestampLocal < to)
                .Where(x => x.TimestampLocal.TimeOfDay >= NyWindowStart && x.TimestampLocal.TimeOfDay < NyWindowEnd)
                .OrderBy(x => x.TimestampLocal)
                .ToList();
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
                        item.Instrument,
                        item.Contract,
                        timestampUtc.ToString("O", CultureInfo.InvariantCulture),
                        bar.TradingDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        IntervalSeconds.ToString(CultureInfo.InvariantCulture),
                        bar.Open.ToString(CultureInfo.InvariantCulture),
                        bar.High.ToString(CultureInfo.InvariantCulture),
                        bar.Low.ToString(CultureInfo.InvariantCulture),
                        bar.Close.ToString(CultureInfo.InvariantCulture),
                        bar.Volume.ToString(CultureInfo.InvariantCulture),
                        "2",
                        "NinjaTrader BarsRequest Repository",
                        bar.Bid.HasValue ? bar.Bid.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
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
