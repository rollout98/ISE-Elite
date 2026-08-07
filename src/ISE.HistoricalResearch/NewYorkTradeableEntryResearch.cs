using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum NewYorkTradeableEntryType
    {
        None = 0,
        ContinuationAfterValidatedReset = 1,
        DirectReversal = 2,
        ContinuationFailureReversal = 3
    }

    public enum NewYorkTradeSequenceResult
    {
        None = 0,
        StopFirst = 1,
        LowerObjectiveFirst = 2,
        UpperObjectiveFirst = 3,
        TimedOut = 4
    }

    public sealed class NewYorkTradeableEntryConfig
    {
        public NewYorkTradeableEntryConfig(
            decimal minimumResetFraction = 0.20m,
            decimal maximumResetFraction = 0.60m,
            int pivotBarsEachSide = 1,
            int resumptionLookbackBars = 2,
            int reversalConfirmationBars = 1,
            decimal stopBufferTicks = 1m,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2.00m,
            int contracts = 2,
            decimal lowerObjective = 500m,
            decimal upperObjective = 1000m)
        {
            if (minimumResetFraction <= 0m) throw new ArgumentOutOfRangeException(nameof(minimumResetFraction));
            if (maximumResetFraction <= minimumResetFraction) throw new ArgumentOutOfRangeException(nameof(maximumResetFraction));
            if (pivotBarsEachSide <= 0) throw new ArgumentOutOfRangeException(nameof(pivotBarsEachSide));
            if (resumptionLookbackBars <= 0) throw new ArgumentOutOfRangeException(nameof(resumptionLookbackBars));
            if (reversalConfirmationBars <= 0) throw new ArgumentOutOfRangeException(nameof(reversalConfirmationBars));
            if (stopBufferTicks < 0m) throw new ArgumentOutOfRangeException(nameof(stopBufferTicks));
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (pointValuePerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(pointValuePerContract));
            if (contracts <= 0) throw new ArgumentOutOfRangeException(nameof(contracts));
            if (lowerObjective <= 0m) throw new ArgumentOutOfRangeException(nameof(lowerObjective));
            if (upperObjective < lowerObjective) throw new ArgumentOutOfRangeException(nameof(upperObjective));

            MinimumResetFraction = minimumResetFraction;
            MaximumResetFraction = maximumResetFraction;
            PivotBarsEachSide = pivotBarsEachSide;
            ResumptionLookbackBars = resumptionLookbackBars;
            ReversalConfirmationBars = reversalConfirmationBars;
            StopBufferTicks = stopBufferTicks;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
            LowerObjective = lowerObjective;
            UpperObjective = upperObjective;
        }

        public decimal MinimumResetFraction { get; }
        public decimal MaximumResetFraction { get; }
        public int PivotBarsEachSide { get; }
        public int ResumptionLookbackBars { get; }
        public int ReversalConfirmationBars { get; }
        public decimal StopBufferTicks { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal LowerObjective { get; }
        public decimal UpperObjective { get; }
    }

    public sealed class NewYorkTradeableEntryOutcome
    {
        public NewYorkTradeableEntryOutcome(
            DateTime sessionDateCentral,
            NewYorkEightFortyFiveState transitionState,
            NewYorkTradeableEntryType entryType,
            NewYorkResearchDirection direction,
            DateTimeOffset? transitionSignalUtc,
            DateTimeOffset? pivotUtc,
            DateTimeOffset? setupCompleteUtc,
            DateTimeOffset? entryUtc,
            decimal entryPrice,
            decimal stopPrice,
            decimal resetFraction,
            bool continuationInvalidated,
            DateTimeOffset? continuationInvalidatedUtc,
            NewYorkTradeSequenceResult sequenceResult,
            DateTimeOffset? sequenceResolvedUtc,
            DateTimeOffset? lowerObjectiveFirstHitUtc,
            DateTimeOffset? upperObjectiveFirstHitUtc,
            DateTimeOffset? stopFirstHitUtc)
        {
            SessionDateCentral = sessionDateCentral.Date;
            TransitionState = transitionState;
            EntryType = entryType;
            Direction = direction;
            TransitionSignalUtc = transitionSignalUtc;
            PivotUtc = pivotUtc;
            SetupCompleteUtc = setupCompleteUtc;
            EntryUtc = entryUtc;
            EntryPrice = entryPrice;
            StopPrice = stopPrice;
            ResetFraction = resetFraction;
            ContinuationInvalidated = continuationInvalidated;
            ContinuationInvalidatedUtc = continuationInvalidatedUtc;
            SequenceResult = sequenceResult;
            SequenceResolvedUtc = sequenceResolvedUtc;
            LowerObjectiveFirstHitUtc = lowerObjectiveFirstHitUtc;
            UpperObjectiveFirstHitUtc = upperObjectiveFirstHitUtc;
            StopFirstHitUtc = stopFirstHitUtc;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkEightFortyFiveState TransitionState { get; }
        public NewYorkTradeableEntryType EntryType { get; }
        public NewYorkResearchDirection Direction { get; }
        public DateTimeOffset? TransitionSignalUtc { get; }
        public DateTimeOffset? PivotUtc { get; }
        public DateTimeOffset? SetupCompleteUtc { get; }
        public DateTimeOffset? EntryUtc { get; }
        public decimal EntryPrice { get; }
        public decimal StopPrice { get; }
        public decimal ResetFraction { get; }
        public bool ContinuationInvalidated { get; }
        public DateTimeOffset? ContinuationInvalidatedUtc { get; }
        public NewYorkTradeSequenceResult SequenceResult { get; }
        public DateTimeOffset? SequenceResolvedUtc { get; }
        public DateTimeOffset? LowerObjectiveFirstHitUtc { get; }
        public DateTimeOffset? UpperObjectiveFirstHitUtc { get; }
        public DateTimeOffset? StopFirstHitUtc { get; }
        public bool HasEntry => EntryUtc.HasValue;
        public bool LowerObjectiveBeforeStop => LowerObjectiveFirstHitUtc.HasValue && (!StopFirstHitUtc.HasValue || LowerObjectiveFirstHitUtc.Value < StopFirstHitUtc.Value);
        public bool UpperObjectiveBeforeStop => UpperObjectiveFirstHitUtc.HasValue && (!StopFirstHitUtc.HasValue || UpperObjectiveFirstHitUtc.Value < StopFirstHitUtc.Value);
        public decimal InitialRiskTicks(decimal tickSize) => !HasEntry || tickSize <= 0m ? 0m : Math.Abs(EntryPrice - StopPrice) / tickSize;
    }

    /// <summary>
    /// Research-only tradeability layer. Continue requires a bounded reset, a confirmed local pivot,
    /// and structural resumption. A destructive reset hands off to reversal confirmation instead of
    /// ending the session. Entry is the next bar open after setup completion. The study then records
    /// whether the structural stop or daily-objective thresholds occur first. If stop and target are
    /// touched in the same one-minute bar, the study resolves conservatively as stop-first.
    /// </summary>
    public sealed class NewYorkTradeableEntryAnalyzer
    {
        private static readonly TimeSpan OpeningStart = new TimeSpan(8, 30, 0);
        private static readonly TimeSpan SearchStart = new TimeSpan(8, 45, 0);
        private static readonly TimeSpan SearchEnd = new TimeSpan(9, 20, 0);
        private static readonly TimeSpan OutcomeEnd = new TimeSpan(9, 30, 0);
        private readonly NewYorkTradeableEntryConfig config;

        public NewYorkTradeableEntryAnalyzer(NewYorkTradeableEntryConfig? config = null)
        {
            this.config = config ?? new NewYorkTradeableEntryConfig();
        }

        public IReadOnlyList<NewYorkTradeableEntryOutcome> Analyze(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<NewYorkEightFortyFiveTransitionOutcome> transitions)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            if (bars.Count == 0 || transitions.Count == 0) return Array.Empty<NewYorkTradeableEntryOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars.Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .OrderBy(x => x.Local).ToList();
            var byDate = localized.GroupBy(x => x.Local.Date).ToDictionary(x => x.Key, x => x.ToList());
            var result = new List<NewYorkTradeableEntryOutcome>();

            foreach (var transition in transitions.OrderBy(x => x.SessionDateCentral))
            {
                if (!byDate.TryGetValue(transition.SessionDateCentral.Date, out var session)) continue;
                result.Add(AnalyzeSession(transition, session));
            }
            return result;
        }

        private NewYorkTradeableEntryOutcome AnalyzeSession(NewYorkEightFortyFiveTransitionOutcome transition, IReadOnlyList<LocalBar> session)
        {
            if (transition.State == NewYorkEightFortyFiveState.StandAside || !transition.SignalTimestampUtc.HasValue)
                return Empty(transition, false, null);

            var opening = session.Where(x => x.Local.TimeOfDay >= OpeningStart && x.Local.TimeOfDay < SearchStart).OrderBy(x => x.Local).ToList();
            var search = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < SearchEnd).OrderBy(x => x.Local).ToList();
            var outcome = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < OutcomeEnd).OrderBy(x => x.Local).ToList();
            if (opening.Count < 2 || search.Count < 3 || outcome.Count < 3) return Empty(transition, false, null);

            var signalIndex = search.FindIndex(x => x.Bar.TimestampUtc == transition.SignalTimestampUtc.Value);
            if (signalIndex < 0) return Empty(transition, false, null);

            SetupCandidate? candidate;
            bool invalidated = false;
            DateTimeOffset? invalidatedUtc = null;

            if (transition.State == NewYorkEightFortyFiveState.Continue)
            {
                candidate = FindContinuation(search, signalIndex, transition.TradeDirection, transition.OpeningRange, out invalidatedUtc);
                invalidated = invalidatedUtc.HasValue;
                if (candidate == null && invalidatedUtc.HasValue)
                {
                    var invalidatedIndex = search.FindIndex(x => x.Bar.TimestampUtc == invalidatedUtc.Value);
                    if (invalidatedIndex >= 0)
                        candidate = FindReversal(search, invalidatedIndex, Opposite(transition.TradeDirection), NewYorkTradeableEntryType.ContinuationFailureReversal);
                }
            }
            else
            {
                candidate = FindReversal(search, signalIndex, transition.TradeDirection, NewYorkTradeableEntryType.DirectReversal);
            }

            if (candidate == null) return Empty(transition, invalidated, invalidatedUtc);

            var setupIndex = outcome.FindIndex(x => x.Bar.TimestampUtc == candidate.SetupComplete.Bar.TimestampUtc);
            if (setupIndex < 0 || setupIndex + 1 >= outcome.Count) return Empty(transition, invalidated, invalidatedUtc);

            var entry = outcome[setupIndex + 1];
            var entryPrice = entry.Bar.Open;
            var stopBuffer = config.StopBufferTicks * config.TickSize;
            var stopPrice = candidate.Direction == NewYorkResearchDirection.Long
                ? candidate.StructurePrice - stopBuffer
                : candidate.StructurePrice + stopBuffer;

            var lowerPoints = config.LowerObjective / (config.PointValuePerContract * config.Contracts);
            var upperPoints = config.UpperObjective / (config.PointValuePerContract * config.Contracts);
            var sequence = ResolveSequence(outcome.Skip(setupIndex + 1).ToList(), entryPrice, stopPrice, candidate.Direction, lowerPoints, upperPoints);

            return new NewYorkTradeableEntryOutcome(
                transition.SessionDateCentral,
                transition.State,
                candidate.EntryType,
                candidate.Direction,
                transition.SignalTimestampUtc,
                candidate.Pivot?.Bar.TimestampUtc,
                candidate.SetupComplete.Bar.TimestampUtc,
                entry.Bar.TimestampUtc,
                entryPrice,
                stopPrice,
                candidate.ResetFraction,
                invalidated,
                invalidatedUtc,
                sequence.Result,
                sequence.ResolvedUtc,
                sequence.LowerUtc,
                sequence.UpperUtc,
                sequence.StopUtc);
        }

        private SetupCandidate? FindContinuation(
            IReadOnlyList<LocalBar> bars,
            int signalIndex,
            NewYorkResearchDirection direction,
            decimal openingRange,
            out DateTimeOffset? invalidatedUtc)
        {
            invalidatedUtc = null;
            var minReset = Math.Max(config.TickSize, openingRange * config.MinimumResetFraction);
            var maxReset = Math.Max(minReset + config.TickSize, openingRange * config.MaximumResetFraction);
            var favorableExtreme = direction == NewYorkResearchDirection.Long ? bars[signalIndex].Bar.High : bars[signalIndex].Bar.Low;

            for (var i = signalIndex + 1; i < bars.Count; i++)
            {
                if (direction == NewYorkResearchDirection.Long)
                {
                    favorableExtreme = Math.Max(favorableExtreme, bars[i].Bar.High);
                    var depth = favorableExtreme - bars[i].Bar.Low;
                    if (depth > maxReset)
                    {
                        invalidatedUtc = bars[i].Bar.TimestampUtc;
                        return null;
                    }
                }
                else
                {
                    favorableExtreme = Math.Min(favorableExtreme, bars[i].Bar.Low);
                    var depth = bars[i].Bar.High - favorableExtreme;
                    if (depth > maxReset)
                    {
                        invalidatedUtc = bars[i].Bar.TimestampUtc;
                        return null;
                    }
                }

                if (i < config.PivotBarsEachSide || i + config.PivotBarsEachSide >= bars.Count) continue;
                if (!IsPivot(bars, i, direction)) continue;

                var pivot = bars[i];
                var resetDepth = direction == NewYorkResearchDirection.Long
                    ? favorableExtreme - pivot.Bar.Low
                    : pivot.Bar.High - favorableExtreme;
                if (resetDepth < minReset || resetDepth > maxReset) continue;

                for (var j = i + config.PivotBarsEachSide + 1; j < bars.Count; j++)
                {
                    var depthNow = direction == NewYorkResearchDirection.Long
                        ? favorableExtreme - bars[j].Bar.Low
                        : bars[j].Bar.High - favorableExtreme;
                    if (depthNow > maxReset)
                    {
                        invalidatedUtc = bars[j].Bar.TimestampUtc;
                        return null;
                    }
                    if (j < config.ResumptionLookbackBars) continue;
                    var prior = bars.Skip(j - config.ResumptionLookbackBars).Take(config.ResumptionLookbackBars).ToList();
                    var breaks = direction == NewYorkResearchDirection.Long
                        ? bars[j].Bar.Close > prior.Max(x => x.Bar.High)
                        : bars[j].Bar.Close < prior.Min(x => x.Bar.Low);
                    if (breaks)
                    {
                        var structurePrice = direction == NewYorkResearchDirection.Long ? pivot.Bar.Low : pivot.Bar.High;
                        return new SetupCandidate(NewYorkTradeableEntryType.ContinuationAfterValidatedReset, direction, pivot, bars[j], structurePrice, resetDepth / openingRange);
                    }
                }
            }
            return null;
        }

        private SetupCandidate? FindReversal(IReadOnlyList<LocalBar> bars, int startIndex, NewYorkResearchDirection direction, NewYorkTradeableEntryType entryType)
        {
            var confirmed = 0;
            for (var i = startIndex + 1; i < bars.Count; i++)
            {
                var confirms = direction == NewYorkResearchDirection.Long
                    ? bars[i].Bar.Close > bars[i - 1].Bar.High
                    : bars[i].Bar.Close < bars[i - 1].Bar.Low;
                confirmed = confirms ? confirmed + 1 : 0;
                if (confirmed < config.ReversalConfirmationBars) continue;

                var structurePrice = direction == NewYorkResearchDirection.Long
                    ? bars.Skip(startIndex).Take(i - startIndex + 1).Min(x => x.Bar.Low)
                    : bars.Skip(startIndex).Take(i - startIndex + 1).Max(x => x.Bar.High);
                return new SetupCandidate(entryType, direction, null, bars[i], structurePrice, 0m);
            }
            return null;
        }

        private bool IsPivot(IReadOnlyList<LocalBar> bars, int index, NewYorkResearchDirection direction)
        {
            var candidate = bars[index];
            for (var offset = 1; offset <= config.PivotBarsEachSide; offset++)
            {
                if (direction == NewYorkResearchDirection.Long)
                {
                    if (candidate.Bar.Low > bars[index - offset].Bar.Low || candidate.Bar.Low > bars[index + offset].Bar.Low) return false;
                }
                else
                {
                    if (candidate.Bar.High < bars[index - offset].Bar.High || candidate.Bar.High < bars[index + offset].Bar.High) return false;
                }
            }
            return true;
        }

        private SequenceResolution ResolveSequence(
            IReadOnlyList<LocalBar> bars,
            decimal entryPrice,
            decimal stopPrice,
            NewYorkResearchDirection direction,
            decimal lowerPoints,
            decimal upperPoints)
        {
            DateTimeOffset? lower = null;
            DateTimeOffset? upper = null;
            DateTimeOffset? stop = null;

            foreach (var bar in bars)
            {
                var stopHit = direction == NewYorkResearchDirection.Long ? bar.Bar.Low <= stopPrice : bar.Bar.High >= stopPrice;
                var lowerHit = direction == NewYorkResearchDirection.Long ? bar.Bar.High >= entryPrice + lowerPoints : bar.Bar.Low <= entryPrice - lowerPoints;
                var upperHit = direction == NewYorkResearchDirection.Long ? bar.Bar.High >= entryPrice + upperPoints : bar.Bar.Low <= entryPrice - upperPoints;

                if (stopHit)
                {
                    stop = bar.Bar.TimestampUtc;
                    return new SequenceResolution(NewYorkTradeSequenceResult.StopFirst, stop.Value, lower, upper, stop);
                }
                if (lower == null && lowerHit) lower = bar.Bar.TimestampUtc;
                if (upper == null && upperHit) upper = bar.Bar.TimestampUtc;
                if (upper.HasValue)
                    return new SequenceResolution(NewYorkTradeSequenceResult.UpperObjectiveFirst, upper.Value, lower, upper, stop);
                if (lower.HasValue)
                    return new SequenceResolution(NewYorkTradeSequenceResult.LowerObjectiveFirst, lower.Value, lower, upper, stop);
            }

            return new SequenceResolution(NewYorkTradeSequenceResult.TimedOut, bars.Count == 0 ? (DateTimeOffset?)null : bars[bars.Count - 1].Bar.TimestampUtc, lower, upper, stop);
        }

        private static NewYorkTradeableEntryOutcome Empty(NewYorkEightFortyFiveTransitionOutcome transition, bool invalidated, DateTimeOffset? invalidatedUtc)
        {
            return new NewYorkTradeableEntryOutcome(transition.SessionDateCentral, transition.State, NewYorkTradeableEntryType.None,
                NewYorkResearchDirection.None, transition.SignalTimestampUtc, null, null, null, 0m, 0m, 0m, invalidated,
                invalidatedUtc, NewYorkTradeSequenceResult.None, null, null, null, null);
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

        private sealed class SetupCandidate
        {
            public SetupCandidate(NewYorkTradeableEntryType entryType, NewYorkResearchDirection direction, LocalBar? pivot, LocalBar setupComplete, decimal structurePrice, decimal resetFraction)
            {
                EntryType = entryType;
                Direction = direction;
                Pivot = pivot;
                SetupComplete = setupComplete;
                StructurePrice = structurePrice;
                ResetFraction = resetFraction;
            }
            public NewYorkTradeableEntryType EntryType { get; }
            public NewYorkResearchDirection Direction { get; }
            public LocalBar? Pivot { get; }
            public LocalBar SetupComplete { get; }
            public decimal StructurePrice { get; }
            public decimal ResetFraction { get; }
        }

        private sealed class SequenceResolution
        {
            public SequenceResolution(NewYorkTradeSequenceResult result, DateTimeOffset? resolvedUtc, DateTimeOffset? lowerUtc, DateTimeOffset? upperUtc, DateTimeOffset? stopUtc)
            {
                Result = result;
                ResolvedUtc = resolvedUtc;
                LowerUtc = lowerUtc;
                UpperUtc = upperUtc;
                StopUtc = stopUtc;
            }
            public NewYorkTradeSequenceResult Result { get; }
            public DateTimeOffset? ResolvedUtc { get; }
            public DateTimeOffset? LowerUtc { get; }
            public DateTimeOffset? UpperUtc { get; }
            public DateTimeOffset? StopUtc { get; }
        }

        private sealed class LocalBar
        {
            public LocalBar(HistoricalBar bar, DateTime local) { Bar = bar; Local = local; }
            public HistoricalBar Bar { get; }
            public DateTime Local { get; }
        }
    }
}
