using System;
using System.Collections.Generic;
using System.Linq;
using ISE.OrderFlowAnalysis.Models;

namespace ISE.OrderFlowAnalysis.Components
{
    /// <summary>
    /// Detects institutional order absorption
    /// When large orders are filled by market participants at key levels
    /// Indicates institutional buying/selling interest
    /// </summary>
    public sealed class OrderAbsorptionAnalyzer
    {
        private readonly List<long> _recentBidVolumes = new List<long>();
        private readonly List<long> _recentAskVolumes = new List<long>();
        private const int MaxRecentSnapshots = 10;

        /// <summary>
        /// Analyze a DOM snapshot for absorption patterns
        /// Returns 0-100 score (0=none, 100=heavy absorption)
        /// </summary>
        public double AnalyzeAbsorption(DomSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            _recentBidVolumes.Add(snapshot.TotalBidVolume);
            _recentAskVolumes.Add(snapshot.TotalAskVolume);

            if (_recentBidVolumes.Count > MaxRecentSnapshots)
                _recentBidVolumes.RemoveAt(0);
            if (_recentAskVolumes.Count > MaxRecentSnapshots)
                _recentAskVolumes.RemoveAt(0);

            return CalculateAbsorptionScore();
        }

        /// <summary>
        /// Calculate absorption based on volume spike patterns
        /// Heavy absorption = large volume appearing then disappearing quickly
        /// </summary>
        private double CalculateAbsorptionScore()
        {
            if (_recentBidVolumes.Count < 2) return 0;

            // Check for bid-side absorption (institutional buying)
            var bidAbsorption = DetectVolumeAbsorption(_recentBidVolumes);

            // Check for ask-side absorption (institutional selling)
            var askAbsorption = DetectVolumeAbsorption(_recentAskVolumes);

            // Return the higher absorption score
            return Math.Max(bidAbsorption, askAbsorption);
        }

        /// <summary>
        /// Detect if volumes show absorption pattern (spike and decay)
        /// </summary>
        private static double DetectVolumeAbsorption(List<long> volumes)
        {
            if (volumes.Count < 3) return 0;

            var avgVolume = volumes.Take(volumes.Count - 1).Average();
            var lastVolume = volumes[volumes.Count - 1];
            var prevVolume = volumes[volumes.Count - 2];

            // Absorption: volume spikes then drops
            // Peak volume is 2x+ average, current is back to normal
            var maxRecent = volumes.Skip(Math.Max(0, volumes.Count - 5)).Max();

            if (maxRecent > avgVolume * 1.5 && lastVolume < avgVolume * 1.2)
            {
                // Score based on how much volume was absorbed
                var absorptionRatio = (maxRecent - lastVolume) / (decimal)maxRecent;
                return Math.Min(100, (double)absorptionRatio * 100);
            }

            return 0;
        }

        /// <summary>
        /// Check if bid-side showing absorption (institutional buying)
        /// </summary>
        public bool IsBidAbsorption(DomSnapshot snapshot, double threshold = 40)
        {
            var absorption = AnalyzeAbsorption(snapshot);
            return absorption > threshold && snapshot.Ratio > 1.0; // Absorption + buy pressure
        }

        /// <summary>
        /// Check if ask-side showing absorption (institutional selling)
        /// </summary>
        public bool IsAskAbsorption(DomSnapshot snapshot, double threshold = 40)
        {
            var absorption = AnalyzeAbsorption(snapshot);
            return absorption > threshold && snapshot.Ratio < 1.0; // Absorption + sell pressure
        }

        /// <summary>
        /// Reset analysis history
        /// </summary>
        public void Reset()
        {
            _recentBidVolumes.Clear();
            _recentAskVolumes.Clear();
        }
    }
}
