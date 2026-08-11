// Supervised, read-only NinjaTrader 8 dataset probe for ISE Elite Historical Research.
// Generates both MNQ and MGC datasets in a single run.
// Requires ISEEliteHistoricalBarsRequestClient.cs to be present in bin\Custom and compiled.

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
    public sealed class ISEEliteMNQMGCDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime RequestedFromCentral = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RequestedToCentral = new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan WindowStart = TimeSpan.Zero;
        private static readonly TimeSpan WindowEnd = new TimeSpan(24, 0, 0);
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Generates MNQ and MGC datasets for ISE Elite research.";
                Name = "ISEEliteMNQMGCDatasetProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-MNQ-MGC-DATASET LOADED");
                StartProbe();
            }
        }

        private void StartProbe()
        {
            var instrumentFullName = Instrument == null ? null : Instrument.FullName;
            if (string.IsNullOrWhiteSpace(instrumentFullName))
            {
                Print("ISE-MNQ-MGC-DATASET ERROR instrument is unavailable.");
                return;
            }

            Print("ISE-MNQ-MGC-DATASET START instrument=" + instrumentFullName
                + " fromCentral=" + RequestedFromCentral.ToString("yyyy-MM-dd")
                + " toCentral=" + RequestedToCentral.ToString("yyyy-MM-dd")
                + " window=" + WindowStart.ToString(@"hh\:mm") + "-" + WindowEnd.ToString(@"hh\:mm")
                + " interval=60s");

            Task.Run(() => RunRequest(instrumentFullName));
        }

        private void RunRequest(string instrumentFullName)
        {
            try
            {
                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var centralTimeZone = ResolveCentralTimeZone();

                // Fetch both MNQ and MGC
                foreach (var contract in new[] { "09-26", "U26" })  // MNQ 09-26, MGC U26
                {
                    var contractName = contract.StartsWith("U") ? "MGC" : "MNQ";
                    var request = new NinjaTraderHistoricalBarsRequest(
                        instrumentFullName.Replace("MNQ", contractName),
                        RequestedFromCentral,
                        RequestedToCentral,
                        IntervalSeconds,
                        NinjaTraderHistoricalLookupPolicy.Repository,
                        TradingHoursTemplate);

                    try
                    {
                        var records = client.Request(request);
                        if (records == null)
                            throw new InvalidOperationException("NinjaTrader historical client returned null.");

                        var inRange = records
                            .Where(x => x.TimestampLocal >= RequestedFromCentral && x.TimestampLocal < RequestedToCentral)
                            .OrderBy(x => x.TimestampLocal)
                            .ToList();

                        var sessions = inRange
                            .GroupBy(x => x.TimestampLocal.Date)
                            .OrderBy(x => x.Key)
                            .ToList();

                        var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                        Directory.CreateDirectory(outputDirectory);

                        var safeInstrument = instrumentFullName.Replace(' ', '-').Replace('/', '-').Replace('\\', '-');
                        var outputPath = Path.Combine(
                            outputDirectory,
                            "ny-" + safeInstrument.Replace("MNQ", contractName)
                                + "-" + RequestedFromCentral.ToString("yyyyMMdd")
                                + "-" + RequestedToCentral.AddDays(-1).ToString("yyyyMMdd")
                                + "-0000-2400-60s-repository.tsv");

                        WriteHistoricalDataFile(outputPath, instrumentFullName, inRange, centralTimeZone);

                        Print($"ISE-MNQ-MGC-DATASET {contractName} sourceBars={records.Count}"
                            + $" inRangeBars={inRange.Count} sessions={sessions.Count}"
                            + $" firstSession={sessions[0].Key:yyyy-MM-dd} lastSession={sessions[sessions.Count - 1].Key:yyyy-MM-dd}");
                        Print($"ISE-MNQ-MGC-DATASET FILE {outputPath}");
                    }
                    catch (Exception ex)
                    {
                        Print($"ISE-MNQ-MGC-DATASET ERROR fetching {contractName}: {ex.Message}");
                    }
                }

                Print("ISE-MNQ-MGC-DATASET COMPLETE");
            }
            catch (Exception ex)
            {
                Print($"ISE-MNQ-MGC-DATASET ERROR: {ex.Message}");
            }
        }

        private void WriteHistoricalDataFile(string path, string instrumentName, List<NinjaTraderHistoricalBarRecord> bars, TimeZoneInfo tz)
        {
            const string header = "instrument\tcontract\ttimestampUtc\ttradingDay\tintervalSeconds\topen\thigh\tlow\tclose\tvolume\tsourceKind\tsourceName\tbid\task";

            using (var writer = new StreamWriter(path, false, System.Text.Encoding.UTF8))
            {
                writer.WriteLine(header);
                foreach (var bar in bars)
                {
                    var utc = TimeZoneInfo.ConvertTimeToUtc(
                        DateTime.SpecifyKind(bar.TimestampLocal, DateTimeKind.Unspecified), tz);

                    writer.WriteLine(string.Join("\t", new[]
                    {
                        instrumentName,
                        bar.TimestampLocal.ToString("yyyyMMdd"),
                        utc.ToString("O"),
                        bar.TradingDay.ToString("yyyy-MM-dd"),
                        "60",
                        bar.Open.ToString(CultureInfo.InvariantCulture),
                        bar.High.ToString(CultureInfo.InvariantCulture),
                        bar.Low.ToString(CultureInfo.InvariantCulture),
                        bar.Close.ToString(CultureInfo.InvariantCulture),
                        bar.Volume.ToString(),
                        "NinjaTraderRepository",
                        "NinjaTrader BarsRequest Repository",
                        (bar.Bid ?? 0m).ToString(CultureInfo.InvariantCulture),
                        (bar.Ask ?? 0m).ToString(CultureInfo.InvariantCulture)
                    }));
                }
            }
        }

        private TimeZoneInfo ResolveCentralTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
