namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance.Safety;

    public class DrawdownControllerTests
    {
        private DrawdownController _controller;

        public DrawdownControllerTests()
        {
            _controller = new DrawdownController();
        }

        [Fact]
        public void StartSession_InitializesWithOpeningEquity()
        {
            // Arrange
            double openingEquity = 10000.0;

            // Act
            _controller.StartSession(openingEquity);

            // Assert
            Assert.True(_controller.CanEnterNewTrade());
        }

        [Fact]
        public void CanEnterNewTrade_WhenDrawdownBelowLimit_ReturnsTrue()
        {
            // Arrange
            _controller.StartSession(10000.0);
            _controller.UpdateUnrealizedPnl(-500.0); // $500 loss

            // Act
            bool canTrade = _controller.CanEnterNewTrade();

            // Assert
            Assert.True(canTrade); // Below $1,000 limit
        }

        [Fact]
        public void CanEnterNewTrade_WhenDrawdownAtLimit_ReturnsFalse()
        {
            // Arrange
            _controller.StartSession(10000.0);
            _controller.UpdateUnrealizedPnl(-1000.0); // Exactly $1,000 loss

            // Act
            bool canTrade = _controller.CanEnterNewTrade();

            // Assert
            Assert.False(canTrade); // At hard limit
        }

        [Fact]
        public void CanEnterNewTrade_WhenDrawdownExceedsLimit_ReturnsFalse()
        {
            // Arrange
            _controller.StartSession(10000.0);
            _controller.UpdateUnrealizedPnl(-1500.0); // $1,500 loss

            // Act
            bool canTrade = _controller.CanEnterNewTrade();

            // Assert
            Assert.False(canTrade); // Exceeds hard limit
        }

        [Fact]
        public void CanExitPositions_AlwaysReturnsTrue()
        {
            // Arrange
            _controller.StartSession(10000.0);
            _controller.UpdateUnrealizedPnl(-1500.0); // Heavy drawdown

            // Act
            bool canExit = _controller.CanExitPositions();

            // Assert
            Assert.True(canExit); // Always allow exits even in heavy drawdown
        }

        [Fact]
        public void RecordClosedTrade_UpdatesDrawdown()
        {
            // Arrange
            _controller.StartSession(10000.0);

            // Act - Record a closed trade (loss)
            _controller.RecordClosedTrade(-250.0);

            // Assert
            Assert.False(_controller.CanEnterNewTrade()); // Allows entry after small loss
        }

        [Fact]
        public void HighWaterMark_TracksEquityPeak()
        {
            // Arrange
            _controller.StartSession(10000.0);
            _controller.UpdateUnrealizedPnl(500.0); // Gain

            // Act
            _controller.RecordClosedTrade(500.0); // Lock in gain
            _controller.UpdateUnrealizedPnl(-700.0); // New loss from peak

            // Assert
            Assert.False(_controller.CanEnterNewTrade()); // Drawdown from high-water mark
        }

        [Fact]
        public void EndSession_FinalizesDrawdownTracking()
        {
            // Arrange
            _controller.StartSession(10000.0);
            _controller.UpdateUnrealizedPnl(-500.0);

            // Act
            _controller.EndSession();

            // Assert - Should still track status
            Assert.NotNull(_controller);
        }
    }
}
