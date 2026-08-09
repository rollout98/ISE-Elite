using ISE.OrderFlowIntelligence.Models;

namespace ISE.OrderFlowIntelligence
{
    /// <summary>
    /// Depth of Market (Level 2) data feed
    /// Receives updates from NinjaTrader OnLevel2Update() events
    /// Maintains current DOM snapshot and history for analysis
    /// </summary>
    public class DomDataFeed
    {
        /// <summary>
        /// Current DOM snapshot (top of book + depth)
        /// </summary>
        private DomSnapshot? _currentSnapshot;

        /// <summary>
        /// Previous DOM snapshot (for change detection)
        /// </summary>
        private DomSnapshot? _previousSnapshot;

        /// <summary>
        /// History of DOM snapshots (for trend analysis)
        /// Keeps last N snapshots for detecting changes
        /// </summary>
        private readonly Queue<DomSnapshot> _domHistory = new(100);

        /// <summary>
        /// Updates received since last analysis
        /// </summary>
        private int _updateCount = 0;

        /// <summary>
        /// Timestamp of last DOM update
        /// </summary>
        private DateTime _lastUpdateTime = DateTime.UtcNow;

        /// <summary>
        /// Maximum depth to track (number of bid/ask levels)
        /// </summary>
        public int MaxDepth { get; set; } = 20;

        /// <summary>
        /// Maximum history size
        /// </summary>
        public int MaxHistory { get; set; } = 100;

        /// <summary>
        /// Data freshness threshold (seconds)
        /// If no update in this many seconds, consider data stale
        /// </summary>
        public double FreshnesThresholdSeconds { get; set; } = 1.0;

        /// <summary>
        /// Gets the current DOM snapshot
        /// Returns null if no data received yet
        /// </summary>
        public DomSnapshot? CurrentSnapshot => _currentSnapshot;

        /// <summary>
        /// Gets the previous DOM snapshot (for change detection)
        /// </summary>
        public DomSnapshot? PreviousSnapshot => _previousSnapshot;

        /// <summary>
        /// Gets current update count
        /// </summary>
        public int UpdateCount => _updateCount;

        /// <summary>
        /// Gets whether data is fresh (received recently)
        /// </summary>
        public bool IsFresh
        {
            get
            {
                if (_currentSnapshot == null)
                    return false;

                double secondsSinceUpdate = (DateTime.UtcNow - _lastUpdateTime).TotalSeconds;
                return secondsSinceUpdate <= FreshnesThresholdSeconds;
            }
        }

        public DomDataFeed()
        {
        }

        /// <summary>
        /// Update DOM data from NinjaTrader Level 2 event
        /// Called from OnLevel2Update() in strategy
        /// </summary>
        public void UpdateFromNinjaTrader(DateTime timestamp, double bidPrice, double askPrice, 
                                        long bidVolume, long askVolume, 
                                        List<(double price, long volume)>? bidLevels = null,
                                        List<(double price, long volume)>? askLevels = null)
        {
            _previousSnapshot = _currentSnapshot;

            _currentSnapshot = new DomSnapshot(timestamp, bidPrice, askPrice, bidVolume, askVolume)
            {
                BidLevels = bidLevels ?? new(),
                AskLevels = askLevels ?? new()
            };

            _updateCount++;
            _lastUpdateTime = DateTime.UtcNow;

            // Keep history
            _domHistory.Enqueue(_currentSnapshot);
            while (_domHistory.Count > MaxHistory)
                _domHistory.Dequeue();
        }

        /// <summary>
        /// Get the latest N DOM snapshots for analysis
        /// </summary>
        public List<DomSnapshot> GetRecentSnapshots(int count)
        {
            if (_domHistory.Count == 0)
                return new();

            return _domHistory.TakeLast(Math.Min(count, _domHistory.Count)).ToList();
        }

        /// <summary>
        /// Detect if order book is being "pulled" (liquidity withdrawn)
        /// Indicates potential rejection or reversal
        /// Returns: (withdrew, severity 0-1)
        /// </summary>
        public (bool withdrew, double severity) DetectOrderBookPull()
        {
            if (_currentSnapshot == null || _previousSnapshot == null)
                return (false, 0.0);

            long currentBidVol = _currentSnapshot.BidVolume;
            long previousBidVol = _previousSnapshot.BidVolume;
            long currentAskVol = _currentSnapshot.AskVolume;
            long previousAskVol = _previousSnapshot.AskVolume;

            // Check if volume decreased significantly on both sides
            double bidVolumeChange = previousBidVol > 0 ? (double)(previousBidVol - currentBidVol) / previousBidVol : 0.0;
            double askVolumeChange = previousAskVol > 0 ? (double)(previousAskVol - currentAskVol) / previousAskVol : 0.0;

            bool withdrew = bidVolumeChange > 0.3 || askVolumeChange > 0.3; // >30% reduction
            double severity = Math.Max(bidVolumeChange, askVolumeChange);

            return (withdrew, severity);
        }

        /// <summary>
        /// Detect if order book is being "stacked" (large volume added)
        /// Indicates institutional interest at a level
        /// Returns: (onBid, onAsk)
        /// </summary>
        public (bool onBid, bool onAsk) DetectOrderBookStacking()
        {
            if (_currentSnapshot == null || _previousSnapshot == null)
                return (false, false);

            long currentBidVol = _currentSnapshot.BidVolume;
            long previousBidVol = _previousSnapshot.BidVolume;
            long currentAskVol = _currentSnapshot.AskVolume;
            long previousAskVol = _previousSnapshot.AskVolume;

            // Check if volume increased significantly
            double bidVolumeIncrease = previousBidVol > 0 ? (double)(currentBidVol - previousBidVol) / previousBidVol : 0.0;
            double askVolumeIncrease = previousAskVol > 0 ? (double)(currentAskVol - previousAskVol) / previousAskVol : 0.0;

            bool stackedBid = bidVolumeIncrease > 0.5; // >50% increase
            bool stackedAsk = askVolumeIncrease > 0.5; // >50% increase

            return (stackedBid, stackedAsk);
        }

        /// <summary>
        /// Get bid/ask imbalance as a continuous score (-100 to +100)
        /// Negative = buying pressure, Positive = selling pressure
        /// </summary>
        public double GetImbalanceScore()
        {
            return _currentSnapshot?.GetImbalanceScore() ?? 0.0;
        }

        /// <summary>
        /// Get data quality score (0.0 to 1.0)
        /// Factors: freshness, depth, volume consistency
        /// </summary>
        public double GetDataQualityScore()
        {
            if (_currentSnapshot == null)
                return 0.0;

            double qualityScore = 1.0;

            // Freshness factor
            double secondsSinceUpdate = (DateTime.UtcNow - _lastUpdateTime).TotalSeconds;
            if (secondsSinceUpdate > FreshnesThresholdSeconds)
                qualityScore *= 0.5;

            // Depth factor (deeper is better)
            int avgDepth = (_currentSnapshot.BidLevels.Count + _currentSnapshot.AskLevels.Count) / 2;
            qualityScore *= Math.Min(1.0, avgDepth / 20.0); // 20 levels = perfect depth

            // Spread factor (tight is better)
            double maxSpread = 0.1; // Max acceptable spread
            if (_currentSnapshot.Spread > 0)
                qualityScore *= Math.Max(0.1, 1.0 - (_currentSnapshot.Spread / maxSpread));

            return Math.Clamp(qualityScore, 0.0, 1.0);
        }

        /// <summary>
        /// Clear all data (for new session or test reset)
        /// </summary>
        public void Reset()
        {
            _currentSnapshot = null;
            _previousSnapshot = null;
            _domHistory.Clear();
            _updateCount = 0;
            _lastUpdateTime = DateTime.UtcNow;
        }

        public override string ToString()
        {
            if (_currentSnapshot == null)
                return "No DOM data received";

            return $"DOM: {_currentSnapshot} | Updates: {_updateCount} | Fresh: {IsFresh}";
        }
    }
}
