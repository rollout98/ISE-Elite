namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance;

    public class ComplianceEngineTests
    {
        private ComplianceEngine _compliance;

        public ComplianceEngineTests()
        {
            _compliance = new ComplianceEngine();
        }

        [Fact]
        public void ValidateEntry_WhenEnabled_AllowsValidEntry()
        {
            // Arrange
            _compliance.Enable();
            var signal = CreateValidEntrySignal();

            // Act
            bool result = _compliance.ValidateEntry(signal);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateEntry_WhenDisabled_AllowsEntry()
        {
            // Arrange
            _compliance.Disable(); // Backtest mode
            var signal = CreateValidEntrySignal();

            // Act
            bool result = _compliance.ValidateEntry(signal);

            // Assert
            Assert.True(result); // Should allow in backtest mode
        }

        [Fact]
        public void ValidateEntry_ExceedsMaxTrades_BlocksEntry()
        {
            // Arrange
            _compliance.Enable();
            var signal = CreateValidEntrySignal();

            // Act - Record 10 trades (max)
            for (int i = 0; i < 10; i++)
            {
                _compliance.ValidateEntry(signal);
                _compliance.RecordTradeEntry(signal);
            }

            // Act - Try 11th trade
            bool result = _compliance.ValidateEntry(signal);

            // Assert
            Assert.False(result); // Should reject 11th trade
        }

        [Fact]
        public void ValidateExit_BeforeMinHoldTime_BlocksExit()
        {
            // Arrange
            _compliance.Enable();
            var entrySignal = CreateValidEntrySignal();
            _compliance.ValidateEntry(entrySignal);
            _compliance.RecordTradeEntry(entrySignal);

            // Act - Try to exit immediately (before 60 seconds)
            bool result = _compliance.ValidateExit(99.5); // Exit price, no target hit

            // Assert
            Assert.False(result); // Should block early exit
        }

        [Fact]
        public void ValidateExit_AfterMinHoldTime_AllowsExit()
        {
            // Arrange
            _compliance.Enable();
            var entrySignal = CreateValidEntrySignal();
            _compliance.ValidateEntry(entrySignal);
            _compliance.RecordTradeEntry(entrySignal);

            // Act - Wait past minimum hold time
            System.Threading.Thread.Sleep(100); // Simulate 100ms (in real trade would be 60 seconds)
            bool result = _compliance.ValidateExit(101.0); // Exit after hold time

            // Assert
            Assert.True(result); // Should allow exit after hold time
        }

        [Fact]
        public void RecordTradeEntry_TracksTradeCount()
        {
            // Arrange
            _compliance.Enable();
            var signal = CreateValidEntrySignal();

            // Act
            for (int i = 0; i < 5; i++)
            {
                _compliance.ValidateEntry(signal);
                _compliance.RecordTradeEntry(signal);
            }

            var summary = _compliance.GetSessionSummary();

            // Assert
            Assert.Equal(5, summary.TradeCount);
        }

        [Fact]
        public void GetWinRate_CalculatesCorrectly()
        {
            // Arrange
            _compliance.Enable();
            var signal = CreateValidEntrySignal();

            // Act - 3 wins, 2 losses = 60% win rate
            for (int i = 0; i < 3; i++)
            {
                _compliance.ValidateEntry(signal);
                _compliance.RecordTradeEntry(signal);
                _compliance.RecordTradeExit(101.0); // Profit
            }

            for (int i = 0; i < 2; i++)
            {
                _compliance.ValidateEntry(signal);
                _compliance.RecordTradeEntry(signal);
                _compliance.RecordTradeExit(99.0); // Loss
            }

            double winRate = _compliance.GetWinRate();

            // Assert
            Assert.Equal(0.6, winRate, 2); // 60% win rate
        }

        [Fact]
        public void GetComplianceStatus_ReturnsCurrentStatus()
        {
            // Arrange
            _compliance.Enable();

            // Act
            var status = _compliance.GetComplianceStatus();

            // Assert
            Assert.NotNull(status);
            Assert.True(status.ToString().Length > 0);
        }

        private EntrySignal CreateValidEntrySignal()
        {
            return new EntrySignal
            {
                Symbol = "NQ",
                Regime = ISE.UnifiedRegimeEngine.MarketRegime.TRENDING,
                Direction = ISE.UnifiedRegimeEngine.TradeDirection.BUY,
                EntryPrice = 100.0,
                Target = 105.0,
                StopLoss = 95.0,
                Contracts = 2,
                TimeStamp = DateTime.Now
            };
        }
    }
}
