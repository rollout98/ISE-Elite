namespace ISE.Compliance.Safety
{
    /// <summary>
    /// PositionReconciliation - Verify tracked positions match actual NinjaTrader positions
    /// Prevents trading if discrepancies are detected (data integrity check)
    /// </summary>
    public class PositionReconciliation
    {
        private int _trackedPosition = 0; // Shares we think we have
        private int _actualPosition = 0; // Shares NT8 actually has
        private DateTime _lastReconcileTime = DateTime.MinValue;
        private List<ReconciliationEvent> _reconciliationHistory = new();
        private const int MaxAcceptableDiscrepancy = 0; // Zero tolerance

        /// <summary>
        /// Current tracked position (what we calculated)
        /// </summary>
        public int TrackedPosition
        {
            get => _trackedPosition;
            set => _trackedPosition = value;
        }

        /// <summary>
        /// Current actual position (from NinjaTrader)
        /// </summary>
        public int ActualPosition
        {
            get => _actualPosition;
            set => _actualPosition = value;
        }

        /// <summary>
        /// Discrepancy if any (tracked - actual)
        /// </summary>
        public int Discrepancy => _trackedPosition - _actualPosition;

        /// <summary>
        /// Are positions reconciled?
        /// </summary>
        public bool IsReconciled { get; private set; } = true;

        /// <summary>
        /// Reason for any discrepancy
        /// </summary>
        public string? DiscrepancyReason { get; private set; }

        /// <summary>
        /// Last reconciliation time
        /// </summary>
        public DateTime LastReconcileTime => _lastReconcileTime;

        /// <summary>
        /// Can we trade? Only if positions match
        /// </summary>
        public bool CanTrade => IsReconciled;

        /// <summary>
        /// Record an entry (increases position)
        /// </summary>
        public void RecordEntry(int contractsEntered)
        {
            _trackedPosition += contractsEntered;
        }

        /// <summary>
        /// Record an exit (decreases position)
        /// </summary>
        public void RecordExit(int contractsExited)
        {
            _trackedPosition -= contractsExited;
        }

        /// <summary>
        /// Update actual position from NinjaTrader
        /// </summary>
        public void UpdateActualPosition(int ntPosition)
        {
            _actualPosition = ntPosition;
            Reconcile();
        }

        /// <summary>
        /// Perform reconciliation check
        /// </summary>
        private void Reconcile()
        {
            _lastReconcileTime = DateTime.Now;

            int discrepancy = Discrepancy;

            if (Math.Abs(discrepancy) <= MaxAcceptableDiscrepancy)
            {
                // Positions match
                IsReconciled = true;
                DiscrepancyReason = null;

                _reconciliationHistory.Add(new ReconciliationEvent
                {
                    Timestamp = DateTime.Now,
                    TrackedPosition = _trackedPosition,
                    ActualPosition = _actualPosition,
                    Discrepancy = 0,
                    Status = "OK"
                });
            }
            else
            {
                // Mismatch detected
                IsReconciled = false;
                DiscrepancyReason = $"Position mismatch: Tracked={_trackedPosition}, Actual={_actualPosition}, Diff={discrepancy}";

                _reconciliationHistory.Add(new ReconciliationEvent
                {
                    Timestamp = DateTime.Now,
                    TrackedPosition = _trackedPosition,
                    ActualPosition = _actualPosition,
                    Discrepancy = discrepancy,
                    Status = "MISMATCH"
                });
            }
        }

        /// <summary>
        /// Attempt auto-correction (align tracked to actual)
        /// </summary>
        public bool AttemptAutoCorrection()
        {
            if (IsReconciled)
                return true; // Already correct

            // Align tracked to actual
            int correction = Discrepancy;
            _trackedPosition = _actualPosition;

            IsReconciled = true;
            DiscrepancyReason = null;

            _reconciliationHistory.Add(new ReconciliationEvent
            {
                Timestamp = DateTime.Now,
                TrackedPosition = _trackedPosition,
                ActualPosition = _actualPosition,
                Discrepancy = 0,
                Status = "AUTO_CORRECTED"
            });

            return true;
        }

        /// <summary>
        /// Get detailed status
        /// </summary>
        public string GetStatus()
        {
            return $"Tracked: {_trackedPosition} | Actual: {_actualPosition} | " +
                   $"Discrepancy: {Discrepancy} | " +
                   $"Status: {(IsReconciled ? "OK" : "MISMATCH")} | " +
                   (DiscrepancyReason != null ? $"Reason: {DiscrepancyReason}" : "");
        }

        /// <summary>
        /// Get reconciliation history
        /// </summary>
        public List<ReconciliationEvent> GetHistory()
        {
            return _reconciliationHistory.ToList();
        }

        /// <summary>
        /// Reset for new session
        /// </summary>
        public void Reset()
        {
            _trackedPosition = 0;
            _actualPosition = 0;
            IsReconciled = true;
            DiscrepancyReason = null;
            _lastReconcileTime = DateTime.MinValue;
            _reconciliationHistory.Clear();
        }

        public override string ToString()
        {
            return $"Position: Tracked={_trackedPosition} | Actual={_actualPosition} | " +
                   $"Status: {(IsReconciled ? "OK" : "MISMATCH")}";
        }
    }

    /// <summary>
    /// Reconciliation event for audit trail
    /// </summary>
    public class ReconciliationEvent
    {
        public DateTime Timestamp { get; set; }
        public int TrackedPosition { get; set; }
        public int ActualPosition { get; set; }
        public int Discrepancy { get; set; }
        public string Status { get; set; } = "";

        public override string ToString()
        {
            return $"[{Timestamp:HH:mm:ss}] Tracked={TrackedPosition}, Actual={ActualPosition}, " +
                   $"Diff={Discrepancy}, Status={Status}";
        }
    }
}
