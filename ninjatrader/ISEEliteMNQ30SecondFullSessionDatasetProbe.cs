// Supervised, read-only NinjaTrader 8 contract-aware MNQ 30-second full-session dataset probe.
// Created after V7.9.2 showed material one-minute target/stop sequencing ambiguity in the U.S. morning.
// Local repository is preferred; missing or partial 30-second sessions are repaired through the configured historical provider.
// No orders are submitted, changed, cancelled, or flattened by this probe.

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
    public sealed class ISEEliteMNQ30SecondFullSessionDatasetProbe : Indicator
    {
        private bool started;
        private int providerFallbackRequests;
        private int providerFallbackBars;
        private int providerMisses;
        private int providerRepairSessions;
        private int providerRepairRequests;
        private int providerRepairBars;

        private static readonly DateTime WarmupFromTradingDayCentral = new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime ResearchFromTradingDayCentral = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RequestedToTradingDayCentral = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RolloverBoundaryTradingDayCentral = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan SessionStartPreviousDay = new TimeSpan(17, 0, 0);
        private static readonly TimeSpan SessionEndTradingDay = new TimeSpan(15, 0, 0);
        private static readonly TimeSpan CriticalWindowStart = new TimeSpan(6, 0, 0);
        private static readonly TimeSpan CriticalWindowEnd = new TimeSpan(11, 0, 0);
        private const int IntervalSeconds = 30;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only contract-aware MNQ 30-second full-session dataset for V7.9 execution-resolution research.";
                Name = "ISEEliteMNQ30SecondFullSessionDatasetProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded && !started)
            {
                started = true;
                Print("ISE-30S-FULLSESSION-DATASET LOADED");
                Task.Run(Run);
            }
        }

        private void Run()
        {
            try
            {
                Print("ISE-30S-FULLSESSION-DATASET START warmupTradingDays=2026-05-28..2026-05-29 researchTradingDays=2026-06-01..2026-07-31 rolloverTradingDay=2026-06-15 session=17:00-prev-to-15:00 criticalWindow=06:00-11:00 interval=30s source=Repository->ProviderFallback+PartialRepair acquisition=trading-day-chunks");

                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(180));
                var combined = new List<ContractBar>();

                for (var tradingDay = WarmupFromTradingDayCentral.Date; tradingDay < RequestedToTradingDayCentral.Date; tradingDay = tradingDay.AddDays(1))
                {
                    var contract = tradingDay < RolloverBoundaryTradingDayCentral ? "06-26" : "09-26";
                    var instrument = "MNQ " + contract;
                    var selected = RequestTradingSession(client, instrument, tradingDay);
                    combined.AddRange(selected.Select(x => new ContractBar("MNQ", contract, x)));
                }

                combined = combined.OrderBy(x => x.Record.TimestampLocal).ToList();
                if (combined.Count == 0)
                    throw new InvalidOperationException("30-second full-session dataset selected zero bars after Repository and Provider repair. Verify the active historical-data connection supplies sub-minute/tick history for MNQ.");

                ValidateUniqueTimestamps(combined);

                var sessions = combined.GroupBy(x => x.Record.TradingDay.Date).OrderBy(x => x.Key).ToList();
                var researchSessions = sessions.Where(x => x.Key >= ResearchFromTradingDayCentral.Date).ToList();
                var warmupSessions = sessions.Where(x => x.Key < ResearchFromTradingDayCentral.Date).ToList();

                var expectedCriticalBars = ExpectedCriticalBars();
                var expectedFullBars = ExpectedFullSessionBars();
                var partialCritical = researchSessions.Where(x => CountCriticalBars(x) < expectedCriticalBars).ToList();
                var partialFull = researchSessions.Where(x => x.Count() < expectedFullBars).ToList();

                var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, "fullsession-MNQ-contract-aware-warmup-20260528-20260731-1700-1500-30s-repository.tsv");
                Write(outputPath, combined, ResolveCentralTimeZone());

                Print("ISE-30S-FULLSESSION-DATASET RESULT selectedBars=" + combined.Count
                    + " warmupSessions=" + warmupSessions.Count
                    + " researchSessions=" + researchSessions.Count
                    + " firstObservedTradingDay=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastObservedTradingDay=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " expectedFullBars=" + expectedFullBars
                    + " expectedCriticalBars=" + expectedCriticalBars
                    + " minObservedBarsPerSession=" + sessions.Min(x => x.Count())
                    + " maxObservedBarsPerSession=" + sessions.Max(x => x.Count())
                    + " partialFullSessions=" + partialFull.Count
                    + " partialCriticalSessions=" + partialCritical.Count
                    + " providerFallbackRequests=" + providerFallbackRequests
                    + " providerFallbackBars=" + providerFallbackBars
                    + " providerMisses=" + providerMisses
                    + " providerRepairSessions=" + providerRepairSessions
                    + " providerRepairRequests=" + providerRepairRequests
                    + " providerRepairBars=" + providerRepairBars);

                foreach (var session in sessions)
                {
                    var ordered = session.OrderBy(x => x.Record.TimestampLocal).ToList();
                    var criticalBars = CountCriticalBars(session);
                    var gap = GapStats(ordered);
                    Print("ISE-30S-FULLSESSION-DATASET SESSION tradingDay=" + session.Key.ToString("yyyy-MM-dd")
                        + " role=" + (session.Key < ResearchFromTradingDayCentral.Date ? "Warmup" : "Research")
                        + " contract=" + ordered[0].Contract
                        + " bars=" + ordered.Count
                        + " fullComplete=" + (ordered.Count >= expectedFullBars ? "yes" : "no")
                        + " first=" + ordered[0].Record.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss")
                        + " last=" + ordered[ordered.Count - 1].Record.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss")
                        + " criticalBars=" + criticalBars
                        + " criticalComplete=" + (criticalBars >= expectedCriticalBars ? "yes" : "no")
                        + " gapsGt30s=" + gap.Count
                        + " maxGapSeconds=" + gap.MaxSeconds.ToString("0", CultureInfo.InvariantCulture));
                }

                foreach (var p in partialCritical)
                    Print("ISE-30S-FULLSESSION-DATASET CRITICAL-PARTIAL tradingDay=" + p.Key.ToString("yyyy-MM-dd") + " criticalBars=" + CountCriticalBars(p));

                foreach (var p in partialFull)
                    Print("ISE-30S-FULLSESSION-DATASET FULL-PARTIAL tradingDay=" + p.Key.ToString("yyyy-MM-dd") + " bars=" + p.Count());

                Print("ISE-30S-FULLSESSION-DATASET FILE " + outputPath);
                Print("ISE-30S-FULLSESSION-DATASET COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-30S-FULLSESSION-DATASET ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private List<NinjaTraderHistoricalBarRecord> RequestTradingSession(
            ISEEliteHistoricalBarsRequestClient client,
            string instrument,
            DateTime tradingDay)
        {
            var sessionStart = tradingDay.Date.AddDays(-1).Add(SessionStartPreviousDay);
            var sessionEnd = tradingDay.Date.Add(SessionEndTradingDay);
            var raw = new List<NinjaTraderHistoricalBarRecord>();

            // Pass 1: local repository first. Only zero-result calendar chunks fall back immediately.
            for (var day = sessionStart.Date; day <= tradingDay.Date; day = day.AddDays(1))
            {
                raw.AddRange(RequestCalendarDayRepositoryFirst(client, instrument, day));
            }

            var selected = SelectTradingSession(raw, tradingDay, sessionStart, sessionEnd);
            var criticalCount = CountCriticalRecords(selected);
            var expectedCriticalBars = ExpectedCriticalBars();
            var expectedFullBars = ExpectedFullSessionBars();

            // Pass 2: a non-empty repository response can still be partial. If the assembled
            // session is missing full-session or 06:00-11:00 coverage, explicitly request BOTH
            // calendar chunks from Provider, merge them with local data, and reselect/deduplicate.
            if (selected.Count < expectedFullBars || criticalCount < expectedCriticalBars)
            {
                providerRepairSessions++;
                Print("ISE-30S-FULLSESSION-DATASET SESSION-PARTIAL instrument=" + instrument
                    + " tradingDay=" + tradingDay.ToString("yyyy-MM-dd")
                    + " bars=" + selected.Count + "/" + expectedFullBars
                    + " criticalBars=" + criticalCount + "/" + expectedCriticalBars
                    + " action=ProviderRepair");

                for (var day = sessionStart.Date; day <= tradingDay.Date; day = day.AddDays(1))
                {
                    var nextDay = day.AddDays(1);
                    providerRepairRequests++;
                    var provider = client.Request(new NinjaTraderHistoricalBarsRequest(
                        instrument,
                        day,
                        nextDay,
                        IntervalSeconds,
                        NinjaTraderHistoricalLookupPolicy.Provider,
                        TradingHoursTemplate)).ToList();

                    providerRepairBars += provider.Count;
                    raw.AddRange(provider);

                    Print("ISE-30S-FULLSESSION-DATASET PROVIDER-REPAIR instrument=" + instrument
                        + " tradingDay=" + tradingDay.ToString("yyyy-MM-dd")
                        + " calendarDay=" + day.ToString("yyyy-MM-dd")
                        + " returnedBars=" + provider.Count);
                }

                selected = SelectTradingSession(raw, tradingDay, sessionStart, sessionEnd);
                criticalCount = CountCriticalRecords(selected);

                Print("ISE-30S-FULLSESSION-DATASET SESSION-REPAIR-RESULT instrument=" + instrument
                    + " tradingDay=" + tradingDay.ToString("yyyy-MM-dd")
                    + " bars=" + selected.Count + "/" + expectedFullBars
                    + " criticalBars=" + criticalCount + "/" + expectedCriticalBars
                    + " status=" + (selected.Count >= expectedFullBars && criticalCount >= expectedCriticalBars ? "COMPLETE" : "PARTIAL"));
            }

            return selected;
        }

        private List<NinjaTraderHistoricalBarRecord> RequestCalendarDayRepositoryFirst(
            ISEEliteHistoricalBarsRequestClient client,
            string instrument,
            DateTime day)
        {
            var nextDay = day.AddDays(1);
            var repository = client.Request(new NinjaTraderHistoricalBarsRequest(
                instrument,
                day,
                nextDay,
                IntervalSeconds,
                NinjaTraderHistoricalLookupPolicy.Repository,
                TradingHoursTemplate)).ToList();

            if (repository.Count > 0)
                return repository;

            providerFallbackRequests++;
            Print("ISE-30S-FULLSESSION-DATASET REPOSITORY-MISS instrument=" + instrument
                + " calendarDay=" + day.ToString("yyyy-MM-dd")
                + " retry=Provider");

            var provider = client.Request(new NinjaTraderHistoricalBarsRequest(
                instrument,
                day,
                nextDay,
                IntervalSeconds,
                NinjaTraderHistoricalLookupPolicy.Provider,
                TradingHoursTemplate)).ToList();

            providerFallbackBars += provider.Count;

            if (provider.Count == 0)
            {
                providerMisses++;
                Print("ISE-30S-FULLSESSION-DATASET PROVIDER-MISS instrument=" + instrument
                    + " calendarDay=" + day.ToString("yyyy-MM-dd"));
            }
            else
            {
                Print("ISE-30S-FULLSESSION-DATASET PROVIDER-BACKFILL instrument=" + instrument
                    + " calendarDay=" + day.ToString("yyyy-MM-dd")
                    + " bars=" + provider.Count);
            }

            return provider;
        }

        private static List<NinjaTraderHistoricalBarRecord> SelectTradingSession(
            IEnumerable<NinjaTraderHistoricalBarRecord> records,
            DateTime tradingDay,
            DateTime sessionStart,
            DateTime sessionEnd)
        {
            return records
                .Where(x => x.TimestampLocal >= sessionStart && x.TimestampLocal < sessionEnd)
                .Where(x => x.TradingDay.Date == tradingDay.Date)
                .GroupBy(x => x.TimestampLocal)
                .Select(x => x.First())
                .OrderBy(x => x.TimestampLocal)
                .ToList();
        }

        private static int ExpectedCriticalBars()
        {
            return (int)(CriticalWindowEnd - CriticalWindowStart).TotalSeconds / IntervalSeconds;
        }

        private static int ExpectedFullSessionBars()
        {
            // NinjaTrader second bars are timestamped at bar close in this dataset. Healthy
            // 17:00-prev -> 15:00 sessions therefore contain 2,639 timestamps: 17:00:30..14:59:30.
            var duration = TimeSpan.FromDays(1) - SessionStartPreviousDay + SessionEndTradingDay;
            return (int)(duration.TotalSeconds / IntervalSeconds) - 1;
        }

        private static int CountCriticalRecords(IEnumerable<NinjaTraderHistoricalBarRecord> session)
        {
            return session.Count(x => x.TimestampLocal.TimeOfDay >= CriticalWindowStart
                && x.TimestampLocal.TimeOfDay < CriticalWindowEnd);
        }

        private static int CountCriticalBars(IEnumerable<ContractBar> session)
        {
            return session.Count(x => x.Record.TimestampLocal.TimeOfDay >= CriticalWindowStart
                && x.Record.TimestampLocal.TimeOfDay < CriticalWindowEnd);
        }

        private static GapSummary GapStats(IReadOnlyList<ContractBar> ordered)
        {
            var count = 0;
            var max = 0d;
            for (var i = 1; i < ordered.Count; i++)
            {
                var seconds = (ordered[i].Record.TimestampLocal - ordered[i - 1].Record.TimestampLocal).TotalSeconds;
                if (seconds > 45d)
                {
                    count++;
                    if (seconds > max) max = seconds;
                }
            }
            return new GapSummary(count, max);
        }

        private static void ValidateUniqueTimestamps(IReadOnlyList<ContractBar> bars)
        {
            var duplicate = bars.GroupBy(x => x.TimestampKey).FirstOrDefault(x => x.Count() > 1);
            if (duplicate != null)
                throw new InvalidOperationException("Duplicate contract-aware timestamp detected: " + duplicate.Key);
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
                        "NinjaTrader BarsRequest Repository/ProviderFallback+PartialRepair",
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

        private sealed class GapSummary
        {
            public GapSummary(int count, double maxSeconds)
            {
                Count = count;
                MaxSeconds = maxSeconds;
            }

            public int Count { get; }
            public double MaxSeconds { get; }
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
            public string TimestampKey => Contract + "|" + Record.TimestampLocal.ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
