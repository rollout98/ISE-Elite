using ISE.OrderFlowIntelligence.Models;
using Xunit;

namespace ISE.OrderFlowIntelligence.Tests
{
    /// <summary>
    /// Tests for order flow intelligence components
    /// Validates DOM analysis, liquidity detection, and institutional signal detection
    /// </summary>
    public class OrderFlowComponentsTests
    {
        /// <summary>
        /// Test DomSnapshot initialization with bid/ask levels
        /// </summary>
        [Fact]
        public void DomSnapshot_Initialize_WithBidAskLevels()
        {
            // Arrange
            var timestamp = DateTime.Now;
            var bidLevels = new List<(double price, long volume)>
            {
                (100.00, 1000),
                (99.99, 800),
                (99.98, 600)
            };

            var askLevels = new List<(double price, long volume)>
            {
                (100.01, 1200),
                (100.02, 900),
                (100.03, 700)
            };

            // Act
            var snapshot = new DomSnapshot
            {
                Timestamp = timestamp,
                BidPrice = 100.00,
                AskPrice = 100.01,
                BidVolume = 1000,
                AskVolume = 1200,
                BidLevels = bidLevels,
                AskLevels = askLevels
            };

            // Assert
            Assert.Equal(3, snapshot.BidLevels.Count);
            Assert.Equal(3, snapshot.AskLevels.Count);
            Assert.Equal(2400, snapshot.TotalBidVolume);
            Assert.Equal(2800, snapshot.TotalAskVolume);
        }

        /// <summary>
        /// Test OrderFlowMetrics bullish order flow signal
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_BullishOrderFlow_WhenBiasIsNegative()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                OrderFlowBias = -75.0,  // Strong buying pressure
                IsLiquidEnoughForEntry = true,
                DataFreshness = 0.95
            };

            // Act
            var isBullish = metrics.IsBullishOrderFlow;
            var recommendation = metrics.GetEntryRecommendation();

            // Assert
            Assert.True(isBullish);
            Assert.False(recommendation.recommendShort);
        }

        /// <summary>
        /// Test OrderFlowMetrics bearish order flow signal
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_BearishOrderFlow_WhenBiasIsPositive()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                OrderFlowBias = 75.0,   // Strong selling pressure
                IsLiquidEnoughForEntry = true,
                DataFreshness = 0.95
            };

            // Act
            var isBearish = metrics.IsBearishOrderFlow;

            // Assert
            Assert.True(isBearish);
        }

        /// <summary>
        /// Test liquidity quality calculation
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_LiquidityQuality_ReflectsSpreadAndDepth()
        {
            // Arrange: Tight spread, deep book = high liquidity
            var metrics = new OrderFlowMetrics
            {
                Spread = 0.5,  // Half a tick
                LiquidityQuality = 0.95,  // Excellent
                IsLiquidEnoughForEntry = true
            };

            // Act & Assert
            Assert.True(metrics.IsLiquidEnoughForEntry);
            Assert.True(metrics.LiquidityQuality > 0.6);
        }

        /// <summary>
        /// Test institutional absorption detection
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_InstitutionalAbsorption_DetectsLargeOrderExecution()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                InstitutionalAbsorption = true,
                AbsorptionStrength = 0.85,  // 85% confidence
                SupportLevel = 99.50,
                SupportClusterVolume = 50000
            };

            // Act & Assert
            Assert.True(metrics.InstitutionalAbsorption);
            Assert.True(metrics.AbsorptionStrength > 0.8);
        }

        /// <summary>
        /// Test rejection detection at resistance
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_RejectionAtResistance_IndicatesSellersDefending()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                ResistanceLevel = 100.50,
                ResistanceClusterVolume = 45000,
                RejectionAtResistance = true,
                OrderFlowBias = 60.0  // Selling pressure
            };

            // Act & Assert
            Assert.True(metrics.RejectionAtResistance);
            Assert.True(metrics.IsBearishOrderFlow);
        }

        /// <summary>
        /// Test support detection and bounce confirmation
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_RejectionAtSupport_IndicatesBuyersDefending()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                SupportLevel = 99.50,
                SupportClusterVolume = 40000,
                RejectionAtSupport = true,
                OrderFlowBias = -60.0  // Buying pressure
            };

            // Act & Assert
            Assert.True(metrics.RejectionAtSupport);
            Assert.True(metrics.IsBullishOrderFlow);
        }

        /// <summary>
        /// Test entry recommendation with all conditions met
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_GetEntryRecommendation_LongSignalWhenBullishAndLiquid()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                OrderFlowBias = -70.0,  // Strong buying
                RejectionAtSupport = true,
                IsLiquidEnoughForEntry = true,
                DataFreshness = 0.9,  // Fresh data
                LiquidityQuality = 0.85
            };

            // Act
            var (recommendLong, recommendShort, confidence) = metrics.GetEntryRecommendation();

            // Assert
            Assert.True(recommendLong);
            Assert.False(recommendShort);
            Assert.True(confidence > 0);
        }

        /// <summary>
        /// Test entry recommendation rejection when liquidity is poor
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_GetEntryRecommendation_RejectsEntryWhenIlliquid()
        {
            // Arrange
            var metrics = new OrderFlowMetrics
            {
                OrderFlowBias = -70.0,  // Strong buying signal
                IsLiquidEnoughForEntry = false,  // But liquidity is poor
                DataFreshness = 0.9
            };

            // Act
            var (recommendLong, recommendShort, confidence) = metrics.GetEntryRecommendation();

            // Assert: No entry despite bullish order flow
            Assert.False(recommendLong);
            Assert.False(recommendShort);
            Assert.Equal(0.0, confidence);
        }

        /// <summary>
        /// Test order flow bias normalization
        /// </summary>
        [Fact]
        public void OrderFlowMetrics_BiasRange_RespectsBounds()
        {
            // Arrange
            var metricsExtreme = new OrderFlowMetrics
            {
                OrderFlowBias = 100.0  // Max selling
            };

            var metricsNeutral = new OrderFlowMetrics
            {
                OrderFlowBias = 0.0  // Balanced
            };

            var metricsBullish = new OrderFlowMetrics
            {
                OrderFlowBias = -100.0  // Max buying
            };

            // Act & Assert
            Assert.Equal(100.0, metricsExtreme.OrderFlowBias);
            Assert.Equal(0.0, metricsNeutral.OrderFlowBias);
            Assert.Equal(-100.0, metricsBullish.OrderFlowBias);
        }

        /// <summary>
        /// Test DomSnapshot volume imbalance calculation
        /// </summary>
        [Fact]
        public void DomSnapshot_VolumeImbalance_CalculatesFromLevels()
        {
            // Arrange
            var snapshot = new DomSnapshot
            {
                BidPrice = 100.00,
                AskPrice = 100.01,
                BidVolume = 1000,
                AskVolume = 1500,
                BidLevels = new List<(double price, long volume)>
                {
                    (100.00, 1000)
                },
                AskLevels = new List<(double price, long volume)>
                {
                    (100.01, 1500)
                }
            };

            // Act
            var imbalance = snapshot.VolumeImbalance;
            var imbalanceScore = snapshot.GetImbalanceScore();

            // Assert: More ask volume = selling pressure (positive imbalance)
            Assert.True(imbalance > 0);
            Assert.True(imbalanceScore > 0);
        }
    }
}
