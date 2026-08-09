namespace ISE.Integration
{
    using ISE.Compliance.Safety;
    using ISE.Compliance;

    /// <summary>
    /// ExecutionMonitor - Real-time monitoring of all 8 safety components
    /// Tracks component health, triggers alerts, maintains audit trail
    /// </summary>
    public class ExecutionMonitor
    {
        private readonly ConnectionMonitor _connectionMonitor;
        private readonly DrawdownController _drawdownController;
        private readonly PositionReconciliation _positionReconciliation;
        private readonly LiquidityValidator _liquidityValidator;
        private readonly VolatilityCircuitBreaker _volatilityCircuitBreaker;
        private readonly SlippageTracker _slippageTracker;
        private readonly NewsEventFilter _newsEventFilter;
        private readonly EdgeDegradationDetector _edgeDetector;

        private List<HealthAlert> _alerts = new();
        private Dictionary<string, ComponentHealth> _componentHealth = new();
        private bool _isMonitoring = false;

        public ExecutionMonitor(
            ConnectionMonitor connectionMonitor,
            DrawdownController drawdownController,
            PositionReconciliation positionReconciliation,
            LiquidityValidator liquidityValidator,
            VolatilityCircuitBreaker volatilityCircuitBreaker,
            SlippageTracker slippageTracker,
            NewsEventFilter newsEventFilter,
            EdgeDegradationDetector edgeDetector)
        {
            _connectionMonitor = connectionMonitor;
            _drawdownController = drawdownController;
            _positionReconciliation = positionReconciliation;
            _liquidityValidator = liquidityValidator;
            _volatilityCircuitBreaker = volatilityCircuitBreaker;
            _slippageTracker = slippageTracker;
            _newsEventFilter = newsEventFilter;
            _edgeDetector = edgeDetector;

            InitializeComponentHealth();
        }

        /// <summary>
        /// Start monitoring
        /// </summary>
        public void StartMonitoring()
        {
            _isMonitoring = true;
            _alerts.Clear();
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public void StopMonitoring()
        {
            _isMonitoring = false;
        }

        /// <summary>
        /// Perform health check on all components
        /// </summary>
        public HealthReport PerformHealthCheck()
        {
            if (!_isMonitoring)
                return HealthReport.NotMonitoring();

            var report = new HealthReport { Timestamp = DateTime.Now };

            // Check each component
            CheckConnection(report);
            CheckDrawdown(report);
            CheckPosition(report);
            CheckLiquidity(report);
            CheckVolatility(report);
            CheckSlippage(report);
            CheckNews(report);
            CheckEdge(report);

            // Overall status
            report.OverallStatus = DetermineOverallStatus();
            report.ComponentsHealthy = report.Details.Count(d => d.IsHealthy);
            report.ComponentsTotal = report.Details.Count;

            return report;
        }

        /// <summary>
        /// Check connection component
        /// </summary>
        private void CheckConnection(HealthReport report)
        {
            bool isHealthy = _connectionMonitor.CanExecuteTrades();

            report.Details.Add(new ComponentDetail
            {
                Component = "Connection Monitor",
                IsHealthy = isHealthy,
                Status = isHealthy ? "CONNECTED" : "DISCONNECTED",
                Message = _connectionMonitor.DisconnectionReason ?? "OK"
            });

            if (!isHealthy)
                AddAlert("CONNECTION", AlertSeverity.CRITICAL, $"Disconnected: {_connectionMonitor.DisconnectionReason}");
        }

        /// <summary>
        /// Check drawdown component
        /// </summary>
        private void CheckDrawdown(HealthReport report)
        {
            bool isHealthy = _drawdownController.CanEnterNewTrade();

            report.Details.Add(new ComponentDetail
            {
                Component = "Drawdown Controller",
                IsHealthy = isHealthy,
                Status = isHealthy ? "GREEN" : "EXCEEDED",
                Message = _drawdownController.GetStatus()
            });

            if (!isHealthy)
                AddAlert("DRAWDOWN", AlertSeverity.WARNING, "Drawdown limit reached");
        }

        /// <summary>
        /// Check position reconciliation component
        /// </summary>
        private void CheckPosition(HealthReport report)
        {
            bool isHealthy = _positionReconciliation.IsReconciled;

            report.Details.Add(new ComponentDetail
            {
                Component = "Position Reconciliation",
                IsHealthy = isHealthy,
                Status = isHealthy ? "MATCHED" : "MISMATCH",
                Message = _positionReconciliation.GetStatus()
            });

            if (!isHealthy)
                AddAlert("POSITION", AlertSeverity.CRITICAL, $"Position mismatch: {_positionReconciliation.DiscrepancyReason}");
        }

        /// <summary>
        /// Check liquidity component
        /// </summary>
        private void CheckLiquidity(HealthReport report)
        {
            bool isHealthy = _liquidityValidator.IsLiquidEnoughForEntry;
            bool isWide = _liquidityValidator.IsSpreadWide();

            report.Details.Add(new ComponentDetail
            {
                Component = "Liquidity Validator",
                IsHealthy = isHealthy,
                Status = isHealthy ? (isWide ? "WIDE_SPREAD" : "OK") : "THIN",
                Message = _liquidityValidator.GetStatus()
            });

            if (isWide && isHealthy)
                AddAlert("LIQUIDITY", AlertSeverity.INFO, $"Spread wide: {_liquidityValidator.SpreadTicks:F2} ticks");

            if (!isHealthy)
                AddAlert("LIQUIDITY", AlertSeverity.WARNING, _liquidityValidator.RejectionReason ?? "Insufficient liquidity");
        }

        /// <summary>
        /// Check volatility component
        /// </summary>
        private void CheckVolatility(HealthReport report)
        {
            bool isHealthy = _volatilityCircuitBreaker.CanEnterNewTrades();

            report.Details.Add(new ComponentDetail
            {
                Component = "Volatility Circuit Breaker",
                IsHealthy = isHealthy,
                Status = isHealthy ? "NORMAL" : "TRIPPED",
                Message = _volatilityCircuitBreaker.GetStatus()
            });

            if (!isHealthy)
                AddAlert("VOLATILITY", AlertSeverity.WARNING, $"Circuit breaker tripped: {_volatilityCircuitBreaker.TripReason}");
        }

        /// <summary>
        /// Check slippage component
        /// </summary>
        private void CheckSlippage(HealthReport report)
        {
            bool isHealthy = !_slippageTracker.IsSlippageDegraded;

            report.Details.Add(new ComponentDetail
            {
                Component = "Slippage Tracker",
                IsHealthy = isHealthy,
                Status = isHealthy ? "GOOD" : "DEGRADED",
                Message = _slippageTracker.GetStatus()
            });

            if (!isHealthy)
                AddAlert("SLIPPAGE", AlertSeverity.INFO, $"Slippage quality degraded: {_slippageTracker.SlippageQuality:F2}");
        }

        /// <summary>
        /// Check news event filter component
        /// </summary>
        private void CheckNews(HealthReport report)
        {
            bool isHealthy = _newsEventFilter.CanTrade();

            report.Details.Add(new ComponentDetail
            {
                Component = "News Event Filter",
                IsHealthy = isHealthy,
                Status = isHealthy ? "CLEAR" : "BLACKOUT",
                Message = _newsEventFilter.GetStatus()
            });

            if (!isHealthy)
                AddAlert("NEWS", AlertSeverity.INFO, _newsEventFilter.BlackoutReason ?? "In news blackout");
        }

        /// <summary>
        /// Check edge degradation component
        /// </summary>
        private void CheckEdge(HealthReport report)
        {
            bool isHealthy = _edgeDetector.CanTrade();

            report.Details.Add(new ComponentDetail
            {
                Component = "Edge Degradation Detector",
                IsHealthy = isHealthy,
                Status = isHealthy ? "ACTIVE" : "PAUSED",
                Message = _edgeDetector.GetStatus()
            });

            if (!isHealthy)
                AddAlert("EDGE", AlertSeverity.WARNING, $"Edge lost - win rate {_edgeDetector.CurrentWinRate:P1} below threshold");
        }

        /// <summary>
        /// Determine overall system status
        /// </summary>
        private string DetermineOverallStatus()
        {
            // CRITICAL = any critical failures
            if (_alerts.Any(a => a.Severity == AlertSeverity.CRITICAL))
                return "CRITICAL";

            // WARNING = multiple warnings
            if (_alerts.Count(a => a.Severity == AlertSeverity.WARNING) >= 2)
                return "WARNING";

            // INFO = only info level
            if (_alerts.Any(a => a.Severity == AlertSeverity.INFO))
                return "INFO";

            return "HEALTHY";
        }

        /// <summary>
        /// Add alert
        /// </summary>
        private void AddAlert(string component, AlertSeverity severity, string message)
        {
            _alerts.Add(new HealthAlert
            {
                Timestamp = DateTime.Now,
                Component = component,
                Severity = severity,
                Message = message
            });
        }

        /// <summary>
        /// Get all alerts
        /// </summary>
        public List<HealthAlert> GetAlerts()
        {
            return _alerts.ToList();
        }

        /// <summary>
        /// Get alerts of specific severity
        /// </summary>
        public List<HealthAlert> GetAlerts(AlertSeverity severity)
        {
            return _alerts.Where(a => a.Severity == severity).ToList();
        }

        /// <summary>
        /// Initialize component health tracking
        /// </summary>
        private void InitializeComponentHealth()
        {
            _componentHealth.Add("Connection", new ComponentHealth { Name = "Connection Monitor" });
            _componentHealth.Add("Drawdown", new ComponentHealth { Name = "Drawdown Controller" });
            _componentHealth.Add("Position", new ComponentHealth { Name = "Position Reconciliation" });
            _componentHealth.Add("Liquidity", new ComponentHealth { Name = "Liquidity Validator" });
            _componentHealth.Add("Volatility", new ComponentHealth { Name = "Volatility Circuit Breaker" });
            _componentHealth.Add("Slippage", new ComponentHealth { Name = "Slippage Tracker" });
            _componentHealth.Add("News", new ComponentHealth { Name = "News Event Filter" });
            _componentHealth.Add("Edge", new ComponentHealth { Name = "Edge Degradation Detector" });
        }

        public override string ToString()
        {
            var report = PerformHealthCheck();
            return $"ExecutionMonitor: {report.ComponentsHealthy}/{report.ComponentsTotal} healthy | " +
                   $"Status: {report.OverallStatus} | Alerts: {_alerts.Count}";
        }
    }

    /// <summary>
    /// Health report
    /// </summary>
    public class HealthReport
    {
        public DateTime Timestamp { get; set; }
        public string OverallStatus { get; set; } = "UNKNOWN";
        public int ComponentsHealthy { get; set; }
        public int ComponentsTotal { get; set; }
        public List<ComponentDetail> Details { get; set; } = new();

        public static HealthReport NotMonitoring()
            => new() { OverallStatus = "NOT_MONITORING" };

        public override string ToString()
        {
            return $"Health: {ComponentsHealthy}/{ComponentsTotal} | Status: {OverallStatus}";
        }
    }

    /// <summary>
    /// Component health detail
    /// </summary>
    public class ComponentDetail
    {
        public string Component { get; set; } = "";
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Health alert
    /// </summary>
    public class HealthAlert
    {
        public DateTime Timestamp { get; set; }
        public string Component { get; set; } = "";
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = "";

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] {Severity} ({Component}): {Message}";
        }
    }

    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        INFO,
        WARNING,
        CRITICAL
    }

    /// <summary>
    /// Component health tracking
    /// </summary>
    public class ComponentHealth
    {
        public string Name { get; set; } = "";
        public DateTime LastCheck { get; set; }
        public bool IsHealthy { get; set; } = true;
        public int FailureCount { get; set; }
        public int SuccessCount { get; set; }
    }
}
