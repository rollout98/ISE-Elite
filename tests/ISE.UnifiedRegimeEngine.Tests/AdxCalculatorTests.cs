using ISE.UnifiedRegimeEngine;
using ISE.UnifiedRegimeEngine.Models;
using ISE.UnifiedRegimeEngine.RegimeCalculations;
using Xunit;

namespace ISE.UnifiedRegimeEngine.Tests
{
    /// <summary>
    /// Unit tests for AdxCalculator
    /// Validates ADX 14-period calculation with Wilder's smoothing
    /// </summary>
    public class AdxCalculatorTests
    {
        private readonly AdxCalculator _calculator = new();

        /// <summary>
        /// Test ADX calculation with synthetic uptrend data
        /// </summary>
        [Fact]
        public void Calculate_WithUptrendData_ReturnsHighAdx()
        {
            // Arrange: Strong uptrend (20 bars, each bar higher close)
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

                _calculator.Calculate(bar);
            }

            // Act: Get final ADX
            var finalBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(26),
                Open = 112.5,
                High = 113.5,
                Low = 111.5,
                Close = 113,
                Volume = 1000
            };

            var (adx, diPlus, diMinus) = _calculator.Calculate(finalBar);

            // Assert: ADX should be elevated for uptrend
            Assert.True(adx > 30, "ADX should exceed 30 for strong uptrend");
            Assert.True(diPlus > diMinus, "DI+ should exceed DI- in uptrend");
            Assert.True(diPlus > 20, "DI+ should be significant");
        }

        /// <summary>
        /// Test ADX calculation with synthetic downtrend data
        /// </summary>
        [Fact]
        public void Calculate_WithDowntrendData_ReturnsHighAdxWithDiMinusUp()
        {
            // Arrange: Strong downtrend (25 bars, each bar lower close)
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 200 - (i * 0.5),
                    High = 201 - (i * 0.5),
                    Low = 199 - (i * 0.5),
                    Close = 200.5 - (i * 0.5),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Act: Get final ADX
            var finalBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(26),
                Open = 187.5,
                High = 188.5,
                Low = 186.5,
                Close = 187,
                Volume = 1000
            };

            var (adx, diPlus, diMinus) = _calculator.Calculate(finalBar);

            // Assert: ADX should be elevated, DI- > DI+
            Assert.True(adx > 30, "ADX should exceed 30 for strong downtrend");
            Assert.True(diMinus > diPlus, "DI- should exceed DI+ in downtrend");
            Assert.True(diMinus > 20, "DI- should be significant");
        }

        /// <summary>
        /// Test ADX calculation with ranging/consolidation data
        /// </summary>
        [Fact]
        public void Calculate_WithRangingData_ReturnsLowAdx()
        {
            // Arrange: Tight range, no directional bias
            for (int i = 1; i <= 25; i++)
            {
                double basePrice = 150 + (i % 4 * 0.1); // Oscillates within 0.4 point range

                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = basePrice,
                    High = basePrice + 0.2,
                    Low = basePrice - 0.2,
                    Close = basePrice + (i % 2 == 0 ? 0.1 : -0.1),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Act: Get final ADX
            var finalBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(26),
                Open = 150.5,
                High = 150.7,
                Low = 150.3,
                Close = 150.4,
                Volume = 1000
            };

            var (adx, _, _) = _calculator.Calculate(finalBar);

            // Assert: ADX should be low for ranging market
            Assert.True(adx < 25, "ADX should be below 25 for ranging market");
        }

        /// <summary>
        /// Test ADX warm-up period (needs 14+ bars before accurate calculation)
        /// </summary>
        [Fact]
        public void Calculate_DuringWarmupPeriod_GradualllyIncreases()
        {
            // Arrange: Feed bars one at a time during warm-up
            var adxValues = new List<double>();

            for (int i = 1; i <= 20; i++)
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

                var (adx, _, _) = _calculator.Calculate(bar);
                adxValues.Add(adx);
            }

            // Assert: ADX should gradually increase as more bars are processed
            // First few bars will have low/zero ADX
            Assert.True(adxValues[0] < 10, "First bar ADX should be minimal");
            // Later bars should show higher ADX for uptrend
            Assert.True(adxValues[adxValues.Count - 1] > adxValues[5], "ADX should increase over time for trending market");
        }

        /// <summary>
        /// Test gap up scenario (high < previous close)
        /// </summary>
        [Fact]
        public void Calculate_WithGapUp_CalculatesTrueRange()
        {
            // Arrange: Gap up event (high lower than previous close)
            for (int i = 1; i <= 15; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + i,
                    High = 101 + i,
                    Low = 99 + i,
                    Close = 100 + i,
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Gap up: close is 110, next bar opens at 115
            var gapBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(16),
                Open = 115,
                High = 116,
                Low = 114,
                Close = 115.5,
                Volume = 1000
            };

            var (adx, diPlus, diMinus) = _calculator.Calculate(gapBar);

            // Assert: Should handle gap correctly (True Range = max(high, prevClose) - min(low, prevClose))
            Assert.True(adx >= 0, "ADX should not be negative");
            Assert.True(diPlus >= 0, "DI+ should not be negative");
            Assert.True(diMinus >= 0, "DI- should not be negative");
        }

        /// <summary>
        /// Test identical prices (no volatility)
        /// </summary>
        [Fact]
        public void Calculate_WithIdenticalPrices_ReturnsZeroAdx()
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

            var (adx, diPlus, diMinus) = _calculator.Calculate(bar20);

            // Assert: ADX should be 0 or very low with no directional movement
            Assert.True(adx < 5, "ADX should be minimal with no price movement");
            Assert.True(diPlus < 5, "DI+ should be minimal");
            Assert.True(diMinus < 5, "DI- should be minimal");
        }

        /// <summary>
        /// Test extreme volatility (large gaps and reversals)
        /// </summary>
        [Fact]
        public void Calculate_WithExtremeVolatility_CalculatesCorrectly()
        {
            // Arrange: Volatile market with 2% moves per bar
            for (int i = 1; i <= 20; i++)
            {
                double basePrice = 100 * (1 + ((i % 2 == 0 ? 0.02 : -0.02)));

                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = basePrice,
                    High = basePrice * 1.01m,
                    Low = basePrice * 0.99m,
                    Close = basePrice,
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Act
            var volatileBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(21),
                Open = 102,
                High = 104,
                Low = 100,
                Close = 103,
                Volume = 2000
            };

            var (adx, diPlus, diMinus) = _calculator.Calculate(volatileBar);

            // Assert: Should handle volatility without errors
            Assert.True(adx >= 0, "ADX should not be negative");
            Assert.True(adx < 100, "ADX should not exceed 100");
            Assert.True(diPlus + diMinus > 0, "At least one DI should be positive");
        }

        /// <summary>
        /// Test reset functionality
        /// </summary>
        [Fact]
        public void Reset_ClearsCalculatorState()
        {
            // Arrange: Feed some data
            for (int i = 1; i <= 20; i++)
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

            // Act: Reset
            _calculator.Reset();

            // Feed one bar after reset
            var postResetBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(1),
                Open = 100,
                High = 101,
                Low = 99,
                Close = 100.5,
                Volume = 1000
            };

            var (adx, _, _) = _calculator.Calculate(postResetBar);

            // Assert: Should start from scratch
            Assert.True(adx < 10, "ADX should be minimal after reset with just one bar");
        }

        /// <summary>
        /// Test validation of DI+ + DI- relationship
        /// </summary>
        [Fact]
        public void Calculate_DiPlusPlusDiMinusAlwaysValid()
        {
            // Arrange: Feed 30 bars of data
            for (int i = 1; i <= 30; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (Math.Sin(i * 0.3) * 5),
                    High = 105 + (Math.Sin(i * 0.3) * 5),
                    Low = 95 + (Math.Sin(i * 0.3) * 5),
                    Close = 100 + (Math.Sin(i * 0.3) * 5),
                    Volume = 1000
                };

                var (adx, diPlus, diMinus) = _calculator.Calculate(bar);

                // Assert: Every calculation should have valid DI values
                Assert.True(diPlus >= 0 && diPlus <= 100, $"DI+ should be 0-100, got {diPlus}");
                Assert.True(diMinus >= 0 && diMinus <= 100, $"DI- should be 0-100, got {diMinus}");
                Assert.True(adx >= 0 && adx <= 100, $"ADX should be 0-100, got {adx}");
            }
        }
    }
}
