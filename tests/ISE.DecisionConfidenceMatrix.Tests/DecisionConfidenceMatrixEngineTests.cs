using ISE.DecisionConfidenceMatrix;
using Xunit;

namespace ISE.DecisionConfidenceMatrix.Tests
{
    public sealed class DecisionConfidenceMatrixEngineTests
    {
        [Fact]
        public void Strong_alignment_is_approved_at_full_size()
        {
            var result = new DecisionConfidenceMatrixEngine().Evaluate(new[]
            {
                Evidence("Trend", 93, 1.2m), Evidence("Liquidity", 91, 1.0m),
                Evidence("Narrative", 90, 1.1m), Evidence("Execution Quality", 88, 1.2m)
            });

            Assert.Equal(DecisionMatrixStatus.Approved, result.Status);
            Assert.Equal(DecisionPosture.FullSize, result.Posture);
            Assert.True(result.OverallScore >= 85);
        }

        [Fact]
        public void Weak_execution_quality_reduces_participation()
        {
            var result = new DecisionConfidenceMatrixEngine().Evaluate(new[]
            {
                Evidence("Trend", 92, 1.0m), Evidence("Narrative", 88, 1.0m),
                Evidence("Execution Quality", 58, 1.5m)
            });

            Assert.Equal(DecisionMatrixStatus.Reduced, result.Status);
            Assert.Equal(DecisionPosture.ReducedSize, result.Posture);
        }

        [Fact]
        public void Failed_required_evidence_rejects_the_decision()
        {
            var result = new DecisionConfidenceMatrixEngine().Evaluate(new[]
            {
                Evidence("Market Intelligence", 94, 1.0m), Evidence("Risk", 40, 2.0m, true),
                Evidence("Execution Quality", 90, 1.0m)
            });

            Assert.Equal(DecisionMatrixStatus.Rejected, result.Status);
            Assert.Equal(DecisionPosture.StandAside, result.Posture);
            Assert.Contains("Risk", result.Reasons[0]);
        }

        [Fact]
        public void Authoritative_risk_block_overrides_elite_alignment()
        {
            var result = new DecisionConfidenceMatrixEngine().Evaluate(new[]
            {
                Evidence("Trend", 98, 1.0m), Evidence("Liquidity", 97, 1.0m),
                Evidence("Execution Quality", 96, 1.0m)
            }, authoritativeRiskBlock: true);

            Assert.Equal(DecisionMatrixStatus.Blocked, result.Status);
            Assert.Equal(0, result.OverallScore);
            Assert.Equal(DecisionPosture.StandAside, result.Posture);
        }

        private static DecisionEvidence Evidence(string name, int score, decimal weight, bool required = false) =>
            new DecisionEvidence(name, score, weight, required);
    }
}
