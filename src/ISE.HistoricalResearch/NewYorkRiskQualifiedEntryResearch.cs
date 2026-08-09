using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum NewYorkRiskQualifiedDisposition
    {
        None = 0,
        Accepted = 1,
        RejectedRisk = 2,
        HandoffNoSetup = 3,
        NoEntry = 4
    }

    public enum NewYorkRiskSequenceResult
    {
        None = 0,
        StopFirst = 1,
        IntermediateObjectiveFirst = 2,
        TimedOut = 3
    }

    public sealed class NewYorkRiskQualifiedEntryConfig
    {
        public NewYorkRiskQualifiedEntryConfig(
            decimal maximumInitialRiskTicks = 200m,
            decimal intermediateObjective = 300m,
            decimal lowerObjective = 500m,
            decimal upperObjective = 1000m,
            decimal handoffImpulseOpeningRangeFraction = 0.20m,
            decimal handoffRetestImpulseFraction = 0.20m,
            int handoffResumptionLookbackBars = 2,
            decimal stopBufferTicks = 1m,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2.00m,
            int contracts = 2)
        {
            if (maximumInitialRiskTicks <= 0m) throw new ArgumentOutOfRangeException(nameof(maximumInitialRiskTicks));
            if (intermediateObjective <= 0m) throw new ArgumentOutOfRangeException(nameof(intermediateObjective));
            if (lowerObjective < intermediateObjective) throw new ArgumentOutOfRangeException(nameof(lowerObjective));
            if (upperObjective < lowerObjective) throw new ArgumentOutOfRangeException(nameof(upperObjective));
            if (handoffImpulseOpeningRangeFraction <= 0m) throw new ArgumentOutOfRangeException(nameof(handoffImpulseOpeningRangeFraction));
            if (handoffRetestImpulseFraction <= 0m || handoffRetestImpulseFraction >= 1m) throw new ArgumentOutOfRangeException(nameof(handoffRetestImpulseFraction));
            if (handoffResumptionLookbackBars <= 0) throw new ArgumentOutOfRangeException(nameof(handoffResumptionLookbackBars));
            if (stopBufferTicks < 0m) throw new ArgumentOutOfRangeException(nameof(stopBufferTicks));
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (pointValuePerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(pointValuePerContract));
            if (contracts <= 0) throw new ArgumentOutOfRangeException(nameof(contracts));

            MaximumInitialRiskTicks = maximumInitialRiskTicks;
            IntermediateObjective = intermediateObjective;
            LowerObjective = lowerObjective;
            UpperObjective = upperObjective;
            HandoffImpulseOpeningRangeFraction = handoffImpulseOpeningRangeFraction;
            HandoffRetestImpulseFraction = handoffRetestImpulseFraction;
            HandoffResumptionLookbackBars = handoffResumptionLookbackBars;
            StopBufferTicks = stopBufferTicks;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
        }

        public decimal MaximumInitialRiskTicks { get; }
        public decimal IntermediateObjective { get; }
        public decimal LowerObjective { get; }
        public decimal UpperObjective { get; }
        public decimal HandoffImpulseOpeningRangeFraction { get; }
        public decimal HandoffRetestImpulseFraction { get; }
        public int HandoffResumptionLookbackBars { get; }
        public decimal StopBufferTicks { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
    }

    public sealed class NewYorkRiskQualifiedEntryOutcome
    {
        public NewYorkRiskQualifiedEntryOutcome(
            DateTime sessionDateCentral,
            NewYorkTradeableEntryType entryType,
            NewYorkRiskQualifiedDisposition disposition,
            NewYorkResearchDirection direction,
            DateTimeOffset? setupCompleteUtc,
            DateTimeOffset? entryUtc,
            decimal entryPrice,
            decimal stopPrice,
            decimal initialRiskTicks,
            NewYorkRiskSequenceResult sequenceResult,
            DateTimeOffset? resolvedUtc,
            DateTimeOffset? intermediateHitUtc,
            DateTimeOffset? lowerHitUtc,
            DateTimeOffset? upperHitUtc,
            DateTimeOffset? stopHitUtc)
        {
            SessionDateCentral = sessionDateCentral.Date;
            EntryType = entryType;
            Disposition = disposition;
            Direction = direction;
            SetupCompleteUtc = setupCompleteUtc;
            EntryUtc = entryUtc;
            EntryPrice = entryPrice;
            StopPrice = stopPrice;
            InitialRiskTicks = initialRiskTicks;
            SequenceResult = sequenceResult;
            ResolvedUtc = resolvedUtc;
            IntermediateHitUtc = intermediateHitUtc;
            LowerHitUtc = lowerHitUtc;
            UpperHitUtc = upperHitUtc;
            StopHitUtc = stopHitUtc;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkTradeableEntryType EntryType { get; }
        public NewYorkRiskQualifiedDisposition Disposition { get; }
        public NewYorkResearchDirection Direction { get; }
        public DateTimeOffset? SetupCompleteUtc { get; }
        public DateTimeOffset? EntryUtc { get; }
        public decimal EntryPrice { get; }
        public decimal StopPrice { get; }
        public decimal InitialRiskTicks { get; }
        public NewYorkRiskSequenceResult SequenceResult { get; }
        public DateTimeOffset? ResolvedUtc { get; }
        public DateTimeOffset? IntermediateHitUtc { get; }
        public DateTimeOffset? LowerHitUtc { get; }
        public DateTimeOffset? UpperHitUtc { get; }
        public DateTimeOffset? StopHitUtc { get; }
        public bool HasAcceptedEntry => Disposition == NewYorkRiskQualifiedDisposition.Accepted && EntryUtc.HasValue;
        public bool IntermediateBeforeStop => IntermediateHitUtc.HasValue && (!StopHitUtc.HasValue || IntermediateHitUtc.Value < StopHitUtc.Value);
        public bool LowerBeforeStop => LowerHitUtc.HasValue && (!StopHitUtc.HasValue || LowerHitUtc.Value < StopHitUtc.Value);
        public bool UpperBeforeStop => UpperHitUtc.HasValue && (!StopHitUtc.HasValue || UpperHitUtc.Value < StopHitUtc.Value);
    }

    /// <summary>
    /// Research-only selection layer applied after the tradeability study. It rejects entries whose
    /// structural stop requires more than the configured risk ceiling. Continuation-failure reversals
    /// are rebuilt from the invalidation point and must form an opposite impulse, a retest, and a
    /// structural resumption before entry. Accepted entries are sequenced against $300/$500/$1000
    /// objectives and the structural stop through 09:30 CT, resolving same-bar stop/target ambiguity
    /// conservatively as stop-first.
    /// </summary>
    public sealed class NewYorkRiskQualifiedEntryAnalyzer
    {
        private static readonly TimeSpan SearchStart = new TimeSpan(8, 45, 0);
        private static readonly TimeSpan SearchEnd = new TimeSpan(9, 20, 0);
        private static readonly TimeSpan OutcomeEnd = new TimeSpan(9, 30, 0);
        private readonly NewYorkRiskQualifiedEntryConfig config;

        public NewYorkRiskQualifiedEntryAnalyzer(NewYorkRiskQualifiedEntryConfig? config = null)
        {
            this.config = config ?? new NewYorkRiskQualifiedEntryConfig();
        }

        public IReadOnlyList<NewYorkRiskQualifiedEntryOutcome> Analyze(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<NewYorkEightFortyFiveTransitionOutcome> transitions,
            IReadOnlyList<NewYorkTradeableEntryOutcome> tradeable)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            if (tradeable == null) throw new ArgumentNullException(nameof(tradeable));
            if (bars.Count == 0 || transitions.Count == 0 || tradeable.Count == 0) return Array.Empty<NewYorkRiskQualifiedEntryOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars.Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .OrderBy(x => x.Local).ToList();
            var byDate = localized.GroupBy(x => x.Local.Date).ToDictionary(x => x.Key, x => x.ToList());
            var transitionByDate = transitions.ToDictionary(x => x.SessionDateCentral.Date, x => x);
            var result = new List<NewYorkRiskQualifiedEntryOutcome>();

            foreach (var prior in tradeable.OrderBy(x => x.SessionDateCentral))
            {
                if (!byDate.TryGetValue(prior.SessionDateCentral.Date, out var session)) continue;
                if (!transitionByDate.TryGetValue(prior.SessionDateCentral.Date, out var transition)) continue;
                result.Add(AnalyzeSession(session, transition, prior));
            }
            return result;
        }

        private NewYorkRiskQualifiedEntryOutcome AnalyzeSession(
            IReadOnlyList<LocalBar> session,
            NewYorkEightFortyFiveTransitionOutcome transition,
            NewYorkTradeableEntryOutcome prior)
        {
            if (!prior.HasEntry)
                return Empty(prior.SessionDateCentral, prior.EntryType, NewYorkRiskQualifiedDisposition.NoEntry);

            if (prior.EntryType == NewYorkTradeableEntryType.ContinuationFailureReversal)
                return AnalyzeHandoff(session, transition, prior);

            return QualifyExisting(session, prior);
        }

        private NewYorkRiskQualifiedEntryOutcome QualifyExisting(IReadOnlyList<LocalBar> session, NewYorkTradeableEntryOutcome prior)
        {
            var riskTicks = prior.InitialRiskTicks(config.TickSize);
            if (riskTicks > config.MaximumInitialRiskTicks)
                return RejectedRisk(prior.SessionDateCentral, prior.EntryType, prior.Direction, prior.SetupCompleteUtc, prior.EntryUtc,
                    prior.EntryPrice, prior.StopPrice, riskTicks);

            return SequenceAccepted(session, prior.SessionDateCentral, prior.EntryType, prior.Direction, prior.SetupCompleteUtc,
                prior.EntryUtc!.Value, prior.EntryPrice, prior.StopPrice, riskTicks);
        }

        private NewYorkRiskQualifiedEntryOutcome AnalyzeHandoff(
            IReadOnlyList<LocalBar> session,
            NewYorkEightFortyFiveTransitionOutcome transition,
            NewYorkTradeableEntryOutcome prior)
        {
            if (!prior.ContinuationInvalidatedUtc.HasValue)
                return Empty(prior.SessionDateCentral, prior.EntryType, NewYorkRiskQualifiedDisposition.HandoffNoSetup);

            var search = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < SearchEnd).OrderBy(x => x.Local).ToList();
            var startIndex = search.FindIndex(x => x.Bar.TimestampUtc == prior.ContinuationInvalidatedUtc.Value);
            if (startIndex < 0)
                return Empty(prior.SessionDateCentral, prior.EntryType, NewYorkRiskQualifiedDisposition.HandoffNoSetup);

            var direction = Opposite(transition.TradeDirection);
            var setup = FindHandoffSetup(search, startIndex, direction, transition.OpeningRange);
            if (setup == null)
                return Empty(prior.SessionDateCentral, prior.EntryType, NewYorkRiskQualifiedDisposition.HandoffNoSetup);

            var outcome = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < OutcomeEnd).OrderBy(x => x.Local).ToList();
            var setupIndex = outcome.FindIndex(x => x.Bar.TimestampUtc == setup.SetupComplete.Bar.TimestampUtc);
            if (setupIndex < 0 || setupIndex + 1 >= outcome.Count)
                return Empty(prior.SessionDateCentral, prior.EntryType, NewYorkRiskQualifiedDisposition.HandoffNoSetup);

            var entry = outcome[setupIndex + 1];
            var stopBuffer = config.StopBufferTicks * config.TickSize;
            var stopPrice = direction == NewYorkResearchDirection.Long ? setup.RetestExtreme - stopBuffer : setup.RetestExtreme + stopBuffer;
            var riskTicks = Math.Abs(entry.Bar.Open - stopPrice) / config.TickSize;
            if (riskTicks > config.MaximumInitialRiskTicks)
                return RejectedRisk(prior.SessionDateCentral, prior.EntryType, direction, setup.SetupComplete.Bar.TimestampUtc,
                    entry.Bar.TimestampUtc, entry.Bar.Open, stopPrice, riskTicks);

            return SequenceAccepted(session, prior.SessionDateCentral, prior.EntryType, direction, setup.SetupComplete.Bar.TimestampUtc,
                entry.Bar.TimestampUtc, entry.Bar.Open, stopPrice, riskTicks);
        }

        private HandoffSetup? FindHandoffSetup(
            IReadOnlyList<LocalBar> bars,
            int startIndex,
            NewYorkResearchDirection direction,
            decimal openingRange)
        {
            var minimumImpulse = Math.Max(config.TickSize, openingRange * config.HandoffImpulseOpeningRangeFraction);
            var startPrice = bars[startIndex].Bar.Close;
            decimal favorableExtreme = startPrice;
            bool impulseSeen = false;
            bool retestSeen = false;
            decimal retestExtreme = startPrice;
            decimal frozenImpulseDistance = 0m;

            for (var i = startIndex + 1; i < bars.Count; i++)
            {
                var bar = bars[i];
                if (!retestSeen)
                {
                    if (direction == NewYorkResearchDirection.Long)
                    {
                        favorableExtreme = Math.Max(favorableExtreme, bar.Bar.High);
                        var impulse = favorableExtreme - startPrice;
                        if (impulse >= minimumImpulse) impulseSeen = true;
                        if (impulseSeen && favorableExtreme - bar.Bar.Low >= impulse * config.HandoffRetestImpulseFraction)
                        {
                            retestSeen = true;
                            frozenImpulseDistance = impulse;
                            retestExtreme = bar.Bar.Low;
                        }
                    }
                    else
                    {
                        favorableExtreme = Math.Min(favorableExtreme, bar.Bar.Low);
                        var impulse = startPrice - favorableExtreme;
                        if (impulse >= minimumImpulse) impulseSeen = true;
                        if (impulseSeen && bar.Bar.High - favorableExtreme >= impulse * config.HandoffRetestImpulseFraction)
                        {
                            retestSeen = true;
                            frozenImpulseDistance = impulse;
                            retestExtreme = bar.Bar.High;
                        }
                    }
                    continue;
                }

                retestExtreme = direction == NewYorkResearchDirection.Long
                    ? Math.Min(retestExtreme, bar.Bar.Low)
                    : Math.Max(retestExtreme, bar.Bar.High);

                if (i < config.HandoffResumptionLookbackBars) continue;
                var prior = bars.Skip(i - config.HandoffResumptionLookbackBars).Take(config.HandoffResumptionLookbackBars).ToList();
                var resumes = direction == NewYorkResearchDirection.Long
                    ? bar.Bar.Close > prior.Max(x => x.Bar.High)
                    : bar.Bar.Close < prior.Min(x => x.Bar.Low);

                if (resumes && frozenImpulseDistance > 0m)
                    return new HandoffSetup(bar, retestExtreme);
            }
            return null;
        }

        private NewYorkRiskQualifiedEntryOutcome SequenceAccepted(
            IReadOnlyList<LocalBar> session,
            DateTime date,
            NewYorkTradeableEntryType type,
            NewYorkResearchDirection direction,
            DateTimeOffset? setupUtc,
            DateTimeOffset entryUtc,
            decimal entryPrice,
            decimal stopPrice,
            decimal riskTicks)
        {
            var outcome = session.Where(x => x.Local.TimeOfDay >= SearchStart && x.Local.TimeOfDay < OutcomeEnd).OrderBy(x => x.Local).ToList();
            var entryIndex = outcome.FindIndex(x => x.Bar.TimestampUtc == entryUtc);
            if (entryIndex < 0)
                return Empty(date, type, NewYorkRiskQualifiedDisposition.NoEntry);

            var forward = outcome.Skip(entryIndex).ToList();
            var intermediatePoints = config.IntermediateObjective / (config.PointValuePerContract * config.Contracts);
            var lowerPoints = config.LowerObjective / (config.PointValuePerContract * config.Contracts);
            var upperPoints = config.UpperObjective / (config.PointValuePerContract * config.Contracts);
            var sequence = ResolveSequence(forward, entryPrice, stopPrice, direction, intermediatePoints, lowerPoints, upperPoints);

            return new NewYorkRiskQualifiedEntryOutcome(date, type, NewYorkRiskQualifiedDisposition.Accepted, direction,
                setupUtc, entryUtc, entryPrice, stopPrice, riskTicks, sequence.Result, sequence.ResolvedUtc,
                sequence.IntermediateUtc, sequence.LowerUtc, sequence.UpperUtc, sequence.StopUtc);
        }

        private SequenceResolution ResolveSequence(
            IReadOnlyList<LocalBar> bars,
            decimal entryPrice,
            decimal stopPrice,
            NewYorkResearchDirection direction,
            decimal intermediatePoints,
            decimal lowerPoints,
            decimal upperPoints)
        {
            DateTimeOffset? intermediate = null;
            DateTimeOffset? lower = null;
            DateTimeOffset? upper = null;
            DateTimeOffset? stop = null;

            foreach (var bar in bars)
            {
                var stopHit = direction == NewYorkResearchDirection.Long ? bar.Bar.Low <= stopPrice : bar.Bar.High >= stopPrice;
                var intermediateHit = direction == NewYorkResearchDirection.Long ? bar.Bar.High >= entryPrice + intermediatePoints : bar.Bar.Low <= entryPrice - intermediatePoints;
                var lowerHit = direction == NewYorkResearchDirection.Long ? bar.Bar.High >= entryPrice + lowerPoints : bar.Bar.Low <= entryPrice - lowerPoints;
                var upperHit = direction == NewYorkResearchDirection.Long ? bar.Bar.High >= entryPrice + upperPoints : bar.Bar.Low <= entryPrice - upperPoints;

                if (stopHit)
                {
                    stop = bar.Bar.TimestampUtc;
                    return new SequenceResolution(NewYorkRiskSequenceResult.StopFirst, stop.Value, intermediate, lower, upper, stop);
                }

                if (intermediate == null && intermediateHit) intermediate = bar.Bar.TimestampUtc;
                if (lower == null && lowerHit) lower = bar.Bar.TimestampUtc;
                if (upper == null && upperHit) upper = bar.Bar.TimestampUtc;

                if (intermediate.HasValue)
                    return new SequenceResolution(NewYorkRiskSequenceResult.IntermediateObjectiveFirst, intermediate.Value, intermediate, lower, upper, stop);
            }

            return new SequenceResolution(NewYorkRiskSequenceResult.TimedOut,
                bars.Count == 0 ? (DateTimeOffset?)null : bars[bars.Count - 1].Bar.TimestampUtc,
                intermediate, lower, upper, stop);
        }

        private static NewYorkRiskQualifiedEntryOutcome RejectedRisk(
            DateTime date,
            NewYorkTradeableEntryType type,
            NewYorkResearchDirection direction,
            DateTimeOffset? setupUtc,
            DateTimeOffset? entryUtc,
            decimal entryPrice,
            decimal stopPrice,
            decimal riskTicks)
        {
            return new NewYorkRiskQualifiedEntryOutcome(date, type, NewYorkRiskQualifiedDisposition.RejectedRisk, direction,
                setupUtc, entryUtc, entryPrice, stopPrice, riskTicks, NewYorkRiskSequenceResult.None, null, null, null, null, null);
        }

        private static NewYorkRiskQualifiedEntryOutcome Empty(DateTime date, NewYorkTradeableEntryType type, NewYorkRiskQualifiedDisposition disposition)
        {
            return new NewYorkRiskQualifiedEntryOutcome(date, type, disposition, NewYorkResearchDirection.None,
                null, null, 0m, 0m, 0m, NewYorkRiskSequenceResult.None, null, null, null, null, null);
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

        private sealed class HandoffSetup
        {
            public HandoffSetup(LocalBar setupComplete, decimal retestExtreme)
            {
                SetupComplete = setupComplete;
                RetestExtreme = retestExtreme;
            }
            public LocalBar SetupComplete { get; }
            public decimal RetestExtreme { get; }
        }

        private sealed class SequenceResolution
        {
            public SequenceResolution(NewYorkRiskSequenceResult result, DateTimeOffset? resolvedUtc,
                DateTimeOffset? intermediateUtc, DateTimeOffset? lowerUtc, DateTimeOffset? upperUtc, DateTimeOffset? stopUtc)
            {
                Result = result;
                ResolvedUtc = resolvedUtc;
                IntermediateUtc = intermediateUtc;
                LowerUtc = lowerUtc;
                UpperUtc = upperUtc;
                StopUtc = stopUtc;
            }
            public NewYorkRiskSequenceResult Result { get; }
            public DateTimeOffset? ResolvedUtc { get; }
            public DateTimeOffset? IntermediateUtc { get; }
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
