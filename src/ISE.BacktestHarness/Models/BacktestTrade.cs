using System;

namespace ISE.BacktestHarness.Models
{
    /// <summary>
    /// Represents a single executed trade in backtest
    /// </summary>
    public sealed class BacktestTrade
    {
        public BacktestTrade(
            DateTime entryTimeUtc,
            DateTime exitTimeUtc,
            string direction,
            decimal entryPrice,
            decimal exitPrice,
            int contracts,
            decimal pnl,
            decimal slippage,
            string exitReason = "UNKNOWN")
        {
            EntryTimeUtc = entryTimeUtc;
            ExitTimeUtc = exitTimeUtc;
            Direction = direction; // LONG or SHORT
            EntryPrice = entryPrice;
            ExitPrice = exitPrice;
            Contracts = contracts;
            PnL = pnl;
            Slippage = slippage;
            ExitReason = exitReason;
        }

        public DateTime EntryTimeUtc { get; }
        public DateTime ExitTimeUtc { get; }
        public string Direction { get; }
        public decimal EntryPrice { get; }
        public decimal ExitPrice { get; }
        public int Contracts { get; }
        public decimal PnL { get; }
        public decimal Slippage { get; }
        public bool IsWin => PnL > 0;

        /// <summary>
        /// What actually closed this trade: REVERSAL (opposing VectorFlow signal),
        /// STOP (initial stop or locked floor), TARGET, TIMECAP (max-hold reached),
        /// or ENDOFDATA. Without this we cannot tell a hold-to-reversal result from a
        /// time-exit result wearing the same label.
        /// </summary>
        public string ExitReason { get; }
    }
}
