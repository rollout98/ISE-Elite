using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    /// <summary>
    /// Research-only configuration for V5 opportunity-potential diagnostics.
    /// The score intentionally excludes market-state labels, setup-family labels,
    /// VectorFlow bias, future MFE/MAE, and realized P&L so the same evidence is not
    /// double-counted and future outcome cannot leak into entry selection.
    /// </summary>
    public sealed class MorningOpportunityPotentialConfig
    {
        public MorningOpportunityPotentialConfig(
            int lookbackBars = 30,
            int shortBars = 6,
            int pullbackLookbackBars = 12,
            int freshMoveMaxBars = 12,
            decimal tickSize = 0.25m)
        {
            if (lookbackBars < 12) throw new ArgumentOutOfRangeException(nameof(lookbackBars));
            if (shortBars < 3 || shortBars >= lookbackBars) throw new ArgumentOutOfRangeException(nameof(shortBars));
            if (pullbackLookbackBars < 3 || pullbackLookbackBars > lookbackBars) throw new ArgumentOutOfRangeException(nameof(pullbackLookbackBars));
            if (freshMoveMaxBars < 1 || freshMoveMaxBars >= lookbackBars) throw new ArgumentOutOfRangeException(nameof(freshMoveMaxBars));
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));

            LookbackBars = lookbackBars;
            ShortBars = shortBars;
            PullbackLookbackBars = pullbackLookbackBars;
            FreshMoveMaxBars = freshMoveMaxBars;
            TickSize = tickSize;
        }

        public int LookbackBars { get; }
        public int ShortBars { get; }
        public int PullbackLookbackBars { get; }
        public int FreshMoveMaxBars { get; }
        public decimal TickSize { get; }
    }

    public sealed class MorningOpportunityPotentialFeatures
    {
        public MorningOpportunityPotentialFeatures(
            int moveAgeBars,
            decimal consumedDisplacementTicks,
            decimal consumedDisplacementFraction,
            decimal directionalEfficiency,
            decimal efficiencyDelta,
            decimal compressionRatio,
            int pullbackResetCount,
            decimal riskEfficiency,
            decimal accelerationRatio,
            decimal exhaustionRisk)
        {
            MoveAgeBars = moveAgeBars;
            ConsumedDisplacementTicks = consumedDisplacementTicks;
            ConsumedDisplacementFraction = consumedDisplacementFraction;
            DirectionalEfficiency = directionalEfficiency;
            EfficiencyDelta = efficiencyDelta;
            CompressionRatio = compressionRatio;
            PullbackResetCount = pullbackResetCount;
            RiskEfficiency = riskEfficiency;
            AccelerationRatio = accelerationRatio;
            ExhaustionRisk = exhaustionRisk;
        }

        public int MoveAgeBars { get; }
        public decimal ConsumedDisplacementTicks { get; }
        public decimal ConsumedDisplacementFraction { get; }
        public decimal DirectionalEfficiency { get; }
        public decimal EfficiencyDelta { get; }
        public decimal CompressionRatio { get; }
        public int PullbackResetCount { get; }
        public decimal RiskEfficiency { get; }
        public decimal AccelerationRatio { get; }
        public decimal ExhaustionRisk { get; }
    }

    public sealed class MorningOpportunityPotentialObservation
    {
        public MorningOpportunityPotentialObservation(
            MorningAdaptiveTradeOutcome source,
            MorningOpportunityPotentialFeatures features,
            decimal potentialScore)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Features = features ?? throw new ArgumentNullException(nameof(features));
            PotentialScore = potentialScore;
        }

        public MorningAdaptiveTradeOutcome Source { get; }
        public MorningOpportunityPotentialFeatures Features { get; }
        public decimal PotentialScore { get; }
    }

    public sealed class MorningOpportunityPotentialBucket
    {
        public MorningOpportunityPotentialBucket(
            string label,
            int count,
            decimal averageMfeTicks,
            decimal averageRealizedDollars,
            decimal positiveOutcomeRate)
        {
            Label = label;
            Count = count;
            AverageMfeTicks = averageMfeTicks;
            AverageRealizedDollars = averageRealizedDollars;
            PositiveOutcomeRate = positiveOutcomeRate;
        }

        public string Label { get; }
        public int Count { get; }
        public decimal AverageMfeTicks { get; }
        public decimal AverageRealizedDollars { get; }
        public decimal PositiveOutcomeRate { get; }
    }

    /// <summary>
    /// V5 causal opportunity-potential layer.
    ///
    /// This analyzer does not decide whether a Range opportunity exists. It evaluates
    /// how much usable expansion may remain after the existing entry authority has
    /// created a candidate. It is designed to sit between causal opportunity creation
    /// and daily sequencing.
    ///
    /// Entry-time score inputs:
    /// - move age from the most recent directional origin
    /// - consumed displacement versus the available context range
    /// - directional efficiency and whether it is strengthening or weakening
    /// - compression versus broader context
    /// - pullback/reset count
    /// - structural risk efficiency
    /// - short-horizon acceleration
    /// - exhaustion risk
    ///
    /// Future MFE and realized P&L are used only after scoring to evaluate score buckets.
    /// </summary>
    public sealed class MorningOpportunityPotentialAnalyzer
    {
        private readonly MorningOpportunityPotentialConfig config;

        public MorningOpportunityPotentialAnalyzer(MorningOpportunityPotentialConfig? config = null)
        {
            this.config = config ?? new MorningOpportunityPotentialConfig();
        }

        public IReadOnlyList<MorningOpportunityPotentialObservation> Analyze(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<MorningAdaptiveTradeOutcome> candidates)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            var orderedBars = bars.OrderBy(x => x.TimestampUtc).ToList();
            var result = new List<MorningOpportunityPotentialObservation>();

            foreach (var candidate in candidates.OrderBy(x => x.EntryUtc))
            {
                var features = BuildFeatures(orderedBars, candidate);
                if (features == null) continue;
                result.Add(new MorningOpportunityPotentialObservation(candidate, features, Score(features)));
            }

            return result;
        }

        public MorningOpportunityPotentialFeatures? BuildFeatures(
            IReadOnlyList<HistoricalBar> orderedBars,
            MorningAdaptiveTradeOutcome candidate)
        {
            if (orderedBars == null) throw new ArgumentNullException(nameof(orderedBars));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (candidate.Direction == NewYorkResearchDirection.None) return null;

            var prior = orderedBars
                .Where(x => x.TimestampUtc <= candidate.SetupUtc)
                .OrderByDescending(x => x.TimestampUtc)
                .Take(config.LookbackBars)
                .OrderBy(x => x.TimestampUtc)
                .ToList();

            if (prior.Count < config.LookbackBars) return null;

            var sign = candidate.Direction == NewYorkResearchDirection.Long ? 1m : -1m;
            var contextHigh = prior.Max(x => x.High);
            var contextLow = prior.Min(x => x.Low);
            var contextRange = Math.Max(config.TickSize, contextHigh - contextLow);

            var originIndex = candidate.Direction == NewYorkResearchDirection.Long
                ? IndexOfMinimum(prior, x => x.Low)
                : IndexOfMaximum(prior, x => x.High);
            var originPrice = candidate.Direction == NewYorkResearchDirection.Long
                ? prior[originIndex].Low
                : prior[originIndex].High;

            var moveAgeBars = Math.Max(0, prior.Count - 1 - originIndex);
            var consumed = Math.Max(0m, sign * (candidate.EntryPrice - originPrice));
            var consumedTicks = consumed / config.TickSize;
            var consumedFraction = Clamp(consumed / contextRange, 0m, 2m);

            var fullEfficiency = DirectionalEfficiency(prior, sign);
            var half = Math.Max(3, prior.Count / 2);
            var early = prior.Take(half).ToList();
            var late = prior.Skip(prior.Count - half).ToList();
            var efficiencyDelta = DirectionalEfficiency(late, sign) - DirectionalEfficiency(early, sign);

            var shortWindow = prior.Skip(prior.Count - config.ShortBars).ToList();
            var shortRange = Math.Max(config.TickSize, shortWindow.Max(x => x.High) - shortWindow.Min(x => x.Low));
            var compressionRatio = Clamp(shortRange / contextRange, 0m, 2m);

            var pullbackWindow = prior.Skip(prior.Count - config.PullbackLookbackBars).ToList();
            var pullbackResetCount = CountPullbackResets(pullbackWindow, sign);

            var contextRangeTicks = contextRange / config.TickSize;
            var riskEfficiency = candidate.InitialRiskTicks <= 0m
                ? 0m
                : Clamp(contextRangeTicks / candidate.InitialRiskTicks, 0m, 10m);

            var accelerationRatio = CalculateAcceleration(prior, sign);
            var exhaustionRisk = CalculateExhaustionRisk(
                moveAgeBars,
                consumedFraction,
                fullEfficiency,
                efficiencyDelta,
                pullbackResetCount,
                accelerationRatio);

            return new MorningOpportunityPotentialFeatures(
                moveAgeBars,
                consumedTicks,
                consumedFraction,
                fullEfficiency,
                efficiencyDelta,
                compressionRatio,
                pullbackResetCount,
                riskEfficiency,
                accelerationRatio,
                exhaustionRisk);
        }

        public decimal Score(MorningOpportunityPotentialFeatures features)
        {
            if (features == null) throw new ArgumentNullException(nameof(features));

            decimal score = 50m;

            if (features.MoveAgeBars <= config.FreshMoveMaxBars) score += 14m;
            else if (features.MoveAgeBars <= config.FreshMoveMaxBars * 2) score += 5m;
            else score -= 10m;

            if (features.ConsumedDisplacementFraction <= 0.45m) score += 8m;
            else if (features.ConsumedDisplacementFraction >= 0.90m) score -= 10m;

            if (features.DirectionalEfficiency >= 0.45m) score += 12m;
            else if (features.DirectionalEfficiency >= 0.30m) score += 7m;
            else if (features.DirectionalEfficiency < 0.18m) score -= 8m;

            if (features.EfficiencyDelta >= 0.08m) score += 10m;
            else if (features.EfficiencyDelta <= -0.08m) score -= 10m;

            if (features.CompressionRatio <= 0.35m) score += 8m;
            else if (features.CompressionRatio >= 0.75m) score -= 5m;

            if (features.PullbackResetCount >= 1 && features.PullbackResetCount <= 3) score += 8m;
            else if (features.PullbackResetCount >= 6) score -= 6m;

            if (features.RiskEfficiency >= 1.50m) score += 12m;
            else if (features.RiskEfficiency >= 1.00m) score += 7m;
            else if (features.RiskEfficiency < 0.60m) score -= 10m;

            if (features.AccelerationRatio >= 1.20m) score += 8m;
            else if (features.AccelerationRatio < 0.60m) score -= 7m;

            score -= features.ExhaustionRisk * 20m;
            return Clamp(score, 0m, 100m);
        }

        public IReadOnlyList<MorningOpportunityPotentialBucket> BuildBuckets(
            IReadOnlyList<MorningOpportunityPotentialObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));

            var definitions = new[]
            {
                new BucketDefinition("0-39", 0m, 40m),
                new BucketDefinition("40-54", 40m, 55m),
                new BucketDefinition("55-69", 55m, 70m),
                new BucketDefinition("70-84", 70m, 85m),
                new BucketDefinition("85-100", 85m, 101m)
            };

            var result = new List<MorningOpportunityPotentialBucket>();
            foreach (var definition in definitions)
            {
                var members = observations
                    .Where(x => x.PotentialScore >= definition.Minimum && x.PotentialScore < definition.MaximumExclusive)
                    .ToList();

                result.Add(new MorningOpportunityPotentialBucket(
                    definition.Label,
                    members.Count,
                    members.Count == 0 ? 0m : members.Average(x => x.Source.MaxFavorableTicks),
                    members.Count == 0 ? 0m : members.Average(x => x.Source.RealizedDollars),
                    members.Count == 0 ? 0m : (decimal)members.Count(x => x.Source.RealizedDollars > 0m) / members.Count));
            }

            return result;
        }

        private static decimal DirectionalEfficiency(IReadOnlyList<HistoricalBar> bars, decimal sign)
        {
            if (bars.Count < 2) return 0m;
            var displacement = sign * (bars[bars.Count - 1].Close - bars[0].Open);
            decimal travel = 0m;
            for (var i = 1; i < bars.Count; i++)
                travel += Math.Abs(bars[i].Close - bars[i - 1].Close);
            if (travel <= 0m) return 0m;
            return Clamp(displacement / travel, -1m, 1m);
        }

        private static int CountPullbackResets(IReadOnlyList<HistoricalBar> bars, decimal sign)
        {
            var count = 0;
            var inPullback = false;
            foreach (var bar in bars)
            {
                var oppositeBody = sign * (bar.Close - bar.Open) < 0m;
                if (oppositeBody && !inPullback)
                {
                    count++;
                    inPullback = true;
                }
                else if (!oppositeBody)
                {
                    inPullback = false;
                }
            }
            return count;
        }

        private static decimal CalculateAcceleration(IReadOnlyList<HistoricalBar> bars, decimal sign)
        {
            if (bars.Count < 10) return 1m;
            var recent = bars.Skip(bars.Count - 5).ToList();
            var previous = bars.Skip(bars.Count - 10).Take(5).ToList();
            var recentDisplacement = Math.Max(0m, sign * (recent[recent.Count - 1].Close - recent[0].Open));
            var previousDisplacement = Math.Max(0m, sign * (previous[previous.Count - 1].Close - previous[0].Open));
            if (previousDisplacement <= 0m) return recentDisplacement > 0m ? 2m : 1m;
            return Clamp(recentDisplacement / previousDisplacement, 0m, 3m);
        }

        private decimal CalculateExhaustionRisk(
            int moveAgeBars,
            decimal consumedFraction,
            decimal efficiency,
            decimal efficiencyDelta,
            int pullbackResetCount,
            decimal accelerationRatio)
        {
            decimal risk = 0m;
            if (moveAgeBars > config.FreshMoveMaxBars * 2) risk += 0.25m;
            if (consumedFraction >= 0.80m) risk += 0.25m;
            if (efficiencyDelta <= -0.08m) risk += 0.25m;
            if (efficiency < 0.25m) risk += 0.10m;
            if (pullbackResetCount == 0 && moveAgeBars > config.FreshMoveMaxBars) risk += 0.10m;
            if (accelerationRatio < 0.60m) risk += 0.15m;
            return Clamp(risk, 0m, 1m);
        }

        private static int IndexOfMinimum(IReadOnlyList<HistoricalBar> bars, Func<HistoricalBar, decimal> selector)
        {
            var index = 0;
            var value = selector(bars[0]);
            for (var i = 1; i < bars.Count; i++)
            {
                var candidate = selector(bars[i]);
                if (candidate < value)
                {
                    value = candidate;
                    index = i;
                }
            }
            return index;
        }

        private static int IndexOfMaximum(IReadOnlyList<HistoricalBar> bars, Func<HistoricalBar, decimal> selector)
        {
            var index = 0;
            var value = selector(bars[0]);
            for (var i = 1; i < bars.Count; i++)
            {
                var candidate = selector(bars[i]);
                if (candidate > value)
                {
                    value = candidate;
                    index = i;
                }
            }
            return index;
        }

        private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private sealed class BucketDefinition
        {
            public BucketDefinition(string label, decimal minimum, decimal maximumExclusive)
            {
                Label = label;
                Minimum = minimum;
                MaximumExclusive = maximumExclusive;
            }

            public string Label { get; }
            public decimal Minimum { get; }
            public decimal MaximumExclusive { get; }
        }
    }
}
