using ISE.OrderFlowIntelligence.Models;

namespace ISE.OrderFlowIntelligence
{
    /// <summary>
    /// Rejection Detector
    /// Identifies when price is rejected at order book levels
    /// Rejection indicates institutional resistance/support is preventing price movement
    /// Used for early exit signals (price is being defended)
    /// </summary>
    public class RejectionDetector
    {
        /// <summary>
        /// Recent price action history (for wick detection)
        /// </summary>
        private readonly Queue<(double high, double low, double close)> _priceHistory = new(5);

        /// <summary>
        /// Current rejection status
        /// </summary>
        public RejectionState CurrentRejection { get; private set; } = RejectionState.None;

        /// <summary>
        /// Price level where rejection is occurring
        /// </summary>
        public double RejectionLevel { get; private set; } = 0.0;

        /// <summary>
        /// Rejection strength (0.0 to 1.0)
        /// 1.0 = strong rejection, 0.0 = no rejection
        /// </summary>
        public double RejectionStrength { get; private set; } = 0.0;

        /// <summary>
        /// Number of bars since rejection started
        /// </summary>
        public int RejectionBars { get; private set; } = 0;

        /// <summary>
        /// Minimum wick size to consider as rejection (in price units)
        /// </summary>
        public double MinimumRejectionWickSize { get; set; } = 0.05;

        /// <summary>
        /// Maximum price continuation before rejection ends (as % of wick)
        /// </summary>
        public double MaximumContinuationPercent { get; set; } = 0.3; // 30% of wick

        public RejectionDetector()
        {
        }

        /// <summary>
        /// Detect rejection at resistance or support
        /// Call this on each bar with price and DOM data
        /// </summary>
        public void Detect(double currentHigh, double currentLow, double currentClose, 
                          double resistanceLevel, double supportLevel, DomSnapshot domSnapshot)
        {
            _priceHistory.Enqueue((currentHigh, currentLow, currentClose));

            // Keep history limited
            while (_priceHistory.Count > 5)
                _priceHistory.Dequeue();

            // Check for rejection at resistance (price wicked up and rejected down)
            if (DetectResistanceRejection(currentHigh, currentLow, currentClose, resistanceLevel, domSnapshot))
            {
                CurrentRejection = RejectionState.AtResistance;
                RejectionLevel = resistanceLevel;
                RejectionBars++;
                return;
            }

            // Check for rejection at support (price wicked down and rejected up)
            if (DetectSupportRejection(currentHigh, currentLow, currentClose, supportLevel, domSnapshot))
            {
                CurrentRejection = RejectionState.AtSupport;
                RejectionLevel = supportLevel;
                RejectionBars++;
                return;
            }

            // No rejection detected
            if (RejectionBars > 0)
            {
                RejectionBars = 0; // Reset counter
            }
            CurrentRejection = RejectionState.None;
            RejectionLevel = 0.0;
            RejectionStrength = 0.0;
        }

        /// <summary>
        /// Detect rejection at resistance level
        /// Price touches resistance and falls back down
        /// </summary>
        private bool DetectResistanceRejection(double high, double low, double close,
                                               double resistance, DomSnapshot dom)
        {
            if (resistance == 0 || dom == null)
                return false;

            // Wick above resistance
            double wickAboveResistance = high - resistance;

            // Price rejected down (close well below the high wick)
            double wickSize = high - low;

            // No significant wick = no rejection
            if (wickAboveResistance < MinimumRejectionWickSize)
                return false;

            // Check if sellers are defending (high volume at ask near resistance)
            bool strongSellers = dom.AskVolume > dom.BidVolume * 1.5;

            // Price must close significantly below the high (not continuing up through resistance)
            bool priceRejected = (high - close) > (wickSize * MaximumContinuationPercent);

            if (wickAboveResistance > 0 && strongSellers && priceRejected)
            {
                RejectionStrength = Math.Min(1.0, wickAboveResistance / MinimumRejectionWickSize);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Detect rejection at support level
        /// Price touches support and bounces back up
        /// </summary>
        private bool DetectSupportRejection(double high, double low, double close,
                                            double support, DomSnapshot dom)
        {
            if (support == 0 || dom == null)
                return false;

            // Wick below support
            double wickBelowSupport = support - low;

            // Price rejected up (close well above the low wick)
            double wickSize = high - low;

            // No significant wick = no rejection
            if (wickBelowSupport < MinimumRejectionWickSize)
                return false;

            // Check if buyers are defending (high volume at bid near support)
            bool strongBuyers = dom.BidVolume > dom.AskVolume * 1.5;

            // Price must close significantly above the low (not continuing down through support)
            bool priceRejected = (close - low) > (wickSize * MaximumContinuationPercent);

            if (wickBelowSupport > 0 && strongBuyers && priceRejected)
            {
                RejectionStrength = Math.Min(1.0, wickBelowSupport / MinimumRejectionWickSize);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Is rejection still active? (hasn't expired)
        /// Rejection expires if price moves beyond level again
        /// </summary>
        public bool IsRejectionActive(double currentPrice)
        {
            if (CurrentRejection == RejectionState.None)
                return false;

            if (RejectionBars > 10) // Reject if too old
                return false;

            // Check if price has moved back past rejection level (expired)
            if (CurrentRejection == RejectionState.AtResistance && currentPrice > RejectionLevel)
                return false;

            if (CurrentRejection == RejectionState.AtSupport && currentPrice < RejectionLevel)
                return false;

            return true;
        }

        /// <summary>
        /// Get the exit signal (when rejection ends)
        /// Used for early exit detection
        /// </summary>
        public bool ShouldExitDueToRejection(double currentPrice)
        {
            return !IsRejectionActive(currentPrice) && CurrentRejection != RejectionState.None;
        }

        /// <summary>
        /// Reset detector (for new session)
        /// </summary>
        public void Reset()
        {
            _priceHistory.Clear();
            CurrentRejection = RejectionState.None;
            RejectionLevel = 0.0;
            RejectionStrength = 0.0;
            RejectionBars = 0;
        }

        public override string ToString()
        {
            if (CurrentRejection == RejectionState.None)
                return "No rejection detected";

            return $"Rejection: {CurrentRejection} at {RejectionLevel:F2} (strength: {RejectionStrength:F2}, bars: {RejectionBars})";
        }
    }

    /// <summary>
    /// Rejection state enumeration
    /// </summary>
    public enum RejectionState
    {
        /// <summary>
        /// No rejection detected
        /// </summary>
        None = 0,

        /// <summary>
        /// Price rejected at support (buyers defending)
        /// </summary>
        AtSupport = 1,

        /// <summary>
        /// Price rejected at resistance (sellers defending)
        /// </summary>
        AtResistance = 2
    }
}
