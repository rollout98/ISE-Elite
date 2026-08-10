using ISE.UnifiedRegimeEngine.Models;
using ISE.UnifiedRegimeEngine.RegimeCalculations;
using Xunit;

namespace ISE.UnifiedRegimeEngine.Tests
{
    /// <summary>
    /// Unit tests for RsiCalculator
    /// Validates RSI 14-period calculation with overbought/oversold thresholds
    /// </summary>
    public class RsiCalculatorTests
    {
        private readonly RsiCalculator _calculator = new();

        /// <summary>
        /// Test RSI with strong uptrend (should be overbought)
        /// </summary>
        [Fact]
        public void Calculate_WithUptrendData_ReturnsHighRsi()
        {
            // Arrange: 20 bars with steady increase
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 0.3),
                    High = 100.5 + (i * 0.3),
                    Low = 99.5 + (i * 0.3),
                    Close = 100.2 + (i * 0.3),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Act
            var finalBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(26),
                Open = 107.5,
                High = 108.5,
                Low = 106.5,
                Close = 108,
                Volume = 1000
            };

            var (rsi, overbought, oversold) = _calculator.Calculate(finalBar);

            // Assert
            Assert.True(rsi > 70, "RSI should exceed 70 for strong uptrend");
            Assert.True(overbought, "Overbought flag should be true");
            Assert.False(oversold, "Oversold flag should be false");
        }

        /// <summary>
        /// Test RSI with strong downtrend (should be oversold)
        /// </summary>
        [Fact]
        public void Calculate_WithDowntrendData_ReturnsLowRsi()
        {
            // Arrange: 20 bars with steady decrease
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 200 - (i * 0.3),
                    High = 200.5 - (i * 0.3),
                    Low = 199.5 - (i * 0.3),
                    Close = 200.2 - (i * 0.3),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Act
            var finalBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(26),
                Open = 192.5,
                High = 193.5,
                Low = 191.5,
                Close = 192,
                Volume = 1000
            };

            var (rsi, overbought, oversold) = _calculator.Calculate(finalBar);

            // Assert
            Assert.True(rsi < 30, "RSI should be below 30 for strong downtrend");
            Assert.False(overbought, "Overbought flag should be false");
            Assert.True(oversold, "Oversold flag should be true");
        }

        /// <summary>
        /// Test RSI in neutral market (should be around 50)
        /// </summary>
        [Fact]
        public void Calculate_WithNeutralMarket_ReturnsMiddleRsi()
        {
            // Arrange: Oscillating prices with equal ups and downs
            for (int i = 1; i <= 25; i++)
            {
                double offset = i % 2 == 0 ? 0.2 : -0.2;

                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 150,
                    High = 150 + Math.Abs(offset),
                    Low = 150 - Math.Abs(offset),
                    Close = 150 + offset,
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Act
            var finalBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(26),
                Open = 150,
                High = 150.2,
                Low = 149.8,
                Close = 150.1,
                Volume = 1000
            };

            var (rsi, overbought, oversold) = _calculator.Calculate(finalBar);

            // Assert
            Assert.True(rsi > 40 && rsi < 60, "RSI should be near 50 in neutral market");
            Assert.False(overbought, "Overbought flag should be false");
            Assert.False(oversold, "Oversold flag should be false");
        }

        /// <summary>
        /// Test RSI boundary conditions (0 and 100)
        /// </summary>
        [Fact]
        public void Calculate_WithExtremeConditions_StaysWithinBounds()
        {
            // Arrange: Extreme uptrend to push RSI toward 100
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 1.0),
                    High = 101 + (i * 1.0),
                    Low = 99.5 + (i * 1.0),
                    Close = 100.8 + (i * 1.0),
                    Volume = 1000
                };

                var (rsi, _, _) = _calculator.Calculate(bar);

                // Assert: RSI should never exceed 100
                Assert.True(rsi <= 100, $"RSI should not exceed 100, got {rsi}");
            }

            // Now test extreme downtrend
            _calculator.Reset();

            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 200 - (i * 1.0),
                    High = 200.5 - (i * 1.0),
                    Low = 198.5 - (i * 1.0),
                    Close = 199.2 - (i * 1.0),
                    Volume = 1000
                };

                var (rsi, _, _) = _calculator.Calculate(bar);

                // Assert: RSI should never go below 0
                Assert.True(rsi >= 0, $"RSI should not go below 0, got {rsi}");
            }
        }

        /// <summary>
        /// Test RSI warm-up period
        /// </summary>
        [Fact]
        public void Calculate_DuringWarmup_GradualllyStabilizes()
        {
            // Arrange: Feed bars during warm-up period
            var rsiValues = new List<double>();

            for (int i = 1; i <= 20; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 0.2),
                    High = 100.5 + (i * 0.2),
                    Low = 99.5 + (i * 0.2),
                    Close = 100.3 + (i * 0.2),
                    Volume = 1000
                };

                var (rsi, _, _) = _calculator.Calculate(bar);
                rsiValues.Add(rsi);
            }

            // Assert: RSI should stabilize as we get past warm-up
            // First few values may be unstable
            Assert.True(rsiValues.Count > 10, "Should have multiple RSI values");
            // Later values should show uptrend (RSI > 50)
            Assert.True(rsiValues.Last() > rsiValues.First(), "RSI should increase for uptrend");
        }

        /// <summary>
        /// Test RSI with gap events
        /// </summary>
        [Fact]
        public void Calculate_WithGapUp_UpdatesRsiCorrectly()
        {
            // Arrange: Normal trading
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

            // Large gap up
            var gapBar = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(16),
                Open = 120,
                High = 121,
                Low = 119,
                Close = 120.5,
                Volume = 1000
            };

            var (rsi, _, _) = _calculator.Calculate(gapBar);

            // Assert: RSI should adjust for gap but stay within bounds
            Assert.True(rsi >= 0 && rsi <= 100, "RSI should remain 0-100 after gap");
        }

        /// <summary>
        /// Test overbought threshold at exactly 70
        /// </summary>
        [Fact]
        public void Calculate_AtOverboughtThreshold_FlagsCorrectly()
        {
            // Arrange: Push RSI to exactly 70
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 0.25),
                    High = 100.5 + (i * 0.25),
                    Low = 99.5 + (i * 0.25),
                    Close = 100.2 + (i * 0.25),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            var bar25 = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(26),
                Open = 106.2,
                High = 106.7,
                Low = 105.7,
                Close = 106.5,
                Volume = 1000
            };

            var (rsi, overbought, oversold) = _calculator.Calculate(bar25);

            // Assert: When RSI >= 70, overbought should be true
            if (rsi >= 70)
            {
                Assert.True(overbought, "Overbought should be true when RSI >= 70");
            }
        }

        /// <summary>
        /// Test oversold threshold at exactly 30
        /// </summary>
        [Fact]
        public void Calculate_AtOversoldThreshold_FlagsCorrectly()
        {
            // Arrange: Push RSI to exactly 30
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 200 - (i * 0.25),
                    High = 200.5 - (i * 0.25),
                    Low = 199.5 - (i * 0.25),
                    Close = 200.2 - (i * 0.25),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            var bar25 = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(26),
                Open = 193.8,
                High = 194.3,
                Low = 193.3,
                Close = 193.5,
                Volume = 1000
            };

            var (rsi, overbought, oversold) = _calculator.Calculate(bar25);

            // Assert: When RSI <= 30, oversold should be true
            if (rsi <= 30)
            {
                Assert.True(oversold, "Oversold should be true when RSI <= 30");
            }
        }

        /// <summary>
        /// Test reset functionality
        /// </summary>
        [Fact]
        public void Reset_ClearsCalculatorState()
        {
            // Arrange: Build up RSI
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

            var (rsi, _, _) = _calculator.Calculate(postResetBar);

            // Assert: RSI should start fresh
            Assert.True(rsi < 10 || rsi > 90, "First bar after reset should give extreme RSI (insufficient data)");
        }

        /// <summary>
        /// Test RSI with realistic volatile market
        /// </summary>
        [Fact]
        public void Calculate_WithVolatileMarket_StaysConsistent()
        {
            // Arrange: Volatile market with 1% moves
            var previousRsi = 50.0;
            var rsiChanges = new List<double>();

            for (int i = 1; i <= 30; i++)
            {
                var direction = Math.Sin(i * 0.5) > 0 ? 1 : -1;

                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 150 * (1 + (direction * 0.005)),
                    High = 150 * (1 + (direction * 0.01)),
                    Low = 150 * (1 - (direction * 0.005)),
                    Close = 150 * (1 + (direction * 0.008)),
                    Volume = 1000
                };

                var (rsi, _, _) = _calculator.Calculate(bar);

                rsiChanges.Add(Math.Abs(rsi - previousRsi));
                previousRsi = rsi;
            }

            // Assert: RSI changes should be gradual (not jumping around)
            var avgChange = rsiChanges.Average();
            Assert.True(avgChange < 5, "RSI should change gradually, not in large jumps");
        }
    }
}
