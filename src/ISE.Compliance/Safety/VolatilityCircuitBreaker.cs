namespace ISE.Compliance.Safety
{
    /// <summary>
    /// VolatilityCircuitBreaker - Pause trading if volatility spikes
    /// Prevents trading into extreme moves where edge degrades
    /// </summary>
    public class VolatilityCircuitBreaker
    {
        private double _currentAtr = 0;
        private double _averageAtr = 0;
        private Queue<double> _atrHistory = new(20); // 20-bar rolling average
        private bool _isTripped = false;
        private DateTime _tripTime = DateTime.MinValue;
        private const double VolatilityRatioThreshold = 1.5; // ATR > 150% of average
        private const int RecoveryBarsRequired = 2; // 2 bars at normal vol to resume
        private int _recoveryBarCount = 0;

        // Liquidity thresholds
        private const double MaxSpreadForTrading = 3.0; // ticks
        private const double MinOrderBookVolume = 500.0; // shares

        /// <summary>
        /// Is circuit breaker tripped (trading paused)?
        /// </summary>
        public bool IsTripped { get; private set; } = false;

        /// <summary>
        /// Reason for trip
        /// </summary>
        public string? TripReason { get; private set; }

        /// <summary>
        /// Current volatility ratio (ATR / avg ATR)
        /// </summary>
        public double VolatilityRatio { get; private set; } = 0;

        /// <summary>
        /// Bars since trip (for recovery countdown)
        /// </summary>
        public int BarsSinceTrip { get; private set; } = 0;

        /// <summary>
        /// Record ATR update
        /// </summary>
        public void UpdateAtr(double atr)
        {
            _currentAtr = atr;

            // Add to history and maintain rolling average
            _atrHistory.Enqueue(atr);
            if (_atrHistory.Count > 20)
                _atrHistory.Dequeue();

            if (_atrHistory.Count > 0)
                _averageAtr = _atrHistory.Average();

            // Calculate ratio
            if (_averageAtr > 0)
                VolatilityRatio = _currentAtr / _averageAtr;

            // Check for trip
            CheckForTrip();

            // Check for recovery
            if (IsTripped)
                CheckForRecovery();
        }

        /// <summary>
        /// Check if volatility exceeds threshold
        /// </summary>
        private void CheckForTrip()
        {
            if (IsTripped)
                return; // Already tripped

            if (VolatilityRatio > VolatilityRatioThreshold)
            {
                IsTripped = true;
                _tripTime = DateTime.Now;
                _recoveryBarCount = 0;
                TripReason = $"Volatility spike: ATR {_currentAtr:F2} / avg {_averageAtr:F2} = {VolatilityRatio:F2}x threshold";
            }
        }

        /// <summary>
        /// Check if conditions normalized (can resume trading)
        /// </summary>
        private void CheckForRecovery()
        {
            if (!IsTripped)
                return;

            // Count bars back below threshold
            if (VolatilityRatio <= VolatilityRatioThreshold)
            {
                _recoveryBarCount++;
            }
            else
            {
                _recoveryBarCount = 0; // Reset if spike again
            }

            // Recovery complete after 2 bars at normal vol
            if (_recoveryBarCount >= RecoveryBarsRequired)
            {
                IsTripped = false;
                TripReason = null;
                _recoveryBarCount = 0;
                BarsSinceTrip = 0;
            }

            BarsSinceTrip = (int)(DateTime.Now - _tripTime).TotalSeconds / 60; // Approximate bars (minutes)
        }

        /// <summary>
        /// Check liquidity during volatility (additional check)
        /// </summary>
        public bool IsLiquidityAcceptable(double bidAskSpread, double orderBookVolume)
        {
            // During extreme volatility, be extra strict on liquidity
            if (IsTripped)
            {
                return bidAskSpread <= MaxSpreadForTrading && 
                       orderBookVolume >= MinOrderBookVolume;
            }

            return true;
        }

        /// <summary>
        /// Can we enter new trades?
        /// </summary>
        public bool CanEnterNewTrades()
        {
            return !IsTripped;
        }

        /// <summary>
        /// Can we exit trades?
        /// Yes, always (need to close in extreme vol)
        /// </summary>
        public bool CanExitTrades()
        {
            return true;
        }

        /// <summary>
        /// Get detailed status
        /// </summary>
        public string GetStatus()
        {
            return $"Current ATR: {_currentAtr:F2} | Avg ATR: {_averageAtr:F2} | " +
                   $"Ratio: {VolatilityRatio:F2}x | " +
                   $"Status: {(IsTripped ? "TRIPPED" : "NORMAL")} | " +
                   (IsTripped ? $"Recovery: {_recoveryBarCount}/{RecoveryBarsRequired} | " : "") +
                   (TripReason != null ? $"Reason: {TripReason}" : "");
        }

        /// <summary>
        /// Reset for new session
        /// </summary>
        public void Reset()
        {
            _currentAtr = 0;
            _averageAtr = 0;
            _atrHistory.Clear();
            IsTripped = false;
            TripReason = null;
            _tripTime = DateTime.MinValue;
            _recoveryBarCount = 0;
            VolatilityRatio = 0;
            BarsSinceTrip = 0;
        }

        public override string ToString()
        {
            return $"Volatility: Ratio={VolatilityRatio:F2}x | " +
                   $"Status: {(IsTripped ? "TRIPPED" : "OK")}";
        }
    }
}
