// Read-only NinjaTrader 8 probe for ISE Elite V7.8.2 expanded MNQ validation.
//
// Purpose:
// - acquire causal MNQ 1-minute data from 03:00-11:00 Central;
// - include 2025-11-17 through 2025-11-30 as warmup/context only;
// - provide independent evaluation data from 2025-12-01 through 2026-03-24;
// - preserve explicit contract identity across 12-25 -> 03-26 -> 06-26;
// - select a new front contract only on the NEXT day after the new contract led
//   the completed prior research window in volume.
//
// Historical Research only. No order entry, modification, cancellation, flatten,
// account governance, or live trading behavior.

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
    public sealed class ISEEliteMNQPreCalibrationValidationDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime WarmupFromCentral =
            new DateTime(2025, 11, 17, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly DateTime EvaluationFromCentral =
            new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly DateTime ToCentralExclusive =
            new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly DateTime DecemberOverlapFrom =
            new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly DateTime DecemberOverlapToExclusive =
            new DateTime(2025, 12, 22, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly DateTime MarchOverlapFrom =
            new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly DateTime MarchOverlapToExclusive =
            new DateTime(2026, 3, 23, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly TimeSpan WindowStart =
            new TimeSpan(3, 0, 0);

        private static readonly TimeSpan WindowEnd =
            new TimeSpan(11, 0, 0);

        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate =
            "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description =
                    "Read-only V7.8.2 causal MNQ pre-calibration validation dataset probe.";

                Name =
                    "ISEEliteMNQPreCalibrationValidationDatasetProbe";

                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;

                Print(
                    "ISE-V781-PRECAL LOADED");

                Task.Run(Run);
            }
        }

        private void Run()
        {
            try
            {
                Print(
                    "ISE-V781-PRECAL START warmup=2025-11-17 evaluation=2025-12-01..2026-03-24 window=03:00-11:00 source=Repository acquisition=daily-chunks selection=prior-completed-day-volume-leader");

                var client =
                    new ISEEliteHistoricalBarsRequestClient(
                        TimeSpan.FromSeconds(120));

                var selected =
                    new List<ContractBar>();

                var activeContract =
                    "12-25";

                DailyContractVolume previousDecember =
                    null;

                DailyContractVolume previousMarch =
                    null;

                DateTime? decemberSwitch = null;
                DateTime? marchSwitch = null;

                for (var day = WarmupFromCentral.Date;
                    day < ToCentralExclusive.Date;
                    day = day.AddDays(1))
                {
                    // Causal transition: use only the prior completed day's
                    // volume comparison to determine today's contract.
                    if (activeContract == "12-25"
                        && previousDecember != null
                        && previousDecember.NextVolume
                            > previousDecember.CurrentVolume
                        && previousDecember.NextBars > 0)
                    {
                        activeContract = "03-26";

                        if (!decemberSwitch.HasValue)
                        {
                            decemberSwitch = day;

                            Print(
                                "ISE-V781-PRECAL SWITCH day="
                                + day.ToString("yyyy-MM-dd")
                                + " from=12-25 to=03-26 basedOn="
                                + previousDecember.Date.ToString("yyyy-MM-dd")
                                + " currentVolume="
                                + previousDecember.CurrentVolume
                                + " nextVolume="
                                + previousDecember.NextVolume);
                        }
                    }

                    if (activeContract == "03-26"
                        && previousMarch != null
                        && previousMarch.NextVolume
                            > previousMarch.CurrentVolume
                        && previousMarch.NextBars > 0)
                    {
                        activeContract = "06-26";

                        if (!marchSwitch.HasValue)
                        {
                            marchSwitch = day;

                            Print(
                                "ISE-V781-PRECAL SWITCH day="
                                + day.ToString("yyyy-MM-dd")
                                + " from=03-26 to=06-26 basedOn="
                                + previousMarch.Date.ToString("yyyy-MM-dd")
                                + " currentVolume="
                                + previousMarch.CurrentVolume
                                + " nextVolume="
                                + previousMarch.NextVolume);
                        }
                    }

                    var currentRecords =
                        RequestDay(
                            client,
                            "MNQ " + activeContract,
                            day);

                    selected.AddRange(
                        currentRecords.Select(
                            x => new ContractBar(
                                "MNQ",
                                activeContract,
                                x)));

                    if (day >= DecemberOverlapFrom
                        && day < DecemberOverlapToExclusive
                        && activeContract == "12-25")
                    {
                        var oldBars =
                            currentRecords;

                        var newBars =
                            RequestDay(
                                client,
                                "MNQ 03-26",
                                day);

                        previousDecember =
                            new DailyContractVolume(
                                day,
                                oldBars.Count,
                                oldBars.Sum(x => x.Volume),
                                newBars.Count,
                                newBars.Sum(x => x.Volume));

                        PrintVolumeComparison(
                            "DEC",
                            previousDecember,
                            "12-25",
                            "03-26");
                    }

                    if (day >= MarchOverlapFrom
                        && day < MarchOverlapToExclusive
                        && activeContract == "03-26")
                    {
                        var oldBars =
                            currentRecords;

                        var newBars =
                            RequestDay(
                                client,
                                "MNQ 06-26",
                                day);

                        previousMarch =
                            new DailyContractVolume(
                                day,
                                oldBars.Count,
                                oldBars.Sum(x => x.Volume),
                                newBars.Count,
                                newBars.Sum(x => x.Volume));

                        PrintVolumeComparison(
                            "MAR",
                            previousMarch,
                            "03-26",
                            "06-26");
                    }
                }

                selected =
                    selected
                        .OrderBy(x => x.Record.TimestampLocal)
                        .ToList();

                if (selected.Count == 0)
                {
                    throw new InvalidOperationException(
                        "V7.8.2 pre-calibration acquisition selected zero bars.");
                }

                ValidateUniqueTimestamps(selected);

                var sessions =
                    selected
                        .GroupBy(x => x.Record.TimestampLocal.Date)
                        .OrderBy(x => x.Key)
                        .ToList();

                var expectedBars =
                    (int)(WindowEnd - WindowStart).TotalMinutes;

                var partial =
                    sessions
                        .Where(x => x.Count() < expectedBars)
                        .ToList();

                var outputDirectory =
                    Path.Combine(
                        NinjaTrader.Core.Globals.UserDataDir,
                        "ISEEliteResearch");

                Directory.CreateDirectory(
                    outputDirectory);

                var outputPath =
                    Path.Combine(
                        outputDirectory,
                        "morning-MNQ-contract-aware-warmup-20251117-20260324-0300-1100-60s-causal-frontcontract.tsv");

                Write(
                    outputPath,
                    selected,
                    ResolveCentralTimeZone());

                Print(
                    "ISE-V781-PRECAL RESULT selectedBars="
                    + selected.Count
                    + " sessions="
                    + sessions.Count
                    + " firstSession="
                    + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastSession="
                    + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " minBarsPerSession="
                    + sessions.Min(x => x.Count())
                    + " maxBarsPerSession="
                    + sessions.Max(x => x.Count())
                    + " partialSessions="
                    + partial.Count
                    + " decemberSwitch="
                    + (decemberSwitch.HasValue
                        ? decemberSwitch.Value.ToString("yyyy-MM-dd")
                        : "NONE")
                    + " marchSwitch="
                    + (marchSwitch.HasValue
                        ? marchSwitch.Value.ToString("yyyy-MM-dd")
                        : "NONE"));

                foreach (var p in partial)
                {
                    Print(
                        "ISE-V781-PRECAL PARTIAL date="
                        + p.Key.ToString("yyyy-MM-dd")
                        + " bars="
                        + p.Count());
                }

                Print(
                    "ISE-V781-PRECAL FILE "
                    + outputPath);

                Print(
                    "ISE-V781-PRECAL COMPLETE");
            }
            catch (Exception ex)
            {
                Print(
                    "ISE-V781-PRECAL ERROR "
                    + ex.GetType().Name
                    + ": "
                    + ex.Message);
            }
        }

        private static List<NinjaTraderHistoricalBarRecord> RequestDay(
            ISEEliteHistoricalBarsRequestClient client,
            string instrumentFullName,
            DateTime day)
        {
            var nextDay =
                day.AddDays(1);

            var records =
                client.Request(
                    new NinjaTraderHistoricalBarsRequest(
                        instrumentFullName,
                        day,
                        nextDay,
                        IntervalSeconds,
                        NinjaTraderHistoricalLookupPolicy.Repository,
                        TradingHoursTemplate));

            if (records == null)
            {
                throw new InvalidOperationException(
                    "NinjaTrader historical client returned null for "
                    + instrumentFullName
                    + " "
                    + day.ToString("yyyy-MM-dd"));
            }

            return records
                .Where(x =>
                    x.TimestampLocal >= day
                    && x.TimestampLocal < nextDay)
                .Where(x =>
                    x.TimestampLocal.TimeOfDay >= WindowStart
                    && x.TimestampLocal.TimeOfDay < WindowEnd)
                .OrderBy(x => x.TimestampLocal)
                .ToList();
        }

        private static void PrintVolumeComparison(
            string label,
            DailyContractVolume comparison,
            string currentContract,
            string nextContract)
        {
            Print(
                "ISE-V781-PRECAL VOLUME "
                + label
                + " date="
                + comparison.Date.ToString("yyyy-MM-dd")
                + " current="
                + currentContract
                + " currentBars="
                + comparison.CurrentBars
                + " currentVolume="
                + comparison.CurrentVolume
                + " next="
                + nextContract
                + " nextBars="
                + comparison.NextBars
                + " nextVolume="
                + comparison.NextVolume);
        }

        private static void ValidateUniqueTimestamps(
            IReadOnlyList<ContractBar> bars)
        {
            var duplicate =
                bars
                    .GroupBy(x => x.Record.TimestampLocal)
                    .FirstOrDefault(x => x.Count() > 1);

            if (duplicate != null)
            {
                throw new InvalidOperationException(
                    "Duplicate causal front-contract timestamp detected: "
                    + duplicate.Key.ToString("O"));
            }
        }

        private static void Write(
            string path,
            IReadOnlyList<ContractBar> bars,
            TimeZoneInfo centralTimeZone)
        {
            const string header =
                "instrument\tcontract\ttimestampUtc\ttradingDay\tintervalSeconds\topen\thigh\tlow\tclose\tvolume\tsourceKind\tsourceName\tbid\task";

            using (var writer =
                new StreamWriter(path, false))
            {
                writer.WriteLine(header);

                foreach (var item in bars)
                {
                    var bar =
                        item.Record;

                    var local =
                        DateTime.SpecifyKind(
                            bar.TimestampLocal,
                            DateTimeKind.Unspecified);

                    if (centralTimeZone.IsInvalidTime(local)
                        || centralTimeZone.IsAmbiguousTime(local))
                    {
                        throw new InvalidOperationException(
                            "Historical timestamp requires explicit DST disambiguation: "
                            + local.ToString("O"));
                    }

                    var utc =
                        TimeZoneInfo.ConvertTimeToUtc(
                            local,
                            centralTimeZone);

                    var timestampUtc =
                        new DateTimeOffset(
                            utc,
                            TimeSpan.Zero);

                    writer.WriteLine(
                        string.Join(
                            "\t",
                            new[]
                            {
                                item.Instrument,
                                item.Contract,
                                timestampUtc.ToString(
                                    "O",
                                    CultureInfo.InvariantCulture),
                                bar.TradingDay.ToString(
                                    "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture),
                                IntervalSeconds.ToString(
                                    CultureInfo.InvariantCulture),
                                bar.Open.ToString(
                                    CultureInfo.InvariantCulture),
                                bar.High.ToString(
                                    CultureInfo.InvariantCulture),
                                bar.Low.ToString(
                                    CultureInfo.InvariantCulture),
                                bar.Close.ToString(
                                    CultureInfo.InvariantCulture),
                                bar.Volume.ToString(
                                    CultureInfo.InvariantCulture),
                                "2",
                                "NinjaTrader BarsRequest Repository",
                                bar.Bid.HasValue
                                    ? bar.Bid.Value.ToString(
                                        CultureInfo.InvariantCulture)
                                    : string.Empty,
                                bar.Ask.HasValue
                                    ? bar.Ask.Value.ToString(
                                        CultureInfo.InvariantCulture)
                                    : string.Empty
                            }));
                }
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    "Central Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById(
                    "America/Chicago");
            }
        }

        private sealed class ContractBar
        {
            public ContractBar(
                string instrument,
                string contract,
                NinjaTraderHistoricalBarRecord record)
            {
                Instrument = instrument;
                Contract = contract;
                Record = record;
            }

            public string Instrument { get; }
            public string Contract { get; }
            public NinjaTraderHistoricalBarRecord Record { get; }
        }

        private sealed class DailyContractVolume
        {
            public DailyContractVolume(
                DateTime date,
                int currentBars,
                long currentVolume,
                int nextBars,
                long nextVolume)
            {
                Date = date;
                CurrentBars = currentBars;
                CurrentVolume = currentVolume;
                NextBars = nextBars;
                NextVolume = nextVolume;
            }

            public DateTime Date { get; }
            public int CurrentBars { get; }
            public long CurrentVolume { get; }
            public int NextBars { get; }
            public long NextVolume { get; }
        }
    }
}
