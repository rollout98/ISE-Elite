using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    /// <summary>
    /// V5.6 research-only stability-weighted Potential model.
    /// Uses only causal features that showed the most stable directional relationship
    /// across blocked time windows: move age, efficiency delta, acceleration, and exhaustion.
    /// Other V5 features remain available as context/diagnostics but do not directly affect this score.
    /// Future MFE/MAE/realized P&L are never score inputs.
    /// </summary>
    public sealed class MorningStabilityWeightedPotentialObservation
    {
        public MorningStabilityWeightedPotentialObservation(
            MorningOpportunityPotentialObservation source,
            decimal stabilityWeightedScore)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            StabilityWeightedScore = stabilityWeightedScore;
        }

        public MorningOpportunityPotentialObservation Source { get; }
        public decimal StabilityWeightedScore { get; }
    }

    public sealed class MorningStabilityWeightedPotentialAnalyzer
    {
        public IReadOnlyList<MorningStabilityWeightedPotentialObservation> Analyze(
            IReadOnlyList<MorningOpportunityPotentialObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));

            return observations
                .Select(x => new MorningStabilityWeightedPotentialObservation(x, Score(x.Features)))
                .ToList();
        }

        public decimal Score(MorningOpportunityPotentialFeatures features)
        {
            if (features == null) throw new ArgumentNullException(nameof(features));

            var freshness = Clamp((24m - features.MoveAgeBars) / 18m, -1m, 1m);
            var strengthening = Clamp(features.EfficiencyDelta / 0.60m, -1m, 1m);
            var acceleration = Clamp((features.AccelerationRatio - 1m) / 1.50m, -1m, 1m);
            var exhaustionPenalty = 20m * Clamp(features.ExhaustionRisk, 0m, 1m);

            var score = 50m
                + (30m * freshness)
                + (20m * strengthening)
                + (10m * acceleration)
                - exhaustionPenalty;

            return Clamp(score, 0m, 100m);
        }

        private static decimal Clamp(decimal value, decimal min, decimal max)
            => value < min ? min : value > max ? max : value;
    }
}
