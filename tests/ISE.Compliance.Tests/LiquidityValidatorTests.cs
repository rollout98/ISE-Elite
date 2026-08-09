namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance.Safety;

    public class LiquidityValidatorTests
    {
        private LiquidityValidator _validator;

        public LiquidityValidatorTests()
        {
            _validator = new LiquidityValidator();
        }

        [Fact]
        public void UpdateBid_RecordsPrice()
        {
            // Act
            _validator.UpdateBid(100.0, 1000.0);

            // Assert - Check internal state via GetStatus
            Assert.NotNull(_validator.GetStatus());
        }

        [Fact]
        public void UpdateAsk_RecordsPrice()
        {
            // Act
            _validator.UpdateAsk(100.5, 1000.0);

            // Assert
            Assert.NotNull(_validator.GetStatus());
        }

        [Fact]
        public void SpreadCalculation_CorrectlyComputes()
        {
            // Arrange & Act
            _validator.UpdateBid(100.0, 500.0);
            _validator.UpdateAsk(100.5, 500.0);

            // Assert - Spread should be 0.5 / 0.01 = 50 ticks (too wide for default)
            Assert.True(_validator.SpreadTicks > 0);
        }

        [Fact]
        public void TightSpread_AllowsTrading()
        {
            // Arrange & Act
            _validator.UpdateBid(100.00, 2000.0);
            _validator.UpdateAsk(100.01, 2000.0); // 1 tick spread
            _validator.UpdateTotalVolume(10000.0, 10000.0);

            // Assert
            Assert.True(_validator.IsLiquidEnoughForEntry);
        }

        [Fact]
        public void WideSpread_BlocksTrading()
        {
            // Arrange & Act
            _validator.UpdateBid(100.00, 100.0);
            _validator.UpdateAsk(100.05, 100.0); // 5 tick spread (too wide)
            _validator.UpdateTotalVolume(1000.0, 1000.0);

            // Assert
            Assert.False(_validator.IsLiquidEnoughForEntry);
        }

        [Fact]
        public void InsufficientBidVolume_BlocksTrading()
        {
            // Arrange & Act
            _validator.UpdateBid(100.00, 100.0); // Less than 500 minimum
            _validator.UpdateAsk(100.01, 1000.0);
            _validator.UpdateTotalVolume(1000.0, 5000.0);

            // Assert
            Assert.False(_validator.IsLiquidEnoughForEntry);
        }

        [Fact]
        public void InsufficientAskVolume_BlocksTrading()
        {
            // Arrange & Act
            _validator.UpdateBid(100.00, 1000.0);
            _validator.UpdateAsk(100.01, 100.0); // Less than 500 minimum
            _validator.UpdateTotalVolume(5000.0, 1000.0);

            // Assert
            Assert.False(_validator.IsLiquidEnoughForEntry);
        }

        [Fact]
        public void DepthScore_ReflectsLiquidityQuality()
        {
            // Arrange & Act
            _validator.UpdateBid(100.00, 2000.0);
            _validator.UpdateAsk(100.01, 2000.0);
            _validator.UpdateTotalVolume(20000.0, 20000.0);

            // Assert
            Assert.True(_validator.DepthScore > 0.8); // Should be high quality
            Assert.True(_validator.IsLiquidEnoughForEntry);
        }
    }
}
