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
            string exitReason = "UNKNOWN",
            DateTime tradingDay = default)
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
            TradingDay = tradingDay == default ? exitTimeUtc.Date : tradingDay;
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

        /// <summary>
        /// Exchange trading day this trade CLOSED in, taken from the data feed's
        /// tradingDay column. NOT the UTC calendar date: the futures session opens
        /// 17:00 CT and UTC midnight falls at 19:00 CT, so grouping by UTC date files
        /// the first two hours of every session under the previous day.
        /// </summary>
        public DateTime TradingDay { get; }
    }
}
