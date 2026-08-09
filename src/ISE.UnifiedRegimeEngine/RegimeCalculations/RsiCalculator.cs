using ISE.UnifiedRegimeEngine.Models;

namespace ISE.UnifiedRegimeEngine.RegimeCalculations
{
    /// <summary>
    /// RSI (Relative Strength Index) Calculator
    /// Measures momentum: > 70 overbought, < 30 oversold
    /// Uses 14-period smoothing (standard)
    /// </summary>
    public class RsiCalculator
    {
        private const int Period = 14;
        private const double MinimumValidRsi = 0.0;
        private const double MaximumValidRsi = 100.0;
        private const double DefaultOverboughtThreshold = 70.0;
        private const double DefaultOversoldThreshold = 30.0;

        /// <summary>
        /// Close price buffer (for gain/loss calculation)
        /// </summary>
        private readonly Queue<double> _closeBuffer = new(Period + 5);

        /// <summary>
        /// Gains buffer (upward moves)
        /// </summary>
        private readonly Queue<double> _gainsBuffer = new(Period + 5);

        /// <summary>
        /// Losses buffer (downward moves)
        /// </summary>
        private readonly Queue<double> _lossesBuffer = new(Period + 5);

        /// <summary>
        /// Average gain (smoothed using Wilder's method)
        /// </summary>
        private double _averageGain = 0.0;

        /// <summary>
        /// Average loss (smoothed using Wilder's method)
        /// </summary>
        private double _averageLoss = 0.0;

        /// <summary>
        /// Current RSI value
        /// </summary>
        private double _rsi = 50.0;

        /// <summary>
        /// Overbought threshold (default 70, adjustable per instrument)
        /// </summary>
        public double OverboughtThreshold { get; set; } = DefaultOverboughtThreshold;

        /// <summary>
        /// Oversold threshold (default 30, adjustable per instrument)
        /// </summary>
        public double OversoldThreshold { get; set; } = DefaultOversoldThreshold;

        /// <summary>
        /// Total bars processed
        /// </summary>
        private int _barCount = 0;

        public int BarCount => _barCount;
        public bool IsReady => _barCount >= Period * 2; // Need 2x period for reliable RSI

        public RsiCalculator()
        {
        }

        /// <summary>
        /// Calculate RSI for current bar
        /// Returns: (RSI value, is overbought, is oversold)
        /// </summary>
        public (double rsi, bool overbought, bool oversold) Calculate(RegimeInput bar)
        {
            if (!bar.IsValid())
                throw new ArgumentException("Invalid bar data");

            _barCount++;
            _closeBuffer.Enqueue(bar.Close);

            double gain = 0.0;
            double loss = 0.0;

            if (_barCount > 1)
            {
                double priorClose = _closeBuffer.Count > 1 
                    ? _closeBuffer.ElementAt(_closeBuffer.Count - 2) 
                    : bar.Close;

                double change = bar.Close - priorClose;

                if (change > 0)
                    gain = change;
                else
                    loss = Math.Abs(change);
            }

            _gainsBuffer.Enqueue(gain);
            _lossesBuffer.Enqueue(loss);

            // Calculate average gain and loss using Wilder's smoothing
            if (_barCount <= Period)
            {
                // Initialization period: simple average
                var gains = _gainsBuffer.TakeLast(_barCount).ToList();
                var losses = _lossesBuffer.TakeLast(_barCount).ToList();

                _averageGain = gains.Count > 0 ? gains.Sum() / gains.Count : 0.0;
                _averageLoss = losses.Count > 0 ? losses.Sum() / losses.Count : 0.0;
            }
            else
            {
                // Wilder's smoothing: (previous average * (period-1) + current value) / period
                var recentGains = _gainsBuffer.TakeLast(Period).ToList();
                var recentLosses = _lossesBuffer.TakeLast(Period).ToList();

                _averageGain = (_averageGain * (Period - 1) + recentGains.Last()) / Period;
                _averageLoss = (_averageLoss * (Period - 1) + recentLosses.Last()) / Period;
            }

            // Calculate RSI
            if (_averageLoss == 0)
            {
                _rsi = _averageGain > 0 ? 100.0 : 50.0;
            }
            else
            {
                double rs = _averageGain / _averageLoss;
                _rsi = 100.0 - (100.0 / (1.0 + rs));
            }

            // Clamp to valid range
            _rsi = Math.Clamp(_rsi, MinimumValidRsi, MaximumValidRsi);

            // Determine overbought/oversold conditions
            bool overbought = _rsi > OverboughtThreshold;
            bool oversold = _rsi < OversoldThreshold;

            return (_rsi, overbought, oversold);
        }

        /// <summary>
        /// Reset calculator state (for testing or new session)
        /// </summary>
        public void Reset()
        {
            _closeBuffer.Clear();
            _gainsBuffer.Clear();
            _lossesBuffer.Clear();
            _averageGain = 0.0;
            _averageLoss = 0.0;
            _rsi = 50.0;
            _barCount = 0;
        }

        /// <summary>
        /// Get current RSI (without processing new bar)
        /// </summary>
        public double GetRsi() => _rsi;

        public bool IsOverbought() => _rsi > OverboughtThreshold;
        public bool IsOversold() => _rsi < OversoldThreshold;
    }
}
