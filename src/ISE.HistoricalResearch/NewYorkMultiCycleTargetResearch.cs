using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class NewYorkMultiCycleTargetConfig
    {
        public NewYorkMultiCycleTargetConfig(
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2.00m,
            int contracts = 2,
            decimal lowerDailyObjective = 500m,
            decimal upperDailyObjective = 1000m)
        {
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (pointValuePerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(pointValuePerContract));
            if (contracts <= 0) throw new ArgumentOutOfRangeException(nameof(contracts));
            if (lowerDailyObjective <= 0m) throw new ArgumentOutOfRangeException(nameof(lowerDailyObjective));
            if (upperDailyObjective < lowerDailyObjective) throw new ArgumentOutOfRangeException(nameof(upperDailyObjective));

            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
            LowerDailyObjective = lowerDailyObjective;
            UpperDailyObjective = upperDailyObjective;
        }

        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal LowerDailyObjective { get; }
        public decimal UpperDailyObjective { get; }
    }

    public sealed class NewYorkResearchCycleWindow
    {
        public NewYorkResearchCycleWindow(int cycleNumber, string name, TimeSpan startCentral, TimeSpan endCentral)
        {
            if (cycleNumber <= 0) throw new ArgumentOutOfRangeException(nameof(cycleNumber));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Cycle name is required.", nameof(name));
            if (endCentral <= startCentral) throw new ArgumentException("Cycle end must be after start.");
            CycleNumber = cycleNumber;
            Name = name.Trim();
            StartCentral = startCentral;
            EndCentral = endCentral;
        }

        public int CycleNumber { get; }
        public string Name { get; }
        public TimeSpan StartCentral { get; }
        public TimeSpan EndCentral { get; }
    }

    public sealed class NewYorkCycleOpportunityEnvelope
    {
        public NewYorkCycleOpportunityEnvelope(
            DateTime sessionDateCentral,
            NewYorkResearchCycleWindow window,
            NewYorkResearchDirection direction,
            DateTimeOffset entryTimestampUtc,
            DateTimeOffset exitTimestampUtc,
            decimal entryPrice,
            decimal exitPrice,
            decimal favorablePoints,
            decimal favorableTicks,
            decimal favorableDollars,
            bool lowerObjectiveAvailable,
            bool upperObjectiveAvailable,
            DateTimeOffset? lowerObjectiveFirstHitUtc,
            DateTimeOffset? upperObjectiveFirstHitUtc)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Window = window ?? throw new ArgumentNullException(nameof(window));
            Direction = direction;
            EntryTimestampUtc = entryTimestampUtc;
            ExitTimestampUtc = exitTimestampUtc;
            EntryPrice = entryPrice;
            ExitPrice = exitPrice;
            FavorablePoints = favorablePoints;
            FavorableTicks = favorableTicks;
            FavorableDollars = favorableDollars;
            LowerObjectiveAvailable = lowerObjectiveAvailable;
            UpperObjectiveAvailable = upperObjectiveAvailable;
            LowerObjectiveFirstHitUtc = lowerObjectiveFirstHitUtc;
            UpperObjectiveFirstHitUtc = upperObjectiveFirstHitUtc;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkResearchCycleWindow Window { get; }
        public NewYorkResearchDirection Direction { get; }
        public DateTimeOffset EntryTimestampUtc { get; }
        public DateTimeOffset ExitTimestampUtc { get; }
        public decimal EntryPrice { get; }
        public decimal ExitPrice { get; }
        public decimal FavorablePoints { get; }
        public decimal FavorableTicks { get; }
        public decimal FavorableDollars { get; }
        public bool LowerObjectiveAvailable { get; }
        public bool UpperObjectiveAvailable { get; }
        public DateTimeOffset? LowerObjectiveFirstHitUtc { get; }
        public DateTimeOffset? UpperObjectiveFirstHitUtc { get; }
    }

    public sealed class NewYorkMultiCycleSessionStudy
    {
        public NewYorkMultiCycleSessionStudy(DateTime sessionDateCentral, IReadOnlyList<NewYorkCycleOpportunityEnvelope> cycles, decimal lowerObjective, decimal upperObjective)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Cycles = cycles ?? throw new ArgumentNullException(nameof(cycles));
            CumulativeEnvelopeDollars = cycles.Sum(x => x.FavorableDollars);
            CyclesToLowerObjective = CountCyclesToObjective(cycles, lowerObjective);
            CyclesToUpperObjective = CountCyclesToObjective(cycles, upperObjective);
        }

        public DateTime SessionDateCentral { get; }
        public IReadOnlyList<NewYorkCycleOpportunityEnvelope> Cycles { get; }
        public decimal CumulativeEnvelopeDollars { get; }
        public int? CyclesToLowerObjective { get; }
        public int? CyclesToUpperObjective { get; }

        private static int? CountCyclesToObjective(IReadOnlyList<NewYorkCycleOpportunityEnvelope> cycles, decimal objective)
        {
            decimal cumulative = 0m;
            for (var i = 0; i < cycles.Count; i++)
            {
                cumulative += cycles[i].FavorableDollars;
                if (cumulative >= objective) return i + 1;
            }
            return null;
        }
    }

    /// <summary>
    /// Research-only opportunity-envelope study. It intentionally uses hindsight inside each non-overlapping
    /// clock window to measure whether enough directional movement existed to support the daily objective.
    /// It is not an executable entry model and must not be interpreted as an achieved trading result.
    /// </summary>
    public sealed class NewYorkMultiCycleTargetAnalyzer
    {
        private readonly NewYorkMultiCycleTargetConfig config;
        private readonly IReadOnlyList<NewYorkResearchCycleWindow> windows;

        public NewYorkMultiCycleTargetAnalyzer(NewYorkMultiCycleTargetConfig? config = null, IReadOnlyList<NewYorkResearchCycleWindow>? windows = null)
        {
            this.config = config ?? new NewYorkMultiCycleTargetConfig();
            this.windows = windows ?? DefaultWindows();
            if (this.windows.Count == 0) throw new ArgumentException("At least one research cycle window is required.", nameof(windows));
            for (var i = 1; i < this.windows.Count; i++)
            {
                if (this.windows[i].StartCentral < this.windows[i - 1].EndCentral)
                    throw new ArgumentException("Research cycle windows must not overlap.", nameof(windows));
            }
        }

        public IReadOnlyList<NewYorkMultiCycleSessionStudy> Analyze(IReadOnlyList<HistoricalBar> bars)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (bars.Count == 0) return Array.Empty<NewYorkMultiCycleSessionStudy>();

            var first = bars[0];
            if (bars.Any(x => !string.Equals(x.Instrument, first.Instrument, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Multi-cycle target research requires one instrument per dataset.");
            if (bars.Any(x => x.IntervalSeconds != first.IntervalSeconds))
                throw new InvalidOperationException("Multi-cycle target research requires one bar interval per dataset.");

            var central = ResolveCentralTimeZone();
            var localized = bars
                .Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .OrderBy(x => x.Local)
                .ToList();

            var result = new List<NewYorkMultiCycleSessionStudy>();
            foreach (var group in localized.GroupBy(x => x.Local.Date).OrderBy(x => x.Key))
            {
                var cycleResults = new List<NewYorkCycleOpportunityEnvelope>();
                foreach (var window in windows)
                {
                    var windowBars = group
                        .Where(x => x.Local.TimeOfDay >= window.StartCentral && x.Local.TimeOfDay < window.EndCentral)
                        .OrderBy(x => x.Local)
                        .ToList();
                    if (windowBars.Count < 2) continue;
                    cycleResults.Add(BuildEnvelope(group.Key, window, windowBars));
                }

                if (cycleResults.Count > 0)
                    result.Add(new NewYorkMultiCycleSessionStudy(group.Key, cycleResults, config.LowerDailyObjective, config.UpperDailyObjective));
            }

            return result;
        }

        public static IReadOnlyList<NewYorkResearchCycleWindow> DefaultWindows()
        {
            return new[]
            {
                new NewYorkResearchCycleWindow(1, "Opening", new TimeSpan(8, 30, 0), new TimeSpan(8, 45, 0)),
                new NewYorkResearchCycleWindow(2, "EarlyReset", new TimeSpan(8, 45, 0), new TimeSpan(9, 30, 0)),
                new NewYorkResearchCycleWindow(3, "LaterReset", new TimeSpan(9, 30, 0), new TimeSpan(10, 30, 0))
            };
        }

        private NewYorkCycleOpportunityEnvelope BuildEnvelope(DateTime date, NewYorkResearchCycleWindow window, IReadOnlyList<LocalBar> bars)
        {
            Candidate? best = null;
            for (var i = 0; i < bars.Count - 1; i++)
            {
                var entry = bars[i];
                for (var j = i; j < bars.Count; j++)
                {
                    var later = bars[j];
                    var longPoints = Math.Max(0m, later.Bar.High - entry.Bar.Open);
                    if (best == null || longPoints > best.Points)
                        best = new Candidate(NewYorkResearchDirection.Long, entry, later, entry.Bar.Open, later.Bar.High, longPoints);

                    var shortPoints = Math.Max(0m, entry.Bar.Open - later.Bar.Low);
                    if (best == null || shortPoints > best.Points)
                        best = new Candidate(NewYorkResearchDirection.Short, entry, later, entry.Bar.Open, later.Bar.Low, shortPoints);
                }
            }

            if (best == null) throw new InvalidOperationException("Unable to build cycle opportunity envelope.");

            var lowerPoints = config.LowerDailyObjective / (config.PointValuePerContract * config.Contracts);
            var upperPoints = config.UpperDailyObjective / (config.PointValuePerContract * config.Contracts);
            var lowerHit = FirstHit(bars, best, lowerPoints);
            var upperHit = FirstHit(bars, best, upperPoints);
            var dollars = best.Points * config.PointValuePerContract * config.Contracts;

            return new NewYorkCycleOpportunityEnvelope(
                date,
                window,
                best.Direction,
                best.Entry.Bar.TimestampUtc,
                best.Exit.Bar.TimestampUtc,
                best.EntryPrice,
                best.ExitPrice,
                best.Points,
                best.Points / config.TickSize,
                dollars,
                dollars >= config.LowerDailyObjective,
                dollars >= config.UpperDailyObjective,
                lowerHit,
                upperHit);
        }

        private static DateTimeOffset? FirstHit(IReadOnlyList<LocalBar> bars, Candidate best, decimal targetPoints)
        {
            var startIndex = -1;
            for (var i = 0; i < bars.Count; i++)
            {
                if (ReferenceEquals(bars[i], best.Entry)) { startIndex = i; break; }
            }
            if (startIndex < 0) return null;

            for (var i = startIndex; i < bars.Count; i++)
            {
                var achieved = best.Direction == NewYorkResearchDirection.Long
                    ? bars[i].Bar.High - best.EntryPrice
                    : best.EntryPrice - bars[i].Bar.Low;
                if (achieved >= targetPoints) return bars[i].Bar.TimestampUtc;
            }
            return null;
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

        private sealed class Candidate
        {
            public Candidate(NewYorkResearchDirection direction, LocalBar entry, LocalBar exit, decimal entryPrice, decimal exitPrice, decimal points)
            {
                Direction = direction;
                Entry = entry;
                Exit = exit;
                EntryPrice = entryPrice;
                ExitPrice = exitPrice;
                Points = points;
            }

            public NewYorkResearchDirection Direction { get; }
            public LocalBar Entry { get; }
            public LocalBar Exit { get; }
            public decimal EntryPrice { get; }
            public decimal ExitPrice { get; }
            public decimal Points { get; }
        }
    }
}
