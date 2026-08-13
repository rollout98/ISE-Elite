// Read-only NinjaTrader 8 data probe for ISE Elite V7.8.7.
// Starts with 2026-08-10 as warmup/context.
// Each run extends through the current completed Central trading date.
// Frozen validation begins after 2026-08-10.

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
    public sealed class ISEEliteMNQContinuousForwardValidationDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime FromCentral =
            new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Unspecified);

        private static readonly TimeSpan WindowStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan WindowEnd = new TimeSpan(11, 0, 0);

        private const int IntervalSeconds = 60;
        private const int MinimumAcceptedBars = 468;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only V7.8.7 continuous MNQ forward-validation dataset probe.";
                Name = "ISEEliteMNQContinuousForwardValidationDatasetProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-V787-FWD LOADED");
                Task.Run(Run);
            }
        }

        private void Run()
        {
            try
            {
                var central = ResolveCentralTimeZone();
                var nowCentral = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, central);
                var toExclusive = nowCentral.Date.AddDays(1);

                Print("ISE-V787-FWD START from=2026-08-10 through="
                    + nowCentral.Date.ToString("yyyy-MM-dd")
                    + " instrument=MNQ-09-26 window=03:00-11:00");

                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var selected = new List<SelectedBar>();

                for (var day = FromCentral.Date; day < toExclusive.Date; day = day.AddDays(1))
                {
                    var best = RequestBestDay(client, day);

                    if (best.Records.Count >= MinimumAcceptedBars)
                    {
                        selected.AddRange(best.Records.Select(x => new SelectedBar(best.Policy, x)));
                        Print("ISE-V787-FWD DAY date=" + day.ToString("yyyy-MM-dd")
                            + " bars=" + best.Records.Count
                            + " source=" + best.Policy);
                    }
                    else if (best.Records.Count > 0)
                    {
                        Print("ISE-V787-FWD SKIP-PARTIAL date=" + day.ToString("yyyy-MM-dd")
                            + " bars=" + best.Records.Count
                            + " source=" + best.Policy);
                    }
                }

                selected = selected.OrderBy(x => x.Record.TimestampLocal).ToList();

                if (selected.Count == 0)
                    throw new InvalidOperationException("V7.8.7 selected zero bars.");

                var sessions = selected
                    .GroupBy(x => x.Record.TimestampLocal.Date)
                    .OrderBy(x => x.Key)
                    .ToList();

                var outputDirectory = Path.Combine(
                    NinjaTrader.Core.Globals.UserDataDir,
                    "ISEEliteResearch");

                Directory.CreateDirectory(outputDirectory);

                var outputPath = Path.Combine(
                    outputDirectory,
                    "morning-MNQ-09-26-continuous-forward-20260810-current-0300-1100-60s.tsv");

                Write(outputPath, selected, central);

                Print("ISE-V787-FWD RESULT bars=" + selected.Count
                    + " sessions=" + sessions.Count
                    + " first=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " last=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " minBars=" + sessions.Min(x => x.Count())
                    + " maxBars=" + sessions.Max(x => x.Count()));

                Print("ISE-V787-FWD FILE " + outputPath);
                Print("ISE-V787-FWD COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-V787-FWD ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private DayBars RequestBestDay(
            ISEEliteHistoricalBarsRequestClient client,
            DateTime day)
        {
            var repo = RequestDay(client, day, NinjaTraderHistoricalLookupPolicy.Repository);

            if (repo.Count >= MinimumAcceptedBars)
                return new DayBars(repo, NinjaTraderHistoricalLookupPolicy.Repository);

            var provider = RequestDay(client, day, NinjaTraderHistoricalLookupPolicy.Provider);

            if (provider.Count > repo.Count)
            {
                Print("ISE-V787-FWD SOURCE-FALLBACK date=" + day.ToString("yyyy-MM-dd")
                    + " repositoryBars=" + repo.Count
                    + " providerBars=" + provider.Count);

                return new DayBars(provider, NinjaTraderHistoricalLookupPolicy.Provider);
            }

            return new DayBars(repo, NinjaTraderHistoricalLookupPolicy.Repository);
        }

        private static List<NinjaTraderHistoricalBarRecord> RequestDay(
            ISEEliteHistoricalBarsRequestClient client,
            DateTime day,
            NinjaTraderHistoricalLookupPolicy policy)
        {
            var records = client.Request(
                new NinjaTraderHistoricalBarsRequest(
                    "MNQ 09-26",
                    day,
                    day.AddDays(1),
                    IntervalSeconds,
                    policy,
                    TradingHoursTemplate));

            return records
                .Where(x => x.TimestampLocal >= day && x.TimestampLocal < day.AddDays(1))
                .Where(x => x.TimestampLocal.TimeOfDay >= WindowStart && x.TimestampLocal.TimeOfDay < WindowEnd)
                .OrderBy(x => x.TimestampLocal)
                .ToList();
        }

        private static void Write(
            string path,
            IReadOnlyList<SelectedBar> bars,
            TimeZoneInfo centralTimeZone)
        {
            const string header =
                "instrument\tcontract\ttimestampUtc\ttradingDay\tintervalSeconds\topen\thigh\tlow\tclose\tvolume\tsourceKind\tsourceName\tbid\task";

            using (var writer = new StreamWriter(path, false))
            {
                writer.WriteLine(header);

                foreach (var item in bars)
                {
                    var bar = item.Record;
                    var local = DateTime.SpecifyKind(bar.TimestampLocal, DateTimeKind.Unspecified);

                    if (centralTimeZone.IsInvalidTime(local) || centralTimeZone.IsAmbiguousTime(local))
                        throw new InvalidOperationException("DST ambiguity: " + local.ToString("O"));

                    var utc = TimeZoneInfo.ConvertTimeToUtc(local, centralTimeZone);
                    var timestampUtc = new DateTimeOffset(utc, TimeSpan.Zero);

                    writer.WriteLine(string.Join("\t", new[]
                    {
                        "MNQ",
                        "09-26",
                        timestampUtc.ToString("O", CultureInfo.InvariantCulture),
                        bar.TradingDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        IntervalSeconds.ToString(CultureInfo.InvariantCulture),
                        bar.Open.ToString(CultureInfo.InvariantCulture),
                        bar.High.ToString(CultureInfo.InvariantCulture),
                        bar.Low.ToString(CultureInfo.InvariantCulture),
                        bar.Close.ToString(CultureInfo.InvariantCulture),
                        bar.Volume.ToString(CultureInfo.InvariantCulture),
                        ((int)item.Policy).ToString(CultureInfo.InvariantCulture),
                        "NinjaTrader BarsRequest " + item.Policy,
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

        private sealed class DayBars
        {
            public DayBars(List<NinjaTraderHistoricalBarRecord> records, NinjaTraderHistoricalLookupPolicy policy)
            {
                Records = records;
                Policy = policy;
            }

            public List<NinjaTraderHistoricalBarRecord> Records { get; }
            public NinjaTraderHistoricalLookupPolicy Policy { get; }
        }

        private sealed class SelectedBar
        {
            public SelectedBar(NinjaTraderHistoricalLookupPolicy policy, NinjaTraderHistoricalBarRecord record)
            {
                Policy = policy;
                Record = record;
            }

            public NinjaTraderHistoricalLookupPolicy Policy { get; }
            public NinjaTraderHistoricalBarRecord Record { get; }
        }
    }
}
