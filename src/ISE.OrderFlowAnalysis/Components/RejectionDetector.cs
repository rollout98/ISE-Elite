using System;
using System.Collections.Generic;
using System.Linq;
using ISE.OrderFlowAnalysis.Models;

namespace ISE.OrderFlowAnalysis.Components
{
    /// <summary>
    /// Detects price rejection at DOM levels
    /// When price tests a level then reverses sharply with heavy volume
    /// Indicates institutional defense of a price zone
    /// </summary>
    public sealed class RejectionDetector
    {
        private readonly List<decimal> _recentLows = new List<decimal>();
        private readonly List<decimal> _recentHighs = new List<decimal>();
        private const int MaxRecentBars = 10;

        /// <summary>
        /// Track recent price extremes for rejection detection
        /// </summary>
        public void UpdatePriceRange(decimal high, decimal low)
        {
            _recentHighs.Add(high);
            _recentLows.Add(low);

            if (_recentHighs.Count > MaxRecentBars)
                _recentHighs.RemoveAt(0);
            if (_recentLows.Count > MaxRecentBars)
                _recentLows.RemoveAt(0);
        }

        /// <summary>
        /// Detect rejection at resistance (ask cluster)
        /// Price touches resistance cluster, then falls sharply
        /// </summary>
        public bool DetectResistanceRejection(
            decimal currentPrice,
            decimal currentHigh,
            DomSnapshot snapshot,
            decimal rejectionThreshold = 0.5m) // Min 2 ticks move down
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (_recentHighs.Count < 2) return false;

            // Get resistance cluster
            var clusterDetector = new LiquidityClusterDetector();
            var (resistanceLevel, resistanceVolume) = clusterDetector.GetResistanceCluster(currentPrice, snapshot);

            if (resistanceLevel == 0 || resistanceVolume == 0) return false;

            // Check if price tested resistance
            var testedResistance = currentHigh >= resistanceLevel - 0.25m;

            // Check if price is now lower (rejection)
            var prevHigh = _recentHighs.Count >= 2 ? _recentHighs[_recentHighs.Count - 2] : currentHigh;
            var rejectionMove = prevHigh - currentPrice;

            return testedResistance && rejectionMove >= rejectionThreshold;
        }

        /// <summary>
        /// Detect rejection at support (bid cluster)
        /// Price tests support cluster, then rises sharply
        /// </summary>
        public bool DetectSupportRejection(
            decimal currentPrice,
            decimal currentLow,
            DomSnapshot snapshot,
            decimal rejectionThreshold = 0.5m) // Min 2 ticks move up
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (_recentLows.Count < 2) return false;

            // Get support cluster
            var clusterDetector = new LiquidityClusterDetector();
            var (supportLevel, supportVolume) = clusterDetector.GetSupportCluster(currentPrice, snapshot);

            if (supportLevel == 0 || supportVolume == 0) return false;

            // Check if price tested support
            var testedSupport = currentLow <= supportLevel + 0.25m;

            // Check if price is now higher (rejection)
            var prevLow = _recentLows.Count >= 2 ? _recentLows[_recentLows.Count - 2] : currentLow;
            var rejectionMove = currentPrice - prevLow;

            return testedSupport && rejectionMove >= rejectionThreshold;
        }

        /// <summary>
        /// Get rejection strength (0-100)
        /// Based on how much price moved away from cluster after testing
        /// </summary>
        public double GetRejectionStrength(
            decimal currentPrice,
            decimal testPrice,
            decimal rejectionMagnitude)
        {
            if (rejectionMagnitude <= 0) return 0;

            // Scale 0-2 ticks to 0-100 score
            return Math.Min(100, (double)rejectionMagnitude * 50);
        }

        /// <summary>
        /// Reset price history
        /// </summary>
        public void Reset()
        {
            _recentHighs.Clear();
            _recentLows.Clear();
        }
    }
}
