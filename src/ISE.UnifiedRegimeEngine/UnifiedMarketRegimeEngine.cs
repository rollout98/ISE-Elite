using ISE.UnifiedRegimeEngine.Models;
using ISE.UnifiedRegimeEngine.RegimeCalculations;

namespace ISE.UnifiedRegimeEngine
{
    /// <summary>
    /// Unified Market Regime Engine
    /// Orchestrates all indicator calculators (ADX, RSI, MACD, ATR)
    /// Produces comprehensive market regime signal with confidence
    /// 
    /// Regime Classification:
    /// - TRENDING: ADX > threshold (strong directional market)
    /// - RANGING: ADX < threshold (consolidation/sideways)
    /// - INDETERMINATE: insufficient data or transition
    /// </summary>
    public class UnifiedMarketRegimeEngine
    {
        // Calculators (one instance each)
        private readonly AdxCalculator _adxCalculator = new();
        private readonly RsiCalculator _rsiCalculator = new();
        private readonly MacdCalculator _macdCalculator = new();
        private readonly AtrCalculator _atrCalculator = new();

        // Configuration (adjustable per instrument)
        public double AdxTrendThreshold { get; set; } = 25.0; // NQ: 25, GC: 20
        public double RsiOverboughtThreshold { get; set; } = 70.0; // NQ: 70, GC: 75
        public double RsiOversoldThreshold { get; set; } = 30.0; // NQ: 30, GC: 25

        // Warm-up configuration
        private const int MinimumBarsForConfidence = 50; // Need 50 bars before high confidence
        private const int MinimumBarsForEntry = 30; // Can start considering entries after 30 bars

        // State tracking
        private int _totalBarsProcessed = 0;
        private RegimeSignal? _lastSignal;

        // Regime transition tracking
        private RegimeState _previousRegime = RegimeState.Indeterminate;
        private int _regimeChangeCount = 0;

        public int TotalBarsProcessed => _totalBarsProcessed;
        public RegimeState CurrentRegime => _lastSignal?.Regime ?? RegimeState.Indeterminate;
        public double CurrentConfidence => _lastSignal?.RegimeConfidence ?? 0.0;

        public UnifiedMarketRegimeEngine()
        {
            // Thresholds can be adjusted after construction
        }

        /// <summary>
        /// Configure thresholds for specific instrument
        /// </summary>
        public void ConfigureForInstrument(string symbol)
        {
            switch (symbol.ToUpper())
            {
                case "NQ":
                case "MNQ":
                    AdxTrendThreshold = 25.0;
                    RsiOverboughtThreshold = 70.0;
                    RsiOversoldThreshold = 30.0;
                    break;

                case "GC":
                case "MGC":
                    AdxTrendThreshold = 20.0;
                    RsiOverboughtThreshold = 75.0;
                    RsiOversoldThreshold = 25.0;
                    break;

                default:
                    // Use defaults
                    break;
            }
        }

        /// <summary>
        /// Calculate regime signal for current bar
        /// This is the main entry point for each bar update
        /// </summary>
        public RegimeSignal CalculateRegimeSignal(RegimeInput bar)
        {
            if (!bar.IsValid())
                throw new ArgumentException("Invalid bar data", nameof(bar));

            _totalBarsProcessed++;

            // Calculate all indicators
            var (adx, diPlus, diMinus) = _adxCalculator.Calculate(bar);
            var (rsi, rsiOverbought, rsiOversold) = _rsiCalculator.Calculate(bar);
            var (macdLine, signalLine, histogram, bullishCross, bearishCross) = _macdCalculator.Calculate(bar);
            var (atr, atrPercent) = _atrCalculator.Calculate(bar);

            // Create output signal
            var signal = new RegimeSignal
            {
                Timestamp = bar.Timestamp,
                BarCount = _totalBarsProcessed,

                // ADX values
                Adx = adx,
                DiPlus = diPlus,
                DiMinus = diMinus,

                // RSI values
                Rsi = rsi,
                RsiOverbought = rsiOverbought,
                RsiOversold = rsiOversold,

                // MACD values
                MacdLine = macdLine,
                MacdSignal = signalLine,
                MacdHistogram = histogram,
                MacdBullishCross = bullishCross,
                MacdBearishCross = bearishCross,

                // ATR values
                Atr = atr,
                AtrPercent = atrPercent,

                // Warm-up detection
                IsWarmingUp = _totalBarsProcessed < MinimumBarsForConfidence
            };

            // Classify regime based on ADX
            ClassifyRegime(signal);

            // Calculate directional bias (DI+ vs DI-)
            CalculateDirectionalBias(signal);

            // Calculate confidence
            CalculateConfidence(signal);

            _previousRegime = signal.Regime;
            _lastSignal = signal;

            return signal;
        }

        /// <summary>
        /// Classify market regime based on ADX level
        /// TRENDING: ADX > threshold (strong trend)
        /// RANGING: ADX < threshold (consolidation)
        /// INDETERMINATE: warming up or on threshold boundary
        /// </summary>
        private void ClassifyRegime(RegimeSignal signal)
        {
            // Insufficient data = indeterminate
            if (signal.IsWarmingUp)
            {
                signal.Regime = RegimeState.Indeterminate;
                return;
            }

            // ADX thresholds determine regime
            double adxThreshold = AdxTrendThreshold;

            if (signal.Adx > adxThreshold)
            {
                signal.Regime = RegimeState.Trending;
            }
            else if (signal.Adx < adxThreshold - 5.0) // 5-point hysteresis to avoid whipsaws
            {
                signal.Regime = RegimeState.Ranging;
            }
            else
            {
                // In the hysteresis band (threshold ±5)
                // Stay with previous regime if available
                signal.Regime = _previousRegime != RegimeState.Indeterminate 
                    ? _previousRegime 
                    : RegimeState.Indeterminate;
            }

            // Track regime changes
            if (signal.Regime != _previousRegime && _previousRegime != RegimeState.Indeterminate)
            {
                _regimeChangeCount++;
            }
        }

        /// <summary>
        /// Calculate directional bias from DI+ vs DI-
        /// LongBias: DI+ > DI- (bulls have advantage)
        /// ShortBias: DI- > DI+ (bears have advantage)
        /// </summary>
        private void CalculateDirectionalBias(RegimeSignal signal)
        {
            // Only calculate bias when trending (ADX high)
            if (signal.Regime != RegimeState.Trending)
            {
                signal.LongBiasDi = false;
                signal.ShortBiasDi = false;
                return;
            }

            double diDifference = signal.DiPlus - signal.DiMinus;

            // Strong long bias: DI+ rising and above DI-
            signal.LongBiasDi = signal.DiPlus > signal.DiMinus && signal.DiPlus > 20.0;

            // Strong short bias: DI- rising and above DI+
            signal.ShortBiasDi = signal.DiMinus > signal.DiPlus && signal.DiMinus > 20.0;
        }

        /// <summary>
        /// Calculate confidence in regime classification (0.0 to 1.0)
        /// Factors: ADX distance from threshold, data freshness, indicator agreement
        /// </summary>
        private void CalculateConfidence(RegimeSignal signal)
        {
            if (signal.IsWarmingUp)
            {
                // Gradually increase confidence during warm-up
                double warmupPercent = (double)signal.BarCount / MinimumBarsForConfidence;
                signal.RegimeConfidence = Math.Min(0.5, warmupPercent * 0.5); // Max 50% during warm-up
                return;
            }

            double confidence = 0.0;

            // Factor 1: ADX distance from threshold (0-50 points)
            double adxThreshold = AdxTrendThreshold;
            double adxDistance = Math.Abs(signal.Adx - adxThreshold);
            double adxConfidence = Math.Min(1.0, adxDistance / 30.0); // Max confidence at 30+ points away
            confidence += adxConfidence * 0.5; // ADX is 50% of confidence

            // Factor 2: DI convergence (how separated are DI+ and DI-)
            double diDifference = Math.Abs(signal.DiPlus - signal.DiMinus);
            double diConfidence = Math.Min(1.0, diDifference / 30.0);
            confidence += diConfidence * 0.2; // DI is 20% of confidence

            // Factor 3: MACD alignment with regime
            // Bullish histogram aligns with long bias
            // Bearish histogram aligns with short bias
            bool macdAligned = (signal.MacdHistogram > 0 && signal.LongBiasDi) ||
                               (signal.MacdHistogram < 0 && signal.ShortBiasDi) ||
                               (Math.Abs(signal.MacdHistogram) < 0.01); // Neutral is okay

            double macdConfidence = macdAligned ? 1.0 : 0.5;
            confidence += macdConfidence * 0.2; // MACD is 20% of confidence

            // Factor 4: RSI not at extremes (prevents entries at tops/bottoms)
            bool rsiExtreme = signal.RsiOverbought || signal.RsiOversold;
            double rsiConfidence = rsiExtreme ? 0.7 : 1.0;
            confidence += rsiConfidence * 0.1; // RSI is 10% of confidence

            signal.RegimeConfidence = Math.Clamp(confidence / 1.0, 0.0, 1.0);
        }

        /// <summary>
        /// Check if current regime is reliable for entry
        /// </summary>
        public bool IsRegimeReliableForEntry()
        {
            if (_lastSignal == null)
                return false;

            // Not warming up + sufficient confidence + not at extremes
            return !_lastSignal.IsWarmingUp && 
                   _lastSignal.RegimeConfidence >= 0.6 &&
                   _totalBarsProcessed >= MinimumBarsForEntry;
        }

        /// <summary>
        /// Get entry recommendation based on current regime + indicators
        /// Returns: (canEnterLong, canEnterShort, confidence 0-1)
        /// </summary>
        public (bool canLong, bool canShort, double confidence) GetEntryRecommendation()
        {
            if (_lastSignal == null || !IsRegimeReliableForEntry())
                return (false, false, 0.0);

            var signal = _lastSignal;

            bool canLong = false;
            bool canShort = false;
            double confidence = signal.RegimeConfidence;

            if (signal.Regime == RegimeState.Trending)
            {
                // TRENDING: Use DI bias for direction
                canLong = signal.LongBiasDi && !signal.RsiOverbought;
                canShort = signal.ShortBiasDi && !signal.RsiOversold;
            }
            else if (signal.Regime == RegimeState.Ranging)
            {
                // RANGING: Use RSI extremes for mean reversion
                canLong = signal.RsiOversold; // Oversold = potential buy
                canShort = signal.RsiOverbought; // Overbought = potential sell
            }

            // Require MACD alignment for higher confidence
            bool macdAligned = (signal.MacdHistogram > 0 && canLong) ||
                               (signal.MacdHistogram < 0 && canShort);

            if (macdAligned)
                confidence *= 1.1; // Boost confidence if aligned
            else if (canLong || canShort)
                confidence *= 0.7; // Reduce confidence if not aligned

            confidence = Math.Clamp(confidence, 0.0, 1.0);

            return (canLong, canShort, confidence);
        }

        /// <summary>
        /// Get the last calculated signal (without recalculating)
        /// </summary>
        public RegimeSignal? GetLastSignal()
        {
            return _lastSignal;
        }

        /// <summary>
        /// Detailed regime analysis for logging/reporting
        /// </summary>
        public string GetRegimeAnalysis()
        {
            if (_lastSignal == null)
                return "No regime calculated yet";

            var sig = _lastSignal;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"=== Regime Analysis ({sig.Timestamp:HH:mm:ss}) ===");
            sb.AppendLine($"Regime: {sig.Regime} | Confidence: {sig.RegimeConfidence:P0}");
            sb.AppendLine($"ADX: {sig.Adx:F1} (threshold: {AdxTrendThreshold}) | DI+: {sig.DiPlus:F1} | DI-: {sig.DiMinus:F1}");
            sb.AppendLine($"RSI: {sig.Rsi:F1} (OB: {(sig.RsiOverbought ? "Y" : "N")} | OS: {(sig.RsiOversold ? "Y" : "N")})");
            sb.AppendLine($"MACD: {sig.MacdHistogram:F3} | Signal: {sig.MacdSignal:F3} | Bullish: {(sig.MacdBullishCross ? "Y" : "N")} | Bearish: {(sig.MacdBearishCross ? "Y" : "N")}");
            sb.AppendLine($"ATR: {sig.Atr:F2} ({sig.AtrPercent:F2}%)");
            sb.AppendLine($"Directional Bias - Long: {(sig.LongBiasDi ? "Y" : "N")} | Short: {(sig.ShortBiasDi ? "Y" : "N")}");
            sb.AppendLine($"Bars: {sig.BarCount} | Warming Up: {sig.IsWarmingUp}");

            var (canLong, canShort, entryConf) = GetEntryRecommendation();
            sb.AppendLine($"Entry Recommendation - Long: {(canLong ? "Y" : "N")} | Short: {(canShort ? "Y" : "N")} (conf: {entryConf:P0})");

            return sb.ToString();
        }

        /// <summary>
        /// Reset engine for new session
        /// </summary>
        public void Reset()
        {
            _adxCalculator.Reset();
            _rsiCalculator.Reset();
            _macdCalculator.Reset();
            _atrCalculator.Reset();
            _totalBarsProcessed = 0;
            _lastSignal = null;
            _previousRegime = RegimeState.Indeterminate;
            _regimeChangeCount = 0;
        }

        public override string ToString()
        {
            if (_lastSignal == null)
                return "Regime Engine: Not initialized";

            return $"Regime: {_lastSignal.Regime} | ADX: {_lastSignal.Adx:F1} | RSI: {_lastSignal.Rsi:F1} | Confidence: {_lastSignal.RegimeConfidence:P0}";
        }
    }
}
