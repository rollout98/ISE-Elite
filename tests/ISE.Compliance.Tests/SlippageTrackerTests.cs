namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance.Safety;

    public class SlippageTrackerTests
    {
        private SlippageTracker _tracker;

        public SlippageTrackerTests()
        {
            _tracker = new SlippageTracker();
        }

        [Fact]
        public void RecordEntryFill_TracksEntrySlippage()
        {
            // Act
            _tracker.RecordEntryFill(100.00, 100.05, 2, DateTime.Now);

            // Assert
            Assert.Equal(0.05, _tracker.AverageEntrySlippage, 2);
        }

        [Fact]
        public void RecordExitFill_TracksExitSlippage()
        {
            // Arrange
            _tracker.RecordEntryFill(100.00, 100.05, 2, DateTime.Now);

            // Act
            _tracker.RecordExitFill(105.00, 104.95, DateTime.Now);

            // Assert
            Assert.Equal(0.05, _tracker.AverageExitSlippage, 2);
        }

        [Fact]
        public void MultipleEntries_CalculatesAverageSlippage()
        {
            // Act
            _tracker.RecordEntryFill(100.00, 100.05, 1, DateTime.Now);
            _tracker.RecordEntryFill(101.00, 101.15, 1, DateTime.Now);

            // Assert - Average should be (0.05 + 0.15) / 2 = 0.10
            Assert.True(_tracker.AverageEntrySlippage >= 0.08 && _tracker.AverageEntrySlippage <= 0.12);
        }

        [Fact]
        public void ZeroSlippage_IndicatesPerfectExecution()
        {
            // Act
            _tracker.RecordEntryFill(100.00, 100.00, 2, DateTime.Now); // Perfect fill

            // Assert
            Assert.Equal(0.0, _tracker.AverageEntrySlippage);
            Assert.True(_tracker.SlippageQuality > 0.95); // High quality
        }

        [Fact]
        public void SlippageQuality_ReflectsExecutionQuality()
        {
            // Arrange
            _tracker.RecordEntryFill(100.00, 100.05, 1, DateTime.Now);
            _tracker.RecordExitFill(105.00, 104.95, DateTime.Now);

            // Act & Assert
            Assert.True(_tracker.SlippageQuality >= 0.5); // Some slippage detected
        }

        [Fact]
        public void GetRecentSlippage_ReturnsCompletedTrades()
        {
            // Arrange
            _tracker.RecordEntryFill(100.00, 100.05, 1, DateTime.Now);
            _tracker.RecordExitFill(105.00, 104.95, DateTime.Now);
            _tracker.RecordEntryFill(106.00, 106.10, 1, DateTime.Now);

            // Act
            var recent = _tracker.GetRecentSlippage(10);

            // Assert
            Assert.NotEmpty(recent);
        }

        [Fact]
        public void GetWorstSlippageTrade_IdentifiesProblem()
        {
            // Arrange
            _tracker.RecordEntryFill(100.00, 100.05, 1, DateTime.Now);
            _tracker.RecordExitFill(105.00, 104.95, DateTime.Now);

            _tracker.RecordEntryFill(106.00, 106.50, 1, DateTime.Now); // Worse slippage
            _tracker.RecordExitFill(110.00, 109.50, DateTime.Now);

            // Act
            var worst = _tracker.GetWorstSlippageTrade();

            // Assert
            Assert.NotNull(worst);
            Assert.True(worst.TotalSlippage > 0.5);
        }

        [Fact]
        public void TradeCount_AccuratelyTracked()
        {
            // Act
            _tracker.RecordEntryFill(100.00, 100.05, 1, DateTime.Now);
            _tracker.RecordExitFill(105.00, 104.95, DateTime.Now);

            _tracker.RecordEntryFill(106.00, 106.10, 1, DateTime.Now);
            _tracker.RecordExitFill(110.00, 109.90, DateTime.Now);

            // Assert
            Assert.Equal(2, _tracker.TradeCount);
        }
    }
}
