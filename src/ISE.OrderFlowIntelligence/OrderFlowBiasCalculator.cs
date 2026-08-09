using ISE.OrderFlowIntelligence.Models;

namespace ISE.OrderFlowIntelligence
{
    /// <summary>
    /// Order Flow Bias Calculator
    /// Measures institutional buying vs selling pressure
    /// Score: -100 (pure buying) to +100 (pure selling)
    /// </summary>
    public class OrderFlowBiasCalculator
    {
        /// <summary>
        /// Window size for moving average bias (last N snapshots)
        /// </summary>
        private const int BiasWindowSize = 5;

        /// <summary>
        /// Bias scores history (for smoothing)
        /// </summary>
        private readonly Queue<double> _biasHistory = new(BiasWindowSize + 5);

        /// <summary>
        /// Smoothed order flow bias
        /// </summary>
        private double _smoothedBias = 0.0;

        /// <summary>
        /// Get current smoothed bias
        /// </summary>
        public double CurrentBias => _smoothedBias;

        /// <summary>
        /// Get current bias strength (absolute value, 0-100)
        /// How strong is the current bias (regardless of direction)
        /// </summary>
        public double BiasStrength => Math.Abs(_smoothedBias);

        /// <summary>
        /// Is current bias bullish (< -50)?
        /// </summary>
        public bool IsBullish => _smoothedBias < -50.0;

        /// <summary>
        /// Is current bias bearish (> +50)?
        /// </summary>
        public bool IsBearish => _smoothedBias > 50.0;

        public OrderFlowBiasCalculator()
        {
        }

        /// <summary>
        /// Calculate order flow bias from DOM snapshot
        /// Returns smoothed bias score (-100 to +100)
        /// </summary>
        public double Calculate(DomSnapshot snapshot)
        {
            if (snapshot == null)
                return _smoothedBias;

            // Calculate raw bias from this snapshot
            double rawBias = snapshot.GetImbalanceScore();
            _biasHistory.Enqueue(rawBias);

            // Keep history limited
            while (_biasHistory.Count > BiasWindowSize)
                _biasHistory.Dequeue();

            // Smooth using moving average
            _smoothedBias = _biasHistory.Count > 0 
                ? _biasHistory.Average() 
                : 0.0;

            // Clamp to valid range
            _smoothedBias = Math.Clamp(_smoothedBias, -100.0, 100.0);

            return _smoothedBias;
        }

        /// <summary>
        /// Get bias direction as text
        /// </summary>
        public string GetBiasDirection()
        {
            if (Math.Abs(_smoothedBias) < 30.0)
                return "Neutral";
            else if (_smoothedBias < 0)
                return "Bullish";
            else
                return "Bearish";
        }

        /// <summary>
        /// Get bias intensity (0-1, where 1 = maximum strength)
        /// </summary>
        public double GetIntensity()
        {
            return Math.Min(1.0, Math.Abs(_smoothedBias) / 100.0);
        }

        /// <summary>
        /// Check if bias is strong enough for entry confirmation
        /// Returns true if bias > threshold in required direction
        /// </summary>
        public bool IsStrongBullish(double threshold = 50.0)
        {
            return _smoothedBias < -threshold;
        }

        public bool IsStrongBearish(double threshold = 50.0)
        {
            return _smoothedBias > threshold;
        }

        /// <summary>
        /// Check if bias is transitioning (changing direction)
        /// Useful for detecting early exits
        /// </summary>
        public bool IsTransitioning()
        {
            if (_biasHistory.Count < 2)
                return false;

            var recent = _biasHistory.TakeLast(2).ToList();
            double bias1 = recent[0];
            double bias2 = recent[1];

            // Transitioning if sign is changing
            return (bias1 < 0 && bias2 > 0) || (bias1 > 0 && bias2 < 0);
        }

        /// <summary>
        /// Get bias momentum (how fast is it changing)
        /// Positive = strengthening, Negative = weakening
        /// </summary>
        public double GetMomentum()
        {
            if (_biasHistory.Count < 2)
                return 0.0;

            var recent = _biasHistory.TakeLast(2).ToList();
            return recent.Last() - recent.First();
        }

        /// <summary>
        /// Reset calculator (for new session)
        /// </summary>
        public void Reset()
        {
            _biasHistory.Clear();
            _smoothedBias = 0.0;
        }

        public override string ToString()
        {
            return $"OrderFlowBias: {_smoothedBias:F1} ({GetBiasDirection()}) | Momentum: {GetMomentum():F1}";
        }
    }
}
