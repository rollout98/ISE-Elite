namespace ISE.Compliance.Safety
{
    /// <summary>
    /// SlippageTracker - Monitor execution quality and slippage
    /// Tracks difference between intended and actual fill prices
    /// </summary>
    public class SlippageTracker
    {
        private List<SlippageRecord> _trades = new();
        private const int SmoothingPeriod = 20; // 20-trade rolling average
        private const double MaxAcceptableSlippage = 1.0; // 1 tick

        /// <summary>
        /// Average entry slippage in points
        /// </summary>
        public double AverageEntrySlippage { get; private set; }

        /// <summary>
        /// Average exit slippage in points
        /// </summary>
        public double AverageExitSlippage { get; private set; }

        /// <summary>
        /// Overall slippage quality score (0-1.0, 1.0 = zero slippage)
        /// </summary>
        public double SlippageQuality { get; private set; } = 1.0;

        /// <summary>
        /// Is slippage degraded?
        /// </summary>
        public bool IsSlippageDegraded { get; private set; } = false;

        /// <summary>
        /// Total trades tracked
        /// </summary>
        public int TradeCount => _trades.Count;

        /// <summary>
        /// Record an entry fill
        /// </summary>
        public void RecordEntryFill(
            double intendedPrice,
            double actualPrice,
            int contracts,
            DateTime timestamp)
        {
            var slippage = Math.Abs(actualPrice - intendedPrice);

            if (_trades.Count == 0 || _trades.Last().ExitPrice == null)
            {
                // New trade entry
                _trades.Add(new SlippageRecord
                {
                    EntryTime = timestamp,
                    IntendedEntryPrice = intendedPrice,
                    ActualEntryPrice = actualPrice,
                    EntrySlippage = slippage,
                    Contracts = contracts
                });
            }

            UpdateMetrics();
        }

        /// <summary>
        /// Record an exit fill
        /// </summary>
        public void RecordExitFill(
            double intendedPrice,
            double actualPrice,
            DateTime timestamp)
        {
            if (_trades.Count == 0)
                return;

            var lastTrade = _trades.Last();
            if (lastTrade.ExitPrice != null)
                return; // Trade already closed

            var slippage = Math.Abs(actualPrice - intendedPrice);

            lastTrade.ExitTime = timestamp;
            lastTrade.IntendedExitPrice = intendedPrice;
            lastTrade.ActualExitPrice = actualPrice;
            lastTrade.ExitSlippage = slippage;

            UpdateMetrics();
        }

        /// <summary>
        /// Calculate average slippage metrics
        /// </summary>
        private void UpdateMetrics()
        {
            if (_trades.Count == 0)
                return;

            // Get last 20 trades for smoothing
            int lookback = Math.Min(SmoothingPeriod, _trades.Count);
            var recentTrades = _trades.Skip(_trades.Count - lookback).ToList();

            // Entry slippage
            var completedTrades = recentTrades.Where(t => t.ExitPrice.HasValue).ToList();
            if (completedTrades.Count > 0)
            {
                AverageEntrySlippage = completedTrades.Average(t => t.EntrySlippage);
            }

            // Exit slippage
            if (completedTrades.Count > 0)
            {
                AverageExitSlippage = completedTrades.Average(t => t.ExitSlippage ?? 0);
            }

            // Quality score (inverse of slippage)
            double totalSlippage = AverageEntrySlippage + AverageExitSlippage;
            SlippageQuality = Math.Max(0, 1.0 - (totalSlippage / (MaxAcceptableSlippage * 2.0)));

            // Check if degraded
            IsSlippageDegraded = totalSlippage > MaxAcceptableSlippage;
        }

        /// <summary>
        /// Get slippage for last N trades
        /// </summary>
        public List<SlippageRecord> GetRecentSlippage(int trades = 20)
        {
            int startIndex = Math.Max(0, _trades.Count - trades);
            return _trades.Skip(startIndex).ToList();
        }

        /// <summary>
        /// Get detailed status
        /// </summary>
        public string GetStatus()
        {
            return $"Avg Entry Slippage: {AverageEntrySlippage:F3} pts | " +
                   $"Avg Exit Slippage: {AverageExitSlippage:F3} pts | " +
                   $"Quality Score: {SlippageQuality:F2} | " +
                   $"Trades: {TradeCount} | " +
                   $"Status: {(IsSlippageDegraded ? "DEGRADED" : "GOOD")}";
        }

        /// <summary>
        /// Get worst slippage trade
        /// </summary>
        public SlippageRecord? GetWorstSlippageTrade()
        {
            var completedTrades = _trades.Where(t => t.ExitPrice.HasValue).ToList();
            if (completedTrades.Count == 0)
                return null;

            var totalSlippage = completedTrades
                .OrderByDescending(t => (t.EntrySlippage + (t.ExitSlippage ?? 0)))
                .FirstOrDefault();

            return totalSlippage;
        }

        /// <summary>
        /// Reset for new session
        /// </summary>
        public void Reset()
        {
            _trades.Clear();
            AverageEntrySlippage = 0;
            AverageExitSlippage = 0;
            SlippageQuality = 1.0;
            IsSlippageDegraded = false;
        }

        public override string ToString()
        {
            return $"Slippage: Entry={AverageEntrySlippage:F3} | Exit={AverageExitSlippage:F3} | " +
                   $"Quality={SlippageQuality:F2} | Status: {(IsSlippageDegraded ? "DEGRADED" : "OK")}";
        }
    }

    /// <summary>
    /// Individual trade slippage record
    /// </summary>
    public class SlippageRecord
    {
        public DateTime EntryTime { get; set; }
        public double IntendedEntryPrice { get; set; }
        public double ActualEntryPrice { get; set; }
        public double EntrySlippage { get; set; }
        public int Contracts { get; set; }

        public DateTime? ExitTime { get; set; }
        public double? IntendedExitPrice { get; set; }
        public double? ActualExitPrice { get; set; }
        public double? ExitSlippage { get; set; }

        public double TotalSlippage => EntrySlippage + (ExitSlippage ?? 0);

        public override string ToString()
        {
            return $"Entry: {IntendedEntryPrice:F2} → {ActualEntryPrice:F2} ({EntrySlippage:F3}) | " +
                   $"Exit: {(IntendedExitPrice?.ToString("F2") ?? "—")} → " +
                   $"{(ActualExitPrice?.ToString("F2") ?? "—")} ({ExitSlippage:F3}) | " +
                   $"Total: {TotalSlippage:F3} pts";
        }
    }
}
