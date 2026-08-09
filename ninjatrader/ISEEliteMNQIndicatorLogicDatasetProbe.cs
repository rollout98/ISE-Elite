// Supervised, read-only NinjaTrader 8 contract-aware MNQ full-session dataset probe.
// Supplies pre-research warmup for the user's 3-minute Range Filter entry logic and 5-minute VectorFlow hold logic,
// then diagnoses the 03:00-11:00 Central research window separately from non-critical full-session gaps.
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
    public sealed class ISEEliteMNQIndicatorLogicDatasetProbe : Indicator
    {
        private bool started;

        private static readonly DateTime WarmupFromTradingDayCentral = new DateTime(2026, 5, 28, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime ResearchFromTradingDayCentral = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RequestedToTradingDayCentral = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime RolloverBoundaryTradingDayCentral = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
        private static readonly TimeSpan SessionStartPreviousDay = new TimeSpan(17, 0, 0);
        private static readonly TimeSpan SessionEndTradingDay = new TimeSpan(16, 0, 0);
        private static readonly TimeSpan ResearchWindowStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan ResearchWindowEnd = new TimeSpan(11, 0, 0);
        private const int IntervalSeconds = 60;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only contract-aware MNQ full ETH-session dataset with warmup for Range Filter and VectorFlow research.";
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
                Print("ISE-INDICATOR-DATASET START warmupTradingDays=2026-05-28..2026-05-29 researchTradingDays=2026-06-01..2026-07-31 rolloverTradingDay=2026-06-15 session=17:00-prev-to-16:00 researchWindow=03:00-11:00 interval=60s source=Repository acquisition=trading-day-chunks");
                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var combined = new List<ContractBar>();

                for (var tradingDay = WarmupFromTradingDayCentral.Date; tradingDay < RequestedToTradingDayCentral.Date; tradingDay = tradingDay.AddDays(1))
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
                var researchSessions = sessions.Where(x => x.Key >= ResearchFromTradingDayCentral.Date).ToList();
                var warmupSessions = sessions.Where(x => x.Key < ResearchFromTradingDayCentral.Date).ToList();
                var researchStartAt = ResearchFromTradingDayCentral.Date.Add(ResearchWindowStart);
                var preResearchWarmupBars = combined.Count(x => x.Record.TimestampLocal < researchStartAt);
                var expectedMorningBars = (int)(ResearchWindowEnd - ResearchWindowStart).TotalMinutes;
                var partialMorning = researchSessions.Where(x => CountMorningBars(x) < expectedMorningBars).ToList();

                var outputDirectory = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, "indicatorlogic-MNQ-contract-aware-warmup-20260528-20260731-1700-1600-60s-repository.tsv");
                Write(outputPath, combined, ResolveCentralTimeZone());

                Print("ISE-INDICATOR-DATASET RESULT selectedBars=" + combined.Count
                    + " warmupSessions=" + warmupSessions.Count
                    + " researchSessions=" + researchSessions.Count
                    + " firstObservedTradingDay=" + sessions[0].Key.ToString("yyyy-MM-dd")
                    + " lastObservedTradingDay=" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd")
                    + " preResearchWarmupBars=" + preResearchWarmupBars
                    + " minObservedBarsPerSession=" + sessions.Min(x => x.Count())
                    + " maxObservedBarsPerSession=" + sessions.Max(x => x.Count())
                    + " partialMorningSessions=" + partialMorning.Count);

                foreach (var session in sessions)
                {
                    var ordered = session.OrderBy(x => x.Record.TimestampLocal).ToList();
                    var morningBars = CountMorningBars(session);
                    var gap = GapStats(ordered);
                    Print("ISE-INDICATOR-DATASET SESSION tradingDay=" + session.Key.ToString("yyyy-MM-dd")
                        + " role=" + (session.Key < ResearchFromTradingDayCentral.Date ? "Warmup" : "Research")
                        + " contract=" + ordered[0].Contract
                        + " bars=" + ordered.Count
                        + " first=" + ordered[0].Record.TimestampLocal.ToString("yyyy-MM-dd HH:mm")
                        + " last=" + ordered[ordered.Count - 1].Record.TimestampLocal.ToString("yyyy-MM-dd HH:mm")
                        + " morningBars=" + morningBars
                        + " gapsGt1m=" + gap.Count
                        + " maxGapMinutes=" + gap.MaxMinutes.ToString("0.0", CultureInfo.InvariantCulture));
                }

                foreach (var p in partialMorning)
                    Print("ISE-INDICATOR-DATASET MORNING-PARTIAL tradingDay=" + p.Key.ToString("yyyy-MM-dd") + " morningBars=" + CountMorningBars(p));

                if (preResearchWarmupBars < 1200)
                    Print("ISE-INDICATOR-DATASET WARNING preResearchWarmupBars below 1200; Range Filter/VectorFlow initialization may be under-warmed.");

                Print("ISE-INDICATOR-DATASET FILE " + outputPath);
                Print("ISE-INDICATOR-DATASET COMPLETE");
            }
            catch (Exception ex)
            {
                Print("ISE-INDICATOR-DATASET ERROR " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static int CountMorningBars(IEnumerable<ContractBar> session)
        {
            return session.Count(x => x.Record.TimestampLocal.TimeOfDay >= ResearchWindowStart
                && x.Record.TimestampLocal.TimeOfDay < ResearchWindowEnd);
        }

        private static GapSummary GapStats(IReadOnlyList<ContractBar> ordered)
        {
            var count = 0;
            var max = 0d;
            for (var i = 1; i < ordered.Count; i++)
            {
                var minutes = (ordered[i].Record.TimestampLocal - ordered[i - 1].Record.TimestampLocal).TotalMinutes;
                if (minutes > 1.5d)
                {
                    count++;
                    if (minutes > max) max = minutes;
                }
            }
            return new GapSummary(count, max);
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

        private sealed class GapSummary
        {
            public GapSummary(int count, double maxMinutes) { Count = count; MaxMinutes = maxMinutes; }
            public int Count { get; }
            public double MaxMinutes { get; }
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
