using ISE.UnifiedRegimeEngine;

namespace ISE.Compliance
{
    /// <summary>
    /// ComplianceEngine - Orchestrates all safety checks and compliance rules
    /// Serves as the gatekeeper for all trading decisions
    /// 
    /// Philosophy: Account preservation > Profitability
    /// 
    /// Compliance Rules (Hard Stops):
    /// 1. Minimum 60-second hold OR profit target
    /// 2. Maximum 10 trades per day
    /// 3. No trading if daily drawdown > $1,000
    /// 4. No trading if NinjaTrader disconnected
    /// </summary>
    public class ComplianceEngine
    {
        private readonly Safety.DrawdownController _drawdownController = new();
        private readonly Safety.ConnectionMonitor _connectionMonitor = new();

        // Trade tracking
        private List<TradeRecord> _dailyTrades = new();
        private DateTime _sessionStartTime = DateTime.MinValue;
        private const int MaxTradesPerDay = 10;
        private const int MinimumHoldTimeSeconds = 60;

        /// <summary>
        /// Enable/disable compliance (for backtesting without safety constraints)
        /// </summary>
        public bool ComplianceEnabled { get; set; } = true;

        /// <summary>
        /// Current trade count (today)
        /// </summary>
        public int DailyTradeCount => _dailyTrades.Count;

        /// <summary>
        /// Current drawdown
        /// </summary>
        public double CurrentDrawdown => _drawdownController.CurrentDrawdown;

        /// <summary>
        /// Is connection active?
        /// </summary>
        public bool IsConnectionActive => _connectionMonitor.CanExecuteTrades();

        /// <summary>
        /// Can we enter new trades?
        /// </summary>
        public bool CanEnterNewTrades
        {
            get
            {
                if (!ComplianceEnabled)
                    return true;

                return _drawdownController.CanEnterNewTrade() &&
                       _connectionMonitor.CanExecuteTrades() &&
                       DailyTradeCount < MaxTradesPerDay;
            }
        }

        /// <summary>
        /// Initialize at session start (9:30 AM)
        /// </summary>
        public void StartSession(double openingEquity)
        {
            _sessionStartTime = DateTime.Now;
            _dailyTrades.Clear();
            _drawdownController.StartSession(openingEquity);
            _connectionMonitor.Reset();
        }

        /// <summary>
        /// Validate entry request against all compliance rules
        /// Returns (allowed, reason) tuple
        /// </summary>
        public (bool allowed, string reason) ValidateEntry(
            EntrySignal entrySignal,
            double currentEquity)
        {
            if (!ComplianceEnabled)
                return (true, "Compliance disabled");

            // Check 1: Connection
            if (!_connectionMonitor.CanExecuteTrades())
            {
                return (false, $"Connection issue: {_connectionMonitor.DisconnectionReason}");
            }

            // Check 2: Drawdown limit
            if (!_drawdownController.CanEnterNewTrade())
            {
                return (false, $"Drawdown exceeded: ${_drawdownController.CurrentDrawdown:F2} > $1,000");
            }

            // Check 3: Daily trade limit
            if (DailyTradeCount >= MaxTradesPerDay)
            {
                return (false, $"Daily trade limit reached: {DailyTradeCount}/{MaxTradesPerDay}");
            }

            // Check 4: Entry signal validity
            if (!entrySignal.CanEnterLong && !entrySignal.CanEnterShort)
            {
                return (false, entrySignal.RejectReason ?? "Entry signal not valid");
            }

            // All checks passed
            return (true, "Entry allowed");
        }

        /// <summary>
        /// Validate exit request
        /// Can always exit (to close positions and preserve capital)
        /// </summary>
        public (bool allowed, string reason) ValidateExit(
            double timeInTradeSeconds,
            double unrealizedPnL,
            double profitTarget)
        {
            if (!ComplianceEnabled)
                return (true, "Compliance disabled");

            // Check minimum hold time
            if (timeInTradeSeconds < MinimumHoldTimeSeconds && unrealizedPnL < profitTarget)
            {
                return (false, 
                    $"Minimum hold time not met: {timeInTradeSeconds:F0}s < {MinimumHoldTimeSeconds}s " +
                    $"(and P&amp;L ${unrealizedPnL:F2} < target ${profitTarget:F2})");
            }

            // Otherwise always allow exit
            return (true, "Exit allowed");
        }

        /// <summary>
        /// Record an entered trade
        /// </summary>
        public void RecordTradeEntry(
            EntrySignal signal,
            double entryPrice,
            DateTime entryTime,
            double profitTarget,
            double stopLoss)
        {
            var trade = new TradeRecord
            {
                EntryTime = entryTime,
                EntryPrice = entryPrice,
                Direction = signal.EntryDirection ?? "UNKNOWN",
                ProfitTarget = profitTarget,
                StopLoss = stopLoss,
                Status = TradeStatus.Open
            };

            _dailyTrades.Add(trade);
        }

        /// <summary>
        /// Record a closed trade
        /// </summary>
        public void RecordTradeExit(
            double exitPrice,
            double profitLoss,
            DateTime exitTime,
            string exitReason)
        {
            if (_dailyTrades.Count == 0)
                return;

            var lastTrade = _dailyTrades.Last();
            lastTrade.ExitTime = exitTime;
            lastTrade.ExitPrice = exitPrice;
            lastTrade.PnL = profitLoss;
            lastTrade.ExitReason = exitReason;
            lastTrade.Status = profitLoss > 0 ? TradeStatus.Win : TradeStatus.Loss;

            _drawdownController.RecordClosedTrade(profitLoss);
        }

        /// <summary>
        /// Update drawdown with unrealized P&amp;L
        /// </summary>
        public void UpdateUnrealizedPnL(double unrealizedPnL)
        {
            _drawdownController.UpdateUnrealizedPnl(unrealizedPnL);
        }

        /// <summary>
        /// Record connection event (call from NT8 adapter)
        /// </summary>
        public void RecordHeartbeat() => _connectionMonitor.RecordHeartbeat();
        public void RecordLevel2Update() => _connectionMonitor.RecordLevel2Update();
        public void RecordBarUpdate() => _connectionMonitor.RecordBarUpdate();

        /// <summary>
        /// Calculate win rate from today's trades
        /// </summary>
        public double GetWinRate()
        {
            if (_dailyTrades.Count == 0)
                return 0;

            var wins = _dailyTrades.Count(t => t.Status == TradeStatus.Win);
            return (double)wins / _dailyTrades.Count;
        }

        /// <summary>
        /// Get session summary
        /// </summary>
        public SessionSummary GetSessionSummary()
        {
            return new SessionSummary
            {
                SessionStartTime = _sessionStartTime,
                TradeCount = DailyTradeCount,
                WinRate = GetWinRate(),
                CurrentDrawdown = CurrentDrawdown,
                MaxDrawdownToday = _drawdownController.MaxDrawdownToday,
                IsConnectionActive = IsConnectionActive,
                CanTrade = CanEnterNewTrades,
                Trades = _dailyTrades.ToList()
            };
        }

        /// <summary>
        /// End session (4:00 PM market close)
        /// </summary>
        public void EndSession()
        {
            _drawdownController.EndSession();
            // Keep daily trades for audit, but could save to DB here
        }

        /// <summary>
        /// Get detailed compliance status
        /// </summary>
        public string GetComplianceStatus()
        {
            return $"[COMPLIANCE STATUS]\n" +
                   $"Enabled: {ComplianceEnabled}\n" +
                   $"Connection: {_connectionMonitor.GetStatus()}\n" +
                   $"Drawdown: {_drawdownController.GetStatus()}\n" +
                   $"Trades Today: {DailyTradeCount}/{MaxTradesPerDay}\n" +
                   $"Win Rate: {GetWinRate():P1}\n" +
                   $"Can Trade: {(CanEnterNewTrades ? "YES" : "NO")}\n";
        }

        public override string ToString()
        {
            return $"Compliance [{(CanEnterNewTrades ? "ACTIVE" : "HALTED")}] | " +
                   $"Trades: {DailyTradeCount}/{MaxTradesPerDay} | " +
                   $"DD: ${CurrentDrawdown:F2} | " +
                   $"WR: {GetWinRate():P1}";
        }
    }

    /// <summary>
    /// Entry signal (from regime engine)
    /// </summary>
    public class EntrySignal
    {
        public bool CanEnterLong { get; set; }
        public bool CanEnterShort { get; set; }
        public string? EntryDirection { get; set; }
        public string? RejectReason { get; set; }
        public double ProfitTarget { get; set; }
        public double StopLoss { get; set; }
    }

    /// <summary>
    /// Trade record for audit trail
    /// </summary>
    public class TradeRecord
    {
        public DateTime EntryTime { get; set; }
        public double EntryPrice { get; set; }
        public string Direction { get; set; } = "";
        public DateTime? ExitTime { get; set; }
        public double? ExitPrice { get; set; }
        public double ProfitTarget { get; set; }
        public double StopLoss { get; set; }
        public double? PnL { get; set; }
        public string? ExitReason { get; set; }
        public TradeStatus Status { get; set; }

        public double HoldTimeSeconds => ExitTime.HasValue 
            ? (ExitTime.Value - EntryTime).TotalSeconds 
            : 0;
    }

    public enum TradeStatus
    {
        Open,
        Win,
        Loss
    }

    /// <summary>
    /// Session summary for reporting
    /// </summary>
    public class SessionSummary
    {
        public DateTime SessionStartTime { get; set; }
        public int TradeCount { get; set; }
        public double WinRate { get; set; }
        public double CurrentDrawdown { get; set; }
        public double MaxDrawdownToday { get; set; }
        public bool IsConnectionActive { get; set; }
        public bool CanTrade { get; set; }
        public List<TradeRecord> Trades { get; set; } = new();

        public override string ToString()
        {
            return $"Session: {SessionStartTime:HH:mm} | " +
                   $"Trades: {TradeCount} | " +
                   $"WR: {WinRate:P1} | " +
                   $"DD: ${MaxDrawdownToday:F2} | " +
                   $"Status: {(CanTrade ? "ACTIVE" : "HALTED")}";
        }
    }
}
