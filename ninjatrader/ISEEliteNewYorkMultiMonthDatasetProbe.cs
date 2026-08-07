// Supervised, read-only NinjaTrader 8 multi-month dataset probe for ISE Elite Historical Research.
// Requires ISEEliteHistoricalBarsRequestClient.cs to be present in bin\Custom and compiled.
// This probe acquires completed MNQ history from NinjaTrader Repository, filters a broad NY research
// window in U.S. Central time, writes the same tab-delimited schema used by HistoricalDataFileStore,
// and prints coverage diagnostics. It does not submit, change, cancel, or flatten orders.

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
    public sealed class ISEEliteNewYorkMultiMonthDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime RequestedFromCentral = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RequestedToCentral = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan NyWindowStart = new TimeSpan(6, 0, 0);
        private static readonly TimeSpan NyWindowEnd = new TimeSpan(11, 0, 0);
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only supervised probe that creates a multi-month MNQ New York research dataset.";
                Name = "ISEEliteNewYorkMultiMonthDatasetProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-NY-DATASET LOADED");
                StartProbe();
            }
        }

        private void StartProbe()
        {
            var instrumentFullName = Instrument == null ? null : Instrument.FullName;
            if (string.IsNullOrWhiteSpace(instrumentFullName))
            {
                Print("ISE-NY-DATASET ERROR instrument is unavailable.");
                return;
            }

            Print("ISE-NY-DATASET START instrument=" + instrumentFullName
                + " fromCentral=" + RequestedFromCentral.ToString("yyyy-MM-dd")
                + " toCentral=" + RequestedToCentral.ToString("yyyy-MM-dd")
                + " nyWindow=" + NyWindowStart.ToString(@"hh\:mm") + "-" + NyWindowEnd.ToString(@"hh\:mm")
                + " interval=60s source=Repository tradingHours=" + TradingHoursTemplate);

            Task.Run(() => RunRequest(instrumentFullName));
        }

        private void RunRequest(string instrumentFullName)
        {
            try
            {
                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var request = new NinjaTraderHistoricalBarsRequest(
                    instrumentFullName,
                    RequestedFromCentral,
                    RequestedToCentral,
                    IntervalSeconds,
                    NinjaTraderHistoricalLookupPolicy.Repository,
                    TradingHoursTemplate);

                var records = client.Request(request);
                if (records == null)
                    throw new InvalidOperationException("NinjaTrader historical client returned null.");

                var centralTimeZone = ResolveCentralTimeZone();
                var inRange = records
                    .Where(x => x.TimestampLocal >= RequestedFromCentral && x.TimestampLocal < RequestedToCentral)
                    .OrderBy(x => x.TimestampLocal)
                    .ToList();

                var selected = inRange
                    .Where(x => x.TimestampLocal.TimeOfDay >= NyWindowStart && x.TimestampLocal.TimeOfDay < NyWindowEnd)
                    .OrderBy(x => x.TimestampLocal)
                    .ToList();

                if (selected.Count == 0)
                {
                    Print("ISE-NY-DATASET RESULT sourceBars=" + records.Count + " inRangeBars=" + inRange.Count + " selectedBars=0 sessions=0");
                    return;
                }

                var sessions = selected
                    .GroupBy(x => x.TimestampLocal.Date)
                    .OrderBy(x => x.Key)
                    .ToList();

                var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                Directory.CreateDirectory(outputDirectory);
                var safeInstrument = instrumentFullName.Replace(' ', '-').Replace('/', '-').Replace('\\', '-');
                var outputPath = Path.Combine(
                    outputDirectory,
                    "ny-" + safeInstrument + "-20260601-20260731-0600-1100-60s-repository.tsv");

                WriteHistoricalDataFile(outputPath, instrumentFullName, selected, centralTimeZone);

                var expectedBarsPerFullSession = (int)(NyWindowEnd - NyWindowStart).TotalMinutes;
                var partialSessions = sessions.Where(x => x.Count() < expectedBarsPerFullSession).ToList();
                var minBars = sessions.Min(x => x.Count());
                var maxBars = sessions.Max(x => x.Count());

                Print("ISE-NY-DATASET RESULT sourceBars=" + records.Count
                    + " inRangeBars=" + inRange.Count
                    + " selectedBars=" + selected.Count
                    + " sessions=" + sessions.Count
                    + " firstSession=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastSession=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " minBarsPerSession=" + minBars
                    + " maxBarsPerSession=" + maxBars
                    + " partialSessions=" + partialSessions.Count);

                if (partialSessions.Count > 0)
                {
                    var preview = string.Join(",", partialSessions.Take(12).Select(x => x.Key.ToString("yyyy-MM-dd") + ":" + x.Count()));
                    Print("ISE-NY-DATASET PARTIAL preview=" + preview);
                }

                Print("ISE-NY-DATASET FILE " + outputPath);
                Print("ISE-NY-DATASET COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-NY-DATASET ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void WriteHistoricalDataFile(
            string path,
            string instrumentFullName,
            IReadOnlyList<NinjaTraderHistoricalBarRecord> bars,
            TimeZoneInfo centralTimeZone)
        {
            const string header = "instrument\tcontract\ttimestampUtc\ttradingDay\tintervalSeconds\topen\thigh\tlow\tclose\tvolume\tsourceKind\tsourceName\tbid\task";
            var instrument = ExtractInstrument(instrumentFullName);
            var contract = ExtractContract(instrumentFullName);

            using (var writer = new StreamWriter(path, false))
            {
                writer.WriteLine(header);
                foreach (var bar in bars)
                {
                    var local = DateTime.SpecifyKind(bar.TimestampLocal, DateTimeKind.Unspecified);
                    if (centralTimeZone.IsInvalidTime(local) || centralTimeZone.IsAmbiguousTime(local))
                        throw new InvalidOperationException("Historical timestamp requires explicit DST disambiguation: " + local.ToString("O"));

                    var utc = TimeZoneInfo.ConvertTimeToUtc(local, centralTimeZone);
                    var timestampUtc = new DateTimeOffset(utc, TimeSpan.Zero);

                    writer.WriteLine(string.Join("\t", new[]
                    {
                        instrument,
                        contract,
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

        private static string ExtractInstrument(string instrumentFullName)
        {
            var parts = instrumentFullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? instrumentFullName : parts[0];
        }

        private static string ExtractContract(string instrumentFullName)
        {
            var parts = instrumentFullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidOperationException("Expected explicit futures contract in instrument name: " + instrumentFullName);
            return string.Join(" ", parts.Skip(1));
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
            }
        }
    }
}
