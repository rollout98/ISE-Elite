namespace ISE.Integration
{
    /// <summary>
    /// NinjaTraderBridge - Bridges IntegrationOrchestrator to existing NinjaTrader adapter
    /// Acts as the glue between NT8 events (OnBar, OnLevel2Update) and the orchestrator
    /// </summary>
    public class NinjaTraderBridge
    {
        private IntegrationOrchestrator _orchestrator;
        private string _symbol = "";
        private double _accountEquity = 0;
        private ExecutionBridge _executionBridge;
        private bool _isStarted = false;

        public NinjaTraderBridge(ExecutionBridge executionBridge)
        {
            _orchestrator = new IntegrationOrchestrator();
            _executionBridge = executionBridge;
        }

        /// <summary>
        /// Start trading day
        /// Called from NT8 OnStartUp or first bar of day
        /// </summary>
        public void StartTradingDay(string symbol, double openingEquity)
        {
            _symbol = symbol;
            _accountEquity = openingEquity;
            _orchestrator.Initialize(symbol, openingEquity);
            _isStarted = true;
        }

        /// <summary>
        /// Process bar close event
        /// Called from NT8 OnBar event
        /// </summary>
        public void OnBarClose(
            double open,
            double high,
            double low,
            double close,
            double volume,
            DateTime barTime)
        {
            if (!_isStarted)
                return;

            try
            {
                var result = _orchestrator.ProcessBarClose(open, high, low, close, volume, barTime);

                // Route to execution if entry approved
                if (result.Success && result.Signal != null)
                {
                    switch (result.Signal.SignalType)
                    {
                        case SignalType.ENTRY:
                            ExecuteEntry(result.Signal, close);
                            break;
                        case SignalType.EXIT:
                            ExecuteExit(result.Signal, close);
                            break;
                    }
                }
                else if (!result.Success)
                {
                    // Log rejection reason
                    LogRejection(result.RejectReason);
                }
            }
            catch (Exception ex)
            {
                LogError($"OnBarClose error: {ex.Message}");
            }
        }

        /// <summary>
        /// Process Level 2 update event
        /// Called from NT8 OnLevel2Update event
        /// </summary>
        public void OnLevel2Update(
            double bidPrice,
            double askPrice,
            double bidVolume,
            double askVolume,
            double totalBidVolume,
            double totalAskVolume)
        {
            if (!_isStarted)
                return;

            try
            {
                _orchestrator.ProcessLevel2Update(
                    bidPrice,
                    askPrice,
                    bidVolume,
                    askVolume,
                    totalBidVolume,
                    totalAskVolume);
            }
            catch (Exception ex)
            {
                LogError($"OnLevel2Update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute entry order through existing NinjaTrader adapter
        /// </summary>
        private void ExecuteEntry(EntryExitSignal signal, double currentPrice)
        {
            try
            {
                // Route to existing NT8 execution bridge
                _executionBridge.PlaceOrder(
                    _symbol,
                    signal.Direction == TradeDirection.BUY ? OrderAction.BUY : OrderAction.SELL,
                    signal.ContractSize,
                    currentPrice,
                    signal.ProfitTarget,
                    signal.StopLoss);

                LogExecution($"ENTRY EXECUTED: {signal.Direction} {signal.ContractSize} @ {currentPrice:F2} | " +
                           $"Target: {signal.ProfitTarget:F2} | Stop: {signal.StopLoss:F2}");
            }
            catch (Exception ex)
            {
                LogError($"Entry execution error: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute exit order through existing NinjaTrader adapter
        /// </summary>
        private void ExecuteExit(EntryExitSignal signal, double currentPrice)
        {
            try
            {
                // Route to existing NT8 execution bridge
                _executionBridge.ClosePosition(
                    _symbol,
                    signal.ContractSize,
                    currentPrice);

                LogExecution($"EXIT EXECUTED: Close {signal.ContractSize} @ {currentPrice:F2} | P&L: ${signal.PnL:F2}");
            }
            catch (Exception ex)
            {
                LogError($"Exit execution error: {ex.Message}");
            }
        }

        /// <summary>
        /// End trading day
        /// Called from NT8 OnStopUp or end of day
        /// </summary>
        public void EndTradingDay()
        {
            if (!_isStarted)
                return;

            try
            {
                _orchestrator.EndSession();
                var summary = _orchestrator.GetSessionSummary();
                LogExecution($"SESSION CLOSED: Trades: {summary.TradeCount} | P&L: ${summary.SessionPnl:F2} | Win Rate: {summary.WinRate:P1}");
                _isStarted = false;
            }
            catch (Exception ex)
            {
                LogError($"EndTradingDay error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current system status
        /// </summary>
        public SystemStatus GetSystemStatus()
        {
            return _orchestrator.GetStatus();
        }

        /// <summary>
        /// Get event log
        /// </summary>
        public List<OrchestratorEvent> GetEventLog()
        {
            return _orchestrator.GetEventLog();
        }

        /// <summary>
        /// Get session summary
        /// </summary>
        public SessionSummary GetSessionSummary()
        {
            return _orchestrator.GetSessionSummary();
        }

        /// <summary>
        /// Check if bridge is active
        /// </summary>
        public bool IsActive => _isStarted;

        /// <summary>
        /// Log execution event
        /// </summary>
        private void LogExecution(string message)
        {
            // Would integrate with NinjaTrader logging
            System.Diagnostics.Debug.WriteLine($"[EXECUTION] {message}");
        }

        /// <summary>
        /// Log rejection reason
        /// </summary>
        private void LogRejection(string reason)
        {
            // Would integrate with NinjaTrader logging
            System.Diagnostics.Debug.WriteLine($"[REJECTED] {reason}");
        }

        /// <summary>
        /// Log error
        /// </summary>
        private void LogError(string message)
        {
            // Would integrate with NinjaTrader logging
            System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
        }

        public override string ToString()
        {
            return $"NTBridge: {_symbol} | Active: {_isActive} | Status: {GetSystemStatus()}";
        }
    }

    /// <summary>
    /// Execution bridge - interface to existing NinjaTrader execution layer
    /// Implements OrderAction and routing to existing execution adapter
    /// </summary>
    public interface ExecutionBridge
    {
        void PlaceOrder(string symbol, OrderAction action, int contracts, double entryPrice, double target, double stop);
        void ClosePosition(string symbol, int contracts, double price);
    }

    /// <summary>
    /// Order action (BUY or SELL)
    /// </summary>
    public enum OrderAction
    {
        BUY,
        SELL
    }
}
