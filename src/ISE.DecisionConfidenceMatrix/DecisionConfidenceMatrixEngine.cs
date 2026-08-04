using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.DecisionConfidenceMatrix
{
    public enum DecisionMatrixStatus { Approved, Reduced, Rejected, Blocked }
    public enum DecisionPosture { FullSize, ReducedSize, StandAside }

    public sealed class DecisionEvidence
    {
        public DecisionEvidence(string name, int score, decimal weight, bool required = false)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Evidence name is required.", nameof(name));
            if (score < 0 || score > 100) throw new ArgumentOutOfRangeException(nameof(score));
            if (weight <= 0) throw new ArgumentOutOfRangeException(nameof(weight));
            Name = name.Trim();
            Score = score;
            Weight = weight;
            Required = required;
        }

        public string Name { get; }
        public int Score { get; }
        public decimal Weight { get; }
        public bool Required { get; }
    }

    public sealed class DecisionConfidenceResult
    {
        public DecisionConfidenceResult(DecisionMatrixStatus status, DecisionPosture posture, int overallScore,
            IReadOnlyList<DecisionEvidence> evidence, IReadOnlyList<string> reasons)
        {
            Status = status;
            Posture = posture;
            OverallScore = overallScore;
            Evidence = evidence;
            Reasons = reasons;
        }

        public DecisionMatrixStatus Status { get; }
        public DecisionPosture Posture { get; }
        public int OverallScore { get; }
        public IReadOnlyList<DecisionEvidence> Evidence { get; }
        public IReadOnlyList<string> Reasons { get; }
    }

    public sealed class DecisionConfidenceMatrixEngine
    {
        private const int RequiredMinimum = 50;

        public DecisionConfidenceResult Evaluate(IEnumerable<DecisionEvidence> evidence,
            bool authoritativeRiskBlock = false)
        {
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));
            var items = evidence.ToArray();
            if (items.Length == 0) throw new ArgumentException("At least one evidence item is required.", nameof(evidence));

            if (authoritativeRiskBlock)
                return Result(DecisionMatrixStatus.Blocked, DecisionPosture.StandAside, 0, items,
                    "An authoritative risk block overrides all supporting evidence.");

            var failedRequired = items.Where(x => x.Required && x.Score < RequiredMinimum).ToArray();
            int overall = WeightedScore(items);

            if (failedRequired.Length > 0)
                return new DecisionConfidenceResult(DecisionMatrixStatus.Rejected, DecisionPosture.StandAside, overall,
                    items, failedRequired.Select(x => $"Required evidence '{x.Name}' scored {x.Score}, below {RequiredMinimum}.").ToArray());

            int weakest = items.Min(x => x.Score);
            if (overall >= 85 && weakest >= 70)
                return Result(DecisionMatrixStatus.Approved, DecisionPosture.FullSize, overall, items,
                    "Evidence is strongly aligned and no component is materially weak.");

            if (overall >= 70 && weakest >= 50)
                return Result(DecisionMatrixStatus.Reduced, DecisionPosture.ReducedSize, overall, items,
                    "Evidence supports participation, but alignment is not strong enough for full posture.");

            return Result(DecisionMatrixStatus.Rejected, DecisionPosture.StandAside, overall, items,
                "Combined evidence does not meet the minimum decision threshold.");
        }

        private static int WeightedScore(IEnumerable<DecisionEvidence> evidence)
        {
            decimal totalWeight = evidence.Sum(x => x.Weight);
            decimal weighted = evidence.Sum(x => x.Score * x.Weight) / totalWeight;
            return (int)Math.Round(weighted, MidpointRounding.AwayFromZero);
        }

        private static DecisionConfidenceResult Result(DecisionMatrixStatus status, DecisionPosture posture,
            int score, IReadOnlyList<DecisionEvidence> evidence, string reason) =>
            new DecisionConfidenceResult(status, posture, score, evidence, new[] { reason });
    }
}
