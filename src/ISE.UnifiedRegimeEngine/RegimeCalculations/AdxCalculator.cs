using ISE.UnifiedRegimeEngine.Models;

namespace ISE.UnifiedRegimeEngine.RegimeCalculations
{
    /// <summary>
    /// ADX (Average Directional Index) Calculator
    /// Measures trend strength: HIGH = strong trend, LOW = ranging/weak
    /// Uses 14-period smoothing (standard)
    /// </summary>
    public class AdxCalculator
    {
        private const int Period = 14;
        private const double MinimumValidAdx = 0.0;
        private const double MaximumValidAdx = 100.0;

        /// <summary>
        /// Smoothing buffer (stores DI differences for ADX calculation)
        /// </summary>
        private readonly Queue<double> _diDifferenceBuffer = new(Period + 5);

        /// <summary>
        /// True Range buffer (for smoothing)
        /// </summary>
        private readonly Queue<double> _trueRangeBuffer = new(Period + 5);

        /// <summary>
        /// +DM buffer (positive directional movement)
        /// </summary>
        private readonly Queue<double> _dmPlusBuffer = new(Period + 5);

        /// <summary>
        /// -DM buffer (negative directional movement)
        /// </summary>
        private readonly Queue<double> _dmMinusBuffer = new(Period + 5);

        /// <summary>
        /// Smoothed +DI (Directional Indicator Plus)
        /// </summary>
        private double _smoothedDiPlus = 0.0;

        /// <summary>
        /// Smoothed -DI (Directional Indicator Minus)
        /// </summary>
        private double _smoothedDiMinus = 0.0;

        /// <summary>
        /// Smoothed ADX
        /// </summary>
        private double _smoothedAdx = 0.0;

        /// <summary>
        /// Previous close (needed for TR calculation)
        /// </summary>
        private double _previousClose = 0.0;

        /// <summary>
        /// Total bars processed
        /// </summary>
        private int _barCount = 0;

        public int BarCount => _barCount;
        public bool IsReady => _barCount >= Period * 2; // Need 2x period for reliable ADX

        public AdxCalculator()
        {
        }

        /// <summary>
        /// Calculate ADX for current bar
        /// Returns: (ADX value, DI+ value, DI- value)
        /// </summary>
        public (double adx, double diPlus, double diMinus) Calculate(RegimeInput bar)
        {
            if (!bar.IsValid())
                throw new ArgumentException("Invalid bar data");

            _barCount++;

            // True Range calculation
            double trueRange = CalculateTrueRange(bar);
            _trueRangeBuffer.Enqueue(trueRange);

            // Directional Movement calculation
            var (dmPlus, dmMinus) = CalculateDirectionalMovement(bar);
            _dmPlusBuffer.Enqueue(dmPlus);
            _dmMinusBuffer.Enqueue(dmMinus);

            // Smooth the measurements using Wilder's smoothing (14-period)
            double smoothedTr = SmoothValue(_trueRangeBuffer, Period);
            double smoothedDmPlus = SmoothValue(_dmPlusBuffer, Period);
            double smoothedDmMinus = SmoothValue(_dmMinusBuffer, Period);

            // Calculate Directional Indicators (DI+ and DI-)
            if (smoothedTr > 0)
            {
                _smoothedDiPlus = (smoothedDmPlus / smoothedTr) * 100.0;
                _smoothedDiMinus = (smoothedDmMinus / smoothedTr) * 100.0;
            }
            else
            {
                _smoothedDiPlus = 0.0;
                _smoothedDiMinus = 0.0;
            }

            // Clamp DI values
            _smoothedDiPlus = Math.Clamp(_smoothedDiPlus, 0.0, 100.0);
            _smoothedDiMinus = Math.Clamp(_smoothedDiMinus, 0.0, 100.0);

            // Calculate DI Difference for ADX
            double diDifference = Math.Abs(_smoothedDiPlus - _smoothedDiMinus);
            double diSum = _smoothedDiPlus + _smoothedDiMinus;
            double diRatio = diSum > 0 ? (diDifference / diSum) * 100.0 : 0.0;

            _diDifferenceBuffer.Enqueue(diRatio);

            // Calculate ADX (smooth the DI ratio)
            _smoothedAdx = SmoothValue(_diDifferenceBuffer, Period);
            _smoothedAdx = Math.Clamp(_smoothedAdx, MinimumValidAdx, MaximumValidAdx);

            _previousClose = bar.Close;

            return (_smoothedAdx, _smoothedDiPlus, _smoothedDiMinus);
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
        /// Directional Movement:
        /// +DM = (High - PreviousHigh) if positive AND > -DM, else 0
        /// -DM = (PreviousLow - Low) if positive AND > +DM, else 0
        /// </summary>
        private (double dmPlus, double dmMinus) CalculateDirectionalMovement(RegimeInput bar)
        {
            if (_barCount == 1)
                return (0.0, 0.0);

            double upMove = bar.High - (bar.PreviousClose ?? _previousClose);
            double downMove = (bar.PreviousClose ?? _previousClose) - bar.Low;

            double dmPlus = 0.0;
            double dmMinus = 0.0;

            if (upMove > 0 && upMove > downMove)
                dmPlus = upMove;

            if (downMove > 0 && downMove > upMove)
                dmMinus = downMove;

            return (dmPlus, dmMinus);
        }

        /// <summary>
        /// Wilder's smoothing: (sum of last Period values) / Period
        /// Used for True Range, +DM, -DM, and ADX calculations
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
            _diDifferenceBuffer.Clear();
            _trueRangeBuffer.Clear();
            _dmPlusBuffer.Clear();
            _dmMinusBuffer.Clear();
            _smoothedDiPlus = 0.0;
            _smoothedDiMinus = 0.0;
            _smoothedAdx = 0.0;
            _previousClose = 0.0;
            _barCount = 0;
        }

        /// <summary>
        /// Get current ADX (without processing new bar)
        /// </summary>
        public double GetAdx() => _smoothedAdx;
        public double GetDiPlus() => _smoothedDiPlus;
        public double GetDiMinus() => _smoothedDiMinus;
    }
}
