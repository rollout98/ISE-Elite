namespace ISE.BacktestHarness.Models
{
    /// <summary>
    /// Parameter configuration for a single backtest run
    /// </summary>
    public sealed class BacktestConfiguration
    {
        public BacktestConfiguration(
            int configId,
            int maximumContracts,
            double adaptiveRiskMultiplier,
            double stopDistanceRisk,
            double liquidityCapacity,
            bool useTrailingStop = false,
            int trendFilterBars = 0,
            double breakEvenMovePoints = 0,
            bool holdToReversal = false,
            decimal profitFloorDollars = 0m,
            decimal dailyLossLimitDollars = 0m,
            decimal dailyProfitTargetDollars = 0m)
        {
            ConfigId = configId;
            MaximumContracts = maximumContracts;
            AdaptiveRiskMultiplier = adaptiveRiskMultiplier;
            StopDistanceRisk = stopDistanceRisk;
            LiquidityCapacity = liquidityCapacity;
            UseTrailingStop = useTrailingStop;
            TrendFilterBars = trendFilterBars;
            BreakevenMovePoints = breakEvenMovePoints;
            HoldToReversal = holdToReversal;
            ProfitFloorDollars = profitFloorDollars;
            DailyLossLimitDollars = dailyLossLimitDollars;
            DailyProfitTargetDollars = dailyProfitTargetDollars;
        }

        public int ConfigId { get; }
        public int MaximumContracts { get; }
        public double AdaptiveRiskMultiplier { get; }
        public double StopDistanceRisk { get; }
        public double LiquidityCapacity { get; }

        /// <summary>
        /// When true there is NO fixed profit target. The stop trails the best price
        /// reached by StopDistanceRisk points, so the trade rides a run of unknown
        /// size until it reverses. A fixed target cannot capture a 600-900 tick move
        /// because the move's size is not knowable at entry.
        /// </summary>
        public bool UseTrailingStop { get; }

        /// <summary>
        /// Higher-timeframe gate, in 1-minute bars. 0 disables it. The 1-min entry
        /// only fires when it agrees with this slower trend, which is what separates
        /// the day's real move from the ~72 wiggles a day that look the same to a
        /// short-memory crossover.
        /// </summary>
        public int TrendFilterBars { get; }

        /// <summary>
        /// Breakeven move in points. Once profit reaches this level, stop is moved to entry price.
        /// 0 = disabled. Typical: 62.5 to 75 points (250-300 ticks on MNQ).
        /// </summary>
        public double BreakevenMovePoints { get; }

        /// <summary>
        /// Devon's live method. There is NO profit target: the position is held until
        /// the opposite VectorFlow signal fires ("exit governs entry"). Any same-side
        /// signal while in a trade is ignored. Combine with ProfitFloorDollars to lock
        /// a floor once the trade is meaningfully in profit.
        /// </summary>
        public bool HoldToReversal { get; }

        /// <summary>
        /// Once unrealized P&amp;L on the position reaches this dollar amount, the stop
        /// moves to the price that locks it in. 0 disables. Expressed in DOLLARS, not
        /// points, because the floor is a account-level goal ($500) and the point
        /// equivalent differs per instrument and contract count.
        /// </summary>
        public decimal ProfitFloorDollars { get; }

        /// <summary>
        /// Daily circuit breaker, in DOLLARS as a positive number. Once realized P&amp;L
        /// for the trading day reaches -this, the open position is closed and no new
        /// entries are taken until the next trading day. 0 disables.
        /// Bounds the worst day, which a per-trade stop cannot do: three stop-outs in
        /// one session compound into a loss no single stop was sized for.
        /// </summary>
        public decimal DailyLossLimitDollars { get; }

        /// <summary>
        /// Daily profit halt, in DOLLARS. Once realized P&amp;L for the trading day
        /// reaches this, close up and take no further entries until the next session.
        /// 0 disables. This CAPS the upside while leaving the downside uncapped, so it
        /// only pays if the alternative was giving the gains back later in the day.
        /// </summary>
        public decimal DailyProfitTargetDollars { get; }

        public override string ToString()
        {
            return $"Config{ConfigId}: Contracts={MaximumContracts}, " +
                   $"StopDist={StopDistanceRisk:F2}, " +
                   $"MaxHold={LiquidityCapacity:F0}bars, " +
                   (HoldToReversal
                       ? "Exit=REVERSAL"
                       : UseTrailingStop
                           ? $"Exit=TRAIL {StopDistanceRisk:F0}pt"
                           : $"Exit=FIXED {StopDistanceRisk * AdaptiveRiskMultiplier:F0}pt") +
                   (ProfitFloorDollars > 0 ? $", Floor=${ProfitFloorDollars:F0}" : "") +
                   (DailyLossLimitDollars > 0 ? $", DayStop=${DailyLossLimitDollars:F0}" : "") +
                   (DailyProfitTargetDollars > 0 ? $", DayGoal=${DailyProfitTargetDollars:F0}" : "") +
                   (TrendFilterBars > 0 ? $", Filter={TrendFilterBars}bar" : ", Filter=OFF") +
                   (BreakevenMovePoints > 0 ? $", BE={BreakevenMovePoints:F1}pt" : "");
        }
    }
}
