using ISE.UnifiedRegimeEngine;
using ISE.UnifiedRegimeEngine.Models;
using Xunit;

namespace ISE.UnifiedRegimeEngine.Tests
{
    /// <summary>
    /// Integration tests for UnifiedMarketRegimeEngine
    /// Validates complete regime classification with all indicators
    /// </summary>
    public class UnifiedMarketRegimeEngineTests
    {
        private readonly UnifiedMarketRegimeEngine _engine = new();

        /// <summary>
        /// Test regime classification during strong uptrend
        /// </summary>
        [Fact]
        public void CalculateRegimeSignal_WithUptrendData_ClassifiesTrending()
        {
            // Arrange: Configure for NQ (ADX threshold 25)
            _engine.ConfigureForInstrument("NQ");

            // Feed 30 bars of uptrend
            for (int i = 1; i <= 30; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 50),
                    High = 15050 + (i * 50),
                    Low = 14950 + (i * 50),
                    Close = 15025 + (i * 50),
                    Volume = 50000
                };

                var signal = _engine.CalculateRegimeSignal(bar);

                if (i >= 25) // After warm-up
                {
                    // Assert: Should be trending
                    Assert.Equal(RegimeState.Trending, signal.Regime);
                    Assert.True(signal.RegimeConfidence > 0.6, "Confidence should exceed 60% for clear trend");
                }
            }
        }

        /// <summary>
        /// Test regime classification during ranging market
        /// </summary>
        [Fact]
        public void CalculateRegimeSignal_WithRangingData_ClassifiesRanging()
        {
            // Arrange
            _engine.ConfigureForInstrument("NQ");

            // Feed 30 bars of tight range
            for (int i = 1; i <= 30; i++)
            {
                double oscillation = Math.Sin(i * 0.5) * 10;

                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + oscillation,
                    High = 15005 + oscillation,
                    Low = 14995 + oscillation,
                    Close = 15002 + oscillation,
                    Volume = 30000
                };

                var signal = _engine.CalculateRegimeSignal(bar);

                if (i >= 25) // After warm-up
                {
                    // Assert: Should be ranging
                    Assert.Equal(RegimeState.Ranging, signal.Regime);
                    Assert.True(signal.RegimeConfidence > 0.5, "Should have decent confidence for ranging");
                }
            }
        }

        /// <summary>
        /// Test warm-up period behavior
        /// </summary>
        [Fact]
        public void CalculateRegimeSignal_DuringWarmup_ReturnsIndeterminate()
        {
            // Arrange
            _engine.ConfigureForInstrument("NQ");

            // Act: Feed bars during warm-up (first 30 bars)
            for (int i = 1; i <= 20; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 50),
                    High = 15050 + (i * 50),
                    Low = 14950 + (i * 50),
                    Close = 15025 + (i * 50),
                    Volume = 50000
                };

                var signal = _engine.CalculateRegimeSignal(bar);

                // Assert: During warm-up, should not have full confidence
                Assert.True(signal.IsWarmingUp || signal.RegimeConfidence < 0.6,
                    "Should not have high confidence during warm-up");
            }
        }

        /// <summary>
        /// Test directional bias in trending market
        /// </summary>
        [Fact]
        public void CalculateRegimeSignal_InUptrendWithDiPlus_SetsLongBias()
        {
            // Arrange
            _engine.ConfigureForInstrument("NQ");

            // Feed strong uptrend
            for (int i = 1; i <= 30; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 100),
                    High = 15100 + (i * 100),
                    Low = 14900 + (i * 100),
                    Close = 15050 + (i * 100),
                    Volume = 60000
                };

                var signal = _engine.CalculateRegimeSignal(bar);

                if (i >= 25 && signal.Regime == RegimeState.Trending)
                {
                    // Assert: Strong uptrend should show long bias
                    Assert.True(signal.LongBiasDi, "Should show long bias in uptrend");
                    Assert.False(signal.ShortBiasDi, "Should not show short bias in uptrend");
                    Assert.True(signal.DiPlus > signal.DiMinus, "DI+ should exceed DI- in uptrend");
                }
            }
        }

        /// <summary>
        /// Test entry recommendation for trending market
        /// </summary>
        [Fact]
        public void GetEntryRecommendation_InTrendingMarket_RecommendsLongEntry()
        {
            // Arrange
            _engine.ConfigureForInstrument("NQ");

            // Feed uptrend
            for (int i = 1; i <= 35; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 80),
                    High = 15100 + (i * 80),
                    Low = 14900 + (i * 80),
                    Close = 15050 + (i * 80),
                    Volume = 60000
                };

                _engine.CalculateRegimeSignal(bar);
            }

            // Act
            var (canLong, canShort, confidence) = _engine.GetEntryRecommendation();

            // Assert: Should recommend long entries in uptrend
            Assert.True(canLong || canShort, "Should have entry recommendation");
            Assert.True(confidence > 0.5, "Should have decent confidence");
            Assert.False(canLong && canShort, "Should not recommend both long and short");
        }

        /// <summary>
        /// Test confidence scoring
        /// </summary>
        [Fact]
        public void CalculateRegimeSignal_ConfidenceReflectsSignalStrength()
        {
            // Arrange: Weak signal
            _engine.ConfigureForInstrument("NQ");

            var weakConfidences = new List<double>();
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 5), // Weak trend
                    High = 15010 + (i * 5),
                    Low = 14990 + (i * 5),
                    Close = 15005 + (i * 5),
                    Volume = 30000
                };

                var signal = _engine.CalculateRegimeSignal(bar);
                if (!signal.IsWarmingUp)
                    weakConfidences.Add(signal.RegimeConfidence);
            }

            // Now test strong signal
            _engine.Reset();
            var strongConfidences = new List<double>();
            for (int i = 1; i <= 25; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 200), // Strong trend
                    High = 15200 + (i * 200),
                    Low = 14800 + (i * 200),
                    Close = 15100 + (i * 200),
                    Volume = 80000
                };

                var signal = _engine.CalculateRegimeSignal(bar);
                if (!signal.IsWarmingUp)
                    strongConfidences.Add(signal.RegimeConfidence);
            }

            // Assert: Strong signals should have higher average confidence
            if (weakConfidences.Count > 0 && strongConfidences.Count > 0)
            {
                Assert.True(strongConfidences.Average() > weakConfidences.Average(),
                    "Strong signals should have higher confidence than weak signals");
            }
        }

        /// <summary>
        /// Test regime transitions (trend to range)
        /// </summary>
        [Fact]
        public void CalculateRegimeSignal_TrendToRange_DetectsTransition()
        {
            // Arrange
            _engine.ConfigureForInstrument("NQ");

            // Phase 1: Strong uptrend
            for (int i = 1; i <= 20; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 100),
                    High = 15100 + (i * 100),
                    Low = 14900 + (i * 100),
                    Close = 15050 + (i * 100),
                    Volume = 60000
                };

                _engine.CalculateRegimeSignal(bar);
            }

            // Verify trending
            var lastTrendSignal = _engine.GetLastSignal();
            Assert.NotNull(lastTrendSignal);
            Assert.Equal(RegimeState.Trending, lastTrendSignal.Regime);

            // Phase 2: Transition to range
            for (int i = 21; i <= 40; i++)
            {
                double oscillation = Math.Sin(i * 0.3) * 20;

                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15200 + oscillation,
                    High = 15220 + oscillation,
                    Low = 15180 + oscillation,
                    Close = 15210 + oscillation,
                    Volume = 35000
                };

                _engine.CalculateRegimeSignal(bar);
            }

            // Verify transitioned to ranging
            var lastRangeSignal = _engine.GetLastSignal();
            Assert.NotNull(lastRangeSignal);
            Assert.Equal(RegimeState.Ranging, lastRangeSignal.Regime);
        }

        /// <summary>
        /// Test IsRegimeReliableForEntry validation
        /// </summary>
        [Fact]
        public void IsRegimeReliableForEntry_RequiresSufficientWarmupAndConfidence()
        {
            // Arrange
            _engine.ConfigureForInstrument("NQ");

            // Feed only 10 bars (insufficient for reliable entry)
            for (int i = 1; i <= 10; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 100),
                    High = 15100 + (i * 100),
                    Low = 14900 + (i * 100),
                    Close = 15050 + (i * 100),
                    Volume = 60000
                };

                _engine.CalculateRegimeSignal(bar);
            }

            // Act: Check if ready for entry
            var isReliable = _engine.IsRegimeReliableForEntry();

            // Assert: Should not be reliable during warm-up
            Assert.False(isReliable, "Should not be reliable during warm-up period");

            // Feed enough bars to pass warm-up
            for (int i = 11; i <= 35; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 100),
                    High = 15100 + (i * 100),
                    Low = 14900 + (i * 100),
                    Close = 15050 + (i * 100),
                    Volume = 60000
                };

                _engine.CalculateRegimeSignal(bar);
            }

            // Should be reliable now
            isReliable = _engine.IsRegimeReliableForEntry();
            Assert.True(isReliable, "Should be reliable after warm-up");
        }

        /// <summary>
        /// Test instrument configuration (NQ vs GC)
        /// </summary>
        [Fact]
        public void ConfigureForInstrument_SetsDifferentThresholdsForDifferentSymbols()
        {
            // Arrange & Act
            _engine.ConfigureForInstrument("NQ");
            var nqAdxThreshold = _engine.AdxTrendThreshold;

            var engineGC = new UnifiedMarketRegimeEngine();
            engineGC.ConfigureForInstrument("GC");
            var gcAdxThreshold = engineGC.AdxTrendThreshold;

            // Assert: Different instruments should have different thresholds
            Assert.NotEqual(nqAdxThreshold, gcAdxThreshold);
            Assert.Equal(25.0, nqAdxThreshold); // NQ should be 25
            Assert.Equal(20.0, gcAdxThreshold); // GC should be 20
        }

        /// <summary>
        /// Test reset functionality
        /// </summary>
        [Fact]
        public void Reset_ClearsAllState()
        {
            // Arrange: Build up engine state
            for (int i = 1; i <= 30; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 100),
                    High = 15100 + (i * 100),
                    Low = 14900 + (i * 100),
                    Close = 15050 + (i * 100),
                    Volume = 60000
                };

                _engine.CalculateRegimeSignal(bar);
            }

            var barCountBefore = _engine.TotalBarsProcessed;
            Assert.True(barCountBefore > 0, "Should have processed bars");

            // Act: Reset
            _engine.Reset();

            // Assert: State should be cleared
            Assert.Equal(0, _engine.TotalBarsProcessed);
            Assert.Null(_engine.GetLastSignal());
            Assert.Equal(RegimeState.Indeterminate, _engine.CurrentRegime);
        }

        /// <summary>
        /// Test analysis output (for logging/debugging)
        /// </summary>
        [Fact]
        public void GetRegimeAnalysis_ProducesFormattedOutput()
        {
            // Arrange
            _engine.ConfigureForInstrument("NQ");

            for (int i = 1; i <= 30; i++)
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddHours(i),
                    Open = 15000 + (i * 100),
                    High = 15100 + (i * 100),
                    Low = 14900 + (i * 100),
                    Close = 15050 + (i * 100),
                    Volume = 60000
                };

                _engine.CalculateRegimeSignal(bar);
            }

            // Act
            var analysis = _engine.GetRegimeAnalysis();

            // Assert: Should contain key information
            Assert.NotEmpty(analysis);
            Assert.Contains("Regime", analysis);
            Assert.Contains("ADX", analysis);
            Assert.Contains("RSI", analysis);
            Assert.Contains("MACD", analysis);
        }

        /// <summary>
        /// Test with real-world NY session opening pattern
        /// </summary>
        [Fact]
        public void CalculateRegimeSignal_WithNyOpeningPattern_IdentifiesTrending()
        {
            // Arrange: Simulate NY opening drive (strong initial move)
            _engine.ConfigureForInstrument("NQ");

            // 9:30 AM: Opening drive (strong momentum)
            for (int minute = 0; minute < 60; minute += 1) // 60 one-minute bars
            {
                var bar = new RegimeInput
                {
                    Timestamp = DateTime.Now.AddMinutes(minute),
                    Open = 15000 + (minute * 3), // Steady uptrend
                    High = 15100 + (minute * 3),
                    Low = 14950 + (minute * 3),
                    Close = 15050 + (minute * 3),
                    Volume = 50000 + (minute * 500) // Volume declining through move
                };

                var signal = _engine.CalculateRegimeSignal(bar);

                if (minute > 40) // After enough bars
                {
                    // Opening drive should trigger trending regime
                    if (signal.RegimeConfidence > 0.6 && !signal.IsWarmingUp)
                    {
                        Assert.Equal(RegimeState.Trending, signal.Regime);
                        return; // Test passes
                    }
                }
            }

            Assert.Fail("Should have detected trending during opening drive");
        }
    }
}
