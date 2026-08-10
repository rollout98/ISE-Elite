namespace ISE.Compliance.Safety
{
    /// <summary>
    /// EdgeDegradationDetector - Monitor win rate decline and pause trading if edge is lost
    /// Core premise: If win rate &lt; 55%, the edge has degraded and we should stop trading
    /// </summary>
    public class EdgeDegradationDetector
    {
        private Queue<TradeResult> _trades = new();
        private const int MinimumTradesForAnalysis = 10;
        private const double MinimumWinRateThreshold = 0.55; // 55%
        private const double WarningWinRateThreshold = 0.58; // 58% (early warning)
        private const int SmoothingPeriod = 20; // 20-trade rolling window
        private bool _edgeLost = false;

        /// <summary>
        /// Current win rate
        /// </summary>
        public double CurrentWinRate { get; private set; } = 0;

        /// <summary>
        /// Win rate trend (improving, stable, declining)
        /// </summary>
        public string Trend { get; private set; } = "Unknown";

        /// <summary>
        /// Has the edge degraded (stopped trading)?
        /// </summary>
        public bool EdgeLost
        {
            get => _edgeLost;
            private set => _edgeLost = value;
        }

        /// <summary>
        /// Number of losing trades in a row
        /// </summary>
        public int LosingStreak { get; private set; } = 0;

        /// <summary>
        /// Number of winning trades in a row
        /// </summary>
        public int WinningStreak { get; private set; } = 0;

        /// <summary>
        /// Total trades recorded
        /// </summary>
        public int TradeCount => _trades.Count;

        /// <summary>
        /// Record a trade result
        /// </summary>
        public void RecordTrade(double pnl, double riskRatio = 1.0)
        {
            var tradeResult = new TradeResult
            {
                Pnl = pnl,
                IsWin = pnl > 0,
                RiskRatio = riskRatio,
                Timestamp = DateTime.Now
            };

            _trades.Enqueue(tradeResult);

            // Keep only last 50 trades for efficiency
            if (_trades.Count > 50)
                _trades.Dequeue();

            // Update streaks
            UpdateStreaks();

            // Update win rate and check for degradation
            UpdateMetrics();
        }

        /// <summary>
        /// Update win/loss streaks
        /// </summary>
        private void UpdateStreaks()
        {
            if (_trades.Count == 0)
                return;

            var lastTrade = _trades.Last();
            var previousTrade = _trades.Count > 1 ? _trades.ElementAt(_trades.Count - 2) : null;

            if (lastTrade.IsWin)
            {
                WinningStreak++;
                LosingStreak = 0;
            }
            else
            {
                LosingStreak++;
                WinningStreak = 0;
            }
        }

        /// <summary>
        /// Update win rate and check if edge is lost
        /// </summary>
        private void UpdateMetrics()
        {
            if (_trades.Count < MinimumTradesForAnalysis)
            {
                EdgeLost = false;
                Trend = "Insufficient data";
                return;
            }

            // Use smoothing window (last N trades)
            int windowSize = Math.Min(SmoothingPeriod, _trades.Count);
            var recentTrades = _trades.Skip(_trades.Count - windowSize).ToList();

            int wins = recentTrades.Count(t => t.IsWin);
            CurrentWinRate = (double)wins / recentTrades.Count;

            // Determine trend
            if (_trades.Count >= SmoothingPeriod * 2)
            {
                var oldTrades = _trades.Skip(_trades.Count - (SmoothingPeriod * 2))
                                      .Take(SmoothingPeriod)
                                      .ToList();
                var oldWins = oldTrades.Count(t => t.IsWin);
                double oldWinRate = (double)oldWins / oldTrades.Count;

                if (CurrentWinRate > oldWinRate + 0.05)
                    Trend = "Improving";
                else if (CurrentWinRate < oldWinRate - 0.05)
                    Trend = "Declining";
                else
                    Trend = "Stable";
            }

            // Check for edge loss
            if (CurrentWinRate < MinimumWinRateThreshold)
            {
                EdgeLost = true;
            }
            else if (CurrentWinRate >= (MinimumWinRateThreshold + 0.03)) // Hysteresis
            {
                EdgeLost = false;
            }
        }

        /// <summary>
        /// Can we trade? Only if edge exists
        /// </summary>
        public bool CanTrade()
        {
            if (_trades.Count < MinimumTradesForAnalysis)
                return true; // Not enough data, allow trading

            return !EdgeLost;
        }

        /// <summary>
        /// Get win rate status
        /// </summary>
        public string GetWinRateStatus()
        {
            if (_trades.Count < MinimumTradesForAnalysis)
                return $"Insufficient data ({_trades.Count}/{MinimumTradesForAnalysis} trades)";

            string status = CurrentWinRate >= WarningWinRateThreshold
                ? "HEALTHY"
                : CurrentWinRate >= MinimumWinRateThreshold
                ? "WARNING"
                : "CRITICAL";

            return $"Win Rate: {CurrentWinRate:P1} | Status: {status} | Trend: {Trend}";
        }

        /// <summary>
        /// Get expected outcome for next trade
        /// </summary>
        public double GetExpectedValue()
        {
            if (_trades.Count == 0)
                return 0;

            // Average win
            var wins = _trades.Where(t => t.IsWin).ToList();
            double avgWin = wins.Count > 0 ? wins.Average(t => t.Pnl) : 0;

            // Average loss
            var losses = _trades.Where(t => !t.IsWin).ToList();
            double avgLoss = losses.Count > 0 ? Math.Abs(losses.Average(t => t.Pnl)) : 0;

            // EV = (win% × avgWin) - (loss% × avgLoss)
            return (CurrentWinRate * avgWin) - ((1 - CurrentWinRate) * avgLoss);
        }

        /// <summary>
        /// Get detailed status
        /// </summary>
        public string GetStatus()
        {
            return $"Trades: {TradeCount} | Win Rate: {CurrentWinRate:P1} | " +
                   $"Streak: {(WinningStreak > 0 ? $"W{WinningStreak}" : $"L{LosingStreak}")} | " +
                   $"Trend: {Trend} | " +
                   $"Status: {(EdgeLost ? "PAUSED" : "TRADING")} | " +
                   $"EV: ${GetExpectedValue():F2}";
        }

        /// <summary>
        /// Get recent trades
        /// </summary>
        public List<TradeResult> GetRecentTrades(int count = 10)
        {
            return _trades.Skip(Math.Max(0, _trades.Count - count)).ToList();
        }

        /// <summary>
        /// Force resume trading (manual override)
        /// </summary>
        public void ForceResume()
        {
            EdgeLost = false;
        }

        /// <summary>
        /// Force pause trading (manual override)
        /// </summary>
        public void ForcePause()
        {
            EdgeLost = true;
        }

        /// <summary>
        /// Reset for new session
        /// </summary>
        public void Reset()
        {
            _trades.Clear();
            CurrentWinRate = 0;
            Trend = "Unknown";
            EdgeLost = false;
            LosingStreak = 0;
            WinningStreak = 0;
        }

        public override string ToString()
        {
            return $"Edge: {(EdgeLost ? "LOST" : "OK")} | Win Rate: {CurrentWinRate:P1} | " +
                   $"Trades: {TradeCount}";
        }
    }

    /// <summary>
    /// Individual trade result record
    /// </summary>
    public class TradeResult
    {
        public double Pnl { get; set; }
        public bool IsWin { get; set; }
        public double RiskRatio { get; set; } = 1.0;
        public DateTime Timestamp { get; set; }

        public override string ToString()
        {
            return $"{(IsWin ? "W" : "L")} ${Pnl:+0.00;-#.00} (R={RiskRatio:F2})";
        }
    }
}
