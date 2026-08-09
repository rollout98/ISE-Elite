using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum NewYorkStructuralEntryDisposition
    {
        None = 0,
        EntryQualified = 1,
        ContinuationInvalidated = 2,
        NoSetup = 3
    }

    public sealed class NewYorkStructuralEntryConfig
    {
        public NewYorkStructuralEntryConfig(
            decimal continuationResetFraction = 0.20m,
            decimal midpointInvalidationBufferFraction = 0.05m,
            int resumptionLookbackBars = 2,
            int reversalConfirmationBars = 1,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2.00m,
            int contracts = 2,
            decimal lowerObjective = 500m,
            decimal upperObjective = 1000m)
        {
            if (continuationResetFraction <= 0m) throw new ArgumentOutOfRangeException(nameof(continuationResetFraction));
            if (midpointInvalidationBufferFraction < 0m) throw new ArgumentOutOfRangeException(nameof(midpointInvalidationBufferFraction));
            if (resumptionLookbackBars <= 0) throw new ArgumentOutOfRangeException(nameof(resumptionLookbackBars));
            if (reversalConfirmationBars <= 0) throw new ArgumentOutOfRangeException(nameof(reversalConfirmationBars));
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (pointValuePerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(pointValuePerContract));
            if (contracts <= 0) throw new ArgumentOutOfRangeException(nameof(contracts));
            if (lowerObjective <= 0m) throw new ArgumentOutOfRangeException(nameof(lowerObjective));
            if (upperObjective < lowerObjective) throw new ArgumentOutOfRangeException(nameof(upperObjective));

            ContinuationResetFraction = continuationResetFraction;
            MidpointInvalidationBufferFraction = midpointInvalidationBufferFraction;
            ResumptionLookbackBars = resumptionLookbackBars;
            ReversalConfirmationBars = reversalConfirmationBars;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
            LowerObjective = lowerObjective;
            UpperObjective = upperObjective;
        }

        public decimal ContinuationResetFraction { get; }
        public decimal MidpointInvalidationBufferFraction { get; }
        public int ResumptionLookbackBars { get; }
        public int ReversalConfirmationBars { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal LowerObjective { get; }
        public decimal UpperObjective { get; }
    }

    public sealed class NewYorkStructuralEntryOutcome
    {
        public NewYorkStructuralEntryOutcome(
            DateTime sessionDateCentral,
            NewYorkEightFortyFiveState transitionState,
            NewYorkCausalEntryType entryType,
            NewYorkStructuralEntryDisposition disposition,
            NewYorkResearchDirection direction,
            DateTimeOffset? transitionSignalUtc,
            DateTimeOffset? invalidatedUtc,
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
            Disposition = disposition;
            Direction = direction;
            TransitionSignalUtc = transitionSignalUtc;
            InvalidatedUtc = invalidatedUtc;
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
        public NewYorkStructuralEntryDisposition Disposition { get; }
        public NewYorkResearchDirection Direction { get; }
        public DateTimeOffset? TransitionSignalUtc { get; }
        public DateTimeOffset? InvalidatedUtc { get; }
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
    /// Research-only structural entry layer. Continue must survive opening-structure invalidation,
    /// complete a measurable reset, and then close through a multi-bar micro-swing in the original direction.
    /// Reverse retains the prior completed-bar confirmation rule. Every entry reference is the next bar open.
    /// </summary>
    public sealed class NewYorkStructuralEntryAnalyzer
    {
        private static readonly TimeSpan OpeningStart = new TimeSpan(8, 30, 0);
        private static readonly TimeSpan SearchStart = new TimeSpan(8, 45, 0);
        private static readonly TimeSpan SearchEnd = new TimeSpan(9, 20, 0);
        private static readonly TimeSpan OutcomeEnd = new TimeSpan(9, 30, 0);
        private readonly NewYorkStructuralEntryConfig config;

        public NewYorkStructuralEntryAnalyzer(NewYorkStructuralEntryConfig? config = null)
        {
            this.config = config ?? new NewYorkStructuralEntryConfig();
        }

        public IReadOnlyList<NewYorkStructuralEntryOutcome> Analyze(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<NewYorkEightFortyFiveTransitionOutcome> transitions)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            if (bars.Count == 0 || transitions.Count == 0) return Array.Empty<NewYorkStructuralEntryOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars.Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .OrderBy(x => x.Local).ToList();
            var byDate = localized.GroupBy(x => x.Local.Date).ToDictionary(x => x.Key, x => x.ToList());
            var result = new List<NewYorkStructuralEntryOutcome>();

            foreach (var transition in transitions.OrderBy(x => x.SessionDateCentral))
            {
                if (!byDate.TryGetValue(transition.SessionDateCentral.Date, out var session)) continue;
                result.Add(AnalyzeSession(transition, session));
            }

            return result;
        }

        private NewYorkStructuralEntryOutcome AnalyzeSession(NewYorkEightFortyFiveTransitionOutcome transition, IReadOnlyList<LocalBar> session)
        {
            if (transition.State == NewYorkEightFortyFiveState.StandAside || !transition.SignalTimestampUtc.HasValue)
                return Empty(transition, NewYorkStructuralEntryDisposition.NoSetup, null);

            var opening = session.Where(x => x.Local.TimeOfDay >= OpeningStart && x.Local.TimeOfDay < SearchStart).OrderBy(x => x.Local).ToList();
            var search = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < SearchEnd).OrderBy(x => x.Local).ToList();
            var outcome = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < OutcomeEnd).OrderBy(x => x.Local).ToList();
            if (opening.Count < 2 || search.Count < 2 || outcome.Count < 2) return Empty(transition, NewYorkStructuralEntryDisposition.NoSetup, null);

            var signalIndex = search.FindIndex(x => x.Bar.TimestampUtc == transition.SignalTimestampUtc.Value);
            if (signalIndex < 0) return Empty(transition, NewYorkStructuralEntryDisposition.NoSetup, null);

            LocalBar? setupComplete;
            LocalBar? invalidated = null;
            if (transition.State == NewYorkEightFortyFiveState.Continue)
            {
                setupComplete = FindStructuralContinuation(search, signalIndex, opening, transition.TradeDirection, transition.OpeningRange, out invalidated);
                if (invalidated != null)
                    return Empty(transition, NewYorkStructuralEntryDisposition.ContinuationInvalidated, invalidated.Bar.TimestampUtc);
            }
            else
            {
                setupComplete = FindReversalConfirmation(search, signalIndex, transition.TradeDirection);
            }

            if (setupComplete == null) return Empty(transition, NewYorkStructuralEntryDisposition.NoSetup, null);

            var setupIndex = outcome.FindIndex(x => x.Bar.TimestampUtc == setupComplete.Bar.TimestampUtc);
            if (setupIndex < 0 || setupIndex + 1 >= outcome.Count) return Empty(transition, NewYorkStructuralEntryDisposition.NoSetup, null);

            var entry = outcome[setupIndex + 1];
            var forward = outcome.Skip(setupIndex + 1).ToList();
            var direction = transition.TradeDirection;
            var entryPrice = entry.Bar.Open;
            var favorable = direction == NewYorkResearchDirection.Long
                ? Math.Max(0m, forward.Max(x => x.Bar.High) - entryPrice)
                : Math.Max(0m, entryPrice - forward.Min(x => x.Bar.Low));
            var adverse = direction == NewYorkResearchDirection.Long
                ? Math.Max(0m, entryPrice - forward.Min(x => x.Bar.Low))
                : Math.Max(0m, forward.Max(x => x.Bar.High) - entryPrice);
            var lowerPoints = config.LowerObjective / (config.PointValuePerContract * config.Contracts);
            var upperPoints = config.UpperObjective / (config.PointValuePerContract * config.Contracts);

            return new NewYorkStructuralEntryOutcome(
                transition.SessionDateCentral,
                transition.State,
                transition.State == NewYorkEightFortyFiveState.Continue ? NewYorkCausalEntryType.ContinuationAfterReset : NewYorkCausalEntryType.ReversalAfterConfirmation,
                NewYorkStructuralEntryDisposition.EntryQualified,
                direction,
                transition.SignalTimestampUtc,
                null,
                setupComplete.Bar.TimestampUtc,
                entry.Bar.TimestampUtc,
                entryPrice,
                favorable,
                adverse,
                FirstHit(forward, entryPrice, direction, lowerPoints),
                FirstHit(forward, entryPrice, direction, upperPoints));
        }

        private LocalBar? FindStructuralContinuation(
            IReadOnlyList<LocalBar> bars,
            int signalIndex,
            IReadOnlyList<LocalBar> opening,
            NewYorkResearchDirection direction,
            decimal openingRange,
            out LocalBar? invalidated)
        {
            invalidated = null;
            var openingHigh = opening.Max(x => x.Bar.High);
            var openingLow = opening.Min(x => x.Bar.Low);
            var midpoint = (openingHigh + openingLow) / 2m;
            var invalidationBuffer = openingRange * config.MidpointInvalidationBufferFraction;
            var resetThreshold = Math.Max(config.TickSize, openingRange * config.ContinuationResetFraction);
            var favorableExtreme = direction == NewYorkResearchDirection.Long ? bars[signalIndex].Bar.High : bars[signalIndex].Bar.Low;
            var resetSeen = false;

            for (var i = signalIndex + 1; i < bars.Count; i++)
            {
                var bar = bars[i];
                var structurallyInvalid = direction == NewYorkResearchDirection.Long
                    ? bar.Bar.Close < midpoint - invalidationBuffer
                    : bar.Bar.Close > midpoint + invalidationBuffer;
                if (structurallyInvalid)
                {
                    invalidated = bar;
                    return null;
                }

                if (direction == NewYorkResearchDirection.Long)
                {
                    favorableExtreme = Math.Max(favorableExtreme, bar.Bar.High);
                    if (favorableExtreme - bar.Bar.Low >= resetThreshold) resetSeen = true;
                }
                else
                {
                    favorableExtreme = Math.Min(favorableExtreme, bar.Bar.Low);
                    if (bar.Bar.High - favorableExtreme >= resetThreshold) resetSeen = true;
                }

                if (!resetSeen || i < config.ResumptionLookbackBars) continue;
                var prior = bars.Skip(i - config.ResumptionLookbackBars).Take(config.ResumptionLookbackBars).ToList();
                var swingBreak = direction == NewYorkResearchDirection.Long
                    ? bar.Bar.Close > prior.Max(x => x.Bar.High)
                    : bar.Bar.Close < prior.Min(x => x.Bar.Low);
                if (swingBreak) return bar;
            }

            return null;
        }

        private LocalBar? FindReversalConfirmation(IReadOnlyList<LocalBar> bars, int signalIndex, NewYorkResearchDirection direction)
        {
            var confirmed = 0;
            for (var i = signalIndex + 1; i < bars.Count; i++)
            {
                var confirms = direction == NewYorkResearchDirection.Long
                    ? bars[i].Bar.Close > bars[i - 1].Bar.High
                    : bars[i].Bar.Close < bars[i - 1].Bar.Low;
                confirmed = confirms ? confirmed + 1 : 0;
                if (confirmed >= config.ReversalConfirmationBars) return bars[i];
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

        private static NewYorkStructuralEntryOutcome Empty(NewYorkEightFortyFiveTransitionOutcome transition, NewYorkStructuralEntryDisposition disposition, DateTimeOffset? invalidatedUtc)
        {
            return new NewYorkStructuralEntryOutcome(transition.SessionDateCentral, transition.State, NewYorkCausalEntryType.None,
                disposition, transition.TradeDirection, transition.SignalTimestampUtc, invalidatedUtc, null, null, 0m, 0m, 0m, null, null);
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