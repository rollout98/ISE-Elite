using ISE.InstitutionalNarrative;
using Xunit;

namespace ISE.InstitutionalNarrative.Tests
{
    public sealed class InstitutionalNarrativeEngineTests
    {
        [Fact]
        public void Buyer_control_with_acceptance_supports_continuation()
        {
            var result = new InstitutionalNarrativeEngine().Evaluate(new InstitutionalNarrativeInput(0.8m, 0.6m, 0.7m, 0.1m, 0.9m, false));
            Assert.Equal(ParticipantControl.Buyers, result.Control);
            Assert.Equal(InventoryCondition.Accumulation, result.Inventory);
            Assert.Equal(NarrativeExpectation.ContinuationHigher, result.Expectation);
        }

        [Fact]
        public void Sell_side_sweep_supports_bullish_reversal()
        {
            var result = new InstitutionalNarrativeEngine().Evaluate(new InstitutionalNarrativeInput(0.2m, 0.2m, 0.1m, 0.8m, 0.8m, false));
            Assert.Equal(LiquidityEvent.SellSideSweep, result.Liquidity);
            Assert.Equal(NarrativeExpectation.ReversalHigher, result.Expectation);
        }

        [Fact]
        public void Seller_control_with_acceptance_supports_continuation()
        {
            var result = new InstitutionalNarrativeEngine().Evaluate(new InstitutionalNarrativeInput(-0.8m, -0.6m, -0.7m, -0.1m, 0.9m, false));
            Assert.Equal(ParticipantControl.Sellers, result.Control);
            Assert.Equal(InventoryCondition.Distribution, result.Inventory);
            Assert.Equal(NarrativeExpectation.ContinuationLower, result.Expectation);
        }

        [Fact]
        public void Risk_block_overrides_market_narrative()
        {
            var result = new InstitutionalNarrativeEngine().Evaluate(new InstitutionalNarrativeInput(1m, 1m, 1m, 1m, 1m, true));
            Assert.Equal(NarrativeExpectation.StandAside, result.Expectation);
            Assert.Contains("blocked", result.Thesis.ToLowerInvariant());
        }
    }
}
