namespace ISE.Integration
{
    using ISE.UnifiedRegimeEngine;
    using ISE.OrderFlowIntelligence;
    using ISE.Compliance;
    using ISE.Compliance.Safety;

    /// <summary>
    /// IntegrationOrchestrator - Main orchestrator that coordinates all 20 components
    /// Receives market events (OnBar, OnLevel2Update) and routes through the entire system
    /// </summary>
    public class IntegrationOrchestrator
    {
        // Core components
        private readonly UnifiedMarketRegimeEngine _regimeEngine;
        private readonly DomDataFeed _orderFlowFeed;
        private readonly ComplianceEngine _compliance;

        // Safety components
        private readonly ConnectionMonitor _connectionMonitor;
        private readonly DrawdownController _drawdownController;
        private readonly PositionReconciliation _positionReconciliation;
        private readonly LiquidityValidator _liquidityValidator;
        private readonly VolatilityCircuitBreaker _volatilityCircuitBreaker;
        private readonly SlippageTracker _slippageTracker;
        private readonly NewsEventFilter _newsEventFilter;
        private readonly EdgeDegradationDetector _edgeDetector;

        // State
        private int _barCount = 0;
        private double _currentBid = 0;
        private double _currentAsk = 0;
        private List<OrchestratorEvent> _eventLog = new();
        private bool _isInitialized = false;

        public IntegrationOrchestrator()
        {
            _regimeEngine = new UnifiedMarketRegimeEngine();
            _orderFlowFeed = new DomDataFeed();
            _compliance = new ComplianceEngine();
            _connectionMonitor = new ConnectionMonitor();
            _drawdownController = new DrawdownController();
            _positionReconciliation = new PositionReconciliation();
            _liquidityValidator = new LiquidityValidator();
            _volatilityCircuitBreaker = new VolatilityCircuitBreaker();
            _slippageTracker = new SlippageTracker();
            _newsEventFilter = new NewsEventFilter();
            _edgeDetector = new EdgeDegradationDetector();
        }

        /// <summary>
        /// Initialize for trading day
        /// </summary>
        public void Initialize(string instrument, double openingEquity)
        {
            _regimeEngine.ConfigureForInstrument(instrument);
            _drawdownController.StartSession(openingEquity);
            _positionReconciliation.Reset();
            _liquidityValidator.Reset();
            _volatilityCircuitBreaker.Reset();
            _slippageTracker.Reset();
            _newsEventFilter.Reset();
            _edgeDetector.Reset();
            _compliance.Enable();

            _barCount = 0;
            _isInitialized = true;

            LogEvent("SYSTEM", $"Initialized for {instrument}, Opening Equity: ${openingEquity:F2}");
        }

        /// <summary>
        /// Process bar close (OHLCV data)
        /// Called from NinjaTrader OnBar event
        /// </summary>
        public OrchestratorResult ProcessBarClose(
            double open,
            double high,
            double low,
            double close,
            double volume,
            DateTime barTime)
        {
            if (!_isInitialized)
                return OrchestratorResult.NotInitialized();

            _barCount++;

            // Step 1: Check connection health
            _connectionMonitor.RecordHeartbeat();
            if (!_connectionMonitor.CanExecuteTrades())
            {
                LogEvent("CONNECTION", $"Disconnected: {_connectionMonitor.DisconnectionReason}");
                return OrchestratorResult.Disconnected(_connectionMonitor.DisconnectionReason);
            }

            // Step 2: Feed to regime engine
            var regimeSignal = _regimeEngine.AnalyzeBar(open, high, low, close, volume);
            _volatilityCircuitBreaker.UpdateAtr(regimeSignal.AtrValue);

            // Step 3: Check circuit breaker (volatility)
            if (!_volatilityCircuitBreaker.CanEnterNewTrades())
            {
                LogEvent("VOLATILITY", $"Circuit breaker tripped: {_volatilityCircuitBreaker.TripReason}");
                return OrchestratorResult.VolatilityTripped(_volatilityCircuitBreaker.TripReason);
            }

            // Step 4: Generate entry/exit signals based on regime
            EntryExitSignal? signal = null;
            if (regimeSignal.Regime == MarketRegime.TRENDING)
            {
                signal = TrendingModeLogic.EvaluateEntry(regimeSignal, close);
            }
            else if (regimeSignal.Regime == MarketRegime.RANGING)
            {
                signal = RangingModeLogic.EvaluateEntry(regimeSignal, close);
            }

            if (signal != null && signal.SignalType == SignalType.ENTRY)
            {
                // Step 5: Check news blackout
                if (!_newsEventFilter.CanTrade())
                {
                    LogEvent("NEWS", $"In news blackout: {_newsEventFilter.BlackoutReason}");
                    return OrchestratorResult.NewsBlackout(_newsEventFilter.BlackoutReason);
                }

                // Step 6: Check drawdown
                if (!_drawdownController.CanEnterNewTrade())
                {
                    LogEvent("DRAWDOWN", "Drawdown limit reached - new trades blocked");
                    return OrchestratorResult.DrawdownExceeded();
                }

                // Step 7: Check liquidity
                if (!_liquidityValidator.IsLiquidEnoughForEntry)
                {
                    LogEvent("LIQUIDITY", $"Insufficient liquidity: {_liquidityValidator.RejectionReason}");
                    return OrchestratorResult.LiquidityInsufficient(_liquidityValidator.RejectionReason);
                }

                // Step 8: Check edge (win rate)
                if (!_edgeDetector.CanTrade())
                {
                    LogEvent("EDGE", $"Edge degraded - win rate {_edgeDetector.CurrentWinRate:P1} < threshold");
                    return OrchestratorResult.EdgeLost();
                }

                // Step 9: Validate through compliance engine
                var entrySignal = new EntrySignal
                {
                    Symbol = "",
                    Regime = regimeSignal.Regime,
                    Direction = signal.Direction,
                    EntryPrice = close,
                    Target = signal.ProfitTarget,
                    StopLoss = signal.StopLoss,
                    Contracts = signal.ContractSize,
                    TimeStamp = barTime
                };

                bool entryAllowed = _compliance.ValidateEntry(entrySignal);
                if (!entryAllowed)
                {
                    LogEvent("COMPLIANCE", "Entry blocked by compliance engine");
                    return OrchestratorResult.ComplianceRejected();
                }

                // Step 10: All checks passed - entry approved
                _slippageTracker.RecordEntryFill(close, close, signal.ContractSize, barTime);
                _positionReconciliation.RecordEntry(signal.ContractSize);
                _compliance.RecordTradeEntry(entrySignal);

                LogEvent("ENTRY", 
                    $"ENTRY APPROVED | Regime: {regimeSignal.Regime} | " +
                    $"Price: {close:F2} | Target: {signal.ProfitTarget:F2} | " +
                    $"Confidence: {regimeSignal.Confidence:P0}");

                return OrchestratorResult.EntryApproved(signal);
            }

            // Check for exits
            if (signal != null && signal.SignalType == SignalType.EXIT)
            {
                _slippageTracker.RecordExitFill(close, close, signal.ContractSize, barTime);
                _positionReconciliation.RecordExit(signal.ContractSize);
                _edgeDetector.RecordTrade(signal.PnL);
                _compliance.RecordTradeExit(close, barTime);

                LogEvent("EXIT", $"EXIT EXECUTED | Price: {close:F2} | P&L: ${signal.PnL:F2}");

                return OrchestratorResult.ExitExecuted(signal);
            }

            // No signal
            return OrchestratorResult.NoSignal(regimeSignal.Regime);
        }

        /// <summary>
        /// Process Level 2 update (DOM data)
        /// Called from NinjaTrader OnLevel2Update event
        /// </summary>
        public void ProcessLevel2Update(
            double bidPrice,
            double askPrice,
            double bidVolume,
            double askVolume,
            double totalBidVolume,
            double totalAskVolume)
        {
            if (!_isInitialized)
                return;

            _currentBid = bidPrice;
            _currentAsk = askPrice;

            // Feed to order flow intelligence
            _orderFlowFeed.UpdateQuote(bidPrice, askPrice, bidVolume, askVolume);
            _connectionMonitor.RecordLevel2Update();

            // Feed to liquidity validator
            _liquidityValidator.UpdateBid(bidPrice, bidVolume);
            _liquidityValidator.UpdateAsk(askPrice, askVolume);
            _liquidityValidator.UpdateTotalVolume(totalBidVolume, totalAskVolume);
        }

        /// <summary>
        /// Get current system status
        /// </summary>
        public SystemStatus GetStatus()
        {
            return new SystemStatus
            {
                IsInitialized = _isInitialized,
                BarCount = _barCount,
                CurrentBid = _currentBid,
                CurrentAsk = _currentAsk,
                RegimeStatus = _regimeEngine.GetCurrentRegime().ToString(),
                ConnectionStatus = _connectionMonitor.CanExecuteTrades() ? "CONNECTED" : "DISCONNECTED",
                DrawdownStatus = _drawdownController.GetStatus(),
                ComplianceStatus = _compliance.GetComplianceStatus().ToString(),
                SafetyStatus = GetSafetyStatus(),
                EdgeStatus = _edgeDetector.GetStatus()
            };
        }

        /// <summary>
        /// Get aggregated safety status (all 8 components)
        /// </summary>
        private string GetSafetyStatus()
        {
            var checks = new[]
            {
                ("Connection", _connectionMonitor.CanExecuteTrades()),
                ("Drawdown", _drawdownController.CanEnterNewTrade()),
                ("Position", _positionReconciliation.CanTrade),
                ("Liquidity", _liquidityValidator.IsLiquidEnoughForEntry),
                ("Volatility", _volatilityCircuitBreaker.CanEnterNewTrades()),
                ("News", _newsEventFilter.CanTrade()),
                ("Edge", _edgeDetector.CanTrade())
            };

            int passing = checks.Count(c => c.Item2);
            return $"{passing}/{checks.Length} checks passing";
        }

        /// <summary>
        /// End of day - finalize session
        /// </summary>
        public void EndSession()
        {
            _drawdownController.EndSession();
            var summary = _compliance.GetSessionSummary();
            LogEvent("SESSION_END", $"Trades: {summary.TradeCount} | P&L: ${summary.SessionPnl:F2} | Win Rate: {summary.WinRate:P1}");
            _isInitialized = false;
        }

        /// <summary>
        /// Log an event
        /// </summary>
        private void LogEvent(string category, string message)
        {
            _eventLog.Add(new OrchestratorEvent
            {
                Timestamp = DateTime.Now,
                Category = category,
                Message = message
            });
        }

        /// <summary>
        /// Get event log
        /// </summary>
        public List<OrchestratorEvent> GetEventLog()
        {
            return _eventLog.ToList();
        }

        /// <summary>
        /// Get session summary
        /// </summary>
        public SessionSummary GetSessionSummary()
        {
            return _compliance.GetSessionSummary();
        }

        public override string ToString()
        {
            return $"Orchestrator: {_barCount} bars | Regime: {_regimeEngine.GetCurrentRegime()} | " +
                   $"Safety: {GetSafetyStatus()}";
        }
    }

    /// <summary>
    /// Result from orchestrator processing
    /// </summary>
    public class OrchestratorResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public EntryExitSignal? Signal { get; set; }
        public string RejectReason { get; set; } = "";

        public static OrchestratorResult EntryApproved(EntryExitSignal signal)
            => new() { Success = true, Message = "Entry approved", Signal = signal };

        public static OrchestratorResult ExitExecuted(EntryExitSignal signal)
            => new() { Success = true, Message = "Exit executed", Signal = signal };

        public static OrchestratorResult NoSignal(MarketRegime regime)
            => new() { Success = true, Message = $"No signal ({regime})", Signal = null };

        public static OrchestratorResult Disconnected(string reason)
            => new() { Success = false, Message = "Disconnected", RejectReason = reason };

        public static OrchestratorResult VolatilityTripped(string reason)
            => new() { Success = false, Message = "Volatility circuit breaker", RejectReason = reason };

        public static OrchestratorResult NewsBlackout(string reason)
            => new() { Success = false, Message = "News blackout", RejectReason = reason };

        public static OrchestratorResult DrawdownExceeded()
            => new() { Success = false, Message = "Drawdown limit exceeded" };

        public static OrchestratorResult LiquidityInsufficient(string reason)
            => new() { Success = false, Message = "Liquidity insufficient", RejectReason = reason };

        public static OrchestratorResult EdgeLost()
            => new() { Success = false, Message = "Edge degraded - trading paused" };

        public static OrchestratorResult ComplianceRejected()
            => new() { Success = false, Message = "Compliance rejection" };

        public static OrchestratorResult NotInitialized()
            => new() { Success = false, Message = "System not initialized" };
    }

    /// <summary>
    /// System status snapshot
    /// </summary>
    public class SystemStatus
    {
        public bool IsInitialized { get; set; }
        public int BarCount { get; set; }
        public double CurrentBid { get; set; }
        public double CurrentAsk { get; set; }
        public string RegimeStatus { get; set; } = "";
        public string ConnectionStatus { get; set; } = "";
        public string DrawdownStatus { get; set; } = "";
        public string ComplianceStatus { get; set; } = "";
        public string SafetyStatus { get; set; } = "";
        public string EdgeStatus { get; set; } = "";

        public override string ToString()
        {
            return $"Regime: {RegimeStatus} | Connection: {ConnectionStatus} | " +
                   $"Safety: {SafetyStatus} | Edge: {EdgeStatus}";
        }
    }

    /// <summary>
    /// Event log entry
    /// </summary>
    public class OrchestratorEvent
    {
        public DateTime Timestamp { get; set; }
        public string Category { get; set; } = "";
        public string Message { get; set; } = "";

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Category}: {Message}";
        }
    }
}
