namespace ISE.Compliance.Safety
{
    /// <summary>
    /// ConnectionMonitor - Detect NinjaTrader disconnections
    /// Prevents trades during outages or data staleness
    /// </summary>
    public class ConnectionMonitor
    {
        private DateTime _lastHeartbeat = DateTime.Now;
        private DateTime _lastLevel2Update = DateTime.Now;
        private DateTime _lastBarUpdate = DateTime.Now;
        private int _barCount = 0;
        private int _barCountLastCheck = 0;
        private DateTime _barCountCheckTime = DateTime.Now;
        private const int HeartbeatThresholdMs = 2000; // 2 seconds
        private const int DataStalenessThresholdMs = 1000; // 1 second
        private const int BarStaleThresholdMs = 60000; // 60 seconds

        /// <summary>
        /// Is NinjaTrader connected (heartbeat OK)?
        /// </summary>
        public bool IsConnected { get; private set; } = true;

        /// <summary>
        /// Time since last heartbeat (milliseconds)
        /// </summary>
        public int HeartbeatLatencyMs { get; private set; }

        /// <summary>
        /// Time since last Level 2 update (milliseconds)
        /// </summary>
        public int Level2LatencyMs { get; private set; }

        /// <summary>
        /// Time since last bar update (milliseconds)
        /// </summary>
        public int BarLatencyMs { get; private set; }

        /// <summary>
        /// Bars processed per minute (should be consistent)
        /// </summary>
        public double BarsPerMinute { get; private set; }

        /// <summary>
        /// Is data stale (Level 2 or bars not updating)?
        /// </summary>
        public bool IsDataStale { get; private set; }

        /// <summary>
        /// Reason for disconnection or staleness
        /// </summary>
        public string? DisconnectionReason { get; private set; }

        /// <summary>
        /// Record a heartbeat (call every 500ms from NT8 adapter)
        /// </summary>
        public void RecordHeartbeat()
        {
            _lastHeartbeat = DateTime.Now;
            IsConnected = true;
            DisconnectionReason = null;
        }

        /// <summary>
        /// Record a Level 2 update
        /// </summary>
        public void RecordLevel2Update()
        {
            _lastLevel2Update = DateTime.Now;
        }

        /// <summary>
        /// Record a bar update
        /// </summary>
        public void RecordBarUpdate()
        {
            _lastBarUpdate = DateTime.Now;
            _barCount++;

            // Calculate bars per minute
            var timeSinceLastCheck = (DateTime.Now - _barCountCheckTime).TotalSeconds;
            if (timeSinceLastCheck > 60)
            {
                var barsSinceLastCheck = _barCount - _barCountLastCheck;
                BarsPerMinute = (barsSinceLastCheck / timeSinceLastCheck) * 60;
                _barCountLastCheck = _barCount;
                _barCountCheckTime = DateTime.Now;
            }
        }

        /// <summary>
        /// Check connection status and data freshness
        /// Call this every bar to validate connectivity
        /// </summary>
        public void ValidateConnection()
        {
            var now = DateTime.Now;

            // Check heartbeat
            HeartbeatLatencyMs = (int)(now - _lastHeartbeat).TotalMilliseconds;
            if (HeartbeatLatencyMs > HeartbeatThresholdMs)
            {
                IsConnected = false;
                DisconnectionReason = $"No heartbeat for {HeartbeatLatencyMs}ms (threshold: {HeartbeatThresholdMs}ms)";
                return;
            }

            // Check Level 2 freshness
            Level2LatencyMs = (int)(now - _lastLevel2Update).TotalMilliseconds;
            if (Level2LatencyMs > DataStalenessThresholdMs)
            {
                IsDataStale = true;
                DisconnectionReason = $"Level 2 data stale for {Level2LatencyMs}ms";
                return;
            }

            // Check bar freshness
            BarLatencyMs = (int)(now - _lastBarUpdate).TotalMilliseconds;
            if (BarLatencyMs > BarStaleThresholdMs)
            {
                IsDataStale = true;
                DisconnectionReason = $"Bars stale for {BarLatencyMs}ms";
                return;
            }

            // All good
            IsConnected = true;
            IsDataStale = false;
            DisconnectionReason = null;
        }

        /// <summary>
        /// Can we execute trades?
        /// Only if connected AND data is fresh
        /// </summary>
        public bool CanExecuteTrades()
        {
            ValidateConnection();
            return IsConnected && !IsDataStale;
        }

        /// <summary>
        /// Reset monitoring (for session start)
        /// </summary>
        public void Reset()
        {
            _lastHeartbeat = DateTime.Now;
            _lastLevel2Update = DateTime.Now;
            _lastBarUpdate = DateTime.Now;
            _barCount = 0;
            _barCountLastCheck = 0;
            _barCountCheckTime = DateTime.Now;

            IsConnected = true;
            IsDataStale = false;
            DisconnectionReason = null;

            HeartbeatLatencyMs = 0;
            Level2LatencyMs = 0;
            BarLatencyMs = 0;
            BarsPerMinute = 0;
        }

        /// <summary>
        /// Get detailed status for logging
        /// </summary>
        public string GetStatus()
        {
            ValidateConnection();

            return $"Connected: {IsConnected} | DataStale: {IsDataStale} | " +
                   $"Heartbeat: {HeartbeatLatencyMs}ms | " +
                   $"Level2: {Level2LatencyMs}ms | " +
                   $"Bars: {BarLatencyMs}ms ({BarsPerMinute:F1} bars/min) | " +
                   (DisconnectionReason != null ? $"Reason: {DisconnectionReason}" : "OK");
        }

        public override string ToString()
        {
            ValidateConnection();
            return $"Connection: {(IsConnected ? "OK" : "DISCONNECTED")} | Data: {(IsDataStale ? "STALE" : "FRESH")}";
        }
    }
}
