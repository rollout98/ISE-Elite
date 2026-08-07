using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum NewYorkEightFortyFiveState
    {
        StandAside = 0,
        Continue = 1,
        Reverse = 2
    }

    public sealed class NewYorkEightFortyFiveTransitionConfig
    {
        public NewYorkEightFortyFiveTransitionConfig(
            decimal minimumOpeningEfficiency = 0.35m,
            decimal continuationExtensionFraction = 0.15m,
            decimal reversalRetracementFraction = 0.50m,
            decimal reversalMidpointBufferFraction = 0.05m,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2.00m,
            int contracts = 2,
            decimal lowerObjective = 500m,
            decimal upperObjective = 1000m)
        {
            if (minimumOpeningEfficiency < 0m || minimumOpeningEfficiency > 1m) throw new ArgumentOutOfRangeException(nameof(minimumOpeningEfficiency));
            if (continuationExtensionFraction <= 0m) throw new ArgumentOutOfRangeException(nameof(continuationExtensionFraction));
            if (reversalRetracementFraction <= 0m) throw new ArgumentOutOfRangeException(nameof(reversalRetracementFraction));
            if (reversalMidpointBufferFraction < 0m) throw new ArgumentOutOfRangeException(nameof(reversalMidpointBufferFraction));
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (pointValuePerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(pointValuePerContract));
            if (contracts <= 0) throw new ArgumentOutOfRangeException(nameof(contracts));
            if (lowerObjective <= 0m) throw new ArgumentOutOfRangeException(nameof(lowerObjective));
            if (upperObjective < lowerObjective) throw new ArgumentOutOfRangeException(nameof(upperObjective));

            MinimumOpeningEfficiency = minimumOpeningEfficiency;
            ContinuationExtensionFraction = continuationExtensionFraction;
            ReversalRetracementFraction = reversalRetracementFraction;
            ReversalMidpointBufferFraction = reversalMidpointBufferFraction;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
            LowerObjective = lowerObjective;
            UpperObjective = upperObjective;
        }

        public decimal MinimumOpeningEfficiency { get; }
        public decimal ContinuationExtensionFraction { get; }
        public decimal ReversalRetracementFraction { get; }
        public decimal ReversalMidpointBufferFraction { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal LowerObjective { get; }
        public decimal UpperObjective { get; }
    }

    public sealed class NewYorkEightFortyFiveTransitionOutcome
    {
        public NewYorkEightFortyFiveTransitionOutcome(
            DateTime sessionDateCentral,
            NewYorkEightFortyFiveState state,
            NewYorkResearchDirection openingDirection,
            NewYorkResearchDirection tradeDirection,
            DateTimeOffset? signalTimestampUtc,
            DateTimeOffset? referenceEntryTimestampUtc,
            decimal openingRange,
            decimal openingDisplacement,
            decimal openingEfficiency,
            decimal favorablePoints,
            decimal adversePoints,
            DateTimeOffset? lowerObjectiveFirstHitUtc,
            DateTimeOffset? upperObjectiveFirstHitUtc)
        {
            SessionDateCentral = sessionDateCentral.Date;
            State = state;
            OpeningDirection = openingDirection;
            TradeDirection = tradeDirection;
            SignalTimestampUtc = signalTimestampUtc;
            ReferenceEntryTimestampUtc = referenceEntryTimestampUtc;
            OpeningRange = openingRange;
            OpeningDisplacement = openingDisplacement;
            OpeningEfficiency = openingEfficiency;
            FavorablePoints = favorablePoints;
            AdversePoints = adversePoints;
            LowerObjectiveFirstHitUtc = lowerObjectiveFirstHitUtc;
            UpperObjectiveFirstHitUtc = upperObjectiveFirstHitUtc;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkEightFortyFiveState State { get; }
        public NewYorkResearchDirection OpeningDirection { get; }
        public NewYorkResearchDirection TradeDirection { get; }
        public DateTimeOffset? SignalTimestampUtc { get; }
        public DateTimeOffset? ReferenceEntryTimestampUtc { get; }
        public decimal OpeningRange { get; }
        public decimal OpeningDisplacement { get; }
        public decimal OpeningEfficiency { get; }
        public decimal FavorablePoints { get; }
        public decimal AdversePoints { get; }
        public decimal FavorableTicks(decimal tickSize) => tickSize <= 0m ? 0m : FavorablePoints / tickSize;
        public decimal AdverseTicks(decimal tickSize) => tickSize <= 0m ? 0m : AdversePoints / tickSize;
        public DateTimeOffset? LowerObjectiveFirstHitUtc { get; }
        public DateTimeOffset? UpperObjectiveFirstHitUtc { get; }
        public bool LowerObjectiveAvailable => LowerObjectiveFirstHitUtc.HasValue;
        public bool UpperObjectiveAvailable => UpperObjectiveFirstHitUtc.HasValue;
    }

    /// <summary>
    /// Research-only causal 08:45 transition study. A signal is generated from completed one-minute bars only,
    /// and the reference entry is the next bar open. The detector never looks ahead to choose Continue/Reverse.
    /// </summary>
    public sealed class NewYorkEightFortyFiveTransitionAnalyzer
    {
        private static readonly TimeSpan OpeningStart = new TimeSpan(8, 30, 0);
        private static readonly TimeSpan DecisionStart = new TimeSpan(8, 45, 0);
        private static readonly TimeSpan DecisionEnd = new TimeSpan(9, 5, 0);
        private static readonly TimeSpan OutcomeEnd = new TimeSpan(9, 30, 0);
        private readonly NewYorkEightFortyFiveTransitionConfig config;

        public NewYorkEightFortyFiveTransitionAnalyzer(NewYorkEightFortyFiveTransitionConfig? config = null)
        {
            this.config = config ?? new NewYorkEightFortyFiveTransitionConfig();
        }

        public IReadOnlyList<NewYorkEightFortyFiveTransitionOutcome> Analyze(IReadOnlyList<HistoricalBar> bars)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (bars.Count == 0) return Array.Empty<NewYorkEightFortyFiveTransitionOutcome>();

            var first = bars[0];
            if (bars.Any(x => !string.Equals(x.Instrument, first.Instrument, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("08:45 transition research requires one instrument per dataset.");
            if (bars.Any(x => x.IntervalSeconds != first.IntervalSeconds))
                throw new InvalidOperationException("08:45 transition research requires one bar interval per dataset.");

            var central = ResolveCentralTimeZone();
            var localized = bars.Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime)).OrderBy(x => x.Local).ToList();
            var result = new List<NewYorkEightFortyFiveTransitionOutcome>();

            foreach (var group in localized.GroupBy(x => x.Local.Date).OrderBy(x => x.Key))
            {
                var opening = group.Where(x => x.Local.TimeOfDay >= OpeningStart && x.Local.TimeOfDay < DecisionStart).OrderBy(x => x.Local).ToList();
                var decision = group.Where(x => x.Local.TimeOfDay >= DecisionStart && x.Local.TimeOfDay < DecisionEnd).OrderBy(x => x.Local).ToList();
                var outcome = group.Where(x => x.Local.TimeOfDay >= DecisionStart && x.Local.TimeOfDay < OutcomeEnd).OrderBy(x => x.Local).ToList();
                if (opening.Count < 2 || decision.Count < 2 || outcome.Count < 2) continue;

                result.Add(AnalyzeSession(group.Key, opening, decision, outcome));
            }

            return result;
        }

        private NewYorkEightFortyFiveTransitionOutcome AnalyzeSession(DateTime date, IReadOnlyList<LocalBar> opening, IReadOnlyList<LocalBar> decision, IReadOnlyList<LocalBar> outcome)
        {
            var openingOpen = opening[0].Bar.Open;
            var openingClose = opening[opening.Count - 1].Bar.Close;
            var openingHigh = opening.Max(x => x.Bar.High);
            var openingLow = opening.Min(x => x.Bar.Low);
            var openingRange = Math.Max(config.TickSize, openingHigh - openingLow);
            var signedDisplacement = openingClose - openingOpen;
            var openingDirection = signedDisplacement >= 0m ? NewYorkResearchDirection.Long : NewYorkResearchDirection.Short;
            var openingDisplacement = Math.Abs(signedDisplacement);
            var openingEfficiency = openingDisplacement / openingRange;
            var midpoint = (openingHigh + openingLow) / 2m;

            NewYorkEightFortyFiveState state = NewYorkEightFortyFiveState.StandAside;
            NewYorkResearchDirection tradeDirection = NewYorkResearchDirection.None;
            LocalBar? signalBar = null;

            if (openingEfficiency >= config.MinimumOpeningEfficiency)
            {
                foreach (var bar in decision)
                {
                    var continuationDistance = openingDirection == NewYorkResearchDirection.Long
                        ? bar.Bar.Close - openingHigh
                        : openingLow - bar.Bar.Close;

                    var reversalDistance = openingDirection == NewYorkResearchDirection.Long
                        ? openingClose - bar.Bar.Close
                        : bar.Bar.Close - openingClose;

                    var crossedMidpoint = openingDirection == NewYorkResearchDirection.Long
                        ? bar.Bar.Close <= midpoint - openingRange * config.ReversalMidpointBufferFraction
                        : bar.Bar.Close >= midpoint + openingRange * config.ReversalMidpointBufferFraction;

                    if (reversalDistance >= Math.Max(config.TickSize, openingDisplacement * config.ReversalRetracementFraction) && crossedMidpoint)
                    {
                        state = NewYorkEightFortyFiveState.Reverse;
                        tradeDirection = Opposite(openingDirection);
                        signalBar = bar;
                        break;
                    }

                    if (continuationDistance >= openingRange * config.ContinuationExtensionFraction)
                    {
                        state = NewYorkEightFortyFiveState.Continue;
                        tradeDirection = openingDirection;
                        signalBar = bar;
                        break;
                    }
                }
            }

            if (signalBar == null)
            {
                return new NewYorkEightFortyFiveTransitionOutcome(date, state, openingDirection, tradeDirection, null, null,
                    openingRange, openingDisplacement, openingEfficiency, 0m, 0m, null, null);
            }

            var signalIndex = outcome.FindIndex(x => x.Bar.TimestampUtc == signalBar.Bar.TimestampUtc);
            if (signalIndex < 0 || signalIndex + 1 >= outcome.Count)
            {
                return new NewYorkEightFortyFiveTransitionOutcome(date, NewYorkEightFortyFiveState.StandAside, openingDirection,
                    NewYorkResearchDirection.None, signalBar.Bar.TimestampUtc, null, openingRange, openingDisplacement,
                    openingEfficiency, 0m, 0m, null, null);
            }

            var entry = outcome[signalIndex + 1];
            var forward = outcome.Skip(signalIndex + 1).ToList();
            var entryPrice = entry.Bar.Open;
            var favorablePoints = tradeDirection == NewYorkResearchDirection.Long
                ? Math.Max(0m, forward.Max(x => x.Bar.High) - entryPrice)
                : Math.Max(0m, entryPrice - forward.Min(x => x.Bar.Low));
            var adversePoints = tradeDirection == NewYorkResearchDirection.Long
                ? Math.Max(0m, entryPrice - forward.Min(x => x.Bar.Low))
                : Math.Max(0m, forward.Max(x => x.Bar.High) - entryPrice);

            var lowerPoints = config.LowerObjective / (config.PointValuePerContract * config.Contracts);
            var upperPoints = config.UpperObjective / (config.PointValuePerContract * config.Contracts);
            var lowerHit = FirstHit(forward, entryPrice, tradeDirection, lowerPoints);
            var upperHit = FirstHit(forward, entryPrice, tradeDirection, upperPoints);

            return new NewYorkEightFortyFiveTransitionOutcome(date, state, openingDirection, tradeDirection,
                signalBar.Bar.TimestampUtc, entry.Bar.TimestampUtc, openingRange, openingDisplacement, openingEfficiency,
                favorablePoints, adversePoints, lowerHit, upperHit);
        }

        private static DateTimeOffset? FirstHit(IReadOnlyList<LocalBar> bars, decimal entryPrice, NewYorkResearchDirection direction, decimal targetPoints)
        {
            foreach (var bar in bars)
            {
                var achieved = direction == NewYorkResearchDirection.Long
                    ? bar.Bar.High - entryPrice
                    : entryPrice - bar.Bar.Low;
                if (achieved >= targetPoints) return bar.Bar.TimestampUtc;
            }
            return null;
        }

        private static NewYorkResearchDirection Opposite(NewYorkResearchDirection direction)
        {
            return direction == NewYorkResearchDirection.Long ? NewYorkResearchDirection.Short : NewYorkResearchDirection.Long;
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

    internal static class LocalBarListExtensions
    {
        public static int FindIndex<T>(this IReadOnlyList<T> items, Func<T, bool> predicate)
        {
            for (var i = 0; i < items.Count; i++) if (predicate(items[i])) return i;
            return -1;
        }
    }
}
