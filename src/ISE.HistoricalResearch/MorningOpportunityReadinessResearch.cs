using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningOpportunityReadiness
    {
        Observe = 0,
        Tradeable = 1,
        Actionable = 2,
        Exceptional = 3
    }

    public sealed class MorningOpportunityReadinessConfig
    {
        public MorningOpportunityReadinessConfig(MorningResearchAccountStage stage, int maximumAttempts,
            decimal maximumStructuralRiskTicks, decimal tradeableScore, decimal actionableScore,
            decimal exceptionalScore, decimal lowerObjectiveDollars = 500m,
            decimal greenProtectionThresholdDollars = 300m, decimal protectedGreenFloorDollars = 250m)
        {
            if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
            if (maximumStructuralRiskTicks <= 0m) throw new ArgumentOutOfRangeException(nameof(maximumStructuralRiskTicks));
            if (tradeableScore < 0m || actionableScore <= tradeableScore || exceptionalScore <= actionableScore || exceptionalScore > 100m)
                throw new ArgumentOutOfRangeException(nameof(tradeableScore));
            if (lowerObjectiveDollars <= 0m) throw new ArgumentOutOfRangeException(nameof(lowerObjectiveDollars));
            if (greenProtectionThresholdDollars <= 0m || protectedGreenFloorDollars < 0m || protectedGreenFloorDollars >= greenProtectionThresholdDollars)
                throw new ArgumentOutOfRangeException(nameof(greenProtectionThresholdDollars));

            Stage = stage;
            MaximumAttempts = maximumAttempts;
            MaximumStructuralRiskTicks = maximumStructuralRiskTicks;
            TradeableScore = tradeableScore;
            ActionableScore = actionableScore;
            ExceptionalScore = exceptionalScore;
            LowerObjectiveDollars = lowerObjectiveDollars;
            GreenProtectionThresholdDollars = greenProtectionThresholdDollars;
            ProtectedGreenFloorDollars = protectedGreenFloorDollars;
        }

        public static MorningOpportunityReadinessConfig CombineDefault => new MorningOpportunityReadinessConfig(
            MorningResearchAccountStage.Combine, 2, 325m, 58m, 74m, 88m, 500m, 350m, 200m);

        public static MorningOpportunityReadinessConfig FundedDefault => new MorningOpportunityReadinessConfig(
            MorningResearchAccountStage.Funded, 2, 250m, 64m, 80m, 92m, 500m, 300m, 250m);

        public MorningResearchAccountStage Stage { get; }
        public int MaximumAttempts { get; }
        public decimal MaximumStructuralRiskTicks { get; }
        public decimal TradeableScore { get; }
        public decimal ActionableScore { get; }
        public decimal ExceptionalScore { get; }
        public decimal LowerObjectiveDollars { get; }
        public decimal GreenProtectionThresholdDollars { get; }
        public decimal ProtectedGreenFloorDollars { get; }
    }

    public sealed class MorningReadinessDecision
    {
        public MorningReadinessDecision(MorningAdaptiveTradeOutcome candidate, decimal score, MorningOpportunityReadiness readiness,
            bool selected, string reason)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Score = score;
            Readiness = readiness;
            Selected = selected;
            Reason = reason ?? string.Empty;
        }
        public MorningAdaptiveTradeOutcome Candidate { get; }
        public decimal Score { get; }
        public MorningOpportunityReadiness Readiness { get; }
        public bool Selected { get; }
        public string Reason { get; }
    }

    public sealed class MorningReadinessDayOutcome
    {
        public MorningReadinessDayOutcome(DateTime sessionDateCentral, MorningResearchAccountStage stage,
            IReadOnlyList<MorningSelectedTrade> selectedTrades, IReadOnlyList<MorningReadinessDecision> decisions,
            int missedBetterOpportunities, int missedRunnerCapableOpportunities)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Stage = stage;
            SelectedTrades = selectedTrades ?? throw new ArgumentNullException(nameof(selectedTrades));
            Decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
            MissedBetterOpportunities = missedBetterOpportunities;
            MissedRunnerCapableOpportunities = missedRunnerCapableOpportunities;
        }

        public DateTime SessionDateCentral { get; }
        public MorningResearchAccountStage Stage { get; }
        public IReadOnlyList<MorningSelectedTrade> SelectedTrades { get; }
        public IReadOnlyList<MorningReadinessDecision> Decisions { get; }
        public decimal RealizedDollars => SelectedTrades.Sum(x => x.Source.RealizedDollars);
        public int Attempts => SelectedTrades.Count;
        public int MissedBetterOpportunities { get; }
        public int MissedRunnerCapableOpportunities { get; }
    }

    /// <summary>
    /// Research-only opportunity-readiness layer. It does not select the first candidate that merely passes a floor.
    /// It classifies each candidate as Observe/Tradeable/Actionable/Exceptional from entry-time evidence and spends
    /// scarce daily attempts only on actionable opportunities, with a small causal urgency adjustment as the morning
    /// progresses. Future realized/MFE fields are used only after sequencing to diagnose missed better opportunities.
    /// </summary>
    public sealed class MorningOpportunityReadinessAnalyzer
    {
        private readonly MorningOpportunityReadinessConfig config;
        private readonly MorningDailySequencingAnalyzer scorer;

        public MorningOpportunityReadinessAnalyzer(MorningOpportunityReadinessConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            var scoringConfig = new MorningDailySequencingConfig(config.Stage, config.TradeableScore,
                config.MaximumStructuralRiskTicks, config.MaximumAttempts, config.LowerObjectiveDollars, 1000m,
                config.GreenProtectionThresholdDollars, config.ProtectedGreenFloorDollars);
            scorer = new MorningDailySequencingAnalyzer(scoringConfig);
        }

        public IReadOnlyList<MorningReadinessDayOutcome> Analyze(IReadOnlyList<MorningAdaptiveTradeOutcome> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var result = new List<MorningReadinessDayOutcome>();

            foreach (var group in candidates.GroupBy(x => x.SessionDateCentral).OrderBy(x => x.Key))
            {
                var ordered = group.OrderBy(x => x.EntryUtc).ToList();
                var selected = new List<MorningSelectedTrade>();
                var decisions = new List<MorningReadinessDecision>();
                decimal realized = 0m;
                DateTimeOffset? openUntil = null;

                foreach (var candidate in ordered)
                {
                    var score = ReadinessScore(candidate);
                    var readiness = Classify(score);

                    if (openUntil.HasValue && candidate.EntryUtc < openUntil.Value)
                    {
                        decisions.Add(new MorningReadinessDecision(candidate, score, readiness, false, "PositionOpen"));
                        continue;
                    }
                    if (selected.Count >= config.MaximumAttempts || realized >= config.LowerObjectiveDollars)
                    {
                        decisions.Add(new MorningReadinessDecision(candidate, score, readiness, false, "Governance"));
                        continue;
                    }
                    if (candidate.InitialRiskTicks > config.MaximumStructuralRiskTicks)
                    {
                        decisions.Add(new MorningReadinessDecision(candidate, score, readiness, false, "Risk"));
                        continue;
                    }
                    if (readiness < MorningOpportunityReadiness.Actionable)
                    {
                        decisions.Add(new MorningReadinessDecision(candidate, score, readiness, false,
                            readiness == MorningOpportunityReadiness.Tradeable ? "Deferred" : "Observe"));
                        continue;
                    }
                    if (realized >= config.GreenProtectionThresholdDollars)
                    {
                        var available = realized - config.ProtectedGreenFloorDollars;
                        if (available <= 0m || candidate.InitialRiskTicks > available)
                        {
                            decisions.Add(new MorningReadinessDecision(candidate, score, readiness, false, "GreenProtection"));
                            continue;
                        }
                    }

                    realized += candidate.RealizedDollars;
                    selected.Add(new MorningSelectedTrade(candidate, score, realized));
                    decisions.Add(new MorningReadinessDecision(candidate, score, readiness, true, "Selected"));
                    openUntil = candidate.ExitUtc;
                }

                Diagnose(selected, decisions, out var missedBetter, out var missedRunner);
                result.Add(new MorningReadinessDayOutcome(group.Key, config.Stage, selected, decisions, missedBetter, missedRunner));
            }
            return result;
        }

        public decimal ReadinessScore(MorningAdaptiveTradeOutcome candidate)
        {
            var score = scorer.Score(candidate);
            var central = ResolveCentralTimeZone();
            var local = TimeZoneInfo.ConvertTime(candidate.EntryUtc, central).TimeOfDay;

            // Clock is context, not a trigger: scarce attempts require stronger evidence very early, while an unused
            // opportunity budget becomes modestly more urgent as the morning matures.
            if (local < new TimeSpan(5, 0, 0)) score -= 8m;
            else if (local < new TimeSpan(6, 30, 0)) score -= 3m;
            else if (local >= new TimeSpan(7, 0, 0) && local < new TimeSpan(10, 0, 0)) score += 4m;

            if (candidate.State == MorningMarketState.Trending && candidate.SetupType == MorningAdaptiveSetupType.TrendContinuation)
                score += 8m;
            if (candidate.State == MorningMarketState.Trending && candidate.SetupType == MorningAdaptiveSetupType.CompressionResumption)
                score += 5m;
            if (candidate.State == MorningMarketState.DevelopingTrend && candidate.SetupType == MorningAdaptiveSetupType.CompressionResumption)
                score -= 4m;
            if (candidate.State == MorningMarketState.Compressing && candidate.SetupType == MorningAdaptiveSetupType.PullbackRetest)
                score -= 3m;

            var riskEfficiency = candidate.InitialRiskTicks <= 0m ? 0m : candidate.ContextRange / (candidate.InitialRiskTicks * 0.25m);
            if (riskEfficiency >= 4m) score += 5m;
            else if (riskEfficiency < 1.5m) score -= 5m;

            return Math.Max(0m, Math.Min(100m, score));
        }

        private MorningOpportunityReadiness Classify(decimal score)
        {
            if (score >= config.ExceptionalScore) return MorningOpportunityReadiness.Exceptional;
            if (score >= config.ActionableScore) return MorningOpportunityReadiness.Actionable;
            if (score >= config.TradeableScore) return MorningOpportunityReadiness.Tradeable;
            return MorningOpportunityReadiness.Observe;
        }

        private static void Diagnose(IReadOnlyList<MorningSelectedTrade> selected, IReadOnlyList<MorningReadinessDecision> decisions,
            out int missedBetter, out int missedRunner)
        {
            missedBetter = 0;
            missedRunner = 0;
            if (selected.Count == 0) return;

            var lastSelectedEntry = selected[selected.Count - 1].Source.EntryUtc;
            var bestSelectedScore = selected.Max(x => x.SelectionScore);
            var bestSelectedMfe = selected.Max(x => x.Source.MaxFavorableTicks);

            foreach (var decision in decisions.Where(x => !x.Selected && x.Candidate.EntryUtc > lastSelectedEntry))
            {
                // Hindsight diagnostics only; these fields never affect live-like selection above.
                if (decision.Score >= bestSelectedScore + 8m && decision.Candidate.MaxFavorableTicks >= bestSelectedMfe + 150m)
                    missedBetter++;
                if (decision.Candidate.MaxFavorableTicks >= 500m && decision.Candidate.FinalMode != MorningAdaptiveManagementMode.Runner)
                    missedRunner++;
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
