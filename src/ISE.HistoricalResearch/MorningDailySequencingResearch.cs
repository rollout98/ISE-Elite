using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningResearchAccountStage
    {
        Combine = 0,
        Funded = 1
    }

    public sealed class MorningDailySequencingConfig
    {
        public MorningDailySequencingConfig(
            MorningResearchAccountStage stage,
            decimal minimumScore,
            decimal maximumStructuralRiskTicks,
            int maximumAttempts,
            decimal lowerObjectiveDollars = 500m,
            decimal upperObjectiveDollars = 1000m,
            decimal greenProtectionThresholdDollars = 300m,
            decimal protectedGreenFloorDollars = 250m)
        {
            if (minimumScore < 0m || minimumScore > 100m) throw new ArgumentOutOfRangeException(nameof(minimumScore));
            if (maximumStructuralRiskTicks <= 0m) throw new ArgumentOutOfRangeException(nameof(maximumStructuralRiskTicks));
            if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
            if (lowerObjectiveDollars <= 0m || upperObjectiveDollars <= lowerObjectiveDollars) throw new ArgumentOutOfRangeException(nameof(lowerObjectiveDollars));
            if (greenProtectionThresholdDollars <= 0m || protectedGreenFloorDollars < 0m || protectedGreenFloorDollars >= greenProtectionThresholdDollars)
                throw new ArgumentOutOfRangeException(nameof(greenProtectionThresholdDollars));

            Stage = stage;
            MinimumScore = minimumScore;
            MaximumStructuralRiskTicks = maximumStructuralRiskTicks;
            MaximumAttempts = maximumAttempts;
            LowerObjectiveDollars = lowerObjectiveDollars;
            UpperObjectiveDollars = upperObjectiveDollars;
            GreenProtectionThresholdDollars = greenProtectionThresholdDollars;
            ProtectedGreenFloorDollars = protectedGreenFloorDollars;
        }

        public static MorningDailySequencingConfig CombineDefault => new MorningDailySequencingConfig(
            MorningResearchAccountStage.Combine, 58m, 325m, 2, 500m, 1000m, 350m, 200m);

        public static MorningDailySequencingConfig FundedDefault => new MorningDailySequencingConfig(
            MorningResearchAccountStage.Funded, 68m, 250m, 2, 500m, 1000m, 300m, 250m);

        public MorningResearchAccountStage Stage { get; }
        public decimal MinimumScore { get; }
        public decimal MaximumStructuralRiskTicks { get; }
        public int MaximumAttempts { get; }
        public decimal LowerObjectiveDollars { get; }
        public decimal UpperObjectiveDollars { get; }
        public decimal GreenProtectionThresholdDollars { get; }
        public decimal ProtectedGreenFloorDollars { get; }
    }

    public sealed class MorningSelectedTrade
    {
        public MorningSelectedTrade(MorningAdaptiveTradeOutcome source, decimal selectionScore, decimal cumulativeAfterTrade)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            SelectionScore = selectionScore;
            CumulativeAfterTrade = cumulativeAfterTrade;
        }

        public MorningAdaptiveTradeOutcome Source { get; }
        public decimal SelectionScore { get; }
        public decimal CumulativeAfterTrade { get; }
    }

    public sealed class MorningDailySequenceOutcome
    {
        public MorningDailySequenceOutcome(DateTime sessionDateCentral, MorningResearchAccountStage stage,
            IReadOnlyList<MorningSelectedTrade> selectedTrades, int rejectedByQuality, int rejectedByRisk,
            int rejectedWhilePositionOpen, int rejectedByGovernance)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Stage = stage;
            SelectedTrades = selectedTrades ?? throw new ArgumentNullException(nameof(selectedTrades));
            RejectedByQuality = rejectedByQuality;
            RejectedByRisk = rejectedByRisk;
            RejectedWhilePositionOpen = rejectedWhilePositionOpen;
            RejectedByGovernance = rejectedByGovernance;
        }

        public DateTime SessionDateCentral { get; }
        public MorningResearchAccountStage Stage { get; }
        public IReadOnlyList<MorningSelectedTrade> SelectedTrades { get; }
        public int RejectedByQuality { get; }
        public int RejectedByRisk { get; }
        public int RejectedWhilePositionOpen { get; }
        public int RejectedByGovernance { get; }
        public decimal RealizedDollars => SelectedTrades.Sum(x => x.Source.RealizedDollars);
        public int Attempts => SelectedTrades.Count;
        public bool ReachedLowerObjective => RealizedDollars >= 500m;
        public bool ReachedUpperObjective => RealizedDollars >= 1000m;
        public int RunnerCapableButNotRunner => SelectedTrades.Count(x => x.Source.MaxFavorableTicks >= 500m && x.Source.FinalMode != MorningAdaptiveManagementMode.Runner);
    }

    /// <summary>
    /// Converts the broad adaptive opportunity catalogue into a realistic chronological research sequence.
    /// Selection uses only information known at entry time: state, setup family, context efficiency, structural risk,
    /// and clock context. Future outcome fields are never used to decide whether a candidate is selected.
    /// </summary>
    public sealed class MorningDailySequencingAnalyzer
    {
        private readonly MorningDailySequencingConfig config;

        public MorningDailySequencingAnalyzer(MorningDailySequencingConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IReadOnlyList<MorningDailySequenceOutcome> Analyze(IReadOnlyList<MorningAdaptiveTradeOutcome> candidates)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var result = new List<MorningDailySequenceOutcome>();

            foreach (var group in candidates.GroupBy(x => x.SessionDateCentral).OrderBy(x => x.Key))
            {
                var selected = new List<MorningSelectedTrade>();
                var realized = 0m;
                DateTimeOffset? openUntil = null;
                var qualityRejects = 0;
                var riskRejects = 0;
                var overlapRejects = 0;
                var governanceRejects = 0;

                foreach (var candidate in group.OrderBy(x => x.EntryUtc))
                {
                    if (openUntil.HasValue && candidate.EntryUtc < openUntil.Value)
                    {
                        overlapRejects++;
                        continue;
                    }

                    if (selected.Count >= config.MaximumAttempts || realized >= config.LowerObjectiveDollars)
                    {
                        governanceRejects++;
                        continue;
                    }

                    var score = Score(candidate);
                    if (score < config.MinimumScore)
                    {
                        qualityRejects++;
                        continue;
                    }

                    if (candidate.InitialRiskTicks > config.MaximumStructuralRiskTicks)
                    {
                        riskRejects++;
                        continue;
                    }

                    if (realized >= config.GreenProtectionThresholdDollars)
                    {
                        var available = realized - config.ProtectedGreenFloorDollars;
                        var plannedRiskDollars = candidate.InitialRiskTicks;
                        if (available <= 0m || plannedRiskDollars > available)
                        {
                            governanceRejects++;
                            continue;
                        }
                    }

                    realized += candidate.RealizedDollars;
                    selected.Add(new MorningSelectedTrade(candidate, score, realized));
                    openUntil = candidate.ExitUtc;

                    if (realized >= config.UpperObjectiveDollars)
                        break;
                }

                result.Add(new MorningDailySequenceOutcome(group.Key, config.Stage, selected,
                    qualityRejects, riskRejects, overlapRejects, governanceRejects));
            }

            return result;
        }

        public decimal Score(MorningAdaptiveTradeOutcome candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            decimal score = 40m;

            switch (candidate.State)
            {
                case MorningMarketState.Trending: score += 22m; break;
                case MorningMarketState.Reversing: score += 15m; break;
                case MorningMarketState.Compressing: score += 10m; break;
                case MorningMarketState.Range: score += 5m; break;
                case MorningMarketState.DevelopingTrend: score += 2m; break;
                case MorningMarketState.Exhausting: score -= 8m; break;
            }

            switch (candidate.SetupType)
            {
                case MorningAdaptiveSetupType.TrendContinuation: score += 16m; break;
                case MorningAdaptiveSetupType.CompressionResumption: score += 12m; break;
                case MorningAdaptiveSetupType.FailedBreakoutReversal: score += 12m; break;
                case MorningAdaptiveSetupType.RangeResolution: score += 8m; break;
                case MorningAdaptiveSetupType.BreakoutAcceptance: score += 6m; break;
                case MorningAdaptiveSetupType.PullbackRetest: score += 4m; break;
            }

            if (candidate.ContextEfficiency >= 0.50m) score += 12m;
            else if (candidate.ContextEfficiency >= 0.40m) score += 8m;
            else if (candidate.ContextEfficiency >= 0.32m) score += 4m;
            else if (candidate.ContextEfficiency < 0.20m) score -= 6m;

            if (candidate.InitialRiskTicks <= 100m) score += 10m;
            else if (candidate.InitialRiskTicks <= 150m) score += 7m;
            else if (candidate.InitialRiskTicks <= 200m) score += 4m;
            else if (candidate.InitialRiskTicks > 300m) score -= 8m;

            var central = ResolveCentralTimeZone();
            var hour = TimeZoneInfo.ConvertTime(candidate.EntryUtc, central).Hour;
            if (hour >= 6 && hour <= 9) score += 4m;
            else if (hour <= 4) score -= 3m;

            return Math.Max(0m, Math.Min(100m, score));
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
