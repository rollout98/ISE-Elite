using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum NewYorkCausalEntryType
    {
        None = 0,
        ContinuationAfterReset = 1,
        ReversalAfterConfirmation = 2
    }

    public sealed class NewYorkCausalEntryConfig
    {
        public NewYorkCausalEntryConfig(
            decimal continuationResetFraction = 0.20m,
            int reversalConfirmationBars = 1,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2.00m,
            int contracts = 2,
            decimal lowerObjective = 500m,
            decimal upperObjective = 1000m)
        {
            if (continuationResetFraction <= 0m) throw new ArgumentOutOfRangeException(nameof(continuationResetFraction));
            if (reversalConfirmationBars <= 0) throw new ArgumentOutOfRangeException(nameof(reversalConfirmationBars));
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (pointValuePerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(pointValuePerContract));
            if (contracts <= 0) throw new ArgumentOutOfRangeException(nameof(contracts));
            if (lowerObjective <= 0m) throw new ArgumentOutOfRangeException(nameof(lowerObjective));
            if (upperObjective < lowerObjective) throw new ArgumentOutOfRangeException(nameof(upperObjective));

            ContinuationResetFraction = continuationResetFraction;
            ReversalConfirmationBars = reversalConfirmationBars;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
            LowerObjective = lowerObjective;
            UpperObjective = upperObjective;
        }

        public decimal ContinuationResetFraction { get; }
        public int ReversalConfirmationBars { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal LowerObjective { get; }
        public decimal UpperObjective { get; }
    }

    public sealed class NewYorkCausalEntryOutcome
    {
        public NewYorkCausalEntryOutcome(
            DateTime sessionDateCentral,
            NewYorkEightFortyFiveState transitionState,
            NewYorkCausalEntryType entryType,
            NewYorkResearchDirection direction,
            DateTimeOffset? transitionSignalUtc,
            DateTimeOffset? setupCompleteUtc,
            DateTimeOffset? entryUtc,
            decimal entryPrice,
            decimal favorablePoints,
            decimal adversePoints,
            DateTimeOffset? lowerObjectiveFirstHitUtc,
            DateTimeOffset? upperObjectiveFirstHitUtc)
        {
            SessionDateCentral = sessionDateCentral.Date;
            TransitionState = transitionState;
            EntryType = entryType;
            Direction = direction;
            TransitionSignalUtc = transitionSignalUtc;
            SetupCompleteUtc = setupCompleteUtc;
            EntryUtc = entryUtc;
            EntryPrice = entryPrice;
            FavorablePoints = favorablePoints;
            AdversePoints = adversePoints;
            LowerObjectiveFirstHitUtc = lowerObjectiveFirstHitUtc;
            UpperObjectiveFirstHitUtc = upperObjectiveFirstHitUtc;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkEightFortyFiveState TransitionState { get; }
        public NewYorkCausalEntryType EntryType { get; }
        public NewYorkResearchDirection Direction { get; }
        public DateTimeOffset? TransitionSignalUtc { get; }
        public DateTimeOffset? SetupCompleteUtc { get; }
        public DateTimeOffset? EntryUtc { get; }
        public decimal EntryPrice { get; }
        public decimal FavorablePoints { get; }
        public decimal AdversePoints { get; }
        public DateTimeOffset? LowerObjectiveFirstHitUtc { get; }
        public DateTimeOffset? UpperObjectiveFirstHitUtc { get; }
        public bool HasEntry => EntryUtc.HasValue;
        public bool LowerObjectiveAvailable => LowerObjectiveFirstHitUtc.HasValue;
        public bool UpperObjectiveAvailable => UpperObjectiveFirstHitUtc.HasValue;
        public decimal FavorableTicks(decimal tickSize) => tickSize <= 0m ? 0m : FavorablePoints / tickSize;
        public decimal AdverseTicks(decimal tickSize) => tickSize <= 0m ? 0m : AdversePoints / tickSize;
    }

    /// <summary>
    /// Research-only causal entry layer. It consumes the 08:45 Continue/Reverse/StandAside state,
    /// but does not treat that state as an entry. Continue waits for a measurable reset and then
    /// one-minute resumption; Reverse waits for completed-bar confirmation. The entry reference is
    /// always the next bar open after setup completion.
    /// </summary>
    public sealed class NewYorkCausalEntryAnalyzer
    {
        private static readonly TimeSpan SearchStart = new TimeSpan(8, 45, 0);
        private static readonly TimeSpan SearchEnd = new TimeSpan(9, 20, 0);
        private static readonly TimeSpan OutcomeEnd = new TimeSpan(9, 30, 0);
        private readonly NewYorkCausalEntryConfig config;

        public NewYorkCausalEntryAnalyzer(NewYorkCausalEntryConfig? config = null)
        {
            this.config = config ?? new NewYorkCausalEntryConfig();
        }

        public IReadOnlyList<NewYorkCausalEntryOutcome> Analyze(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<NewYorkEightFortyFiveTransitionOutcome> transitions)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            if (bars.Count == 0 || transitions.Count == 0) return Array.Empty<NewYorkCausalEntryOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars
                .Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .OrderBy(x => x.Local)
                .ToList();
            var byDate = localized.GroupBy(x => x.Local.Date).ToDictionary(x => x.Key, x => x.ToList());
            var result = new List<NewYorkCausalEntryOutcome>();

            foreach (var transition in transitions.OrderBy(x => x.SessionDateCentral))
            {
                if (!byDate.TryGetValue(transition.SessionDateCentral.Date, out var session)) continue;
                result.Add(AnalyzeSession(transition, session));
            }

            return result;
        }

        private NewYorkCausalEntryOutcome AnalyzeSession(NewYorkEightFortyFiveTransitionOutcome transition, IReadOnlyList<LocalBar> session)
        {
            if (transition.State == NewYorkEightFortyFiveState.StandAside || !transition.SignalTimestampUtc.HasValue)
                return Empty(transition);

            var search = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < SearchEnd).OrderBy(x => x.Local).ToList();
            var outcome = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < OutcomeEnd).OrderBy(x => x.Local).ToList();
            var signalIndex = search.FindIndex(x => x.Bar.TimestampUtc == transition.SignalTimestampUtc.Value);
            if (signalIndex < 0) return Empty(transition);

            LocalBar? setupComplete = transition.State == NewYorkEightFortyFiveState.Continue
                ? FindContinuationResetCompletion(search, signalIndex, transition.TradeDirection, transition.OpeningRange)
                : FindReversalConfirmation(search, signalIndex, transition.TradeDirection);

            if (setupComplete == null) return Empty(transition);

            var setupIndex = outcome.FindIndex(x => x.Bar.TimestampUtc == setupComplete.Bar.TimestampUtc);
            if (setupIndex < 0 || setupIndex + 1 >= outcome.Count) return Empty(transition);

            var entry = outcome[setupIndex + 1];
            var forward = outcome.Skip(setupIndex + 1).ToList();
            var entryPrice = entry.Bar.Open;
            var direction = transition.TradeDirection;
            var favorable = direction == NewYorkResearchDirection.Long
                ? Math.Max(0m, forward.Max(x => x.Bar.High) - entryPrice)
                : Math.Max(0m, entryPrice - forward.Min(x => x.Bar.Low));
            var adverse = direction == NewYorkResearchDirection.Long
                ? Math.Max(0m, entryPrice - forward.Min(x => x.Bar.Low))
                : Math.Max(0m, forward.Max(x => x.Bar.High) - entryPrice);

            var lowerPoints = config.LowerObjective / (config.PointValuePerContract * config.Contracts);
            var upperPoints = config.UpperObjective / (config.PointValuePerContract * config.Contracts);

            return new NewYorkCausalEntryOutcome(
                transition.SessionDateCentral,
                transition.State,
                transition.State == NewYorkEightFortyFiveState.Continue ? NewYorkCausalEntryType.ContinuationAfterReset : NewYorkCausalEntryType.ReversalAfterConfirmation,
                direction,
                transition.SignalTimestampUtc,
                setupComplete.Bar.TimestampUtc,
                entry.Bar.TimestampUtc,
                entryPrice,
                favorable,
                adverse,
                FirstHit(forward, entryPrice, direction, lowerPoints),
                FirstHit(forward, entryPrice, direction, upperPoints));
        }

        private LocalBar? FindContinuationResetCompletion(IReadOnlyList<LocalBar> bars, int signalIndex, NewYorkResearchDirection direction, decimal openingRange)
        {
            var threshold = Math.Max(config.TickSize, openingRange * config.ContinuationResetFraction);
            decimal favorableExtreme = direction == NewYorkResearchDirection.Long ? bars[signalIndex].Bar.High : bars[signalIndex].Bar.Low;
            bool resetSeen = false;

            for (var i = signalIndex + 1; i < bars.Count; i++)
            {
                var bar = bars[i];
                if (direction == NewYorkResearchDirection.Long)
                {
                    favorableExtreme = Math.Max(favorableExtreme, bar.Bar.High);
                    if (favorableExtreme - bar.Bar.Low >= threshold) resetSeen = true;
                    if (resetSeen && i > 0 && bar.Bar.Close > bars[i - 1].Bar.High) return bar;
                }
                else
                {
                    favorableExtreme = Math.Min(favorableExtreme, bar.Bar.Low);
                    if (bar.Bar.High - favorableExtreme >= threshold) resetSeen = true;
                    if (resetSeen && i > 0 && bar.Bar.Close < bars[i - 1].Bar.Low) return bar;
                }
            }

            return null;
        }

        private LocalBar? FindReversalConfirmation(IReadOnlyList<LocalBar> bars, int signalIndex, NewYorkResearchDirection direction)
        {
            var confirmed = 0;
            for (var i = signalIndex + 1; i < bars.Count; i++)
            {
                var prior = bars[i - 1];
                var bar = bars[i];
                var confirms = direction == NewYorkResearchDirection.Long
                    ? bar.Bar.Close > prior.Bar.High
                    : bar.Bar.Close < prior.Bar.Low;

                confirmed = confirms ? confirmed + 1 : 0;
                if (confirmed >= config.ReversalConfirmationBars) return bar;
            }
            return null;
        }

        private static DateTimeOffset? FirstHit(IReadOnlyList<LocalBar> bars, decimal entryPrice, NewYorkResearchDirection direction, decimal points)
        {
            foreach (var bar in bars)
            {
                var achieved = direction == NewYorkResearchDirection.Long ? bar.Bar.High - entryPrice : entryPrice - bar.Bar.Low;
                if (achieved >= points) return bar.Bar.TimestampUtc;
            }
            return null;
        }

        private static NewYorkCausalEntryOutcome Empty(NewYorkEightFortyFiveTransitionOutcome transition)
        {
            return new NewYorkCausalEntryOutcome(transition.SessionDateCentral, transition.State, NewYorkCausalEntryType.None,
                transition.TradeDirection, transition.SignalTimestampUtc, null, null, 0m, 0m, 0m, null, null);
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
