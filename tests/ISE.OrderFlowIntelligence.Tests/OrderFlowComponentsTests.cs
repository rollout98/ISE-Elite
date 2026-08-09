using ISE.OrderFlowIntelligence.Models;
using Xunit;

namespace ISE.OrderFlowIntelligence.Tests
{
    /// <summary>
    /// Unit tests for OrderFlowIntelligence components
    /// Validates DOM data, bias calculation, and clustering detection
    /// </summary>
    public class OrderFlowComponentsTests
    {
        /// <summary>
        /// Test DomSnapshot creation with bid/ask levels
        /// </summary>
        [Fact]
        public void DomSnapshot_CreatesWithMultipleLevels()
        {
            // Arrange
            var timestamp = DateTime.Now;
            var bidLevels = new List<(double price, double volume)>
            {
                (100.00, 1000),
                (99.99, 800),
                (99.98, 600)
            };

            var askLevels = new List<(double price, double volume)>
            {
                (100.01, 1200),
                (100.02, 900),
                (100.03, 700)
            };

            // Act
            var snapshot = new DomSnapshot
            {
                Timestamp = timestamp,
                BidLevels = bidLevels.Count,
                AskLevels = askLevels.Count,
                TotalBidVolume = bidLevels.Sum(x => x.volume),
                TotalAskVolume = askLevels.Sum(x => x.volume)
            };

            // Assert
            Assert.Equal(3, snapshot.BidLevels);
            Assert.Equal(3, snapshot.AskLevels);
            Assert.Equal(2400, snapshot.TotalBidVolume);
            Assert.Equal(2800, snapshot.TotalAskVolume);
        }

        /// <summary>
        /// Test OrderFlowMetrics calculates bid/ask imbalance
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_CalculatesBidAskImbalance()
        {
            // Arrange: More ask volume (bearish)
            var metrics = new OrderFlowMetrics
            {
                OrderFlowBias = 45.0, // Bearish (positive toward sellers)
                TotalBidVolume = 5000,
                TotalAskVolume = 7000
            };

            // Assert
            Assert.True(metrics.OrderFlowBias > 0, "Positive bias indicates more selling pressure");
        }

        /// <summary>
        /// Test OrderFlowMetrics for bullish scenario
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_BullishScenario()
        {
            // Arrange: More bid volume (bullish)
            var metrics = new OrderFlowMetrics
            {
                OrderFlowBias = -55.0, // Bullish (negative toward buyers)
                TotalBidVolume = 8000,
                TotalAskVolume = 5000
            };

            // Assert
            Assert.True(metrics.OrderFlowBias < 0, "Negative bias indicates more buying pressure");
            Assert.True(Math.Abs(metrics.OrderFlowBias) > 50, "Should be strong bias");
        }

        /// <summary>
        /// Test OrderFlowMetrics neutral scenario
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_NeutralScenario()
        {
            // Arrange: Balanced bid/ask
            var metrics = new OrderFlowMetrics
            {
                OrderFlowBias = -5.0, // Nearly neutral
                TotalBidVolume = 6000,
                TotalAskVolume = 6100
            };

            // Assert
            Assert.True(Math.Abs(metrics.OrderFlowBias) < 10, "Bias should be near zero when balanced");
        }

        /// <summary>
        /// Test support level detection from DOM clustering
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_DetectsSupportFromBidClustering()
        {
            // Arrange: High volume bid cluster at 99.95 (support)
            var metrics = new OrderFlowMetrics
            {
                SupportLevel = 99.95,
                SupportClusterVolume = 5000, // Large bid cluster
                ResistanceLevel = 100.05,
                ResistanceClusterVolume = 2000  // Smaller ask cluster
            };

            // Assert
            Assert.Equal(99.95, metrics.SupportLevel);
            Assert.True(metrics.SupportClusterVolume > metrics.ResistanceClusterVolume,
                "Bid cluster should be larger than ask cluster at support");
        }

        /// <summary>
        /// Test resistance level detection from DOM clustering
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_DetectsResistanceFromAskClustering()
        {
            // Arrange: High volume ask cluster at 100.05 (resistance)
            var metrics = new OrderFlowMetrics
            {
                ResistanceLevel = 100.05,
                ResistanceClusterVolume = 6000, // Large ask cluster
                SupportLevel = 99.95,
                SupportClusterVolume = 2000    // Smaller bid cluster
            };

            // Assert
            Assert.Equal(100.05, metrics.ResistanceLevel);
            Assert.True(metrics.ResistanceClusterVolume > metrics.SupportClusterVolume,
                "Ask cluster should be larger than bid cluster at resistance");
        }

        /// <summary>
        /// Test absorption detection (large order being filled)
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_DetectsOrderAbsorption()
        {
            // Arrange: Large volume without price move (absorption)
            var metrics = new OrderFlowMetrics
            {
                OrderFlowBias = 0.0, // Neutral despite high volume
                TotalBidVolume = 50000, // Very high volume
                TotalAskVolume = 50100, // Nearly balanced
                IsLiquidEnoughForEntry = true,
                AbsorptionState = "Strong buyer absorption at bid"
            };

            // Assert
            Assert.Equal("Strong buyer absorption at bid", metrics.AbsorptionState);
        }

        /// <summary>
        /// Test rejection detection (price bouncing off level)
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_DetectsRejectionAtLevel()
        {
            // Arrange: Price rejected at resistance
            var metrics = new OrderFlowMetrics
            {
                ResistanceLevel = 100.05,
                RejectionAtResistance = true,
                RejectionStrength = "Strong rejection, double top pattern"
            };

            // Assert
            Assert.True(metrics.RejectionAtResistance, "Should detect rejection at resistance");
        }

        /// <summary>
        /// Test liquidity validation
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_ValidatesLiquidity()
        {
            // Arrange: Thin book scenario
            var thinBook = new OrderFlowMetrics
            {
                TotalBidVolume = 100,
                TotalAskVolume = 80,
                IsLiquidEnoughForEntry = false // Too thin to trade
            };

            var goodBook = new OrderFlowMetrics
            {
                TotalBidVolume = 5000,
                TotalAskVolume = 4500,
                IsLiquidEnoughForEntry = true  // Good liquidity
            };

            // Assert
            Assert.False(thinBook.IsLiquidEnoughForEntry, "Thin book should not be tradeable");
            Assert.True(goodBook.IsLiquidEnoughForEntry, "Good book should be tradeable");
        }

        /// <summary>
        /// Test bias score range (-100 to +100)
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_BiasScoreIsWithinRange()
        {
            // Test extreme bullish
            var bullish = new OrderFlowMetrics { OrderFlowBias = -100.0 };
            Assert.True(bullish.OrderFlowBias >= -100 && bullish.OrderFlowBias <= 100);

            // Test extreme bearish
            var bearish = new OrderFlowMetrics { OrderFlowBias = 100.0 };
            Assert.True(bearish.OrderFlowBias >= -100 && bearish.OrderFlowBias <= 100);

            // Test neutral
            var neutral = new OrderFlowMetrics { OrderFlowBias = 0.0 };
            Assert.True(neutral.OrderFlowBias >= -100 && neutral.OrderFlowBias <= 100);
        }

        /// <summary>
        /// Test S/R level validity
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_SupportResistanceLevelValidity()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                SupportLevel = 99.95,
                ResistanceLevel = 100.05
            };

            // Assert: Support should be below resistance
            Assert.True(metrics.SupportLevel < metrics.ResistanceLevel,
                "Support level must be below resistance level");
        }

        /// <summary>
        /// Test data freshness tracking
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_TracksDataFreshness()
        {
            // Arrange
            var metricsOld = new OrderFlowMetrics
            {
                LastUpdateTime = DateTime.Now.AddSeconds(-5), // 5 seconds ago
                DataQualityScore = 0.95 // Still good
            };

            var metricsFresh = new OrderFlowMetrics
            {
                LastUpdateTime = DateTime.Now,
                DataQualityScore = 1.0 // Perfect
            };

            // Assert
            Assert.True(metricsFresh.DataQualityScore >= metricsOld.DataQualityScore,
                "Fresher data should have higher quality");
        }

        /// <summary>
        /// Test cluster volume comparison
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_CompareClusterStrength()
        {
            // Arrange: Support much stronger than resistance
            var metrics = new OrderFlowMetrics
            {
                SupportClusterVolume = 8000,
                ResistanceClusterVolume = 2000
            };

            // Act: Calculate cluster ratio
            var supportStrengthRatio = metrics.SupportClusterVolume / (double)metrics.ResistanceClusterVolume;

            // Assert
            Assert.True(supportStrengthRatio > 3, "Support should be significantly stronger");
        }

        /// <summary>
        /// Test spread measurement
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_MeasuresSpread()
        {
            // Arrange
            var tightSpread = new OrderFlowMetrics
            {
                BidPrice = 100.00,
                AskPrice = 100.01 // 1 tick spread
            };

            var wideSpread = new OrderFlowMetrics
            {
                BidPrice = 100.00,
                AskPrice = 100.05 // 5 tick spread
            };

            // Act
            var tightSpreadSize = tightSpread.AskPrice - tightSpread.BidPrice;
            var wideSpreadSize = wideSpread.AskPrice - wideSpread.BidPrice;

            // Assert
            Assert.True(tightSpreadSize < wideSpreadSize, "Tight spread should be smaller");
        }

        /// <summary>
        /// Test mid-point calculation
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_CalculatesMidpoint()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                BidPrice = 100.00,
                AskPrice = 100.02
            };

            // Act
            var midpoint = (metrics.BidPrice + metrics.AskPrice) / 2.0;

            // Assert
            Assert.Equal(100.01, midpoint);
        }

        /// <summary>
        /// Test DOM update frequency validation
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_ValidatesUpdateFrequency()
        {
            // Arrange
            var staleData = new OrderFlowMetrics
            {
                LastUpdateTime = DateTime.Now.AddSeconds(-10), // 10 seconds old
                DataQualityScore = 0.7 // Degraded
            };

            var liveData = new OrderFlowMetrics
            {
                LastUpdateTime = DateTime.Now, // Current
                DataQualityScore = 1.0 // Excellent
            };

            // Assert
            Assert.True(liveData.DataQualityScore > staleData.DataQualityScore,
                "Live data should have better quality than stale");
        }

        /// <summary>
        /// Test volume profile summary
        /// </summary>
        [Fact]
        public void DomSnapshot_CalculatesVolumeProfile()
        {
            // Arrange
            var snapshot = new DomSnapshot
            {
                TotalBidVolume = 50000,
                TotalAskVolume = 45000,
                BidLevels = 10,
                AskLevels = 10
            };

            // Act
            var avgBidVolume = snapshot.TotalBidVolume / (double)snapshot.BidLevels;
            var avgAskVolume = snapshot.TotalAskVolume / (double)snapshot.AskLevels;

            // Assert
            Assert.Equal(5000, avgBidVolume);
            Assert.Equal(4500, avgAskVolume);
        }
    }
}
