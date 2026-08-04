using ISE.MultiTimeframeIntelligence;
using Xunit;

namespace ISE.MultiTimeframeIntelligence.Tests
{
    public sealed class MultiTimeframeIntelligenceEngineTests
    {
        [Fact]
        public void Aligned_bullish_timeframes_allow_full_size()
        {
            var result = new MultiTimeframeIntelligenceEngine().Evaluate(new[]
            {
                Frame("1m", 1, TimeframeDirection.Bullish, 82, 78),
                Frame("5m", 5, TimeframeDirection.Bullish, 88, 84),
                Frame("15m", 15, TimeframeDirection.Bullish, 80, 86)
            });

            Assert.Equal(TimeframeAlignment.AlignedBullish, result.Alignment);
            Assert.Equal(TimeframeDirection.Bullish, result.ControllingDirection);
            Assert.Equal(TimeframePosture.FullSize, result.Posture);
        }

        [Fact]
        public void Strong_higher_timeframe_overrides_low_timeframe_noise()
        {
            var result = new MultiTimeframeIntelligenceEngine().Evaluate(new[]
            {
                Frame("1m", 1, TimeframeDirection.Bearish, 35, 30),
                Frame("5m", 5, TimeframeDirection.Bullish, 76, 80),
                Frame("60m", 60, TimeframeDirection.Bullish, 94, 92)
            });

            Assert.Equal(TimeframeDirection.Bullish, result.ControllingDirection);
            Assert.Equal("60m", result.ControllingTimeframe);
            Assert.Equal(TimeframeAlignment.AlignedBullish, result.Alignment);
        }

        [Fact]
        public void Cross_timeframe_conflict_reduces_size()
        {
            var result = new MultiTimeframeIntelligenceEngine().Evaluate(new[]
            {
                Frame("1m", 1, TimeframeDirection.Bullish, 90, 86),
                Frame("5m", 5, TimeframeDirection.Bearish, 88, 84),
                Frame("15m", 15, TimeframeDirection.Bullish, 72, 70, true),
                Frame("60m", 60, TimeframeDirection.Bearish, 82, 88)
            });

            Assert.Equal(TimeframeAlignment.Mixed, result.Alignment);
            Assert.Equal(TimeframePosture.ReducedSize, result.Posture);
        }

        [Fact]
        public void Authoritative_risk_block_forces_stand_aside()
        {
            var result = new MultiTimeframeIntelligenceEngine().Evaluate(new[]
            {
                Frame("15m", 15, TimeframeDirection.Bullish, 95, 95)
            }, authoritativeRiskBlock: true);

            Assert.Equal(TimeframePosture.StandAside, result.Posture);
            Assert.Equal(TimeframeDirection.Neutral, result.ControllingDirection);
            Assert.Equal(0, result.Confidence);
        }

        private static TimeframeEvidence Frame(string name, int minutes, TimeframeDirection direction,
            int trendStrength, int structureQuality, bool transitioning = false) =>
            new TimeframeEvidence(name, minutes, direction, trendStrength, structureQuality, transitioning);
    }
}
