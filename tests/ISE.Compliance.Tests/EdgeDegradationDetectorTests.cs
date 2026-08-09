namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance.Safety;

    public class EdgeDegradationDetectorTests
    {
        private EdgeDegradationDetector _detector;

        public EdgeDegradationDetectorTests()
        {
            _detector = new EdgeDegradationDetector();
        }

        [Fact]
        public void RecordTrade_TracksWin()
        {
            // Act
            _detector.RecordTrade(100.0); // Profit = win

            // Assert
            Assert.Equal(1, _detector.TradeCount);
        }

        [Fact]
        public void RecordTrade_TracksLoss()
        {
            // Act
            _detector.RecordTrade(-100.0); // Loss

            // Assert
            Assert.Equal(1, _detector.TradeCount);
        }

        [Fact]
        public void WinRate_CalculatedCorrectly()
        {
            // Act
            _detector.RecordTrade(100.0); // Win
            _detector.RecordTrade(100.0); // Win
            _detector.RecordTrade(-100.0); // Loss

            // Assert - 2 wins out of 3 = 66.67%
            Assert.True(_detector.CurrentWinRate >= 0.66 && _detector.CurrentWinRate <= 0.67);
        }

        [Fact]
        public void EdgeLost_WhenWinRateBelowThreshold()
        {
            // Act - Record 3 wins, 7 losses = 30% win rate
            for (int i = 0; i < 3; i++)
                _detector.RecordTrade(100.0);
            for (int i = 0; i < 7; i++)
                _detector.RecordTrade(-100.0);

            // Assert
            Assert.True(_detector.EdgeLost);
            Assert.False(_detector.CanTrade());
        }

        [Fact]
        public void EdgeHealthy_WhenWinRateAboveThreshold()
        {
            // Act - Record 8 wins, 2 losses = 80% win rate
            for (int i = 0; i < 8; i++)
                _detector.RecordTrade(100.0);
            for (int i = 0; i < 2; i++)
                _detector.RecordTrade(-100.0);

            // Assert
            Assert.False(_detector.EdgeLost);
            Assert.True(_detector.CanTrade());
        }

        [Fact]
        public void Streak_TracksConsecutiveWins()
        {
            // Act
            _detector.RecordTrade(100.0);
            _detector.RecordTrade(100.0);
            _detector.RecordTrade(100.0);

            // Assert
            Assert.Equal(3, _detector.WinningStreak);
            Assert.Equal(0, _detector.LosingStreak);
        }

        [Fact]
        public void Streak_TracksConsecutiveLosses()
        {
            // Act
            _detector.RecordTrade(-100.0);
            _detector.RecordTrade(-100.0);

            // Assert
            Assert.Equal(2, _detector.LosingStreak);
            Assert.Equal(0, _detector.WinningStreak);
        }

        [Fact]
        public void GetStatus_ReturnsReadableStatus()
        {
            // Arrange
            for (int i = 0; i < 5; i++)
                _detector.RecordTrade(100.0);

            // Act
            var status = _detector.GetStatus();

            // Assert
            Assert.NotNull(status);
            Assert.True(status.Length > 0);
            Assert.Contains("Win Rate", status);
        }

        [Fact]
        public void ExpectedValue_CalculatedCorrectly()
        {
            // Act
            _detector.RecordTrade(100.0); // Win $100
            _detector.RecordTrade(100.0); // Win $100
            _detector.RecordTrade(-50.0); // Loss $50

            // Assert - EV = (66.67% × $100) - (33.33% × $50) = $66.67 - $16.67 ≈ $50
            var ev = _detector.GetExpectedValue();
            Assert.True(ev > 40 && ev < 60); // Should be around $50
        }
    }
}
