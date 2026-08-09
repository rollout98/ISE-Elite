using ISE.UnifiedRegimeEngine.Models;

namespace ISE.UnifiedRegimeEngine.RegimeCalculations
{
    /// <summary>
    /// MACD (Moving Average Convergence Divergence) Calculator
    /// Detects trend direction: positive histogram = uptrend, negative = downtrend
    /// Uses 12/26/9 periods (standard)
    /// MACD = 12-EMA - 26-EMA
    /// Signal = 9-EMA of MACD
    /// Histogram = MACD - Signal
    /// </summary>
    public class MacdCalculator
    {
        private const int FastPeriod = 12;
        private const int SlowPeriod = 26;
        private const int SignalPeriod = 9;

        /// <summary>
        /// Close price buffer (for EMA calculations)
        /// </summary>
        private readonly Queue<double> _closeBuffer = new(SlowPeriod + 10);

        /// <summary>
        /// 12-period EMA buffer
        /// </summary>
        private readonly Queue<double> _ema12Buffer = new(SlowPeriod + 10);

        /// <summary>
        /// 26-period EMA buffer
        /// </summary>
        private readonly Queue<double> _ema26Buffer = new(SlowPeriod + 10);

        /// <summary>
        /// MACD line buffer
        /// </summary>
        private readonly Queue<double> _macdBuffer = new(SignalPeriod + 10);

        /// <summary>
        /// Signal line buffer (9-EMA of MACD)
        /// </summary>
        private readonly Queue<double> _signalBuffer = new(SignalPeriod + 10);

        private double _ema12 = 0.0;
        private double _ema26 = 0.0;
        private double _macdLine = 0.0;
        private double _signalLine = 0.0;
        private double _histogram = 0.0;
        private double _previousHistogram = 0.0;
        private int _barCount = 0;

        public int BarCount => _barCount;
        public bool IsReady => _barCount >= SlowPeriod * 2; // Need 2x slow period for reliable MACD

        public MacdCalculator()
        {
        }

        /// <summary>
        /// Calculate MACD for current bar
        /// Returns: (MACD line, signal line, histogram, bullish cross, bearish cross)
        /// </summary>
        public (double macdLine, double signalLine, double histogram, bool bullishCross, bool bearishCross) Calculate(RegimeInput bar)
        {
            if (!bar.IsValid())
                throw new ArgumentException("Invalid bar data");

            _barCount++;
            _closeBuffer.Enqueue(bar.Close);

            // Calculate 12-period EMA
            _ema12 = CalculateEma(_closeBuffer, _ema12, FastPeriod, _barCount);
            _ema12Buffer.Enqueue(_ema12);

            // Calculate 26-period EMA
            _ema26 = CalculateEma(_closeBuffer, _ema26, SlowPeriod, _barCount);
            _ema26Buffer.Enqueue(_ema26);

            // MACD line = 12-EMA - 26-EMA
            _macdLine = _ema12 - _ema26;
            _macdBuffer.Enqueue(_macdLine);

            // Signal line = 9-EMA of MACD
            _signalLine = CalculateSignalLine();
            _signalBuffer.Enqueue(_signalLine);

            // Histogram = MACD - Signal
            _previousHistogram = _histogram;
            _histogram = _macdLine - _signalLine;

            // Detect crosses
            bool bullishCross = _previousHistogram < 0 && _histogram >= 0;
            bool bearishCross = _previousHistogram > 0 && _histogram <= 0;

            return (_macdLine, _signalLine, _histogram, bullishCross, bearishCross);
        }

        /// <summary>
        /// Exponential Moving Average (EMA) calculation
        /// EMA = (Close × Multiplier) + (Previous EMA × (1 - Multiplier))
        /// Multiplier = 2 / (Period + 1)
        /// </summary>
        private double CalculateEma(Queue<double> buffer, double previousEma, int period, int barCount)
        {
            if (buffer.Count == 0)
                return 0.0;

            double multiplier = 2.0 / (period + 1.0);
            double currentClose = buffer.Last();

            if (barCount <= period)
            {
                // Initialization: use SMA until period is reached
                var values = buffer.TakeLast(Math.Min(barCount, period)).ToList();
                return values.Sum() / values.Count;
            }
            else
            {
                // After initialization: use EMA formula
                return (currentClose * multiplier) + (previousEma * (1.0 - multiplier));
            }
        }

        /// <summary>
        /// Calculate signal line (9-EMA of MACD line)
        /// </summary>
        private double CalculateSignalLine()
        {
            if (_macdBuffer.Count == 0)
                return 0.0;

            int signalBarCount = Math.Min(_barCount, SignalPeriod);
            double multiplier = 2.0 / (SignalPeriod + 1.0);

            if (_barCount <= SignalPeriod)
            {
                // Initialization: use SMA
                var values = _macdBuffer.TakeLast(signalBarCount).ToList();
                return values.Count > 0 ? values.Sum() / values.Count : 0.0;
            }
            else
            {
                // Use EMA formula
                var previousSignal = _signalBuffer.Count > 0 ? _signalBuffer.Last() : 0.0;
                return (_macdLine * multiplier) + (previousSignal * (1.0 - multiplier));
            }
        }

        /// <summary>
        /// Reset calculator state (for testing or new session)
        /// </summary>
        public void Reset()
        {
            _closeBuffer.Clear();
            _ema12Buffer.Clear();
            _ema26Buffer.Clear();
            _macdBuffer.Clear();
            _signalBuffer.Clear();
            _ema12 = 0.0;
            _ema26 = 0.0;
            _macdLine = 0.0;
            _signalLine = 0.0;
            _histogram = 0.0;
            _previousHistogram = 0.0;
            _barCount = 0;
        }

        /// <summary>
        /// Get current MACD values (without processing new bar)
        /// </summary>
        public double GetMacdLine() => _macdLine;
        public double GetSignalLine() => _signalLine;
        public double GetHistogram() => _histogram;
        public double GetPreviousHistogram() => _previousHistogram;

        public bool IsBullishCross() => _previousHistogram < 0 && _histogram >= 0;
        public bool IsBearishCross() => _previousHistogram > 0 && _histogram <= 0;
    }
}
