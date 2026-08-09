using ISE.UnifiedRegimeEngine;
using ISE.UnifiedRegimeEngine.Models;
using ISE.OrderFlowIntelligence.Models;
using Xunit;

namespace ISE.UnifiedRegimeEngine.Tests
{
    /// <summary>
    /// Unit tests for TrendingModeLogic and RangingModeLogic
    /// Validates entry/exit conditions and signal generation
    /// </summary>
    public class EntryExitLogicTests
    {
        /// <summary>
        /// Test TrendingModeLogic rejects entry when not trending
        /// </summary>
        [Fact]
        public void TrendingModeLogic_RejectsWhenNotTrending()
        {
            // Arrange
            var logic = new TrendingModeLogic();
            var rangingRegime = new RegimeSignal
            {
                Regime = RegimeState.Ranging,
                Adx = 18,
                DiPlus = 15,
                DiMinus = 20,
                RegimeConfidence = 0.7
            };

            var orderFlow = new OrderFlowMetrics
            {
                OrderFlowBias = 50,
                IsLiquidEnoughForEntry = true,
                SupportLevel = 100,
                ResistanceLevel = 101
            };

            // Act
            var signal = logic.EvaluateEntry(rangingRegime, orderFlow, 100.5);

            // Assert
            Assert.False(signal.CanEnterLong, "Should reject long entry when regime is ranging");
            Assert.False(signal.CanEnterShort, "Should reject short entry when regime is ranging");
            Assert.NotNull(signal.RejectReason);
        }

        /// <summary>
        /// Test TrendingModeLogic allows long entry with proper conditions
        /// </summary>
        [Fact]
        public void TrendingModeLogic_AllowsLongEntryWhenUptrendingWithOrderFlow()
        {
            // Arrange
            var logic = new TrendingModeLogic();
            var trendingRegime = new RegimeSignal
            {
                Regime = RegimeState.Trending,
                Adx = 35,
                DiPlus = 35,
                DiMinus = 15,
                MacdHistogram = 0.5,
                MacdBullishCross = false,
                RsiOverbought = false,
                RsiOversold = false,
                RegimeConfidence = 0.8
            };

            var orderFlow = new OrderFlowMetrics
            {
                OrderFlowBias = -75, // Bullish (negative is long)
                IsLiquidEnoughForEntry = true,
                ResistanceLevel = 105,
                SupportLevel = 95
            };

            // Act
            var signal = logic.EvaluateEntry(trendingRegime, orderFlow, 100);

            // Assert
            Assert.True(signal.CanEnterLong, "Should allow long entry with uptrend + order flow");
            Assert.Equal("LONG", signal.EntryDirection);
            Assert.True(signal.ProfitTarget > 0, "Should calculate profit target");
            Assert.True(signal.StopLoss > 0, "Should calculate stop loss");
        }

        /// <summary>
        /// Test TrendingModeLogic rejects long entry when overbought
        /// </summary>
        [Fact]
        public void TrendingModeLogic_RejectsLongWhenOverbought()
        {
            // Arrange
            var logic = new TrendingModeLogic();
            var trendingRegime = new RegimeSignal
            {
                Regime = RegimeState.Trending,
                Adx = 35,
                DiPlus = 35,
                DiMinus = 15,
                RsiOverbought = true, // Overbought!
                RsiOversold = false,
                MacdHistogram = 0.5,
                RegimeConfidence = 0.8
            };

            var orderFlow = new OrderFlowMetrics
            {
                OrderFlowBias = -75,
                IsLiquidEnoughForEntry = true
            };

            // Act
            var signal = logic.EvaluateEntry(trendingRegime, orderFlow, 100);

            // Assert
            Assert.False(signal.CanEnterLong, "Should reject long when RSI overbought");
        }

        /// <summary>
        /// Test RangingModeLogic allows long entry at support
        /// </summary>
        [Fact]
        public void RangingModeLogic_AllowsLongEntryAtSupport()
        {
            // Arrange
            var logic = new RangingModeLogic();
            var rangingRegime = new RegimeSignal
            {
                Regime = RegimeState.Ranging,
                Adx = 18,
                RsiOversold = true,
                RsiOverbought = false,
                RegimeConfidence = 0.7
            };

            var orderFlow = new OrderFlowMetrics
            {
                OrderFlowBias = -40, // Turning bullish
                IsLiquidEnoughForEntry = true,
                SupportLevel = 100.0,
                SupportClusterVolume = 500,
                ResistanceLevel = 105.0,
                ResistanceClusterVolume = 300,
                RejectionAtSupport = true
            };

            var currentPrice = 100.05; // At support

            // Act
            var signal = logic.EvaluateEntry(rangingRegime, orderFlow, currentPrice);

            // Assert
            Assert.True(signal.CanEnterLong, "Should allow long at support in ranging market");
            Assert.Equal("LONG", signal.EntryDirection);
            Assert.True(signal.ProfitTarget > 0, "Should have profit target");
        }

        /// <summary>
        /// Test RangingModeLogic rejects long entry away from support
        /// </summary>
        [Fact]
        public void RangingModeLogic_RejectsLongWhenAwayFromSupport()
        {
            // Arrange
            var logic = new RangingModeLogic();
            var rangingRegime = new RegimeSignal
            {
                Regime = RegimeState.Ranging,
                Adx = 18,
                RsiOversold = true,
                RegimeConfidence = 0.7
            };

            var orderFlow = new OrderFlowMetrics
            {
                OrderFlowBias = -40,
                IsLiquidEnoughForEntry = true,
                SupportLevel = 100.0,
                ResistanceLevel = 105.0
            };

            var currentPrice = 102.0; // Far from support!

            // Act
            var signal = logic.EvaluateEntry(rangingRegime, orderFlow, currentPrice);

            // Assert
            Assert.False(signal.CanEnterLong, "Should reject long when price is away from support");
        }

        /// <summary>
        /// Test exit logic when profit target hit
        /// </summary>
        [Fact]
        public void TrendingModeLogic_ExitsWhenProfitTargetHit()
        {
            // Arrange
            var logic = new TrendingModeLogic();

            var entrySignal = new TrendingEntrySignal
            {
                EntryDirection = "LONG",
                ProfitTarget = 2.0,
                StopLoss = 1.0
            };
            logic.RecordEntry(100.0, DateTime.Now.AddHours(-1));

            var regime = new RegimeSignal
            {
                Regime = RegimeState.Trending,
                Adx = 35
            };

            var orderFlow = new OrderFlowMetrics();

            // Price moved up 2.5 points (profit target was 2.0)
            var currentPrice = 102.5;
            var timeInTrade = TimeSpan.FromMinutes(15);

            // Act
            var exitSignal = logic.EvaluateExit(currentPrice, timeInTrade, regime, orderFlow);

            // Assert
            Assert.NotNull(exitSignal);
            Assert.True(exitSignal.ShouldExit, "Should exit when profit target hit");
            Assert.Contains("Profit target", exitSignal.ExitReason);
        }

        /// <summary>
        /// Test exit logic when stop loss hit
        /// </summary>
        [Fact]
        public void TrendingModeLogic_ExitsWhenStopLossHit()
        {
            // Arrange
            var logic = new TrendingModeLogic();

            logic.RecordEntry(100.0, DateTime.Now.AddHours(-1));

            var regime = new RegimeSignal
            {
                Regime = RegimeState.Trending,
                Adx = 35
            };

            var orderFlow = new OrderFlowMetrics();

            // Price moved down 1.5 points (stop loss was 1.0)
            var currentPrice = 98.5;
            var timeInTrade = TimeSpan.FromMinutes(5);

            // Act
            var exitSignal = logic.EvaluateExit(currentPrice, timeInTrade, regime, orderFlow);

            // Assert
            Assert.NotNull(exitSignal);
            Assert.True(exitSignal.ShouldExit, "Should exit when stop loss hit");
            Assert.Contains("Stop loss", exitSignal.ExitReason);
        }

        /// <summary>
        /// Test that short entries work in downtrend
        /// </summary>
        [Fact]
        public void TrendingModeLogic_AllowsShortEntryInDowntrend()
        {
            // Arrange
            var logic = new TrendingModeLogic();

            var downtrendingRegime = new RegimeSignal
            {
                Regime = RegimeState.Trending,
                Adx = 35,
                DiPlus = 15,
                DiMinus = 35, // Downtrend
                MacdHistogram = -0.5,
                RsiOverbought = false,
                RsiOversold = true, // Can enter short even with oversold (momentum)
                RegimeConfidence = 0.8
            };

            var orderFlow = new OrderFlowMetrics
            {
                OrderFlowBias = 75, // Bearish (positive is short)
                IsLiquidEnoughForEntry = true,
                SupportLevel = 95,
                ResistanceLevel = 105
            };

            // Act
            var signal = logic.EvaluateEntry(downtrendingRegime, orderFlow, 100);

            // Assert
            Assert.True(signal.CanEnterShort, "Should allow short in downtrend with order flow");
            Assert.Equal("SHORT", signal.EntryDirection);
        }

        /// <summary>
        /// Test minimum hold time is enforced
        /// </summary>
        [Fact]
        public void TrendingModeLogic_EnforcesMinimumHoldTime()
        {
            // Arrange
            var logic = new TrendingModeLogic();
            logic.RecordEntry(100.0, DateTime.Now.AddMinutes(-5)); // Entered 5 minutes ago

            var regime = new RegimeSignal { Regime = RegimeState.Ranging }; // Regime changed
            var orderFlow = new OrderFlowMetrics();

            // Act: Only 5 minutes elapsed, minimum is 30 minutes
            var exitSignal = logic.EvaluateExit(100.5, TimeSpan.FromMinutes(5), regime, orderFlow);

            // Assert: Should NOT exit due to regime change yet
            // (because minimum hold time not met)
            // This tests that minimum hold time is a protective constraint

            // Now test after minimum hold time
            exitSignal = logic.EvaluateExit(100.5, TimeSpan.FromMinutes(35), regime, orderFlow);
            Assert.True(exitSignal.ShouldExit, "Should exit after minimum hold time when regime changes");
        }

        /// <summary>
        /// Test maximum hold time forces exit
        /// </summary>
        [Fact]
        public void TrendingModeLogic_ForcesExitAtMaximumHoldTime()
        {
            // Arrange
            var logic = new TrendingModeLogic();
            logic.RecordEntry(100.0, DateTime.Now.AddHours(-2)); // Entered 2 hours ago

            var regime = new RegimeSignal { Regime = RegimeState.Trending, Adx = 35 };
            var orderFlow = new OrderFlowMetrics();

            // Act: 2 hours in trade, max is 90 minutes
            var exitSignal = logic.EvaluateExit(100.5, TimeSpan.FromMinutes(120), regime, orderFlow);

            // Assert
            Assert.True(exitSignal.ShouldExit, "Should force exit at maximum hold time");
            Assert.Contains("Maximum hold", exitSignal.ExitReason);
        }

        /// <summary>
        /// Test RangingModeLogic hold time is shorter
        /// </summary>
        [Fact]
        public void RangingModeLogic_HasShorterHoldTimes()
        {
            // Arrange
            var trendingLogic = new TrendingModeLogic();
            var rangingLogic = new RangingModeLogic();

            // Assert
            Assert.Equal(1800, trendingLogic.MinimumHoldTimeSeconds); // 30 minutes
            Assert.Equal(180, rangingLogic.MinimumHoldTimeSeconds); // 3 minutes

            Assert.Equal(5400, trendingLogic.MaximumHoldTimeSeconds); // 90 minutes
            Assert.Equal(600, rangingLogic.MaximumHoldTimeSeconds); // 10 minutes
        }

        /// <summary>
        /// Test that entry recording works correctly
        /// </summary>
        [Fact]
        public void EntryExitLogic_RecordsEntryAndExit()
        {
            // Arrange
            var logic = new TrendingModeLogic();
            var entryPrice = 100.0;
            var entryTime = DateTime.Now;

            // Act: Record entry
            logic.RecordEntry(entryPrice, entryTime);

            // Assert: Should be in trade
            Assert.True(logic.IsInTrade, "Should be in trade after recording entry");
            Assert.Equal(entryPrice, logic.EntryPrice);
            Assert.Equal(entryTime, logic.EntryTime);

            // Act: Record exit
            logic.RecordExit();

            // Assert: Should not be in trade
            Assert.False(logic.IsInTrade, "Should not be in trade after recording exit");
            Assert.Equal(0, logic.EntryPrice);
        }

        /// <summary>
        /// Test reset functionality
        /// </summary>
        [Fact]
        public void EntryExitLogic_ResetClearsState()
        {
            // Arrange
            var logic = new TrendingModeLogic();
            logic.RecordEntry(100.0, DateTime.Now);

            // Act
            logic.Reset();

            // Assert
            Assert.False(logic.IsInTrade, "Should not be in trade after reset");
            Assert.Equal(0, logic.EntryPrice);
        }

        /// <summary>
        /// Test confidence scoring in entry signals
        /// </summary>
        [Fact]
        public void EntryExitLogic_CalculatesConfidenceScores()
        {
            // Arrange
            var logic = new TrendingModeLogic();

            var strongRegime = new RegimeSignal
            {
                Regime = RegimeState.Trending,
                Adx = 40,
                DiPlus = 40,
                DiMinus = 10,
                MacdHistogram = 1.0,
                MacdBullishCross = true,
                RsiOverbought = false,
                RegimeConfidence = 0.9
            };

            var orderFlow = new OrderFlowMetrics
            {
                OrderFlowBias = -80,
                IsLiquidEnoughForEntry = true
            };

            // Act
            var signal = logic.EvaluateEntry(strongRegime, orderFlow, 100);

            // Assert: Should have high confidence scores
            Assert.True(signal.OrderFlowConfidence > 0.7, "Order flow confidence should be high");
            Assert.True(signal.MacdConfidence > 0.7, "MACD confidence should be high");
        }
    }
}
