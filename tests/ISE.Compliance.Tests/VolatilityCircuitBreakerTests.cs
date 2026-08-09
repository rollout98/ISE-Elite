namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance.Safety;

    public class VolatilityCircuitBreakerTests
    {
        private VolatilityCircuitBreaker _breaker;

        public VolatilityCircuitBreakerTests()
        {
            _breaker = new VolatilityCircuitBreaker();
        }

        [Fact]
        public void UpdateAtr_WhenNormal_AllowsTrading()
        {
            // Arrange & Act
            _breaker.UpdateAtr(1.0); // Normal ATR
            _breaker.UpdateAtr(1.1);
            _breaker.UpdateAtr(0.9);

            // Assert
            Assert.True(_breaker.CanEnterNewTrades());
        }

        [Fact]
        public void UpdateAtr_WhenSpikeDetected_TripsBreaker()
        {
            // Arrange & Act
            // Set baseline
            for (int i = 0; i < 5; i++)
                _breaker.UpdateAtr(1.0);

            // Spike (ATR > 150% of average)
            _breaker.UpdateAtr(2.5);

            // Assert
            Assert.True(_breaker.IsTripped);
        }

        [Fact]
        public void VolatilityRatio_CalculatedCorrectly()
        {
            // Arrange & Act
            for (int i = 0; i < 5; i++)
                _breaker.UpdateAtr(1.0);

            _breaker.UpdateAtr(1.6); // 60% spike

            // Assert
            Assert.True(_breaker.VolatilityRatio > 1.0);
        }

        [Fact]
        public void CanExitTrades_AlwaysReturnsTrue()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
                _breaker.UpdateAtr(1.0);
            _breaker.UpdateAtr(2.5); // Trip breaker

            // Act & Assert
            Assert.True(_breaker.CanExitTrades()); // Always allow exits
        }

        [Fact]
        public void Recovery_AfterVolatilityNormalizes()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
                _breaker.UpdateAtr(1.0);
            _breaker.UpdateAtr(2.5); // Trip

            // Act - Return to normal
            _breaker.UpdateAtr(1.1); // Back to normal
            _breaker.UpdateAtr(0.9); // Back to normal

            // Assert - Should recover after 2 bars
            Assert.False(_breaker.IsTripped); // Should recover
        }

        [Fact]
        public void TripReason_ProvidesClearnessOnTrip()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
                _breaker.UpdateAtr(1.0);
            _breaker.UpdateAtr(2.5); // Trip

            // Act & Assert
            Assert.NotNull(_breaker.TripReason);
            Assert.True(_breaker.TripReason.Length > 0);
        }

        [Fact]
        public void GetStatus_ReturnsReadableStatus()
        {
            // Arrange & Act
            _breaker.UpdateAtr(1.0);
            var status = _breaker.GetStatus();

            // Assert
            Assert.NotNull(status);
            Assert.True(status.Length > 0);
        }

        [Fact]
        public void Reset_ClearsState()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
                _breaker.UpdateAtr(1.0);
            _breaker.UpdateAtr(2.5); // Trip

            // Act
            _breaker.Reset();

            // Assert
            Assert.False(_breaker.IsTripped);
            Assert.Equal(0, _breaker.BarsSinceTrip);
        }
    }
}
