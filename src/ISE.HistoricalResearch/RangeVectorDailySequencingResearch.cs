using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class RangeVectorDailySelectionConfig
    {
        public RangeVectorDailySelectionConfig(
            MorningResearchAccountStage stage,
            RangeEntryVectorFlowHoldConfig? indicatorConfig = null,
            int maximumAttempts = 2,
            decimal maximumStructuralRiskTicks = 0m,
            decimal tradeableScore = 0m,
            decimal actionableScore = 0m,
            decimal exceptionalScore = 0m,
            decimal lowerObjectiveDollars = 500m,
            decimal upperObjectiveDollars = 1000m,
            decimal greenProtectionThresholdDollars = 0m,
            decimal protectedGreenFloorDollars = 0m,
            int contextBars = 30,
            int shortBars = 8)
        {
            IndicatorConfig = indicatorConfig ?? new RangeEntryVectorFlowHoldConfig();
            Stage = stage;
            MaximumAttempts = maximumAttempts;
            MaximumStructuralRiskTicks = maximumStructuralRiskTicks > 0m
                ? maximumStructuralRiskTicks
                : stage == MorningResearchAccountStage.Combine ? 325m : 250m;
            TradeableScore = tradeableScore > 0m
                ? tradeableScore
                : stage == MorningResearchAccountStage.Combine ? 58m : 64m;
            ActionableScore = actionableScore > 0m
                ? actionableScore
                : stage == MorningResearchAccountStage.Combine ? 74m : 80m;
            ExceptionalScore = exceptionalScore > 0m
                ? exceptionalScore
                : stage == MorningResearchAccountStage.Combine ? 88m : 92m;
            LowerObjectiveDollars = lowerObjectiveDollars;
            UpperObjectiveDollars = upperObjectiveDollars;
            GreenProtectionThresholdDollars = greenProtectionThresholdDollars > 0m
                ? greenProtectionThresholdDollars
                : stage == MorningResearchAccountStage.Combine ? 350m : 300m;
            ProtectedGreenFloorDollars = protectedGreenFloorDollars > 0m
                ? protectedGreenFloorDollars
                : stage == MorningResearchAccountStage.Combine ? 200m : 250m;
            ContextBars = contextBars;
            ShortBars = shortBars;

            if (MaximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
            if (MaximumStructuralRiskTicks <= 0m) throw new ArgumentOutOfRangeException(nameof(maximumStructuralRiskTicks));
            if (TradeableScore < 0m || ActionableScore <= TradeableScore || ExceptionalScore <= ActionableScore || ExceptionalScore > 100m)
                throw new ArgumentOutOfRangeException(nameof(tradeableScore));
            if (LowerObjectiveDollars <= 0m || UpperObjectiveDollars <= LowerObjectiveDollars)
                throw new ArgumentOutOfRangeException(nameof(lowerObjectiveDollars));
            if (GreenProtectionThresholdDollars <= 0m || ProtectedGreenFloorDollars < 0m || ProtectedGreenFloorDollars >= GreenProtectionThresholdDollars)
                throw new ArgumentOutOfRangeException(nameof(greenProtectionThresholdDollars));
            if (ContextBars < 20 || ShortBars < 3 || ShortBars >= ContextBars)
                throw new ArgumentOutOfRangeException(nameof(contextBars));
        }

        public static RangeVectorDailySelectionConfig CombineDefault =>
            new RangeVectorDailySelectionConfig(MorningResearchAccountStage.Combine);

        public static RangeVectorDailySelectionConfig FundedDefault =>
            new RangeVectorDailySelectionConfig(MorningResearchAccountStage.Funded);

        public MorningResearchAccountStage Stage { get; }
        public RangeEntryVectorFlowHoldConfig IndicatorConfig { get; }
        public int MaximumAttempts { get; }
        public decimal MaximumStructuralRiskTicks { get; }
        public decimal TradeableScore { get; }
        public decimal ActionableScore { get; }
        public decimal ExceptionalScore { get; }
        public decimal LowerObjectiveDollars { get; }
        public decimal UpperObjectiveDollars { get; }
        public decimal GreenProtectionThresholdDollars { get; }
        public decimal ProtectedGreenFloorDollars { get; }
        public int ContextBars { get; }
        public int ShortBars { get; }
    }

    public sealed class RangeVectorEntryContext
    {
        public RangeVectorEntryContext(MorningMarketState state, MorningAdaptiveSetupType setupType,
            NewYorkResearchDirection contextDirection, decimal contextEfficiency, decimal shortEfficiency,
            decimal contextRange, bool compressed, bool directionAlignedWithContext)
        {
            State = state;
            SetupType = setupType;
            ContextDirection = contextDirection;
            ContextEfficiency = contextEfficiency;
            ShortEfficiency = shortEfficiency;
            ContextRange = contextRange;
            Compressed = compressed;
            DirectionAlignedWithContext = directionAlignedWithContext;
        }

        public MorningMarketState State { get; }
        public MorningAdaptiveSetupType SetupType { get; }
        public NewYorkResearchDirection ContextDirection { get; }
        public decimal ContextEfficiency { get; }
        public decimal ShortEfficiency { get; }
        public decimal ContextRange { get; }
        public bool Compressed { get; }
        public bool DirectionAlignedWithContext { get; }
    }

    public sealed class RangeVectorDailyDecision
    {
        public RangeVectorDailyDecision(EfficientAdaptiveRangeVectorOutcome candidate, RangeVectorEntryContext? context,
            decimal score, MorningOpportunityReadiness readiness, bool selected, string reason)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Context = context;
            Score = score;
            Readiness = readiness;
            Selected = selected;
            Reason = reason ?? string.Empty;
        }

        public EfficientAdaptiveRangeVectorOutcome Candidate { get; }
        public RangeVectorEntryContext? Context { get; }
        public decimal Score { get; }
        public MorningOpportunityReadiness Readiness { get; }
        public bool Selected { get; }
        public string Reason { get; }
    }

    public sealed class RangeVectorDailySelectedTrade
    {
        public RangeVectorDailySelectedTrade(RangeVectorDailyDecision decision, decimal cumulativeAfterTrade)
        {
            Decision = decision ?? throw new ArgumentNullException(nameof(decision));
            CumulativeAfterTrade = cumulativeAfterTrade;
        }

        public RangeVectorDailyDecision Decision { get; }
        public EfficientAdaptiveRangeVectorOutcome Source => Decision.Candidate;
        public decimal CumulativeAfterTrade { get; }
    }

    public sealed class RangeVectorDailyOutcome
    {
        public RangeVectorDailyOutcome(DateTime sessionDateCentral, MorningResearchAccountStage stage,
            IReadOnlyList<RangeVectorDailySelectedTrade> selectedTrades,
            IReadOnlyList<RangeVectorDailyDecision> decisions,
            int missedThreeHundredOpportunities, int missedFiveHundredOpportunities,
            int missedRunnerCapableOpportunities)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Stage = stage;
            SelectedTrades = selectedTrades ?? throw new ArgumentNullException(nameof(selectedTrades));
            Decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
            MissedThreeHundredOpportunities = missedThreeHundredOpportunities;
            MissedFiveHundredOpportunities = missedFiveHundredOpportunities;
            MissedRunnerCapableOpportunities = missedRunnerCapableOpportunities;
        }

        public DateTime SessionDateCentral { get; }
        public MorningResearchAccountStage Stage { get; }
        public IReadOnlyList<RangeVectorDailySelectedTrade> SelectedTrades { get; }
        public IReadOnlyList<RangeVectorDailyDecision> Decisions { get; }
        public int Attempts => SelectedTrades.Count;
        public decimal RealizedDollars => SelectedTrades.Sum(x => x.Source.ManagedOutcome!.RealizedDollars);
        public bool Green => RealizedDollars > 0m;
        public bool ReachedThreeHundred => RealizedDollars >= 300m;
        public bool ReachedLowerObjective => RealizedDollars >= 500m;
        public bool ReachedUpperObjective => RealizedDollars >= 1000m;
        public int MissedThreeHundredOpportunities { get; }
        public int MissedFiveHundredOpportunities { get; }
        public int MissedRunnerCapableOpportunities { get; }
    }

    /// <summary>
    /// Research-only v4 daily sequencing layer. The confirmed 3-minute Range Filter remains the only directional
    /// opportunity authority. Efficient Entry v3 decides whether and where the opportunity can be entered within the
    /// stage risk cap. V4 then spends at most two daily attempts using only entry-time one-minute market-state,
    /// setup-family, efficiency, structural-risk and clock-context evidence derived from the existing ISE readiness
    /// model. Five-minute VectorFlow is deliberately absent from the entry score and retains hold authority only after
    /// a selected trade is open. Future realized P&L and MFE are used only for post-sequence diagnostics.
    /// </summary>
    public sealed class RangeVectorDailySequencingAnalyzer
    {
        private static readonly TimeSpan MorningStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan MorningEnd = new TimeSpan(11, 0, 0);
        private const decimal TrendEfficiency = 0.32m;
        private const decimal StrongTrendEfficiency = 0.45m;
        private const decimal RangeEfficiency = 0.20m;
        private const decimal CompressionFraction = 0.42m;
        private const decimal ExhaustionFraction = 0.70m;

        private readonly RangeVectorDailySelectionConfig config;

        public RangeVectorDailySequencingAnalyzer(RangeVectorDailySelectionConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IReadOnlyList<RangeVectorDailyOutcome> Analyze(IReadOnlyList<HistoricalBar> oneMinuteBars)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (oneMinuteBars.Count == 0) return Array.Empty<RangeVectorDailyOutcome>();

            var central = ResolveCentralTimeZone();
            var orderedBars = oneMinuteBars.OrderBy(x => x.TimestampUtc).ToList();
            var efficientConfig = new EfficientAdaptiveRangeVectorConfig(config.Stage, config.IndicatorConfig,
                maximumStructuralRiskTicks: config.MaximumStructuralRiskTicks);
            var efficient = new EfficientAdaptiveRangeVectorAnalyzer(efficientConfig).Analyze(orderedBars)
                .Where(x => x.Selected).ToList();

            var sessionDates = orderedBars
                .Select(x => TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime)
                .Where(x => x.TimeOfDay >= MorningStart && x.TimeOfDay < MorningEnd)
                .Select(x => x.Date)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var results = new List<RangeVectorDailyOutcome>();
            foreach (var sessionDate in sessionDates)
            {
                var candidates = efficient.Where(x => x.Source.SessionDateCentral == sessionDate)
                    .OrderBy(x => x.EntryUtc!.Value).ToList();
                results.Add(SequenceDay(sessionDate, candidates, orderedBars, central));
            }
            return results;
        }

        public decimal Score(IReadOnlyList<HistoricalBar> oneMinuteBars, EfficientAdaptiveRangeVectorOutcome candidate)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (!candidate.Selected || !candidate.EntryUtc.HasValue || !candidate.InitialRiskTicks.HasValue) return 0m;

            var orderedBars = oneMinuteBars.OrderBy(x => x.TimestampUtc).ToList();
            var context = BuildEntryContext(orderedBars, candidate);
            return context == null ? 0m : Score(candidate, context);
        }

        private RangeVectorDailyOutcome SequenceDay(DateTime sessionDate,
            IReadOnlyList<EfficientAdaptiveRangeVectorOutcome> candidates,
            IReadOnlyList<HistoricalBar> orderedBars, TimeZoneInfo central)
        {
            var selected = new List<RangeVectorDailySelectedTrade>();
            var decisions = new List<RangeVectorDailyDecision>();
            decimal realized = 0m;
            DateTimeOffset? openUntil = null;

            foreach (var candidate in candidates)
            {
                var context = BuildEntryContext(orderedBars, candidate);
                if (context == null)
                {
                    decisions.Add(new RangeVectorDailyDecision(candidate, null, 0m,
                        MorningOpportunityReadiness.Observe, false, "InsufficientContext"));
                    continue;
                }

                var score = Score(candidate, context);
                var readiness = Classify(score);

                if (openUntil.HasValue && candidate.EntryUtc!.Value < openUntil.Value)
                {
                    decisions.Add(new RangeVectorDailyDecision(candidate, context, score, readiness, false, "PositionOpen"));
                    continue;
                }
                if (selected.Count >= config.MaximumAttempts || realized >= config.LowerObjectiveDollars)
                {
                    decisions.Add(new RangeVectorDailyDecision(candidate, context, score, readiness, false, "Governance"));
                    continue;
                }
                if (candidate.InitialRiskTicks!.Value > config.MaximumStructuralRiskTicks)
                {
                    decisions.Add(new RangeVectorDailyDecision(candidate, context, score, readiness, false, "Risk"));
                    continue;
                }
                if (readiness < MorningOpportunityReadiness.Actionable)
                {
                    decisions.Add(new RangeVectorDailyDecision(candidate, context, score, readiness, false,
                        readiness == MorningOpportunityReadiness.Tradeable ? "DeferredReadiness" : "Observe"));
                    continue;
                }

                if (realized >= config.GreenProtectionThresholdDollars)
                {
                    var availableDollars = realized - config.ProtectedGreenFloorDollars;
                    var plannedRiskDollars = candidate.InitialRiskTicks.Value * config.IndicatorConfig.DollarsPerTick;
                    if (availableDollars <= 0m || plannedRiskDollars > availableDollars)
                    {
                        decisions.Add(new RangeVectorDailyDecision(candidate, context, score, readiness, false, "GreenProtection"));
                        continue;
                    }
                }

                var selectedDecision = new RangeVectorDailyDecision(candidate, context, score, readiness, true, "Selected");
                decisions.Add(selectedDecision);
                realized += candidate.ManagedOutcome!.RealizedDollars;
                selected.Add(new RangeVectorDailySelectedTrade(selectedDecision, realized));
                openUntil = candidate.ManagedOutcome.ExitUtc;

                if (realized >= config.UpperObjectiveDollars)
                    break;
            }

            Diagnose(decisions, out var missed300, out var missed500, out var missedRunner);
            return new RangeVectorDailyOutcome(sessionDate, config.Stage, selected, decisions,
                missed300, missed500, missedRunner);
        }

        private RangeVectorEntryContext? BuildEntryContext(IReadOnlyList<HistoricalBar> orderedBars,
            EfficientAdaptiveRangeVectorOutcome candidate)
        {
            if (!candidate.EntryUtc.HasValue) return null;
            var completed = orderedBars.Where(x => x.TimestampUtc < candidate.EntryUtc.Value)
                .TakeLastCompat(config.ContextBars).ToList();
            if (completed.Count < config.ContextBars) return null;

            var shortWindow = completed.Skip(completed.Count - config.ShortBars).ToList();
            var contextHigh = completed.Max(x => x.High);
            var contextLow = completed.Min(x => x.Low);
            var contextRange = Math.Max(config.IndicatorConfig.TickSize, contextHigh - contextLow);
            var displacement = completed[completed.Count - 1].Close - completed[0].Open;
            var contextDirection = displacement > 0m ? NewYorkResearchDirection.Long
                : displacement < 0m ? NewYorkResearchDirection.Short : NewYorkResearchDirection.None;
            var contextEfficiency = DirectionalEfficiency(completed);
            var shortEfficiency = DirectionalEfficiency(shortWindow);
            var shortRange = shortWindow.Max(x => x.High) - shortWindow.Min(x => x.Low);
            var compressed = shortRange <= contextRange * CompressionFraction;
            var shortDisplacement = shortWindow[shortWindow.Count - 1].Close - shortWindow[0].Open;
            var opposing = contextDirection == NewYorkResearchDirection.Long ? shortDisplacement < 0m
                : contextDirection == NewYorkResearchDirection.Short ? shortDisplacement > 0m : false;

            MorningMarketState state;
            if (contextDirection == NewYorkResearchDirection.None || contextEfficiency <= RangeEfficiency)
                state = compressed ? MorningMarketState.Compressing : MorningMarketState.Range;
            else if (opposing && shortEfficiency >= TrendEfficiency)
                state = MorningMarketState.Reversing;
            else
            {
                var progress = Math.Abs(completed[completed.Count - 1].Close - completed[0].Open) / contextRange;
                if (progress >= ExhaustionFraction && shortEfficiency < RangeEfficiency)
                    state = MorningMarketState.Exhausting;
                else if (compressed)
                    state = MorningMarketState.Compressing;
                else if (contextEfficiency >= StrongTrendEfficiency)
                    state = MorningMarketState.Trending;
                else
                    state = MorningMarketState.DevelopingTrend;
            }

            var signalDirection = ToNewYorkDirection(candidate.Source.Direction);
            var aligned = contextDirection != NewYorkResearchDirection.None && signalDirection == contextDirection;
            var setupType = MapSetup(state, contextDirection, signalDirection, aligned);
            return new RangeVectorEntryContext(state, setupType, contextDirection, contextEfficiency,
                shortEfficiency, contextRange, compressed, aligned);
        }

        private decimal Score(EfficientAdaptiveRangeVectorOutcome candidate, RangeVectorEntryContext context)
        {
            decimal score = 40m;
            switch (context.State)
            {
                case MorningMarketState.Trending: score += 22m; break;
                case MorningMarketState.Reversing: score += 15m; break;
                case MorningMarketState.Compressing: score += 10m; break;
                case MorningMarketState.Range: score += 5m; break;
                case MorningMarketState.DevelopingTrend: score += 2m; break;
                case MorningMarketState.Exhausting: score -= 8m; break;
            }

            switch (context.SetupType)
            {
                case MorningAdaptiveSetupType.TrendContinuation: score += 16m; break;
                case MorningAdaptiveSetupType.CompressionResumption: score += 12m; break;
                case MorningAdaptiveSetupType.FailedBreakoutReversal: score += 12m; break;
                case MorningAdaptiveSetupType.RangeResolution: score += 8m; break;
                case MorningAdaptiveSetupType.BreakoutAcceptance: score += 6m; break;
                case MorningAdaptiveSetupType.PullbackRetest: score += 4m; break;
            }

            if (context.ContextEfficiency >= 0.50m) score += 12m;
            else if (context.ContextEfficiency >= 0.40m) score += 8m;
            else if (context.ContextEfficiency >= 0.32m) score += 4m;
            else if (context.ContextEfficiency < 0.20m) score -= 6m;

            var riskTicks = candidate.InitialRiskTicks!.Value;
            if (riskTicks <= 100m) score += 10m;
            else if (riskTicks <= 150m) score += 7m;
            else if (riskTicks <= 200m) score += 4m;
            else if (riskTicks > 300m) score -= 8m;

            var central = ResolveCentralTimeZone();
            var local = TimeZoneInfo.ConvertTime(candidate.EntryUtc!.Value, central).TimeOfDay;
            if (local < new TimeSpan(5, 0, 0)) score -= 8m;
            else if (local < new TimeSpan(6, 30, 0)) score -= 3m;
            else if (local >= new TimeSpan(7, 0, 0) && local < new TimeSpan(10, 0, 0)) score += 4m;

            if (context.State == MorningMarketState.Trending && context.SetupType == MorningAdaptiveSetupType.TrendContinuation)
                score += 8m;
            if (context.State == MorningMarketState.Trending && context.SetupType == MorningAdaptiveSetupType.CompressionResumption)
                score += 5m;
            if (context.State == MorningMarketState.DevelopingTrend && context.SetupType == MorningAdaptiveSetupType.CompressionResumption)
                score -= 4m;
            if (context.State == MorningMarketState.Compressing && context.SetupType == MorningAdaptiveSetupType.PullbackRetest)
                score -= 3m;

            var riskEfficiency = riskTicks <= 0m ? 0m
                : context.ContextRange / (riskTicks * config.IndicatorConfig.TickSize);
            if (riskEfficiency >= 4m) score += 5m;
            else if (riskEfficiency < 1.5m) score -= 5m;

            // Opportunity age is causal. A long wait for an efficient fill is allowed by v3, but it receives a modest
            // readiness penalty because the original Range Filter opportunity is becoming mature rather than fresh.
            if (candidate.DeferralMinutes > 12) score -= 8m;
            else if (candidate.DeferralMinutes > 6) score -= 4m;
            else if (candidate.DeferralMinutes > 2) score -= 2m;

            return Math.Max(0m, Math.Min(100m, score));
        }

        private MorningOpportunityReadiness Classify(decimal score)
        {
            if (score >= config.ExceptionalScore) return MorningOpportunityReadiness.Exceptional;
            if (score >= config.ActionableScore) return MorningOpportunityReadiness.Actionable;
            if (score >= config.TradeableScore) return MorningOpportunityReadiness.Tradeable;
            return MorningOpportunityReadiness.Observe;
        }

        private static MorningAdaptiveSetupType MapSetup(MorningMarketState state,
            NewYorkResearchDirection contextDirection, NewYorkResearchDirection signalDirection, bool aligned)
        {
            if (state == MorningMarketState.Reversing)
                return !aligned && contextDirection != NewYorkResearchDirection.None
                    ? MorningAdaptiveSetupType.FailedBreakoutReversal
                    : MorningAdaptiveSetupType.PullbackRetest;
            if (state == MorningMarketState.Trending)
                return aligned ? MorningAdaptiveSetupType.TrendContinuation : MorningAdaptiveSetupType.PullbackRetest;
            if (state == MorningMarketState.DevelopingTrend)
                return aligned ? MorningAdaptiveSetupType.BreakoutAcceptance : MorningAdaptiveSetupType.PullbackRetest;
            if (state == MorningMarketState.Compressing)
                return aligned && contextDirection != NewYorkResearchDirection.None
                    ? MorningAdaptiveSetupType.CompressionResumption
                    : MorningAdaptiveSetupType.RangeResolution;
            if (state == MorningMarketState.Range)
                return MorningAdaptiveSetupType.RangeResolution;
            if (state == MorningMarketState.Exhausting)
                return !aligned && contextDirection != NewYorkResearchDirection.None
                    ? MorningAdaptiveSetupType.FailedBreakoutReversal
                    : MorningAdaptiveSetupType.BreakoutAcceptance;
            return signalDirection == NewYorkResearchDirection.None
                ? MorningAdaptiveSetupType.None
                : MorningAdaptiveSetupType.BreakoutAcceptance;
        }

        private static NewYorkResearchDirection ToNewYorkDirection(RangeFilterResearchDirection direction)
        {
            return direction == RangeFilterResearchDirection.Long ? NewYorkResearchDirection.Long
                : direction == RangeFilterResearchDirection.Short ? NewYorkResearchDirection.Short
                : NewYorkResearchDirection.None;
        }

        private static decimal DirectionalEfficiency(IReadOnlyList<HistoricalBar> bars)
        {
            if (bars.Count < 2) return 0m;
            var displacement = Math.Abs(bars[bars.Count - 1].Close - bars[0].Open);
            decimal path = 0m;
            var previous = bars[0].Open;
            foreach (var bar in bars)
            {
                path += Math.Abs(bar.Close - previous);
                previous = bar.Close;
            }
            return path <= 0m ? 0m : Math.Min(1m, displacement / path);
        }

        private static void Diagnose(IReadOnlyList<RangeVectorDailyDecision> decisions,
            out int missed300, out int missed500, out int missedRunner)
        {
            missed300 = 0;
            missed500 = 0;
            missedRunner = 0;
            foreach (var decision in decisions.Where(x => !x.Selected && x.Candidate.ManagedOutcome != null))
            {
                // Hindsight diagnostics only. None of these fields participate in Score or selection above.
                var managed = decision.Candidate.ManagedOutcome!;
                if (managed.RealizedDollars >= 300m) missed300++;
                if (managed.RealizedDollars >= 500m) missed500++;
                if (managed.MaxFavorableTicks >= 500m && managed.FinalMode != RangeVectorManagementMode.Runner)
                    missedRunner++;
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }

    internal static class HistoricalResearchEnumerableCompatibility
    {
        public static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count <= 0) return Enumerable.Empty<T>();
            var queue = new Queue<T>();
            foreach (var item in source)
            {
                queue.Enqueue(item);
                if (queue.Count > count) queue.Dequeue();
            }
            return queue;
        }
    }
}
