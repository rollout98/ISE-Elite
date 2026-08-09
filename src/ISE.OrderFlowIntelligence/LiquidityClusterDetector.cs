using ISE.OrderFlowIntelligence.Models;

namespace ISE.OrderFlowIntelligence
{
    /// <summary>
    /// Liquidity Cluster Detector
    /// Identifies support and resistance levels from order book volume clustering
    /// Clusters indicate where institutions are placing large orders
    /// </summary>
    public class LiquidityClusterDetector
    {
        /// <summary>
        /// Detected support level
        /// </summary>
        public double SupportLevel { get; private set; } = 0.0;

        /// <summary>
        /// Volume at support level
        /// </summary>
        public long SupportVolume { get; private set; } = 0;

        /// <summary>
        /// Detected resistance level
        /// </summary>
        public double ResistanceLevel { get; private set; } = 0.0;

        /// <summary>
        /// Volume at resistance level
        /// </summary>
        public long ResistanceVolume { get; private set; } = 0;

        /// <summary>
        /// Minimum volume to consider as a "cluster"
        /// </summary>
        public long MinimumClusterVolume { get; set; } = 50;

        /// <summary>
        /// Clustering history (for stability checking)
        /// </summary>
        private readonly Queue<(double support, double resistance)> _clusterHistory = new(10);

        public LiquidityClusterDetector()
        {
        }

        /// <summary>
        /// Detect support and resistance clusters from DOM
        /// </summary>
        public void DetectClusters(DomSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            // Find support: highest volume on bid side
            DetectSupport(snapshot.BidLevels);

            // Find resistance: highest volume on ask side
            DetectResistance(snapshot.AskLevels);

            // Store history for stability checking
            _clusterHistory.Enqueue((SupportLevel, ResistanceLevel));
            while (_clusterHistory.Count > 10)
                _clusterHistory.Dequeue();
        }

        /// <summary>
        /// Detect support from bid-side clustering
        /// </summary>
        private void DetectSupport(List<(double price, long volume)> bidLevels)
        {
            if (bidLevels == null || bidLevels.Count == 0)
                return;

            // Find the bid level with highest volume within good range
            var cluster = bidLevels
                .Where(x => x.volume >= MinimumClusterVolume)
                .OrderByDescending(x => x.volume)
                .FirstOrDefault();

            if (cluster != default)
            {
                SupportLevel = cluster.price;
                SupportVolume = cluster.volume;
            }
        }

        /// <summary>
        /// Detect resistance from ask-side clustering
        /// </summary>
        private void DetectResistance(List<(double price, long volume)> askLevels)
        {
            if (askLevels == null || askLevels.Count == 0)
                return;

            // Find the ask level with highest volume within good range
            var cluster = askLevels
                .Where(x => x.volume >= MinimumClusterVolume)
                .OrderByDescending(x => x.volume)
                .FirstOrDefault();

            if (cluster != default)
            {
                ResistanceLevel = cluster.price;
                ResistanceVolume = cluster.volume;
            }
        }

        /// <summary>
        /// Check if price is at support (within tolerance)
        /// </summary>
        public bool IsPriceAtSupport(double currentPrice, double tolerance = 0.05)
        {
            if (SupportLevel == 0)
                return false;

            return Math.Abs(currentPrice - SupportLevel) <= tolerance;
        }

        /// <summary>
        /// Check if price is at resistance (within tolerance)
        /// </summary>
        public bool IsPriceAtResistance(double currentPrice, double tolerance = 0.05)
        {
            if (ResistanceLevel == 0)
                return false;

            return Math.Abs(currentPrice - ResistanceLevel) <= tolerance;
        }

        /// <summary>
        /// Check if support level is strong (high volume, stable over time)
        /// </summary>
        public bool IsStrongSupport()
        {
            if (SupportVolume < MinimumClusterVolume * 2)
                return false;

            // Check stability: is support consistent over recent bars?
            if (_clusterHistory.Count < 3)
                return false;

            var recentSupports = _clusterHistory.TakeLast(3).Select(x => x.support).ToList();
            double avgSupport = recentSupports.Average();
            double variance = recentSupports.Sum(x => Math.Pow(x - avgSupport, 2)) / recentSupports.Count;

            // Support is strong if it's been consistent (low variance)
            return variance < 0.01; // Tight clustering
        }

        /// <summary>
        /// Check if resistance level is strong (high volume, stable over time)
        /// </summary>
        public bool IsStrongResistance()
        {
            if (ResistanceVolume < MinimumClusterVolume * 2)
                return false;

            // Check stability: is resistance consistent over recent bars?
            if (_clusterHistory.Count < 3)
                return false;

            var recentResistances = _clusterHistory.TakeLast(3).Select(x => x.resistance).ToList();
            double avgResistance = recentResistances.Average();
            double variance = recentResistances.Sum(x => Math.Pow(x - avgResistance, 2)) / recentResistances.Count;

            // Resistance is strong if it's been consistent (low variance)
            return variance < 0.01; // Tight clustering
        }

        /// <summary>
        /// Get distance from current price to support (negative = above, positive = below)
        /// </summary>
        public double GetDistanceToSupport(double currentPrice)
        {
            if (SupportLevel == 0)
                return 0.0;

            return currentPrice - SupportLevel;
        }

        /// <summary>
        /// Get distance from current price to resistance (negative = below, positive = above)
        /// </summary>
        public double GetDistanceToResistance(double currentPrice)
        {
            if (ResistanceLevel == 0)
                return 0.0;

            return ResistanceLevel - currentPrice;
        }

        /// <summary>
        /// Get the trading range (distance between support and resistance)
        /// </summary>
        public double GetTradingRange()
        {
            if (SupportLevel == 0 || ResistanceLevel == 0)
                return 0.0;

            return ResistanceLevel - SupportLevel;
        }

        /// <summary>
        /// Reset detector (for new session)
        /// </summary>
        public void Reset()
        {
            SupportLevel = 0.0;
            SupportVolume = 0;
            ResistanceLevel = 0.0;
            ResistanceVolume = 0;
            _clusterHistory.Clear();
        }

        public override string ToString()
        {
            return $"Support: {SupportLevel:F2} (vol: {SupportVolume}) | Resistance: {ResistanceLevel:F2} (vol: {ResistanceVolume}) | Range: {GetTradingRange():F4}";
        }
    }
}
