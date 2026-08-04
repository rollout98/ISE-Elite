using System.Collections.Generic;
using ISE.MarketMemory;
using Xunit;

namespace ISE.MarketMemory.Tests
{
    public sealed class MarketMemoryEngineTests
    {
        [Fact]
        public void Identical_fingerprints_produce_maximum_similarity()
        {
            var engine = new MarketMemoryEngine();
            var fingerprint = Fingerprint();

            Assert.Equal(1.00m, engine.Similarity(fingerprint, fingerprint));
        }

        [Fact]
        public void Similar_fingerprints_rank_ahead_of_unrelated_history()
        {
            var engine = new MarketMemoryEngine();
            var current = Fingerprint();
            var similar = new HistoricalObservation(Fingerprint(confidence: 88), HistoricalOutcome.Continuation, 150, 40);
            var unrelated = new HistoricalObservation(
                new MarketFingerprint("MGC", "Asia", "Balanced", "Rotation", "Neutral", "None", "Low", "Balanced", 40),
                HistoricalOutcome.Rotation, 30, 50);
            var third = new HistoricalObservation(Fingerprint(confidence: 80), HistoricalOutcome.Continuation, 120, 35);

            var result = engine.Evaluate(current, new[] { unrelated, similar, third });

            Assert.Equal(MemoryStatus.InsufficientHistory, result.Status);
            Assert.Equal(2, result.Matches.Count);
            Assert.Same(similar, result.Matches[0].Observation);
            Assert.True(result.Matches[0].Similarity > result.Matches[1].Similarity);
        }

        [Fact]
        public void No_history_returns_insufficient_history()
        {
            var result = new MarketMemoryEngine().Evaluate(Fingerprint(), new List<HistoricalObservation>());

            Assert.Equal(MemoryStatus.InsufficientHistory, result.Status);
            Assert.Empty(result.Matches);
            Assert.Equal(0, result.ConfidenceAdjustment);
        }

        [Fact]
        public void Strong_repeated_evidence_increases_confidence()
        {
            var history = new List<HistoricalObservation>();
            for (int index = 0; index < 10; index++)
                history.Add(new HistoricalObservation(Fingerprint(confidence: 90 - index % 2),
                    HistoricalOutcome.Continuation, 180, 35));

            var result = new MarketMemoryEngine().Evaluate(Fingerprint(), history);

            Assert.Equal(MemoryStatus.Ready, result.Status);
            Assert.Equal(6, result.ConfidenceAdjustment);
            Assert.Equal(1.00m, result.ContinuationProbability);
        }

        private static MarketFingerprint Fingerprint(int confidence = 90) =>
            new MarketFingerprint("MNQ", "NewYorkOpen", "TrendExpansion", "OpeningDrive",
                "InstitutionalBuying", "SellSideSweep", "Normal", "Acceptance", confidence);
    }
}
