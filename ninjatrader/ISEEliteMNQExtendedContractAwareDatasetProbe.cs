// Supervised, read-only NinjaTrader 8 extended contract-aware MNQ dataset probe for ISE Elite V5.7.
// Contract choice for each output day is determined from the PRIOR observed day's total volume.
// This keeps contract stitching causal: no same-day future volume is used to choose the price series.
// The probe does not submit, change, cancel, or flatten orders.

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
    public sealed class ISEEliteMNQExtendedContractAwareDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime RequestedFromCentral = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RequestedToCentral = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan ResearchWindowStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan ResearchWindowEnd = new TimeSpan(11, 0, 0);
        private const int WarmupCalendarDays = 10;
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        private static readonly ContractSpec[] Contracts =
        {
            new ContractSpec("MNQ 12-25", "12-25"),
            new ContractSpec("MNQ 03-26", "03-26"),
            new ContractSpec("MNQ 06-26", "06-26"),
            new ContractSpec("MNQ 09-26", "09-26")
        };

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only extended causal contract-aware MNQ research dataset probe.";
                Name = "ISEEliteMNQExtendedContractAwareDatasetProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-V57-DATASET LOADED");
                Task.Run(Run);
            }
        }

        private void Run()
        {
            try
            {
                Print("ISE-V57-DATASET START fromCentral=2025-12-01 toCentral=2026-08-01 window=03:00-11:00 interval=60s selection=prior-day-volume source=Repository");

                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var outputBars = new List<ContractBar>();
                var selections = new List<DaySelection>();
                var acquisitionStart = RequestedFromCentral.AddDays(-WarmupCalendarDays);
                ContractSpec active = null;
                var expectedBars = (int)(ResearchWindowEnd - ResearchWindowStart).TotalMinutes;
                var minimumAcceptedBars = (int)Math.Floor(expectedBars * 0.90m);
                var skippedPartial = 0;
                var requestedDays = 0;

                for (var day = acquisitionStart.Date; day < RequestedToCentral.Date; day = day.AddDays(1))
                {
                    requestedDays++;
                    var options = RequestDay(client, day);
                    if (options.Count == 0)
                        continue;

                    var dominantToday = options
                        .OrderByDescending(x => x.TotalVolume)
                        .ThenByDescending(x => x.ResearchBars.Count)
                        .First();

                    if (active == null)
                    {
                        // Warmup only. The first OUTPUT day therefore inherits a contract selected
                        // from an earlier observed day instead of using its own future volume.
                        active = dominantToday.Spec;
                    }

                    if (day >= RequestedFromCentral.Date)
                    {
                        var selected = options.FirstOrDefault(x => x.Spec.ContractCode == active.ContractCode);
                        var usedFallback = false;

                        if (selected == null || selected.ResearchBars.Count == 0)
                        {
                            selected = dominantToday;
                            usedFallback = true;
                            Print("ISE-V57-DATASET FALLBACK date=" + day.ToString("yyyy-MM-dd")
                                + " requestedContract=" + active.ContractCode
                                + " fallbackContract=" + selected.Spec.ContractCode);
                        }

                        if (selected.ResearchBars.Count < minimumAcceptedBars)
                        {
                            skippedPartial++;
                            Print("ISE-V57-DATASET SKIP-PARTIAL date=" + day.ToString("yyyy-MM-dd")
                                + " contract=" + selected.Spec.ContractCode
                                + " bars=" + selected.ResearchBars.Count
                                + " expected=" + expectedBars);
                        }
                        else
                        {
                            outputBars.AddRange(selected.ResearchBars.Select(x => new ContractBar("MNQ", selected.Spec.ContractCode, x)));
                            selections.Add(new DaySelection(
                                day,
                                selected.Spec.ContractCode,
                                selected.ResearchBars.Count,
                                selected.TotalVolume,
                                dominantToday.Spec.ContractCode,
                                usedFallback));
                        }
                    }

                    // Today's completed volume determines TOMORROW's active contract.
                    active = dominantToday.Spec;

                    if (requestedDays % 20 == 0)
                        Print("ISE-V57-DATASET PROGRESS through=" + day.ToString("yyyy-MM-dd") + " selectedSessions=" + selections.Count);
                }

                if (outputBars.Count == 0)
                    throw new InvalidOperationException("Extended contract-aware dataset selected zero bars.");

                outputBars = outputBars.OrderBy(x => x.Record.TimestampLocal).ToList();
                ValidateUniqueTimestamps(outputBars);

                var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(
                    outputDirectory,
                    "morning-MNQ-contract-aware-20251201-20260731-0300-1100-60s-causal-frontcontract.tsv");

                Write(outputPath, outputBars, ResolveCentralTimeZone());

                var sessions = outputBars.GroupBy(x => x.Record.TimestampLocal.Date).OrderBy(x => x.Key).ToList();
                Print("ISE-V57-DATASET RESULT bars=" + outputBars.Count
                    + " sessions=" + sessions.Count
                    + " firstSession=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastSession=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " minBarsPerSession=" + sessions.Min(x => x.Count())
                    + " maxBarsPerSession=" + sessions.Max(x => x.Count())
                    + " skippedPartial=" + skippedPartial);

                foreach (var group in selections.GroupBy(x => x.Contract).OrderBy(x => x.Key))
                    Print("ISE-V57-DATASET CONTRACT contract=" + group.Key + " sessions=" + group.Count());

                var switches = selections
                    .Zip(selections.Skip(1), (a, b) => new { Previous = a, Current = b })
                    .Where(x => x.Previous.Contract != x.Current.Contract)
                    .ToList();

                foreach (var s in switches)
                    Print("ISE-V57-DATASET SWITCH effectiveDate=" + s.Current.Date.ToString("yyyy-MM-dd")
                        + " from=" + s.Previous.Contract
                        + " to=" + s.Current.Contract);

                Print("ISE-V57-DATASET FILE " + outputPath);
                Print("ISE-V57-DATASET COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-V57-DATASET ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static List<ContractDay> RequestDay(ISEEliteHistoricalBarsRequestClient client, DateTime day)
        {
            var result = new List<ContractDay>();
            var nextDay = day.AddDays(1);

            foreach (var spec in Contracts)
            {
                try
                {
                    var all = client.Request(new NinjaTraderHistoricalBarsRequest(
                        spec.InstrumentName,
                        day,
                        nextDay,
                        IntervalSeconds,
                        NinjaTraderHistoricalLookupPolicy.Repository,
                        TradingHoursTemplate))
                        .Where(x => x.TimestampLocal >= day && x.TimestampLocal < nextDay)
                        .OrderBy(x => x.TimestampLocal)
                        .ToList();

                    if (all.Count == 0)
                        continue;

                    var research = all
                        .Where(x => x.TimestampLocal.TimeOfDay >= ResearchWindowStart &&
                                    x.TimestampLocal.TimeOfDay < ResearchWindowEnd)
                        .ToList();

                    if (research.Count == 0)
                        continue;

                    var volume = all.Aggregate(
                        0m,
                        (sum, x) => sum + Convert.ToDecimal(x.Volume, CultureInfo.InvariantCulture));

                    result.Add(new ContractDay(spec, research, volume));
                }
                catch
                {
                    // Contract may be unavailable outside its repository history range.
                    // Other candidate contracts are still evaluated for the day.
                }
            }

            return result;
        }

        private static void ValidateUniqueTimestamps(IReadOnlyList<ContractBar> bars)
        {
            var duplicate = bars
                .GroupBy(x => x.Record.TimestampLocal)
                .FirstOrDefault(x => x.Count() > 1);

            if (duplicate != null)
                throw new InvalidOperationException("Duplicate extended contract-aware timestamp detected: " + duplicate.Key.ToString("O"));
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

        private sealed class ContractSpec
        {
            public ContractSpec(string instrumentName, string contractCode)
            {
                InstrumentName = instrumentName;
                ContractCode = contractCode;
            }

            public string InstrumentName { get; }
            public string ContractCode { get; }
        }

        private sealed class ContractDay
        {
            public ContractDay(ContractSpec spec, List<NinjaTraderHistoricalBarRecord> researchBars, decimal totalVolume)
            {
                Spec = spec;
                ResearchBars = researchBars;
                TotalVolume = totalVolume;
            }

            public ContractSpec Spec { get; }
            public List<NinjaTraderHistoricalBarRecord> ResearchBars { get; }
            public decimal TotalVolume { get; }
        }

        private sealed class DaySelection
        {
            public DaySelection(DateTime date, string contract, int bars, decimal volume, string dominantToday, bool usedFallback)
            {
                Date = date;
                Contract = contract;
                Bars = bars;
                Volume = volume;
                DominantToday = dominantToday;
                UsedFallback = usedFallback;
            }

            public DateTime Date { get; }
            public string Contract { get; }
            public int Bars { get; }
            public decimal Volume { get; }
            public string DominantToday { get; }
            public bool UsedFallback { get; }
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
