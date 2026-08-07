using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class NewYorkObjectivePathOutcome
    {
        public NewYorkObjectivePathOutcome(DateTime sessionDateCentral, NewYorkTradeableEntryType entryType,
            DateTimeOffset entryUtc, DateTimeOffset? first300Utc, DateTimeOffset? first500Utc,
            DateTimeOffset? first1000Utc, DateTimeOffset? stopUtc, DateTimeOffset? endUtc)
        {
            SessionDateCentral = sessionDateCentral.Date;
            EntryType = entryType;
            EntryUtc = entryUtc;
            First300Utc = first300Utc;
            First500Utc = first500Utc;
            First1000Utc = first1000Utc;
            StopUtc = stopUtc;
            EndUtc = endUtc;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkTradeableEntryType EntryType { get; }
        public DateTimeOffset EntryUtc { get; }
        public DateTimeOffset? First300Utc { get; }
        public DateTimeOffset? First500Utc { get; }
        public DateTimeOffset? First1000Utc { get; }
        public DateTimeOffset? StopUtc { get; }
        public DateTimeOffset? EndUtc { get; }
        public bool Hit300BeforeStop => First300Utc.HasValue;
        public bool Hit500BeforeStop => First500Utc.HasValue;
        public bool Hit1000BeforeStop => First1000Utc.HasValue;
        public bool StopOccurred => StopUtc.HasValue;
    }

    /// <summary>
    /// Records the complete post-entry path for accepted risk-qualified entries. Unlike the first-event
    /// sequence marker, this continues after $300 so the study can determine whether $500 and $1000 are
    /// subsequently reached before the structural stop. If stop and any target are touched on the same
    /// one-minute bar, the stop wins and targets first touched on that bar are not credited.
    /// </summary>
    public sealed class NewYorkObjectivePathAnalyzer
    {
        private static readonly TimeSpan OutcomeEnd = new TimeSpan(9, 30, 0);
        private readonly NewYorkRiskQualifiedEntryConfig config;

        public NewYorkObjectivePathAnalyzer(NewYorkRiskQualifiedEntryConfig? config = null)
        {
            this.config = config ?? new NewYorkRiskQualifiedEntryConfig();
        }

        public IReadOnlyList<NewYorkObjectivePathOutcome> Analyze(IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<NewYorkRiskQualifiedEntryOutcome> entries)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            if (bars.Count == 0 || entries.Count == 0) return Array.Empty<NewYorkObjectivePathOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars.Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .OrderBy(x => x.Local).ToList();
            var byDate = localized.GroupBy(x => x.Local.Date).ToDictionary(x => x.Key, x => x.ToList());
            var result = new List<NewYorkObjectivePathOutcome>();

            foreach (var entry in entries.Where(x => x.HasAcceptedEntry).OrderBy(x => x.SessionDateCentral))
            {
                if (!byDate.TryGetValue(entry.SessionDateCentral.Date, out var session) || !entry.EntryUtc.HasValue) continue;
                result.Add(AnalyzeEntry(session, entry));
            }
            return result;
        }

        private NewYorkObjectivePathOutcome AnalyzeEntry(IReadOnlyList<LocalBar> session, NewYorkRiskQualifiedEntryOutcome entry)
        {
            var path = session.Where(x => x.Bar.TimestampUtc >= entry.EntryUtc!.Value && x.Local.TimeOfDay < OutcomeEnd)
                .OrderBy(x => x.Local).ToList();
            var points300 = config.IntermediateObjective / (config.PointValuePerContract * config.Contracts);
            var points500 = config.LowerObjective / (config.PointValuePerContract * config.Contracts);
            var points1000 = config.UpperObjective / (config.PointValuePerContract * config.Contracts);
            DateTimeOffset? hit300 = null;
            DateTimeOffset? hit500 = null;
            DateTimeOffset? hit1000 = null;
            DateTimeOffset? stop = null;

            foreach (var bar in path)
            {
                var stopHit = entry.Direction == NewYorkResearchDirection.Long
                    ? bar.Bar.Low <= entry.StopPrice
                    : bar.Bar.High >= entry.StopPrice;
                if (stopHit)
                {
                    stop = bar.Bar.TimestampUtc;
                    break;
                }

                var favorable = entry.Direction == NewYorkResearchDirection.Long
                    ? bar.Bar.High - entry.EntryPrice
                    : entry.EntryPrice - bar.Bar.Low;
                if (!hit300.HasValue && favorable >= points300) hit300 = bar.Bar.TimestampUtc;
                if (!hit500.HasValue && favorable >= points500) hit500 = bar.Bar.TimestampUtc;
                if (!hit1000.HasValue && favorable >= points1000) hit1000 = bar.Bar.TimestampUtc;
            }

            return new NewYorkObjectivePathOutcome(entry.SessionDateCentral, entry.EntryType, entry.EntryUtc.Value,
                hit300, hit500, hit1000, stop, path.Count == 0 ? (DateTimeOffset?)null : path[path.Count - 1].Bar.TimestampUtc);
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }

        private sealed class LocalBar
        {
            public LocalBar(HistoricalBar bar, DateTime local) { Bar = bar; Local = local; }
            public HistoricalBar Bar { get; }
            public DateTime Local { get; }
        }
    }
}
