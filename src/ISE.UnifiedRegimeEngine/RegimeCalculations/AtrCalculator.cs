using ISE.UnifiedRegimeEngine.Models;

namespace ISE.UnifiedRegimeEngine.RegimeCalculations
{
    /// <summary>
    /// ATR (Average True Range) Calculator
    /// Measures volatility: used for position sizing, stop placement, dynamic targets
    /// Uses 14-period smoothing (standard)
    /// </summary>
    public class AtrCalculator
    {
        private const int Period = 14;
        private const double MinimumValidAtr = 0.0;

        /// <summary>
        /// True Range buffer (for smoothing)
        /// </summary>
        private readonly Queue<double> _trueRangeBuffer = new(Period + 5);

        /// <summary>
        /// Smoothed ATR value
        /// </summary>
        private double _smoothedAtr = 0.0;

        /// <summary>
        /// Previous close (needed for TR calculation)
        /// </summary>
        private double _previousClose = 0.0;

        /// <summary>
        /// Total bars processed
        /// </summary>
        private int _barCount = 0;

        public int BarCount => _barCount;
        public bool IsReady => _barCount >= Period * 2; // Need 2x period for reliable ATR

        public AtrCalculator()
        {
        }

        /// <summary>
        /// Calculate ATR for current bar
        /// Returns: (ATR value, ATR as % of close price)
        /// </summary>
        public (double atr, double atrPercent) Calculate(RegimeInput bar)
        {
            if (!bar.IsValid())
                throw new ArgumentException("Invalid bar data");

            _barCount++;

            // True Range calculation
            double trueRange = CalculateTrueRange(bar);
            _trueRangeBuffer.Enqueue(trueRange);

            // Smooth using Wilder's smoothing (14-period)
            _smoothedAtr = SmoothValue(_trueRangeBuffer, Period);
            _smoothedAtr = Math.Max(_smoothedAtr, MinimumValidAtr);

            // Calculate ATR as percentage of current close
            double atrPercent = bar.Close > 0 ? (_smoothedAtr / bar.Close) * 100.0 : 0.0;

            _previousClose = bar.Close;

            return (_smoothedAtr, atrPercent);
        }

        /// <summary>
        /// True Range = max(High - Low, abs(High - PreviousClose), abs(Low - PreviousClose))
        /// </summary>
        private double CalculateTrueRange(RegimeInput bar)
        {
            if (_barCount == 1)
            {
                // First bar: use high - low
                return bar.High - bar.Low;
            }

            double highLow = bar.High - bar.Low;
            double highClose = Math.Abs(bar.High - _previousClose);
            double lowClose = Math.Abs(bar.Low - _previousClose);

            return Math.Max(highLow, Math.Max(highClose, lowClose));
        }

        /// <summary>
        /// Wilder's smoothing: (sum of last Period values) / Period
        /// Used for ATR calculation
        /// </summary>
        private double SmoothValue(Queue<double> buffer, int period)
        {
            if (buffer.Count == 0)
                return 0.0;

            // Take the last 'period' values
            var recent = buffer.TakeLast(Math.Min(period, buffer.Count)).ToList();

            if (recent.Count == 0)
                return 0.0;

            return recent.Sum() / recent.Count;
        }

        /// <summary>
        /// Reset calculator state (for testing or new session)
        /// </summary>
        public void Reset()
        {
            _trueRangeBuffer.Clear();
            _smoothedAtr = 0.0;
            _previousClose = 0.0;
            _barCount = 0;
        }

        /// <summary>
        /// Get current ATR (without processing new bar)
        /// </summary>
        public double GetAtr() => _smoothedAtr;

        /// <summary>
        /// Get ATR as percentage of a given price
        /// Useful for comparing volatility across different price levels
        /// </summary>
        public double GetAtrPercent(double price)
        {
            if (price <= 0)
                return 0.0;
            return (_smoothedAtr / price) * 100.0;
        }
    }
}
