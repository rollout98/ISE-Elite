using System;
using ISE.OrderFlowAnalysis.Components;
using ISE.OrderFlowAnalysis.Models;

namespace ISE.OrderFlowAnalysis
{
    /// <summary>
    /// Main orchestrator for order flow analysis
    /// Combines bias, absorption, rejection, and clustering detection
    /// Outputs comprehensive OrderFlowMetrics for trading decisions
    /// </summary>
    public sealed class OrderFlowAnalysisEngine
    {
        private readonly OrderFlowBiasCalculator _biasCalculator = new OrderFlowBiasCalculator();
        private readonly LiquidityClusterDetector _clusterDetector = new LiquidityClusterDetector();
        private readonly OrderAbsorptionAnalyzer _absorptionAnalyzer = new OrderAbsorptionAnalyzer();
        private readonly RejectionDetector _rejectionDetector = new RejectionDetector();

        /// <summary>
        /// Analyze a DOM snapshot and return comprehensive order flow metrics
        /// </summary>
        public OrderFlowMetrics Analyze(
            DomSnapshot snapshot,
            decimal currentPrice,
            decimal barHigh,
            decimal barLow)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            // Update rejection detector with price range
            _rejectionDetector.UpdatePriceRange(barHigh, barLow);

            // Calculate bias
            var bias = _biasCalculator.CalculateBias(snapshot);
            var smoothedBias = _biasCalculator.GetSmoothedBias();

            // Calculate absorption
            var absorption = _absorptionAnalyzer.AnalyzeAbsorption(snapshot);

            // Detect rejection
            var resistanceRejection = _rejectionDetector.DetectResistanceRejection(
                currentPrice, barHigh, snapshot);
            var supportRejection = _rejectionDetector.DetectSupportRejection(
                currentPrice, barLow, snapshot);
            var rejectionDetected = resistanceRejection || supportRejection;

            // Detect clusters
            var (clusterLevel, clusterVolume, _) = _clusterDetector.DetectLargestCluster(snapshot);
            var (bidClusterLevel, bidClusterVolume) = _clusterDetector.DetectBidCluster(snapshot);
            var (askClusterLevel, askClusterVolume) = _clusterDetector.DetectAskCluster(snapshot);

            // Return aggregated metrics
            return new OrderFlowMetrics(
                biasScore: smoothedBias,
                absorptionScore: absorption,
                rejectionDetected: rejectionDetected,
                clusterBidVolume: bidClusterVolume,
                clusterAskVolume: askClusterVolume,
                clusterLevel: clusterLevel);
        }

        /// <summary>
        /// Check if entry is confirmed by order flow
        /// Entry requires:
        /// 1. Strong bias in direction of trade
        /// 2. Absorption supporting the move
        /// 3. No rejection against entry
        /// </summary>
        public bool IsEntryConfirmedByOrderFlow(
            OrderFlowMetrics metrics,
            string direction,
            double biasThreshold = 50,
            double absorptionThreshold = 30)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));
            if (string.IsNullOrEmpty(direction)) throw new ArgumentNullException(nameof(direction));

            var isBuy = direction.ToUpper() == "BUY";

            // Check bias matches direction
            var biasConfirmed = isBuy
                ? metrics.BiasScore > biasThreshold  // Buy: positive bias
                : metrics.BiasScore < -biasThreshold; // Sell: negative bias

            // Check absorption
            var absorptionConfirmed = metrics.AbsorptionScore > absorptionThreshold;

            // Check no rejection against direction
            var noRejection = !metrics.RejectionDetected;

            return biasConfirmed && absorptionConfirmed && noRejection;
        }

        /// <summary>
        /// Check if exit is warranted due to order flow deterioration
        /// Exit when:
        /// 1. Bias flips against position
        /// 2. Absorption reverses
        /// 3. Strong rejection detected
        /// </summary>
        public bool ShouldExitByOrderFlow(
            OrderFlowMetrics metrics,
            string direction,
            double flipThreshold = 30)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));

            var isBuy = direction.ToUpper() == "BUY";

            // Bias flipped significantly against direction
            var biasFlipped = isBuy
                ? metrics.BiasScore < -flipThreshold
                : metrics.BiasScore > flipThreshold;

            // Rejection detected against direction
            var rejectionAgainstPosition = metrics.RejectionDetected;

            return biasFlipped || rejectionAgainstPosition;
        }

        /// <summary>
        /// Reset all analyzers (useful for new session)
        /// </summary>
        public void Reset()
        {
            _biasCalculator.Reset();
            _absorptionAnalyzer.Reset();
            _rejectionDetector.Reset();
        }
    }
}
