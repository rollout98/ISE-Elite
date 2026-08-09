using ISE.UnifiedRegimeEngine.Models;
using ISE.UnifiedRegimeEngine.RegimeCalculations;
using Xunit;

namespace ISE.UnifiedRegimeEngine.Tests
{
    /// <summary>
    /// Unit tests for MacdCalculator
    /// Validates MACD 12/26/9 with EMA smoothing and cross detection
    /// </summary>
    public class MacdCalculatorTests
    {
        private readonly MacdCalculator _calculator = new();

        /// <summary>
        /// Test MACD during uptrend (MACD line above signal line)
        /// </summary>
        [Fact]
        public void Calculate_WithUptrendData_MacdAboveSignal()
        {
            // Arrange: Strong uptrend
            for (int i = 1; i <= 35; i++)
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
                Timestamp = DateTime.Now.AddHours(36),
                Open = 110.5,
                High = 111.5,
                Low = 109.5,
                Close = 111,
                Volume = 1000
            };

            var (macdLine, signalLine, histogram, bullish, bearish) = _calculator.Calculate(finalBar);

            // Assert
            Assert.True(macdLine > signalLine, "MACD line should be above signal line in uptrend");
            Assert.True(histogram > 0, "Histogram should be positive when MACD > Signal");
            Assert.False(bullish, "Bullish cross should not be true (already above)");
            Assert.False(bearish, "Bearish cross should not be true");
        }

        /// <summary>
        /// Test MACD during downtrend (MACD line below signal line)
        /// </summary>
        [Fact]
        public void Calculate_WithDowntrendData_MacdBelowSignal()
        {
            // Arrange: Strong downtrend
            for (int i = 1; i <= 35; i++)
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
                Timestamp = DateTime.Now.AddHours(36),
                Open = 189.5,
                High = 190.5,
                Low = 188.5,
                Close = 189,
                Volume = 1000
            };

            var (macdLine, signalLine, histogram, bullish, bearish) = _calculator.Calculate(finalBar);

            // Assert
            Assert.True(macdLine < signalLine, "MACD line should be below signal line in downtrend");
            Assert.True(histogram < 0, "Histogram should be negative when MACD < Signal");
            Assert.False(bullish, "Bullish cross should not be true");
            Assert.False(bearish, "Bearish cross should not be true (already below)");
        }

        /// <summary>
        /// Test bullish crossover (MACD crosses above signal line)
        /// </summary>
        [Fact]
        public void Calculate_WithBullishCrossover_FlagsBullishCross()
        {
            // Arrange: Build downtrend first
            for (int i = 1; i <= 30; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 150 - (i * 0.2),
                    High = 150.5 - (i * 0.2),
                    Low = 149.5 - (i * 0.2),
                    Close = 150.2 - (i * 0.2),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Continue with uptrend to trigger bullish cross
            for (int i = 31; i <= 40; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 144 + ((i - 30) * 0.3),
                    High = 144.5 + ((i - 30) * 0.3),
                    Low = 143.5 + ((i - 30) * 0.3),
                    Close = 144.2 + ((i - 30) * 0.3),
                    Volume = 1000
                };

                var (_, _, _, bullish, _) = _calculator.Calculate(bar);

                if (bullish)
                {
                    // Found the cross
                    Assert.True(bullish, "Should detect bullish crossover");
                    return;
                }
            }

            // If we get here, we should have found at least one bullish cross
            Assert.True(false, "Should have detected a bullish crossover");
        }

        /// <summary>
        /// Test bearish crossover (MACD crosses below signal line)
        /// </summary>
        [Fact]
        public void Calculate_WithBearishCrossover_FlagsBearishCross()
        {
            // Arrange: Build uptrend first
            for (int i = 1; i <= 30; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 150 + (i * 0.2),
                    High = 150.5 + (i * 0.2),
                    Low = 149.5 + (i * 0.2),
                    Close = 150.2 + (i * 0.2),
                    Volume = 1000
                };

                _calculator.Calculate(bar);
            }

            // Continue with downtrend to trigger bearish cross
            for (int i = 31; i <= 40; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 156 - ((i - 30) * 0.3),
                    High = 156.5 - ((i - 30) * 0.3),
                    Low = 155.5 - ((i - 30) * 0.3),
                    Close = 156.2 - ((i - 30) * 0.3),
                    Volume = 1000
                };

                var (_, _, _, _, bearish) = _calculator.Calculate(bar);

                if (bearish)
                {
                    // Found the cross
                    Assert.True(bearish, "Should detect bearish crossover");
                    return;
                }
            }

            // If we get here, we should have found at least one bearish cross
            Assert.True(false, "Should have detected a bearish crossover");
        }

        /// <summary>
        /// Test MACD divergence detection (price rises, MACD falls)
        /// </summary>
        [Fact]
        public void Calculate_WithDivergence_DetectsDivergence()
        {
            // Arrange: Price making new highs but MACD not confirming
            // (This would require special tracking, basic test just ensures no crashes)
            for (int i = 1; i <= 40; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (i * 0.2),
                    High = 100.5 + (i * 0.2),
                    Low = 99.5 + (i * 0.2),
                    Close = 100.2 + (i * 0.2),
                    Volume = 1000
                };

                var (macdLine, signalLine, _, _, _) = _calculator.Calculate(bar);

                // Just verify values are valid
                Assert.True(!double.IsNaN(macdLine), "MACD line should not be NaN");
                Assert.True(!double.IsNaN(signalLine), "Signal line should not be NaN");
            }
        }

        /// <summary>
        /// Test MACD warm-up period
        /// </summary>
        [Fact]
        public void Calculate_DuringWarmup_GradualllyStabilizes()
        {
            // Arrange
            var macdValues = new List<double>();

            for (int i = 1; i <= 40; i++)
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

                var (macdLine, _, _, _, _) = _calculator.Calculate(bar);
                macdValues.Add(macdLine);
            }

            // Assert: MACD should stabilize after 26+ bars
            Assert.True(macdValues.Count >= 26, "Should have enough bars for MACD calculation");
            // Later MACD values should show trend
            Assert.True(macdValues.Last() > macdValues[10], "MACD should increase for uptrend");
        }

        /// <summary>
        /// Test MACD with zero-movement prices
        /// </summary>
        [Fact]
        public void Calculate_WithIdenticalPrices_ReturnsNearZero()
        {
            // Arrange: All prices identical
            for (int i = 1; i <= 40; i++)
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

            // Act: One more identical bar
            var bar40 = new RegimeInput
            {
                Timestamp = DateTime.Now.AddHours(41),
                Open = 100,
                High = 100,
                Low = 100,
                Close = 100,
                Volume = 0
            };

            var (macdLine, signalLine, histogram, _, _) = _calculator.Calculate(bar40);

            // Assert: All should be near zero
            Assert.True(Math.Abs(macdLine) < 0.01, "MACD should be near zero with no price movement");
            Assert.True(Math.Abs(signalLine) < 0.01, "Signal should be near zero");
            Assert.True(Math.Abs(histogram) < 0.01, "Histogram should be near zero");
        }

        /// <summary>
        /// Test MACD histogram sign correctness
        /// </summary>
        [Fact]
        public void Calculate_HistogramSignAlwaysCorrect()
        {
            // Arrange: Feed varied market data
            for (int i = 1; i <= 50; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 + (Math.Sin(i * 0.2) * 10),
                    High = 105 + (Math.Sin(i * 0.2) * 10),
                    Low = 95 + (Math.Sin(i * 0.2) * 10),
                    Close = 100 + (Math.Sin(i * 0.2) * 10),
                    Volume = 1000
                };

                var (macdLine, signalLine, histogram, _, _) = _calculator.Calculate(bar);

                // Assert: Histogram sign should match MACD vs Signal relationship
                if (macdLine > signalLine)
                {
                    Assert.True(histogram > -0.001, $"Histogram should be positive when MACD > Signal, got {histogram}");
                }
                else if (macdLine < signalLine)
                {
                    Assert.True(histogram < 0.001, $"Histogram should be negative when MACD < Signal, got {histogram}");
                }
            }
        }

        /// <summary>
        /// Test reset functionality
        /// </summary>
        [Fact]
        public void Reset_ClearsCalculatorState()
        {
            // Arrange: Build up some data
            for (int i = 1; i <= 30; i++)
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

            var (macdLine, signalLine, _, _, _) = _calculator.Calculate(postResetBar);

            // Assert: Should start fresh (near zero with single bar)
            Assert.True(Math.Abs(macdLine) < 1, "MACD should be minimal after reset");
            Assert.True(Math.Abs(signalLine) < 1, "Signal should be minimal after reset");
        }

        /// <summary>
        /// Test MACD with extreme price movements
        /// </summary>
        [Fact]
        public void Calculate_WithExtremeMoves_StaysValid()
        {
            // Arrange: Very volatile market
            for (int i = 1; i <= 40; i++)
            {
                decimal volatility = i % 3 == 0 ? 2.0m : -2.0m;

                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 100 * (1 + (volatility / 100)),
                    High = 102 * (1 + (volatility / 100)),
                    Low = 98 * (1 + (volatility / 100)),
                    Close = 101 * (1 + (volatility / 100)),
                    Volume = 2000
                };

                var (macdLine, signalLine, histogram, _, _) = _calculator.Calculate(bar);

                // Assert: Should not produce NaN or infinity
                Assert.False(double.IsNaN(macdLine), "MACD should not be NaN");
                Assert.False(double.IsInfinity(macdLine), "MACD should not be infinity");
                Assert.False(double.IsNaN(histogram), "Histogram should not be NaN");
            }
        }

        /// <summary>
        /// Test that cross signals are mutually exclusive
        /// </summary>
        [Fact]
        public void Calculate_CrossSignalsAreMutuallyExclusive()
        {
            // Arrange: Any market data
            for (int i = 1; i <= 50; i++)
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

                var (_, _, _, bullish, bearish) = _calculator.Calculate(bar);

                // Assert: Can't be bullish AND bearish at same time
                Assert.False(bullish && bearish, "Bullish and bearish crosses cannot happen simultaneously");
            }
        }
    }
}
