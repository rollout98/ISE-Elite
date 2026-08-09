namespace ISE.UnifiedRegimeEngine.Models
{
    /// <summary>
    /// Market regime classification
    /// Determined by ADX level: Trending (ADX high) vs Ranging (ADX low)
    /// </summary>
    public enum RegimeState
    {
        /// <summary>
        /// Indeterminate regime - ADX transitioning or insufficient data
        /// No trades initiated
        /// </summary>
        Indeterminate = 0,

        /// <summary>
        /// Trending regime (ADX > threshold)
        /// Use momentum/breakout entry logic
        /// Longer hold times (30 min)
        /// Higher profit targets
        /// </summary>
        Trending = 1,

        /// <summary>
        /// Ranging regime (ADX < threshold)
        /// Use support/resistance scalp logic
        /// Shorter hold times (3 min)
        /// Lower profit targets
        /// </summary>
        Ranging = 2
    }
}
