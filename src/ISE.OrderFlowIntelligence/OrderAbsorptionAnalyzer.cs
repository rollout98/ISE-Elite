using ISE.OrderFlowIntelligence.Models;

namespace ISE.OrderFlowIntelligence
{
    /// <summary>
    /// Order Absorption Analyzer
    /// Detects when order book is being "absorbed" by institutional buyers/sellers
    /// Absorption indicates significant institutional activity and order execution
    /// </summary>
    public class OrderAbsorptionAnalyzer
    {
        /// <summary>
        /// Window size for absorption detection (last N DOM snapshots)
        /// </summary>
        private const int AbsorptionWindowSize = 3;

        /// <summary>
        /// Volume history (for detecting volume absorption)
        /// </summary>
        private readonly Queue<DomSnapshot> _volumeHistory = new(AbsorptionWindowSize + 2);

        /// <summary>
        /// Current absorption state: buying, selling, or neutral
        /// </summary>
        public AbsorptionState CurrentAbsorption { get; private set; } = AbsorptionState.Neutral;

        /// <summary>
        /// Absorption strength (0.0 to 1.0)
        /// 1.0 = maximum institutional presence detected
        /// </summary>
        public double AbsorptionStrength { get; private set; } = 0.0;

        /// <summary>
        /// Previous absorption state (for change detection)
        /// </summary>
        public AbsorptionState PreviousAbsorption { get; private set; } = AbsorptionState.Neutral;

        /// <summary>
        /// Minimum volume increase to trigger absorption detection
        /// </summary>
        public double MinimumVolumeIncreaseFactor { get; set; } = 0.5; // 50% increase

        /// <summary>
        /// Minimum spread tightness to confirm absorption
        /// </summary>
        public double MaximumSpreadForAbsorption { get; set; } = 0.1;

        public OrderAbsorptionAnalyzer()
        {
        }

        /// <summary>
        /// Analyze DOM snapshot for absorption patterns
        /// </summary>
        public void Analyze(DomSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            _volumeHistory.Enqueue(snapshot);

            // Keep history limited
            while (_volumeHistory.Count > AbsorptionWindowSize)
                _volumeHistory.Dequeue();

            PreviousAbsorption = CurrentAbsorption;

            if (_volumeHistory.Count < 2)
            {
                CurrentAbsorption = AbsorptionState.Neutral;
                AbsorptionStrength = 0.0;
                return;
            }

            // Detect absorption patterns
            DetectBuyingAbsorption();
            if (CurrentAbsorption == AbsorptionState.Neutral)
                DetectSellingAbsorption();
        }

        /// <summary>
        /// Buying absorption: large volume appearing on bid, orders being filled
        /// </summary>
        private void DetectBuyingAbsorption()
        {
            var recent = _volumeHistory.TakeLast(Math.Min(2, _volumeHistory.Count)).ToList();

            if (recent.Count < 2)
                return;

            DomSnapshot prior = recent[0];
            DomSnapshot current = recent[1];

            // Check for increase in bid volume
            double bidVolumeIncrease = prior.BidVolume > 0 
                ? (double)(current.BidVolume - prior.BidVolume) / prior.BidVolume 
                : 0.0;

            // Check if price is stable or rising (absorption at current level)
            bool priceStable = Math.Abs(current.BidPrice - prior.BidPrice) <= MaximumSpreadForAbsorption;

            // Check for tight spread (buyers stepping up)
            bool tightSpread = current.Spread < MaximumSpreadForAbsorption;

            // Buying absorption: volume increases on bid + price holds + tight spread
            if (bidVolumeIncrease > MinimumVolumeIncreaseFactor && priceStable && tightSpread)
            {
                CurrentAbsorption = AbsorptionState.Buying;
                AbsorptionStrength = Math.Min(1.0, bidVolumeIncrease / 2.0); // Normalize to 0-1
                return;
            }

            CurrentAbsorption = AbsorptionState.Neutral;
            AbsorptionStrength = 0.0;
        }

        /// <summary>
        /// Selling absorption: large volume appearing on ask, orders being filled
        /// </summary>
        private void DetectSellingAbsorption()
        {
            var recent = _volumeHistory.TakeLast(Math.Min(2, _volumeHistory.Count)).ToList();

            if (recent.Count < 2)
                return;

            DomSnapshot prior = recent[0];
            DomSnapshot current = recent[1];

            // Check for increase in ask volume
            double askVolumeIncrease = prior.AskVolume > 0 
                ? (double)(current.AskVolume - prior.AskVolume) / prior.AskVolume 
                : 0.0;

            // Check if price is stable or falling (absorption at current level)
            bool priceStable = Math.Abs(current.AskPrice - prior.AskPrice) <= MaximumSpreadForAbsorption;

            // Check for tight spread (sellers stepping down)
            bool tightSpread = current.Spread < MaximumSpreadForAbsorption;

            // Selling absorption: volume increases on ask + price holds + tight spread
            if (askVolumeIncrease > MinimumVolumeIncreaseFactor && priceStable && tightSpread)
            {
                CurrentAbsorption = AbsorptionState.Selling;
                AbsorptionStrength = Math.Min(1.0, askVolumeIncrease / 2.0); // Normalize to 0-1
                return;
            }

            CurrentAbsorption = AbsorptionState.Neutral;
            AbsorptionStrength = 0.0;
        }

        /// <summary>
        /// Check if absorption state changed (transition detected)
        /// </summary>
        public bool AbsorptionChanged()
        {
            return CurrentAbsorption != PreviousAbsorption;
        }

        /// <summary>
        /// Is there active institutional buying?
        /// </summary>
        public bool IsActiveBuying()
        {
            return CurrentAbsorption == AbsorptionState.Buying && AbsorptionStrength > 0.5;
        }

        /// <summary>
        /// Is there active institutional selling?
        /// </summary>
        public bool IsActiveSelling()
        {
            return CurrentAbsorption == AbsorptionState.Selling && AbsorptionStrength > 0.5;
        }

        /// <summary>
        /// Reset analyzer (for new session)
        /// </summary>
        public void Reset()
        {
            _volumeHistory.Clear();
            CurrentAbsorption = AbsorptionState.Neutral;
            PreviousAbsorption = AbsorptionState.Neutral;
            AbsorptionStrength = 0.0;
        }

        public override string ToString()
        {
            return $"Absorption: {CurrentAbsorption} (strength: {AbsorptionStrength:F2})";
        }
    }

    /// <summary>
    /// Order absorption state enumeration
    /// </summary>
    public enum AbsorptionState
    {
        /// <summary>
        /// No absorption detected
        /// </summary>
        Neutral = 0,

        /// <summary>
        /// Institutional buyers absorbing supply
        /// </summary>
        Buying = 1,

        /// <summary>
        /// Institutional sellers absorbing demand
        /// </summary>
        Selling = 2
    }
}
