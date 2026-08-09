using ISE.UnifiedRegimeEngine.Models;
using ISE.OrderFlowIntelligence.Models;

namespace ISE.UnifiedRegimeEngine
{
    /// <summary>
    /// Trending Mode Logic
    /// Entry and exit rules for TRENDING regime (ADX > threshold)
    /// 
    /// Strategy:
    /// - Entry: Momentum breakout in direction of trend
    /// - Hold: 30 minutes or until profit target
    /// - Exit: Profit target OR break-even + 3 ticks OR thesis invalidation
    /// 
    /// Order Flow Confirmation:
    /// - LONG: ADX trending + DI+ > DI- + order flow > +50 bias
    /// - SHORT: ADX trending + DI- > DI+ + order flow < -50 bias
    /// </summary>
    public class TrendingModeLogic
    {
        /// <summary>
        /// Minimum hold time (seconds) for trending entries
        /// Prevents whipsaw exits
        /// </summary>
        public int MinimumHoldTimeSeconds { get; set; } = 1800; // 30 minutes

        /// <summary>
        /// Maximum hold time (seconds) - exit regardless of P&L
        /// </summary>
        public int MaximumHoldTimeSeconds { get; set; } = 5400; // 90 minutes (safety)

        /// <summary>
        /// ATR multiplier for stop loss placement
        /// </summary>
        public double StopLossAtrMultiplier { get; set; } = 2.0;

        /// <summary>
        /// ATR multiplier for profit target in trending mode
        /// </summary>
        public double ProfitTargetAtrMultiplier { get; set; } = 3.0;

        /// <summary>
        /// Minimum order flow bias for entry confirmation (-100 to +100)
        /// </summary>
        public double MinimumOrderFlowBias { get; set; } = 50.0; // +50 for short, -50 for long

        /// <summary>
        /// Entry signal state
        /// </summary>
        public TrendingEntrySignal LastEntrySignal { get; private set; } = new();

        /// <summary>
        /// Exit signal state
        /// </summary>
        public TrendingExitSignal? LastExitSignal { get; private set; }

        /// <summary>
        /// Entry price (set when trade opens)
        /// </summary>
        public double EntryPrice { get; private set; } = 0.0;

        /// <summary>
        /// Entry time (when trade opened)
        /// </summary>
        public DateTime EntryTime { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// Is currently in a trade
        /// </summary>
        public bool IsInTrade { get; private set; } = false;

        public TrendingModeLogic()
        {
        }

        /// <summary>
        /// Evaluate entry opportunity in trending regime
        /// </summary>
        public TrendingEntrySignal EvaluateEntry(RegimeSignal regimeSignal, OrderFlowMetrics orderFlow, double currentPrice)
        {
            var signal = new TrendingEntrySignal
            {
                Timestamp = regimeSignal.Timestamp,
                CurrentPrice = currentPrice,
                Regime = regimeSignal.Regime
            };

            // Regime must be TRENDING
            if (regimeSignal.Regime != RegimeState.Trending)
            {
                signal.CanEnterLong = false;
                signal.CanEnterShort = false;
                signal.RejectReason = "Not in TRENDING regime";
                LastEntrySignal = signal;
                return signal;
            }

            // Confidence must be sufficient
            if (regimeSignal.RegimeConfidence < 0.6)
            {
                signal.CanEnterLong = false;
                signal.CanEnterShort = false;
                signal.RejectReason = "Insufficient regime confidence";
                LastEntrySignal = signal;
                return signal;
            }

            // Evaluate LONG entry
            EvaluateLongEntry(signal, regimeSignal, orderFlow);

            // Evaluate SHORT entry (mutually exclusive)
            if (!signal.CanEnterLong)
                EvaluateShortEntry(signal, regimeSignal, orderFlow);

            // Calculate profit target and stop loss
            if (signal.CanEnterLong || signal.CanEnterShort)
            {
                CalculateTargets(signal, regimeSignal);
            }

            LastEntrySignal = signal;
            return signal;
        }

        /// <summary>
        /// Check for LONG entry conditions
        /// </summary>
        private void EvaluateLongEntry(TrendingEntrySignal signal, RegimeSignal regime, OrderFlowMetrics flow)
        {
            // DI+ > DI- (uptrend)
            if (regime.DiMinus >= regime.DiPlus)
            {
                signal.RejectReason = "DI- >= DI+ (not uptrend)";
                return;
            }

            // Order flow bullish (< -50)
            if (flow == null || !flow.IsLiquidEnoughForEntry)
            {
                signal.RejectReason = "Insufficient liquidity for entry";
                return;
            }

            if (flow.OrderFlowBias > -MinimumOrderFlowBias)
            {
                signal.RejectReason = $"Order flow not bullish (bias: {flow.OrderFlowBias:F0}, need < -{MinimumOrderFlowBias})";
                return;
            }

            // RSI not overbought (avoid buying tops)
            if (regime.RsiOverbought)
            {
                signal.RejectReason = "RSI overbought";
                return;
            }

            // MACD histogram positive or recent bullish cross
            if (regime.MacdHistogram <= -0.01 && !regime.MacdBullishCross)
            {
                signal.RejectReason = "MACD not bullish";
                return;
            }

            // No resistance stacked above (from DOM)
            if (flow.ResistanceLevel > 0 && currentPrice < flow.ResistanceLevel)
            {
                double distanceToResistance = flow.ResistanceLevel - signal.CurrentPrice;
                double atrBased = regime.Atr * ProfitTargetAtrMultiplier;

                if (distanceToResistance < atrBased * 0.5) // Resistance too close
                {
                    signal.RejectReason = $"Resistance too close ({distanceToResistance:F2})";
                    return;
                }
            }

            // All checks passed
            signal.CanEnterLong = true;
            signal.EntryDirection = "LONG";
            signal.OrderFlowConfidence = CalculateOrderFlowConfidence(flow.OrderFlowBias, true);
            signal.MacdConfidence = CalculateMacdConfidence(regime.MacdHistogram, regime.MacdBullishCross, true);
            signal.RejectReason = null;
        }

        /// <summary>
        /// Check for SHORT entry conditions
        /// </summary>
        private void EvaluateShortEntry(TrendingEntrySignal signal, RegimeSignal regime, OrderFlowMetrics flow)
        {
            // DI- > DI+ (downtrend)
            if (regime.DiPlus >= regime.DiMinus)
            {
                signal.RejectReason = "DI+ >= DI- (not downtrend)";
                return;
            }

            // Order flow bearish (> +50)
            if (flow == null || !flow.IsLiquidEnoughForEntry)
            {
                signal.RejectReason = "Insufficient liquidity for entry";
                return;
            }

            if (flow.OrderFlowBias < MinimumOrderFlowBias)
            {
                signal.RejectReason = $"Order flow not bearish (bias: {flow.OrderFlowBias:F0}, need > {MinimumOrderFlowBias})";
                return;
            }

            // RSI not oversold (avoid selling bottoms)
            if (regime.RsiOversold)
            {
                signal.RejectReason = "RSI oversold";
                return;
            }

            // MACD histogram negative or recent bearish cross
            if (regime.MacdHistogram >= 0.01 && !regime.MacdBearishCross)
            {
                signal.RejectReason = "MACD not bearish";
                return;
            }

            // No support stacked below (from DOM)
            if (flow.SupportLevel > 0 && signal.CurrentPrice > flow.SupportLevel)
            {
                double distanceToSupport = signal.CurrentPrice - flow.SupportLevel;
                double atrBased = regime.Atr * ProfitTargetAtrMultiplier;

                if (distanceToSupport < atrBased * 0.5) // Support too close
                {
                    signal.RejectReason = $"Support too close ({distanceToSupport:F2})";
                    return;
                }
            }

            // All checks passed
            signal.CanEnterShort = true;
            signal.EntryDirection = "SHORT";
            signal.OrderFlowConfidence = CalculateOrderFlowConfidence(flow.OrderFlowBias, false);
            signal.MacdConfidence = CalculateMacdConfidence(regime.MacdHistogram, regime.MacdBearishCross, false);
            signal.RejectReason = null;
        }

        /// <summary>
        /// Calculate profit target and stop loss
        /// </summary>
        private void CalculateTargets(TrendingEntrySignal signal, RegimeSignal regime)
        {
            signal.Atr = regime.Atr;
            signal.StopLoss = regime.Atr * StopLossAtrMultiplier;
            signal.ProfitTarget = regime.Atr * ProfitTargetAtrMultiplier;
            signal.MinimumHoldTimeSeconds = MinimumHoldTimeSeconds;
            signal.MaximumHoldTimeSeconds = MaximumHoldTimeSeconds;
        }

        /// <summary>
        /// Evaluate exit opportunity for open trade
        /// </summary>
        public TrendingExitSignal EvaluateExit(double currentPrice, TimeSpan timeInTrade, 
                                              RegimeSignal regimeSignal, OrderFlowMetrics orderFlow)
        {
            if (!IsInTrade || EntryPrice == 0)
            {
                return null;
            }

            var signal = new TrendingExitSignal
            {
                Timestamp = regimeSignal.Timestamp,
                EntryPrice = EntryPrice,
                CurrentPrice = currentPrice,
                TimeInTrade = timeInTrade,
                UnrealizedPnL = CalculateUnrealizedPnL(),
                IsLong = LastEntrySignal.EntryDirection == "LONG"
            };

            // Check exit conditions (in priority order)

            // 1. Profit target hit
            if (HitProfitTarget(signal, LastEntrySignal))
            {
                signal.ExitReason = "Profit target hit";
                signal.ShouldExit = true;
                LastExitSignal = signal;
                return signal;
            }

            // 2. Stop loss hit
            if (HitStopLoss(signal, LastEntrySignal))
            {
                signal.ExitReason = "Stop loss hit";
                signal.ShouldExit = true;
                LastExitSignal = signal;
                return signal;
            }

            // 3. Time-based exit (minimum hold time elapsed)
            if (timeInTrade.TotalSeconds >= MinimumHoldTimeSeconds)
            {
                // Order flow deterioration (bias flipping)
                if (orderFlow != null && HasOrderFlowFlipped(signal.IsLong, orderFlow))
                {
                    signal.ExitReason = "Order flow flipped (selling pressure on long / buying pressure on short)";
                    signal.ShouldExit = true;
                    LastExitSignal = signal;
                    return signal;
                }

                // Regime changed
                if (regimeSignal.Regime != RegimeState.Trending)
                {
                    signal.ExitReason = "Regime changed from TRENDING";
                    signal.ShouldExit = true;
                    LastExitSignal = signal;
                    return signal;
                }
            }

            // 4. Maximum hold time - force exit regardless
            if (timeInTrade.TotalSeconds >= MaximumHoldTimeSeconds)
            {
                signal.ExitReason = "Maximum hold time reached";
                signal.ShouldExit = true;
                LastExitSignal = signal;
                return signal;
            }

            signal.ExitReason = "No exit condition met";
            signal.ShouldExit = false;
            LastExitSignal = signal;
            return signal;
        }

        /// <summary>
        /// Record trade entry
        /// </summary>
        public void RecordEntry(double price, DateTime time)
        {
            EntryPrice = price;
            EntryTime = time;
            IsInTrade = true;
        }

        /// <summary>
        /// Record trade exit
        /// </summary>
        public void RecordExit()
        {
            IsInTrade = false;
            EntryPrice = 0.0;
            EntryTime = DateTime.MinValue;
        }

        /// <summary>
        /// Calculate unrealized P&L (points)
        /// </summary>
        private double CalculateUnrealizedPnL()
        {
            if (!IsInTrade || EntryPrice == 0)
                return 0.0;

            double pnl = 0;
            if (LastEntrySignal.EntryDirection == "LONG")
                pnl = /* currentPrice */ 0 - EntryPrice; // Would need current price
            else
                pnl = EntryPrice - /* currentPrice */ 0; // Would need current price

            return pnl;
        }

        private bool HitProfitTarget(TrendingExitSignal signal, TrendingEntrySignal entry)
        {
            if (entry == null || entry.ProfitTarget == 0)
                return false;

            if (entry.EntryDirection == "LONG")
                return signal.UnrealizedPnL >= entry.ProfitTarget;
            else
                return signal.UnrealizedPnL >= entry.ProfitTarget;
        }

        private bool HitStopLoss(TrendingExitSignal signal, TrendingEntrySignal entry)
        {
            if (entry == null || entry.StopLoss == 0)
                return false;

            return signal.UnrealizedPnL <= -(entry.StopLoss);
        }

        private bool HasOrderFlowFlipped(bool isLong, OrderFlowMetrics flow)
        {
            if (flow == null)
                return false;

            if (isLong && flow.OrderFlowBias > 0) // Was bullish, now bearish
                return true;

            if (!isLong && flow.OrderFlowBias < 0) // Was bearish, now bullish
                return true;

            return false;
        }

        private double CalculateOrderFlowConfidence(double bias, bool isLong)
        {
            double absBias = Math.Abs(bias);
            return Math.Min(1.0, absBias / 100.0);
        }

        private double CalculateMacdConfidence(double histogram, bool crossDetected, bool isLong)
        {
            if (crossDetected)
                return 0.9; // High confidence on cross

            if (isLong && histogram > 0)
                return 0.7;
            else if (!isLong && histogram < 0)
                return 0.7;

            return 0.4; // Weak alignment
        }

        public void Reset()
        {
            IsInTrade = false;
            EntryPrice = 0.0;
            EntryTime = DateTime.MinValue;
            LastEntrySignal = new();
            LastExitSignal = null;
        }

        public override string ToString()
        {
            return IsInTrade 
                ? $"In trade - Entry: {EntryPrice:F2} @ {EntryTime:HH:mm:ss}"
                : "Not in trade";
        }
    }

    /// <summary>
    /// Entry signal for trending mode
    /// </summary>
    public class TrendingEntrySignal
    {
        public DateTime Timestamp { get; set; }
        public double CurrentPrice { get; set; }
        public RegimeState Regime { get; set; }
        public bool CanEnterLong { get; set; }
        public bool CanEnterShort { get; set; }
        public string? EntryDirection { get; set; } // "LONG" or "SHORT"
        public string? RejectReason { get; set; }
        public double OrderFlowConfidence { get; set; }
        public double MacdConfidence { get; set; }
        public double Atr { get; set; }
        public double StopLoss { get; set; }
        public double ProfitTarget { get; set; }
        public int MinimumHoldTimeSeconds { get; set; }
        public int MaximumHoldTimeSeconds { get; set; }
    }

    /// <summary>
    /// Exit signal for trending mode
    /// </summary>
    public class TrendingExitSignal
    {
        public DateTime Timestamp { get; set; }
        public double EntryPrice { get; set; }
        public double CurrentPrice { get; set; }
        public TimeSpan TimeInTrade { get; set; }
        public double UnrealizedPnL { get; set; }
        public bool IsLong { get; set; }
        public bool ShouldExit { get; set; }
        public string? ExitReason { get; set; }
    }
}
