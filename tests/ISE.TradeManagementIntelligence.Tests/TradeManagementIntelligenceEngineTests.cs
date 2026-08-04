using ISE.TradeManagementIntelligence;
using Xunit;

namespace ISE.TradeManagementIntelligence.Tests
{
    public sealed class TradeManagementIntelligenceEngineTests
    {
        [Fact]
        public void Healthy_trade_is_held()
        {
            var result = new TradeManagementIntelligenceEngine().Evaluate(
                new TradeManagementInput(90, 84, 88, 0.20m, 80m, 15m, false, false));

            Assert.Equal(TradeManagementAction.Hold, result.Action);
            Assert.False(result.MoveToBreakEven);
            Assert.False(result.TrailStop);
        }

        [Fact]
        public void Progressed_trade_is_protected_at_break_even()
        {
            var result = new TradeManagementIntelligenceEngine().Evaluate(
                new TradeManagementInput(82, 75, 80, 0.45m, 140m, 25m, true, false));

            Assert.Equal(TradeManagementAction.Protect, result.Action);
            Assert.True(result.MoveToBreakEven);
        }

        [Fact]
        public void Failed_thesis_exits_the_position()
        {
            var result = new TradeManagementIntelligenceEngine().Evaluate(
                new TradeManagementInput(25, 35, 20, 0.15m, 30m, 75m, false, false));

            Assert.Equal(TradeManagementAction.Exit, result.Action);
            Assert.Equal(1m, result.ReduceFraction);
        }

        [Fact]
        public void Authoritative_risk_block_forces_closure()
        {
            var result = new TradeManagementIntelligenceEngine().Evaluate(
                new TradeManagementInput(95, 95, 95, 0.80m, 250m, 10m, true, true, true));

            Assert.Equal(TradeManagementAction.Blocked, result.Action);
            Assert.Equal(100, result.Confidence);
            Assert.Equal(1m, result.ReduceFraction);
        }
    }
}
