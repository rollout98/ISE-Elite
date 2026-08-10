using System;
using System.Collections.Generic;
using System.Linq;
using ISE.OrderFlowAnalysis.Models;

namespace ISE.OrderFlowAnalysis.Components
{
    /// <summary>
    /// Detects support/resistance levels from order book clustering
    /// Identifies where institutional volume is concentrated
    /// </summary>
    public sealed class LiquidityClusterDetector
    {
        /// <summary>
        /// Detect the largest bid volume cluster (support level)
        /// </summary>
        public (decimal level, long volume) DetectBidCluster(DomSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.BidLevels.Count == 0) return (0m, 0);

            var maxLevel = snapshot.BidLevels.OrderByDescending(l => l.Volume).FirstOrDefault();
            return (maxLevel?.Price ?? 0m, maxLevel?.Volume ?? 0);
        }

        /// <summary>
        /// Detect the largest ask volume cluster (resistance level)
        /// </summary>
        public (decimal level, long volume) DetectAskCluster(DomSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.AskLevels.Count == 0) return (0m, 0);

            var maxLevel = snapshot.AskLevels.OrderByDescending(l => l.Volume).FirstOrDefault();
            return (maxLevel?.Price ?? 0m, maxLevel?.Volume ?? 0);
        }

        /// <summary>
        /// Detect the largest cluster overall (bid or ask side)
        /// </summary>
        public (decimal level, long volume, string side) DetectLargestCluster(DomSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var (bidLevel, bidVolume) = DetectBidCluster(snapshot);
            var (askLevel, askVolume) = DetectAskCluster(snapshot);

            if (bidVolume >= askVolume)
                return (bidLevel, bidVolume, "BID");
            else
                return (askLevel, askVolume, "ASK");
        }

        /// <summary>
        /// Check if price is testing a support/resistance cluster
        /// Support = price near bid cluster
        /// Resistance = price near ask cluster
        /// Tolerance: within 0.5 points (2 ticks for most futures)
        /// </summary>
        public bool IsPriceAtCluster(decimal currentPrice, DomSnapshot snapshot, decimal toleranceTicks = 0.5m)
        {
            var (_, _, side) = DetectLargestCluster(snapshot);
            decimal clusterLevel = side == "BID" 
                ? DetectBidCluster(snapshot).level 
                : DetectAskCluster(snapshot).level;

            if (clusterLevel == 0) return false;

            return Math.Abs(currentPrice - clusterLevel) <= toleranceTicks;
        }

        /// <summary>
        /// Get support cluster below current price
        /// </summary>
        public (decimal level, long volume) GetSupportCluster(decimal currentPrice, DomSnapshot snapshot)
        {
            if (snapshot?.BidLevels == null || snapshot.BidLevels.Count == 0)
                return (0m, 0);

            var supportLevels = snapshot.BidLevels
                .Where(l => l.Price < currentPrice)
                .OrderByDescending(l => l.Volume)
                .FirstOrDefault();

            return supportLevels != null 
                ? (supportLevels.Price, supportLevels.Volume) 
                : (0m, 0);
        }

        /// <summary>
        /// Get resistance cluster above current price
        /// </summary>
        public (decimal level, long volume) GetResistanceCluster(decimal currentPrice, DomSnapshot snapshot)
        {
            if (snapshot?.AskLevels == null || snapshot.AskLevels.Count == 0)
                return (0m, 0);

            var resistanceLevels = snapshot.AskLevels
                .Where(l => l.Price > currentPrice)
                .OrderByDescending(l => l.Volume)
                .FirstOrDefault();

            return resistanceLevels != null 
                ? (resistanceLevels.Price, resistanceLevels.Volume) 
                : (0m, 0);
        }
    }
}
