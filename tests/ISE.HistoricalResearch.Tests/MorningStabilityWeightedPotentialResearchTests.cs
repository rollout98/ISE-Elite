using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningStabilityWeightedPotentialResearchTests
    {
        [Fact]
        public void ContextOnlyFeaturesDoNotChangeScore()
        {
            var analyzer = new MorningStabilityWeightedPotentialAnalyzer();

            var a = new MorningOpportunityPotentialFeatures(
                12, 50m, 0.30m, 0.10m, 0.30m, 0.20m, 1, 0.50m, 1.80m, 0.20m);

            var b = new MorningOpportunityPotentialFeatures(
                12, 500m, 1.40m, 0.90m, 0.30m, 1.50m, 8, 9.00m, 1.80m, 0.20m);

            Assert.Equal(analyzer.Score(a), analyzer.Score(b));
        }

        [Fact]
        public void YoungerMoveScoresHigher()
        {
            var analyzer = new MorningStabilityWeightedPotentialAnalyzer();

            Assert.True(
                analyzer.Score(Features(8, 0.20m, 1.50m, 0.25m)) >
                analyzer.Score(Features(30, 0.20m, 1.50m, 0.25m)));
        }

        [Fact]
        public void StrengtheningEfficiencyScoresHigher()
        {
            var analyzer = new MorningStabilityWeightedPotentialAnalyzer();

            Assert.True(
                analyzer.Score(Features(15, 0.50m, 1.50m, 0.25m)) >
                analyzer.Score(Features(15, -0.30m, 1.50m, 0.25m)));
        }

        [Fact]
        public void AccelerationIsSupportingPositiveEvidence()
        {
            var analyzer = new MorningStabilityWeightedPotentialAnalyzer();

            Assert.True(
                analyzer.Score(Features(15, 0.20m, 2.50m, 0.25m)) >
                analyzer.Score(Features(15, 0.20m, 0.40m, 0.25m)));
        }

        [Fact]
        public void ExhaustionReducesScore()
        {
            var analyzer = new MorningStabilityWeightedPotentialAnalyzer();

            Assert.True(
                analyzer.Score(Features(15, 0.20m, 1.50m, 0.10m)) >
                analyzer.Score(Features(15, 0.20m, 1.50m, 0.80m)));
        }

        [Fact]
        public void ScoreIsBoundedZeroToOneHundred()
        {
            var analyzer = new MorningStabilityWeightedPotentialAnalyzer();

            Assert.InRange(analyzer.Score(Features(0, 5m, 10m, 0m)), 0m, 100m);
            Assert.InRange(analyzer.Score(Features(100, -5m, 0m, 1m)), 0m, 100m);
        }

        private static MorningOpportunityPotentialFeatures Features(
            int moveAge,
            decimal efficiencyDelta,
            decimal acceleration,
            decimal exhaustion)
        {
            return new MorningOpportunityPotentialFeatures(
                moveAge,
                100m,
                0.50m,
                0.30m,
                efficiencyDelta,
                0.40m,
                2,
                2.00m,
                acceleration,
                exhaustion);
        }
    }
}
