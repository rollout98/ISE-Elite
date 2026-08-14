// Supervised, read-only NinjaTrader 8 contract-aware MNQ 30-second full-session dataset probe.
// Created after V7.9.2 showed material one-minute target/stop sequencing ambiguity in the U.S. morning.
// Local repository is preferred; missing 30-second days are retried through the configured historical provider.
// NinjaTrader documents that Provider lookup updates the repository on provider reply.
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
                Print("ISE-30S-FULLSESSION-DATASET START warmupTradingDays=2026-05-28..2026-05-29 researchTradingDays=2026-06-01..2026-07-31 rolloverTradingDay=2026-06-15 session=17:00-prev-to-15:00 criticalWindow=06:00-11:00 interval=30s source=Repository->ProviderFallback acquisition=trading-day-chunks");

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
                    throw new InvalidOperationException("30-second full-session dataset selected zero bars after Repository and Provider fallback. Verify the active historical-data connection supplies sub-minute/tick history for MNQ.");

                ValidateUniqueTimestamps(combined);

                var sessions = combined.GroupBy(x => x.Record.TradingDay.Date).OrderBy(x => x.Key).ToList();
                var researchSessions = sessions.Where(x => x.Key >= ResearchFromTradingDayCentral.Date).ToList();
                var warmupSessions = sessions.Where(x => x.Key < ResearchFromTradingDayCentral.Date).ToList();

                var expectedCriticalBars = (int)(CriticalWindowEnd - CriticalWindowStart).TotalSeconds / IntervalSeconds;
                var partialCritical = researchSessions.Where(x => CountCriticalBars(x) < expectedCriticalBars).ToList();

                var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, "fullsession-MNQ-contract-aware-warmup-20260528-20260731-1700-1500-30s-repository.tsv");
                Write(outputPath, combined, ResolveCentralTimeZone());

                Print("ISE-30S-FULLSESSION-DATASET RESULT selectedBars=" + combined.Count
                    + " warmupSessions=" + warmupSessions.Count
                    + " researchSessions=" + researchSessions.Count
                    + " firstObservedTradingDay=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastObservedTradingDay=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " minObservedBarsPerSession=" + sessions.Min(x => x.Count())
                    + " maxObservedBarsPerSession=" + sessions.Max(x => x.Count())
                    + " partialCriticalSessions=" + partialCritical.Count
                    + " providerFallbackRequests=" + providerFallbackRequests
                    + " providerFallbackBars=" + providerFallbackBars
                    + " providerMisses=" + providerMisses);

                foreach (var session in sessions)
                {
                    var ordered = session.OrderBy(x => x.Record.TimestampLocal).ToList();
                    var criticalBars = CountCriticalBars(session);
                    var gap = GapStats(ordered);
                    Print("ISE-30S-FULLSESSION-DATASET SESSION tradingDay=" + session.Key.ToString("yyyy-MM-dd")
                        + " role=" + (session.Key < ResearchFromTradingDayCentral.Date ? "Warmup" : "Research")
                        + " contract=" + ordered[0].Contract
                        + " bars=" + ordered.Count
                        + " first=" + ordered[0].Record.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss")
                        + " last=" + ordered[ordered.Count - 1].Record.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss")
                        + " criticalBars=" + criticalBars
                        + " gapsGt30s=" + gap.Count
                        + " maxGapSeconds=" + gap.MaxSeconds.ToString("0", CultureInfo.InvariantCulture));
                }

                foreach (var p in partialCritical)
                    Print("ISE-30S-FULLSESSION-DATASET CRITICAL-PARTIAL tradingDay=" + p.Key.ToString("yyyy-MM-dd") + " criticalBars=" + CountCriticalBars(p));

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
            var records = new List<NinjaTraderHistoricalBarRecord>();

            for (var day = sessionStart.Date; day <= tradingDay.Date; day = day.AddDays(1))
            {
                var nextDay = day.AddDays(1);
                var requestRepository = new NinjaTraderHistoricalBarsRequest(
                    instrument,
                    day,
                    nextDay,
                    IntervalSeconds,
                    NinjaTraderHistoricalLookupPolicy.Repository,
                    TradingHoursTemplate);

                var dayRecords = client.Request(requestRepository).ToList();

                if (dayRecords.Count == 0)
                {
                    providerFallbackRequests++;
                    Print("ISE-30S-FULLSESSION-DATASET REPOSITORY-MISS instrument=" + instrument
                        + " calendarDay=" + day.ToString("yyyy-MM-dd")
                        + " retry=Provider");

                    var requestProvider = new NinjaTraderHistoricalBarsRequest(
                        instrument,
                        day,
                        nextDay,
                        IntervalSeconds,
                        NinjaTraderHistoricalLookupPolicy.Provider,
                        TradingHoursTemplate);

                    dayRecords = client.Request(requestProvider).ToList();
                    providerFallbackBars += dayRecords.Count;

                    if (dayRecords.Count == 0)
                    {
                        providerMisses++;
                        Print("ISE-30S-FULLSESSION-DATASET PROVIDER-MISS instrument=" + instrument
                            + " calendarDay=" + day.ToString("yyyy-MM-dd"));
                    }
                    else
                    {
                        Print("ISE-30S-FULLSESSION-DATASET PROVIDER-BACKFILL instrument=" + instrument
                            + " calendarDay=" + day.ToString("yyyy-MM-dd")
                            + " bars=" + dayRecords.Count);
                    }
                }

                records.AddRange(dayRecords);
            }

            return records
                .Where(x => x.TimestampLocal >= sessionStart && x.TimestampLocal < sessionEnd)
                .Where(x => x.TradingDay.Date == tradingDay.Date)
                .GroupBy(x => x.TimestampLocal)
                .Select(x => x.First())
                .OrderBy(x => x.TimestampLocal)
                .ToList();
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
                        "NinjaTrader BarsRequest Repository/ProviderFallback",
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
