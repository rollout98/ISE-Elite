using Xunit;

namespace ISE.Context.Tests
{
    public sealed class ContextEngineTests
    {
        private readonly ContextEngine engine = new ContextEngine();

        [Fact]
        public void Opening_drive_is_identified()
        {
            var result = engine.Evaluate(Input(directional: 0.90d, opening: 0.86d, isOpening: true));

            Assert.Equal(MarketContextType.OpeningDrive, result.Context);
            Assert.True(result.IsActionable);
        }

        [Fact]
        public void Failed_breakout_is_identified()
        {
            var result = engine.Evaluate(Input(acceptance: 0.18d, reversal: 0.82d));

            Assert.Equal(MarketContextType.FailedBreakout, result.Context);
        }

        [Fact]
        public void Trend_pullback_is_identified()
        {
            var result = engine.Evaluate(Input(directional: 0.78d, pullback: 0.74d));

            Assert.Equal(MarketContextType.TrendPullback, result.Context);
        }

        [Fact]
        public void Session_transition_is_identified()
        {
            var result = engine.Evaluate(Input(expansion: 0.64d, isTransition: true));

            Assert.Equal(MarketContextType.SessionTransition, result.Context);
        }

        [Fact]
        public void Compression_is_identified()
        {
            var result = engine.Evaluate(Input(compression: 0.83d, expansion: 0.20d));

            Assert.Equal(MarketContextType.Compression, result.Context);
        }

        [Fact]
        public void Reversal_window_is_identified()
        {
            var result = engine.Evaluate(Input(reversal: 0.78d, isReversal: true));

            Assert.Equal(MarketContextType.ReversalWindow, result.Context);
        }

        private static ContextInput Input(
            double directional = 0.20d,
            double opening = 0.20d,
            double acceptance = 0.60d,
            double pullback = 0.20d,
            double compression = 0.20d,
            double expansion = 0.20d,
            double reversal = 0.20d,
            double balance = 0.20d,
            bool isOpening = false,
            bool isTransition = false,
            bool isReversal = false)
        {
            return new ContextInput(
                directional,
                opening,
                acceptance,
                pullback,
                compression,
                expansion,
                reversal,
                balance,
                isOpening,
                isTransition,
                isReversal);
        }
    }
}
