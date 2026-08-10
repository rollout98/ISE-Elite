using System;

namespace ISE.OrderFlowAnalysis.Models
{
    /// <summary>
    /// Aggregated order flow metrics from recent DOM snapshots
    /// Represents the current market structure and institutional activity
    /// </summary>
    public sealed class OrderFlowMetrics
    {
        public OrderFlowMetrics(
            double biasScore,
            double absorptionScore,
            bool rejectionDetected,
            long clusterBidVolume,
            long clusterAskVolume,
            decimal clusterLevel)
        {
            BiasScore = Math.Max(-100, Math.Min(100, biasScore)); // Clamp to [-100, 100]
            AbsorptionScore = Math.Max(0, Math.Min(100, absorptionScore)); // [0, 100]
            RejectionDetected = rejectionDetected;
            ClusterBidVolume = clusterBidVolume;
            ClusterAskVolume = clusterAskVolume;
            ClusterLevel = clusterLevel;
        }

        /// <summary>
        /// Bid-ask imbalance score (-100=full sell pressure, +100=full buy pressure)
        /// Calculated from recent bid/ask volume ratios
        /// </summary>
        public double BiasScore { get; }

        /// <summary>
        /// Institutional absorption score (0=none, 100=heavy absorption)
        /// Detects large orders being filled by market participants
        /// </summary>
        public double AbsorptionScore { get; }

        /// <summary>
        /// Price rejection detected at key DOM level
        /// Indicates institutional defense of price zone
        /// </summary>
        public bool RejectionDetected { get; }

        /// <summary>
        /// Largest bid volume cluster (support level)</summary>
        public long ClusterBidVolume { get; }

        /// <summary>
        /// Largest ask volume cluster (resistance level)</summary>
        public long ClusterAskVolume { get; }

        /// <summary>
        /// Price level where largest cluster exists</summary>
        public decimal ClusterLevel { get; }

        /// <summary>
        /// Is order flow showing buy-side dominance (BiasScore > +50)?
        /// </summary>
        public bool IsBuyDominant => BiasScore > 50;

        /// <summary>
        /// Is order flow showing sell-side dominance (BiasScore < -50)?
        /// </summary>
        public bool IsSellDominant => BiasScore < -50;

        /// <summary>
        /// Is order flow neutral (BiasScore between -50 and +50)?
        /// </summary>
        public bool IsNeutral => Math.Abs(BiasScore) <= 50;
    }
}
