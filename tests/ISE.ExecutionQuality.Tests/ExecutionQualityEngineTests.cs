using ISE.ExecutionQuality;
using Xunit;

namespace ISE.ExecutionQuality.Tests
{
    public sealed class ExecutionQualityEngineTests
    {
        [Fact]
        public void Ideal_pullback_is_approved_at_full_size()
        {
            var input = new ExecutionQualityInput(4, 0.82m, 0.92m, 0.90m, 0.15m, true);

            var result = new ExecutionQualityEngine().Evaluate(input);

            Assert.Equal(ExecutionQualityState.Ideal, result.State);
            Assert.Equal(ExecutionPosture.FullSize, result.Posture);
            Assert.True(result.Score >= 82);
        }

        [Fact]
        public void Missing_confirmation_is_classified_as_early()
        {
            var input = new ExecutionQualityInput(3, 0.35m, 0.90m, 0.90m, 0.10m, false);

            var result = new ExecutionQualityEngine().Evaluate(input);

            Assert.Equal(ExecutionQualityState.Early, result.State);
            Assert.Equal(ExecutionPosture.Wait, result.Posture);
        }

        [Fact]
        public void Extended_entry_is_rejected_as_chasing()
        {
            var input = new ExecutionQualityInput(22, 0.85m, 0.88m, 0.86m, 0.80m, true);

            var result = new ExecutionQualityEngine().Evaluate(input);

            Assert.Equal(ExecutionQualityState.Chasing, result.State);
            Assert.Equal(ExecutionPosture.StandAside, result.Posture);
        }

        [Fact]
        public void Authoritative_risk_block_overrides_execution_quality()
        {
            var input = new ExecutionQualityInput(2, 0.90m, 0.95m, 0.95m, 0.05m, true, true);

            var result = new ExecutionQualityEngine().Evaluate(input);

            Assert.Equal(ExecutionQualityState.Blocked, result.State);
            Assert.Equal(ExecutionPosture.StandAside, result.Posture);
            Assert.Equal(0, result.Score);
        }
    }
}
