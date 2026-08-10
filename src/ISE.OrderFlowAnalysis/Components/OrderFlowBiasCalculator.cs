using System;
using System.Collections.Generic;
using System.Linq;
using ISE.OrderFlowAnalysis.Models;

namespace ISE.OrderFlowAnalysis.Components
{
    /// <summary>
    /// Calculates buy-side vs sell-side order flow imbalance
    /// Score: -100 (full sell pressure) to +100 (full buy pressure)
    /// Updated with each new DOM snapshot
    /// </summary>
    public sealed class OrderFlowBiasCalculator
    {
        private readonly List<double> _recentRatios = new List<double>();
        private const int MaxRecentSnapshots = 20; // Keep last 20 DOM snapshots

        /// <summary>
        /// Calculate bias from a single DOM snapshot
        /// </summary>
        public double CalculateBias(DomSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            // Bias based on bid/ask volume imbalance
            // Ratio > 1.0 = more bids (bullish)
            // Ratio < 1.0 = more asks (bearish)
            // Ratio = 1.0 = balanced (neutral)

            var ratio = snapshot.Ratio;
            var bias = CalculateBiasFromRatio(ratio);

            _recentRatios.Add(ratio);
            if (_recentRatios.Count > MaxRecentSnapshots)
                _recentRatios.RemoveAt(0);

            return bias;
        }

        /// <summary>
        /// Get smoothed bias over recent snapshots (avoid single-snapshot noise)
        /// </summary>
        public double GetSmoothedBias()
        {
            if (_recentRatios.Count == 0) return 0;

            var avgRatio = _recentRatios.Average();
            return CalculateBiasFromRatio(avgRatio);
        }

        /// <summary>
        /// Convert bid/ask ratio to -100 to +100 bias score
        /// </summary>
        private static double CalculateBiasFromRatio(double ratio)
        {
            if (ratio <= 0) return -100; // No bids
            if (ratio >= 3.0) return 100; // Extreme buy pressure
            if (ratio <= 0.33) return -100; // Extreme sell pressure

            // Linear interpolation between -100 and +100
            // Ratio 1.0 = 0 (neutral)
            // Ratio > 1.0 = positive (buy pressure)
            // Ratio < 1.0 = negative (sell pressure)

            return (ratio - 1.0) * 100; // Scale to [-100, 100] around neutral 1.0
        }

        /// <summary>
        /// Clear history (useful for new session or reset)
        /// </summary>
        public void Reset()
        {
            _recentRatios.Clear();
        }

        /// <summary>
        /// Get the number of snapshots used in smoothed calculation
        /// </summary>
        public int SnapshotCount => _recentRatios.Count;
    }
}
