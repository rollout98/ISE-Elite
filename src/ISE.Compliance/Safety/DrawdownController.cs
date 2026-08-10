namespace ISE.Compliance.Safety
{
    /// <summary>
    /// DrawdownController - Hard stop protection
    /// Prevents new trades if daily drawdown exceeds $1,000
    /// Account preservation is the primary goal
    /// </summary>
    public class DrawdownController
    {
        private double _sessionOpenEquity = 0;
        private double _sessionHighWaterMark = 0;
        private double _currentEquity = 0;
        private DateTime _sessionStartTime = DateTime.MinValue;
        private const double MaxDailyDrawdown = 1000.0; // Hard limit

        /// <summary>
        /// Current daily drawdown in dollars
        /// </summary>
        public double CurrentDrawdown { get; private set; }

        /// <summary>
        /// Maximum drawdown reached today
        /// </summary>
        public double MaxDrawdownToday { get; private set; }

        /// <summary>
        /// Current drawdown as percentage of session open equity
        /// </summary>
        public double DrawdownPercent { get; private set; }

        /// <summary>
        /// True if drawdown exceeds $1,000 (trading should stop)
        /// </summary>
        public bool IsDrawdownExceeded { get; private set; }

        /// <summary>
        /// Current equity value
        /// </summary>
        public double CurrentEquity
        {
            get => _currentEquity;
            set
            {
                _currentEquity = value;
                UpdateDrawdown();
            }
        }

        /// <summary>
        /// Initialize session at market open
        /// </summary>
        public void StartSession(double openingEquity)
        {
            _sessionOpenEquity = openingEquity;
            _sessionHighWaterMark = openingEquity;
            _currentEquity = openingEquity;
            _sessionStartTime = DateTime.Now;

            CurrentDrawdown = 0;
            MaxDrawdownToday = 0;
            DrawdownPercent = 0;
            IsDrawdownExceeded = false;
        }

        /// <summary>
        /// Update drawdown based on current equity
        /// </summary>
        private void UpdateDrawdown()
        {
            if (_sessionOpenEquity == 0)
                return;

            // Update high water mark
            if (_currentEquity > _sessionHighWaterMark)
                _sessionHighWaterMark = _currentEquity;

            // Calculate current drawdown
            CurrentDrawdown = _sessionHighWaterMark - _currentEquity;

            // Update max drawdown
            if (CurrentDrawdown > MaxDrawdownToday)
                MaxDrawdownToday = CurrentDrawdown;

            // Calculate drawdown percentage
            DrawdownPercent = (_sessionOpenEquity > 0) 
                ? (CurrentDrawdown / _sessionOpenEquity) * 100.0 
                : 0;

            // Check if exceeded
            IsDrawdownExceeded = CurrentDrawdown > MaxDailyDrawdown;
        }

        /// <summary>
        /// Record a closed trade's P&amp;L
        /// Equity updates automatically via CurrentEquity property
        /// </summary>
        public void RecordClosedTrade(double tradeProfit)
        {
            CurrentEquity += tradeProfit;
        }

        /// <summary>
        /// Record unrealized P&amp;L from open position
        /// </summary>
        public void UpdateUnrealizedPnl(double unrealizedPnl)
        {
            // Equity = open equity + realized + unrealized
            var totalPnl = (_currentEquity - _sessionOpenEquity) + unrealizedPnl;
            CurrentEquity = _sessionOpenEquity + totalPnl;
        }

        /// <summary>
        /// Can we enter a new trade?
        /// False if drawdown exceeded
        /// </summary>
        public bool CanEnterNewTrade()
        {
            return !IsDrawdownExceeded;
        }

        /// <summary>
        /// Can we exit current trades?
        /// Always true (need to close positions even if DD exceeded)
        /// </summary>
        public bool CanExitPositions()
        {
            return true;
        }

        /// <summary>
        /// Reset for next session (4:00 PM market close)
        /// </summary>
        public void EndSession()
        {
            _sessionOpenEquity = 0;
            _sessionHighWaterMark = 0;
            _currentEquity = 0;
            _sessionStartTime = DateTime.MinValue;

            CurrentDrawdown = 0;
            MaxDrawdownToday = 0;
            DrawdownPercent = 0;
            IsDrawdownExceeded = false;
        }

        /// <summary>
        /// Get detailed status for logging
        /// </summary>
        public string GetStatus()
        {
            return $"Drawdown: ${CurrentDrawdown:F2} / {DrawdownPercent:F1}% | " +
                   $"Max Today: ${MaxDrawdownToday:F2} | " +
                   $"Open Equity: ${_sessionOpenEquity:F2} | " +
                   $"Current: ${_currentEquity:F2} | " +
                   $"Status: {(IsDrawdownExceeded ? "EXCEEDED - NO NEW TRADES" : "OK")}";
        }

        /// <summary>
        /// Get remaining allowed drawdown
        /// </summary>
        public double RemainingDrawdownBuffer()
        {
            return Math.Max(0, MaxDailyDrawdown - CurrentDrawdown);
        }

        public override string ToString()
        {
            return $"DD: ${CurrentDrawdown:F2} ({DrawdownPercent:F1}%) | Status: {(IsDrawdownExceeded ? "HALTED" : "ACTIVE")}";
        }
    }
}
