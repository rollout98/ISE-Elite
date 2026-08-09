using ISE.UnifiedRegimeEngine.Models;
using ISE.UnifiedRegimeEngine.RegimeCalculations;
using Xunit;

namespace ISE.UnifiedRegimeEngine.Tests
{
    /// <summary>
    /// Unit tests for AtrCalculator
    /// Validates ATR 14-period calculation and volatility measurement
    /// </summary>
    public class AtrCalculatorTests
    {
        private readonly AtrCalculator _calculator = new();

        /// <summary>
        /// Test ATR with low volatility market
        /// </summary>
        [Fact]
        public void Calculate_WithLowVolatility_ReturnsLowAtr()
        {
            // Arrange: Tight range, 0.1% moves
            for (int i = 1; i <= 20; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 0.001),
                    High = 100.05 + (i * 0.001),
                    Low = 99.95 + (i * 0.001),
                    Close = 100.02 + (i * 0.001),
                    Volume = 500
                };

                _calculator.Calculate(bar);
            }

            // Act
            var finalBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(21),
                Open = 100.02,
                High = 100.07,
                Low = 99.97,
                Close = 100.04,
                Volume = 500
            };

            var (atr, atrPercent) = _calculator.Calculate(finalBar);

            // Assert
            Assert.True(atr > 0, "ATR should be positive");
            Assert.True(atr < 0.2, "ATR should be low for low volatility market");
            Assert.True(atrPercent < 0.2, "ATR% should be less than 0.2%");
        }

        /// <summary>
        /// Test ATR with high volatility market
        /// </summary>
        [Fact]
        public void Calculate_WithHighVolatility_ReturnsHighAtr()
        {
            // Arrange: Wide range, 2% moves
            for (int i = 1; i <= 20; i++)
            {
                var basePrice = 100 * (1 + ((i % 2 == 0 ? 0.02 : -0.02)));

                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = basePrice,
                    High = basePrice * 1.015m,
                    Low = basePrice * 0.985m,
                    Close = basePrice * 1.01m,
                    Volume = 2000
                };

                _calculator.Calculate(bar);
            }

            // Act
            var finalBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(21),
                Open = 102,
                High = 103.5,
                Low = 100.5,
                Close = 102.5,
                Volume = 2000
            };

            var (atr, atrPercent) = _calculator.Calculate(finalBar);

            // Assert
            Assert.True(atr > 1.0, "ATR should be greater than 1.0 for high volatility");
            Assert.True(atrPercent > 0.5, "ATR% should be at least 0.5% for high volatility");
        }

        /// <summary>
        /// Test ATR calculation of True Range with gap scenarios
        /// </summary>
        [Fact]
        public void Calculate_WithGapUp_IncludesGapInTrueRange()
        {
            // Arrange: Normal trading first
            for (int i = 1; i <= 15; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + i,
                    High = 101 + i,
                    Low = 99 + i,
                    Close = 100.5 + i,
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Gap up: close is 115, but next open is 120
            var gapBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(16),
                Open = 120,
                High = 121,
                Low = 119,
                Close = 120.5,
                Volume = 1000
            };

            var (atr, _) = _calculator.Calculate(gapBar);

            // Assert: True Range should include gap (max(high, prevClose) - min(low, prevClose))
            Assert.True(atr > 2, "ATR should increase significantly with gap");
        }

        /// <summary>
        /// Test ATR with down gap scenario
        /// </summary>
        [Fact]
        public void Calculate_WithGapDown_IncludesGapInTrueRange()
        {
            // Arrange: Normal trading first
            for (int i = 1; i <= 15; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 200 - i,
                    High = 201 - i,
                    Low = 199 - i,
                    Close = 200.5 - i,
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Gap down: close is 185, but next open is 180
            var gapBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(16),
                Open = 180,
                High = 181,
                Low = 179,
                Close = 179.5,
                Volume = 1000
            };

            var (atr, _) = _calculator.Calculate(gapBar);

            // Assert: True Range should include gap
            Assert.True(atr > 2, "ATR should increase with gap down");
        }

        /// <summary>
        /// Test ATR warm-up period (needs 14 bars for valid calculation)
        /// </summary>
        [Fact]
        public void Calculate_DuringWarmup_IncreasesThenStabilizes()
        {
            // Arrange: Feed bars one at a time
            var atrValues = new List<double>();

            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 0.5),
                    High = 101 + (i * 0.5),
                    Low = 99 + (i * 0.5),
                    Close = 100.5 + (i * 0.5),
                    Volume = 1000
                };

                var (atr, _) = _calculator.Calculate(bar);
                atrValues.Add(atr);
            }

            // Assert: ATR should stabilize after warm-up
            Assert.True(atrValues.Count > 14, "Should have values after warm-up period");
            // Early ATR might be zero or minimal
            Assert.True(atrValues[0] < atrValues[20], "ATR should generally increase during uptrend");
        }

        /// <summary>
        /// Test ATR with identical prices (zero volatility)
        /// </summary>
        [Fact]
        public void Calculate_WithNoMovement_ReturnsZeroAtr()
        {
            // Arrange: All prices identical
            for (int i = 1; i <= 20; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100,
                    High = 100,
                    Low = 100,
                    Close = 100,
                    Volume = 0
                };

                _calculator.Calculate(bar);
            }

            // Act
            var bar20 = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(21),
                Open = 100,
                High = 100,
                Low = 100,
                Close = 100,
                Volume = 0
            };

            var (atr, atrPercent) = _calculator.Calculate(bar20);

            // Assert
            Assert.True(atr < 0.01, "ATR should be near zero with no price movement");
            Assert.True(atrPercent < 0.01, "ATR% should be near zero");
        }

        /// <summary>
        /// Test ATR is always positive
        /// </summary>
        [Fact]
        public void Calculate_AtrAlwaysNonNegative()
        {
            // Arrange: Varied market data
            for (int i = 1; i <= 40; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (Math.Sin(i * 0.3) * 10),
                    High = 110 + (Math.Sin(i * 0.3) * 10),
                    Low = 90 + (Math.Sin(i * 0.3) * 10),
                    Close = 100 + (Math.Cos(i * 0.3) * 10),
                    Volume = 1000 + (i * 10)
                };

                var (atr, atrPercent) = _calculator.Calculate(bar);

                // Assert: ATR should never be negative
                Assert.True(atr >= 0, $"ATR should never be negative, got {atr}");
                Assert.True(atrPercent >= 0, $"ATR% should never be negative, got {atrPercent}");
            }
        }

        /// <summary>
        /// Test ATR percent calculation
        /// </summary>
        [Fact]
        public void Calculate_AtrPercentCalculatedCorrectly()
        {
            // Arrange: Feed bars with known volatility
            for (int i = 1; i <= 20; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 0.5),
                    High = 101 + (i * 0.5),
                    Low = 99 + (i * 0.5),
                    Close = 100.5 + (i * 0.5),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Act
            var bar20 = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(21),
                Open = 110,
                High = 112,
                Low = 108,
                Close = 111,
                Volume = 1000
            };

            var (atr, atrPercent) = _calculator.Calculate(bar20);

            // Assert: ATR% should be ATR / close * 100
            var expectedAtrPercent = (atr / 111.0) * 100;
            Assert.True(Math.Abs(atrPercent - expectedAtrPercent) < 0.5, "ATR% calculation should be correct");
        }

        /// <summary>
        /// Test ATR with consecutive limits (limit up/down)
        /// </summary>
        [Fact]
        public void Calculate_WithConsecutiveLimits_HandlesLargeRanges()
        {
            // Arrange: Three consecutive days with 5% moves
            for (int i = 1; i <= 20; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100,
                    High = 100,
                    Low = 100,
                    Close = 100,
                    Volume = 0
                };

                _calculator.Calculate(bar);
            }

            // Limit up days
            for (int i = 1; i <= 3; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(20 + i),
                    Open = 100 * (float)Math.Pow(1.05, i - 1),
                    High = 100 * (float)Math.Pow(1.05, i),
                    Low = 100 * (float)Math.Pow(1.05, i - 1),
                    Close = 100 * (float)Math.Pow(1.05, i),
                    Volume = 1000
                };

                var (atr, _) = _calculator.Calculate(bar);

                // Assert: ATR should be very high
                Assert.True(atr > 2, "ATR should be high with large daily ranges");
            }
        }

        /// <summary>
        /// Test reset functionality
        /// </summary>
        [Fact]
        public void Reset_ClearsCalculatorState()
        {
            // Arrange: Build up high ATR
            for (int i = 1; i <= 20; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 2),
                    High = 103 + (i * 2),
                    Low = 97 + (i * 2),
                    Close = 101 + (i * 2),
                    Volume = 2000
                };

                _calculator.Calculate(bar);
            }

            // Act: Reset
            _calculator.Reset();

            // Feed one bar after reset
            var postResetBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(1),
                Open = 100,
                High = 100.5,
                Low = 99.5,
                Close = 100.2,
                Volume = 1000
            };

            var (atr, _) = _calculator.Calculate(postResetBar);

            // Assert: ATR should start fresh
            Assert.True(atr < 1, "ATR should be minimal after reset with single small-range bar");
        }

        /// <summary>
        /// Test ATR responsiveness to volatility changes
        /// </summary>
        [Fact]
        public void Calculate_IsResponsiveToVolatilityChanges()
        {
            // Arrange: Low volatility first
            var lowVolAtr = 0.0;
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100.001,
                    High = 100.002,
                    Low = 100.0,
                    Close = 100.0015,
                    Volume = 100
                };

                var (atr, _) = _calculator.Calculate(bar);
                lowVolAtr = atr;
            }

            // Now switch to high volatility
            var highVolAtr = 0.0;
            for (int i = 1; i <= 5; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(25 + i),
                    Open = 100 + (i * 2),
                    High = 103 + (i * 2),
                    Low = 97 + (i * 2),
                    Close = 101 + (i * 2),
                    Volume = 2000
                };

                var (atr, _) = _calculator.Calculate(bar);
                highVolAtr = atr;
            }

            // Assert: High volatility ATR should significantly exceed low volatility ATR
            Assert.True(highVolAtr > lowVolAtr * 2, "ATR should respond to volatility changes");
        }

        /// <summary>
        /// Test ATR with realistic NY market data patterns
        /// </summary>
        [Fact]
        public void Calculate_WithNyMarketPatterns_ValidatesRealisticScenarios()
        {
            // Arrange: Simulate opening drive volatility (high at open, lower later)
            var openDriveAtr = 0.0;
            for (int i = 1; i <= 10; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(9).AddMinutes(i * 30), // 9:30 to 10:30 AM
                    Open = 100 + (i * 0.3),
                    High = 101 + (i * 0.5),
                    Low = 99 + (i * 0.1),
                    Close = 100.5 + (i * 0.3),
                    Volume = 3000 - (i * 200)
                };

                var (atr, _) = _calculator.Calculate(bar);
                openDriveAtr = atr;
            }

            // Simulate mid-day consolidation (lower volatility)
            for (int i = 11; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(9).AddMinutes(i * 30), // 10:30 AM to 12:30 PM
                    Open = 103,
                    High = 103.1,
                    Low = 102.9,
                    Close = 103.05,
                    Volume = 500
                };

                var (atr, _) = _calculator.Calculate(bar);

                // Assert: Mid-day ATR should be lower than opening drive
                Assert.True(atr < openDriveAtr, "ATR should decrease during consolidation");
            }
        }
    }
}
