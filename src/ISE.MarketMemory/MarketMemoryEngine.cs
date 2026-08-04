using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.MarketMemory
{
    public enum HistoricalOutcome { Continuation, Reversal, Rotation }
    public enum MemoryStatus { Ready, InsufficientHistory, Blocked }

    public sealed class MarketFingerprint
    {
        public MarketFingerprint(string instrument, string session, string regime, string context,
            string narrative, string liquidity, string volatility, string auction, int confidence)
        {
            Instrument = Required(instrument, nameof(instrument));
            Session = Required(session, nameof(session));
            Regime = Required(regime, nameof(regime));
            Context = Required(context, nameof(context));
            Narrative = Required(narrative, nameof(narrative));
            Liquidity = Required(liquidity, nameof(liquidity));
            Volatility = Required(volatility, nameof(volatility));
            Auction = Required(auction, nameof(auction));
            if (confidence < 0 || confidence > 100) throw new ArgumentOutOfRangeException(nameof(confidence));
            Confidence = confidence;
        }

        public string Instrument { get; }
        public string Session { get; }
        public string Regime { get; }
        public string Context { get; }
        public string Narrative { get; }
        public string Liquidity { get; }
        public string Volatility { get; }
        public string Auction { get; }
        public int Confidence { get; }

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
            return value.Trim();
        }
    }

    public sealed class HistoricalObservation
    {
        public HistoricalObservation(MarketFingerprint fingerprint, HistoricalOutcome outcome,
            decimal maximumFavorableExcursion, decimal maximumAdverseExcursion)
        {
            Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
            Outcome = outcome;
            MaximumFavorableExcursion = maximumFavorableExcursion;
            MaximumAdverseExcursion = maximumAdverseExcursion;
        }

        public MarketFingerprint Fingerprint { get; }
        public HistoricalOutcome Outcome { get; }
        public decimal MaximumFavorableExcursion { get; }
        public decimal MaximumAdverseExcursion { get; }
    }

    public sealed class HistoricalMatch
    {
        public HistoricalMatch(HistoricalObservation observation, decimal similarity)
        {
            Observation = observation;
            Similarity = similarity;
        }

        public HistoricalObservation Observation { get; }
        public decimal Similarity { get; }
    }

    public sealed class MarketMemoryDecision
    {
        public MarketMemoryDecision(MemoryStatus status, IReadOnlyList<HistoricalMatch> matches,
            decimal continuationProbability, decimal reversalProbability, decimal rotationProbability,
            int confidenceAdjustment, string reason)
        {
            Status = status;
            Matches = matches;
            ContinuationProbability = continuationProbability;
            ReversalProbability = reversalProbability;
            RotationProbability = rotationProbability;
            ConfidenceAdjustment = confidenceAdjustment;
            Reason = reason;
        }

        public MemoryStatus Status { get; }
        public IReadOnlyList<HistoricalMatch> Matches { get; }
        public decimal ContinuationProbability { get; }
        public decimal ReversalProbability { get; }
        public decimal RotationProbability { get; }
        public int ConfidenceAdjustment { get; }
        public string Reason { get; }
    }

    public sealed class MarketMemoryEngine
    {
        private const decimal MinimumSimilarity = 0.60m;
        private const int MinimumEvidence = 3;

        public MarketMemoryDecision Evaluate(MarketFingerprint current,
            IEnumerable<HistoricalObservation> history, bool authoritativeRiskBlock = false, int maxMatches = 20)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (history == null) throw new ArgumentNullException(nameof(history));
            if (maxMatches < 1) throw new ArgumentOutOfRangeException(nameof(maxMatches));

            if (authoritativeRiskBlock)
                return new MarketMemoryDecision(MemoryStatus.Blocked, Array.Empty<HistoricalMatch>(), 0, 0, 0, 0,
                    "Historical evidence cannot override an authoritative risk block.");

            var matches = history
                .Select(x => new HistoricalMatch(x, Similarity(current, x.Fingerprint)))
                .Where(x => x.Similarity >= MinimumSimilarity)
                .OrderByDescending(x => x.Similarity)
                .ThenBy(x => x.Observation.Fingerprint.Instrument, StringComparer.Ordinal)
                .Take(maxMatches)
                .ToArray();

            if (matches.Length < MinimumEvidence)
                return new MarketMemoryDecision(MemoryStatus.InsufficientHistory, matches, 0, 0, 0, 0,
                    "Insufficient comparable history is available for a reliable memory decision.");

            decimal totalWeight = matches.Sum(x => x.Similarity);
            decimal continuation = WeightedProbability(matches, HistoricalOutcome.Continuation, totalWeight);
            decimal reversal = WeightedProbability(matches, HistoricalOutcome.Reversal, totalWeight);
            decimal rotation = WeightedProbability(matches, HistoricalOutcome.Rotation, totalWeight);
            decimal averageSimilarity = matches.Average(x => x.Similarity);
            int adjustment = CalculateConfidenceAdjustment(matches.Length, averageSimilarity);

            return new MarketMemoryDecision(MemoryStatus.Ready, matches, continuation, reversal, rotation, adjustment,
                $"{matches.Length} comparable environments produced an average similarity of {averageSimilarity:P0}.");
        }

        public decimal Similarity(MarketFingerprint left, MarketFingerprint right)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));

            decimal score = 0;
            score += Equal(left.Instrument, right.Instrument) ? 0.15m : 0;
            score += Equal(left.Session, right.Session) ? 0.10m : 0;
            score += Equal(left.Regime, right.Regime) ? 0.15m : 0;
            score += Equal(left.Context, right.Context) ? 0.20m : 0;
            score += Equal(left.Narrative, right.Narrative) ? 0.15m : 0;
            score += Equal(left.Liquidity, right.Liquidity) ? 0.08m : 0;
            score += Equal(left.Volatility, right.Volatility) ? 0.07m : 0;
            score += Equal(left.Auction, right.Auction) ? 0.07m : 0;
            score += 0.03m * (1m - Math.Min(100, Math.Abs(left.Confidence - right.Confidence)) / 100m);
            return Math.Round(score, 4, MidpointRounding.AwayFromZero);
        }

        private static decimal WeightedProbability(IEnumerable<HistoricalMatch> matches,
            HistoricalOutcome outcome, decimal totalWeight) =>
            totalWeight == 0 ? 0 : Math.Round(matches.Where(x => x.Observation.Outcome == outcome)
                .Sum(x => x.Similarity) / totalWeight, 4, MidpointRounding.AwayFromZero);

        private static int CalculateConfidenceAdjustment(int count, decimal averageSimilarity)
        {
            if (count >= 10 && averageSimilarity >= 0.85m) return 6;
            if (count >= 5 && averageSimilarity >= 0.75m) return 3;
            return 1;
        }

        private static bool Equal(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
