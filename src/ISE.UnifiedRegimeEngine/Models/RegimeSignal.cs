namespace ISE.UnifiedRegimeEngine.Models
{
    /// <summary>
    /// Complete market regime signal
    /// Contains all calculated indicators + regime classification + confidence
    /// Output from UnifiedMarketRegimeEngine
    /// </summary>
    public class RegimeSignal
    {
        /// <summary>
        /// Timestamp of the bar this signal represents
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Market regime: TRENDING, RANGING, or INDETERMINATE
        /// </summary>
        public RegimeState Regime { get; set; }

        // --- ADX (Average Directional Index) ---
        /// <summary>
        /// ADX value (14-period smoothed)
        /// Range: 0-100
        /// Threshold: typically 25-30 for trend confirmation
        /// </summary>
        public double Adx { get; set; }

        /// <summary>
        /// Directional Indicator Plus (+DI)
        /// Uptrend strength indicator
        /// Range: 0-100
        /// </summary>
        public double DiPlus { get; set; }

        /// <summary>
        /// Directional Indicator Minus (-DI)
        /// Downtrend strength indicator
        /// Range: 0-100
        /// </summary>
        public double DiMinus { get; set; }

        // --- RSI (Relative Strength Index) ---
        /// <summary>
        /// RSI value (14-period)
        /// Range: 0-100
        /// > 70: overbought, < 30: oversold (typical thresholds)
        /// </summary>
        public double Rsi { get; set; }

        /// <summary>
        /// True for overbought condition (RSI > overbought threshold, default 70)
        /// </summary>
        public bool RsiOverbought { get; set; }

        /// <summary>
        /// True for oversold condition (RSI < oversold threshold, default 30)
        /// </summary>
        public bool RsiOversold { get; set; }

        // --- MACD (Moving Average Convergence Divergence) ---
        /// <summary>
        /// MACD line (12-period EMA minus 26-period EMA)
        /// </summary>
        public double MacdLine { get; set; }

        /// <summary>
        /// Signal line (9-period EMA of MACD)
        /// </summary>
        public double MacdSignal { get; set; }

        /// <summary>
        /// MACD histogram (MACD line - Signal line)
        /// Positive: uptrend, Negative: downtrend
        /// Crossing zero is potential reversal
        /// </summary>
        public double MacdHistogram { get; set; }

        /// <summary>
        /// True if MACD crossed above signal line (bullish)
        /// </summary>
        public bool MacdBullishCross { get; set; }

        /// <summary>
        /// True if MACD crossed below signal line (bearish)
        /// </summary>
        public bool MacdBearishCross { get; set; }

        // --- ATR (Average True Range) ---
        /// <summary>
        /// ATR value (14-period)
        /// Volatility measure
        /// </summary>
        public double Atr { get; set; }

        /// <summary>
        /// ATR as percentage of current close
        /// Used for position sizing and stop placement
        /// </summary>
        public double AtrPercent { get; set; }

        // --- Confidence & Quality ---
        /// <summary>
        /// Confidence in regime classification (0.0 to 1.0)
        /// 1.0 = very confident (clear trend or range)
        /// 0.0 = no confidence (uncertain)
        /// </summary>
        public double RegimeConfidence { get; set; }

        /// <summary>
        /// Number of bars processed so far (for warm-up detection)
        /// Confidence should be low if BarCount < 30-50
        /// </summary>
        public int BarCount { get; set; }

        /// <summary>
        /// True if regime signal is still warming up (insufficient data)
        /// Typically BarCount < 50
        /// </summary>
        public bool IsWarmingUp { get; set; }

        // --- Derived Signals ---
        /// <summary>
        /// True if conditions favor long entry (rising DI+, ADX > threshold, no overbought)
        /// </summary>
        public bool LongBiasDi { get; set; }

        /// <summary>
        /// True if conditions favor short entry (rising DI-, ADX > threshold, no oversold)
        /// </summary>
        public bool ShortBiasDi { get; set; }

        public RegimeSignal()
        {
            Timestamp = DateTime.UtcNow;
            Regime = RegimeState.Indeterminate;
            RegimeConfidence = 0.0;
            BarCount = 0;
            IsWarmingUp = true;
        }

        public override string ToString()
        {
            return $"{Timestamp:HH:mm:ss} | Regime: {Regime} | ADX: {Adx:F1} | RSI: {Rsi:F1} | MACD: {MacdHistogram:F2} | ATR: {Atr:F2}";
        }
    }
}
