using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class NewYorkOpportunityOutcomeConfig
    {
        public NewYorkOpportunityOutcomeConfig(decimal tickSize = 0.25m, decimal pointValue = 2.00m, decimal roundTripCommission = 0m, decimal slippageTicksPerSide = 0m)
        {
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (pointValue <= 0m) throw new ArgumentOutOfRangeException(nameof(pointValue));
            if (roundTripCommission < 0m) throw new ArgumentOutOfRangeException(nameof(roundTripCommission));
            if (slippageTicksPerSide < 0m) throw new ArgumentOutOfRangeException(nameof(slippageTicksPerSide));
            TickSize = tickSize;
            PointValue = pointValue;
            RoundTripCommission = roundTripCommission;
            SlippageTicksPerSide = slippageTicksPerSide;
        }

        public decimal TickSize { get; }
        public decimal PointValue { get; }
        public decimal RoundTripCommission { get; }
        public decimal SlippageTicksPerSide { get; }
    }

    public sealed class NewYorkOpportunityOutcome
    {
        public NewYorkOpportunityOutcome(
            DateTime sessionDateCentral,
            NewYorkResearchRegime regime,
            NewYorkOpportunitySeedType seedType,
            NewYorkResearchDirection direction,
            decimal seedScore,
            DateTimeOffset entryTimestampUtc,
            decimal entryPrice,
            decimal mfePoints,
            decimal maePoints,
            decimal mfeTicks,
            decimal maeTicks,
            int minutesToMfe,
            int minutesToMae,
            decimal opportunityWindowCloseMovePoints,
            decimal sessionEndMovePoints,
            decimal grossSessionEndPnlPerContract,
            decimal afterCostSessionEndPnlPerContract,
            decimal favorableOpeningRangeMultiple,
            decimal adverseOpeningRangeMultiple,
            bool reachedHalfOpeningRange,
            bool reachedFullOpeningRange,
            bool reachedOneAndHalfOpeningRange,
            bool opportunityWindowClosedFavorable,
            bool sessionClosedFavorable,
            bool runnerCandidate)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Regime = regime;
            SeedType = seedType;
            Direction = direction;
            SeedScore = seedScore;
            EntryTimestampUtc = entryTimestampUtc;
            EntryPrice = entryPrice;
            MfePoints = mfePoints;
            MaePoints = maePoints;
            MfeTicks = mfeTicks;
            MaeTicks = maeTicks;
            MinutesToMfe = minutesToMfe;
            MinutesToMae = minutesToMae;
            OpportunityWindowCloseMovePoints = opportunityWindowCloseMovePoints;
            SessionEndMovePoints = sessionEndMovePoints;
            GrossSessionEndPnlPerContract = grossSessionEndPnlPerContract;
            AfterCostSessionEndPnlPerContract = afterCostSessionEndPnlPerContract;
            FavorableOpeningRangeMultiple = favorableOpeningRangeMultiple;
            AdverseOpeningRangeMultiple = adverseOpeningRangeMultiple;
            ReachedHalfOpeningRange = reachedHalfOpeningRange;
            ReachedFullOpeningRange = reachedFullOpeningRange;
            ReachedOneAndHalfOpeningRange = reachedOneAndHalfOpeningRange;
            OpportunityWindowClosedFavorable = opportunityWindowClosedFavorable;
            SessionClosedFavorable = sessionClosedFavorable;
            RunnerCandidate = runnerCandidate;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkResearchRegime Regime { get; }
        public NewYorkOpportunitySeedType SeedType { get; }
        public NewYorkResearchDirection Direction { get; }
        public decimal SeedScore { get; }
        public DateTimeOffset EntryTimestampUtc { get; }
        public decimal EntryPrice { get; }
        public decimal MfePoints { get; }
        public decimal MaePoints { get; }
        public decimal MfeTicks { get; }
        public decimal MaeTicks { get; }
        public int MinutesToMfe { get; }
        public int MinutesToMae { get; }
        public decimal OpportunityWindowCloseMovePoints { get; }
        public decimal SessionEndMovePoints { get; }
        public decimal GrossSessionEndPnlPerContract { get; }
        public decimal AfterCostSessionEndPnlPerContract { get; }
        public decimal FavorableOpeningRangeMultiple { get; }
        public decimal AdverseOpeningRangeMultiple { get; }
        public bool ReachedHalfOpeningRange { get; }
        public bool ReachedFullOpeningRange { get; }
        public bool ReachedOneAndHalfOpeningRange { get; }
        public bool OpportunityWindowClosedFavorable { get; }
        public bool SessionClosedFavorable { get; }
        public bool RunnerCandidate { get; }
    }

    public sealed class NewYorkOpportunityOutcomeLabeler
    {
        private static readonly TimeSpan ResearchSessionEnd = new TimeSpan(11, 0, 0);
        private readonly NewYorkOpportunityOutcomeConfig config;

        public NewYorkOpportunityOutcomeLabeler(NewYorkOpportunityOutcomeConfig? config = null)
        {
            this.config = config ?? new NewYorkOpportunityOutcomeConfig();
        }

        public IReadOnlyList<NewYorkOpportunityOutcome> Label(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<NewYorkRegimeClassification> classifications,
            IReadOnlyList<NewYorkOpportunitySeedLabel> seeds)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (classifications == null) throw new ArgumentNullException(nameof(classifications));
            if (seeds == null) throw new ArgumentNullException(nameof(seeds));
            if (seeds.Count == 0) return Array.Empty<NewYorkOpportunityOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars
                .Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .OrderBy(x => x.Local)
                .ToList();

            var classificationsByDate = classifications.ToDictionary(x => x.Features.SessionDateCentral.Date);
            var result = new List<NewYorkOpportunityOutcome>();

            foreach (var seed in seeds.OrderBy(x => x.SessionDateCentral).ThenBy(x => x.WindowStartCentral))
            {
                if (seed.Direction == NewYorkResearchDirection.None)
                    throw new InvalidOperationException("Directional opportunity seed cannot have None direction.");
                if (!classificationsByDate.TryGetValue(seed.SessionDateCentral.Date, out var classification))
                    throw new InvalidOperationException("Opportunity seed has no matching regime classification.");

                var session = localized
                    .Where(x => x.Local.Date == seed.SessionDateCentral.Date
                        && x.Local.TimeOfDay >= seed.WindowStartCentral
                        && x.Local.TimeOfDay < ResearchSessionEnd)
                    .ToList();
                if (session.Count == 0)
                    throw new InvalidOperationException("Opportunity seed has no historical bars at or after its entry window start.");

                var entry = session[0];
                if (entry.Local.TimeOfDay >= seed.WindowEndCentral)
                    throw new InvalidOperationException("Opportunity seed has no entry-reference bar inside its stated window.");

                var windowBars = session.Where(x => x.Local.TimeOfDay < seed.WindowEndCentral).ToList();
                if (windowBars.Count == 0)
                    throw new InvalidOperationException("Opportunity seed has no bars inside its stated window.");

                var entryPrice = entry.Bar.Open;
                var favorableExtreme = seed.Direction == NewYorkResearchDirection.Long
                    ? session.Max(x => x.Bar.High)
                    : session.Min(x => x.Bar.Low);
                var adverseExtreme = seed.Direction == NewYorkResearchDirection.Long
                    ? session.Min(x => x.Bar.Low)
                    : session.Max(x => x.Bar.High);

                var mfePoints = seed.Direction == NewYorkResearchDirection.Long
                    ? Math.Max(0m, favorableExtreme - entryPrice)
                    : Math.Max(0m, entryPrice - favorableExtreme);
                var maePoints = seed.Direction == NewYorkResearchDirection.Long
                    ? Math.Max(0m, entryPrice - adverseExtreme)
                    : Math.Max(0m, adverseExtreme - entryPrice);

                var mfeBar = seed.Direction == NewYorkResearchDirection.Long
                    ? session.First(x => x.Bar.High == favorableExtreme)
                    : session.First(x => x.Bar.Low == favorableExtreme);
                var maeBar = seed.Direction == NewYorkResearchDirection.Long
                    ? session.First(x => x.Bar.Low == adverseExtreme)
                    : session.First(x => x.Bar.High == adverseExtreme);

                var opportunityCloseMove = DirectionalMove(entryPrice, windowBars[windowBars.Count - 1].Bar.Close, seed.Direction);
                var sessionCloseMove = DirectionalMove(entryPrice, session[session.Count - 1].Bar.Close, seed.Direction);
                var openingRange = Math.Max(classification.Features.OpeningRange, config.TickSize);
                var favorableMultiple = mfePoints / openingRange;
                var adverseMultiple = maePoints / openingRange;
                var grossPnl = sessionCloseMove * config.PointValue;
                var slippageCost = config.SlippageTicksPerSide * 2m * config.TickSize * config.PointValue;
                var afterCostPnl = grossPnl - config.RoundTripCommission - slippageCost;

                result.Add(new NewYorkOpportunityOutcome(
                    seed.SessionDateCentral,
                    classification.Regime,
                    seed.Type,
                    seed.Direction,
                    seed.Score,
                    entry.Bar.TimestampUtc,
                    entryPrice,
                    mfePoints,
                    maePoints,
                    mfePoints / config.TickSize,
                    maePoints / config.TickSize,
                    MinutesBetween(entry.Bar.TimestampUtc, mfeBar.Bar.TimestampUtc),
                    MinutesBetween(entry.Bar.TimestampUtc, maeBar.Bar.TimestampUtc),
                    opportunityCloseMove,
                    sessionCloseMove,
                    grossPnl,
                    afterCostPnl,
                    favorableMultiple,
                    adverseMultiple,
                    favorableMultiple >= 0.50m,
                    favorableMultiple >= 1.00m,
                    favorableMultiple >= 1.50m,
                    opportunityCloseMove > 0m,
                    sessionCloseMove > 0m,
                    favorableMultiple >= 1.50m && opportunityCloseMove > 0m));
            }

            return result;
        }

        private static decimal DirectionalMove(decimal entryPrice, decimal laterPrice, NewYorkResearchDirection direction)
        {
            return direction == NewYorkResearchDirection.Long ? laterPrice - entryPrice : entryPrice - laterPrice;
        }

        private static int MinutesBetween(DateTimeOffset start, DateTimeOffset end)
        {
            var value = (int)Math.Round((end - start).TotalMinutes, MidpointRounding.AwayFromZero);
            return Math.Max(0, value);
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
