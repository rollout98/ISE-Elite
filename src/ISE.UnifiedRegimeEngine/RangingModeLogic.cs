using ISE.UnifiedRegimeEngine.Models;
using ISE.OrderFlowIntelligence.Models;

namespace ISE.UnifiedRegimeEngine
{
    /// <summary>
    /// Ranging Mode Logic
    /// Entry and exit rules for RANGING regime (ADX < threshold)
    /// 
    /// Strategy:
    /// - Entry: Mean reversion at support/resistance clusters (from DOM)
    /// - Hold: 3 minutes or until profit target
    /// - Exit: Profit target OR break-even + 2 ticks OR range breakout
    /// 
    /// Order Flow Confirmation:
    /// - LONG: Price at support + order flow turning bullish + buyers stepping in
    /// - SHORT: Price at resistance + order flow turning bearish + sellers stepping in
    /// </summary>
    public class RangingModeLogic
    {
        /// <summary>
        /// Minimum hold time (seconds) for ranging entries
        /// Prevents overtrading
        /// </summary>
        public int MinimumHoldTimeSeconds { get; set; } = 180; // 3 minutes

        /// <summary>
        /// Maximum hold time (seconds) - exit regardless of P&L
        /// </summary>
        public int MaximumHoldTimeSeconds { get; set; } = 600; // 10 minutes (safety)

        /// <summary>
        /// Profit target for ranging mode (in ATR multiples)
        /// Typically smaller targets than trending
        /// </summary>
        public double ProfitTargetAtrMultiplier { get; set; } = 1.5;

        /// <summary>
        /// Stop loss for ranging mode (in ATR multiples)
        /// Tighter stops to protect capital
        /// </summary>
        public double StopLossAtrMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Price tolerance for "at support/resistance" detection
        /// How close to the cluster level to be considered "at" that level
        /// </summary>
        public double PriceTolerancePercent { get; set; } = 0.2; // 0.2% of price

        /// <summary>
        /// Minimum order flow bias strength for entry
        /// Lower threshold than trending (more frequent entries)
        /// </summary>
        public double MinimumOrderFlowBiasForEntry { get; set; } = 30.0;

        /// <summary>
        /// Entry signal state
        /// </summary>
        public RangingEntrySignal LastEntrySignal { get; private set; } = new();

        /// <summary>
        /// Exit signal state
        /// </summary>
        public RangingExitSignal? LastExitSignal { get; private set; }

        /// <summary>
        /// Entry price
        /// </summary>
        public double EntryPrice { get; private set; } = 0.0;

        /// <summary>
        /// Entry time
        /// </summary>
        public DateTime EntryTime { get; private set; } = DateTime.MinValue;

        /// <summary>
        /// Is currently in a trade
        /// </summary>
        public bool IsInTrade { get; private set; } = false;

        public RangingModeLogic()
        {
        }

        /// <summary>
        /// Evaluate entry opportunity in ranging regime
        /// </summary>
        public RangingEntrySignal EvaluateEntry(RegimeSignal regimeSignal, OrderFlowMetrics orderFlow, double currentPrice)
        {
            var signal = new RangingEntrySignal
            {
                Timestamp = regimeSignal.Timestamp,
                CurrentPrice = currentPrice,
                Regime = regimeSignal.Regime
            };

            // Regime must be RANGING
            if (regimeSignal.Regime != RegimeState.Ranging)
            {
                signal.CanEnterLong = false;
                signal.CanEnterShort = false;
                signal.RejectReason = "Not in RANGING regime";
                LastEntrySignal = signal;
                return signal;
            }

            // Order flow data required
            if (orderFlow == null)
            {
                signal.CanEnterLong = false;
                signal.CanEnterShort = false;
                signal.RejectReason = "No order flow data";
                LastEntrySignal = signal;
                return signal;
            }

            // Liquidity must be sufficient
            if (!orderFlow.IsLiquidEnoughForEntry)
            {
                signal.CanEnterLong = false;
                signal.CanEnterShort = false;
                signal.RejectReason = "Insufficient liquidity";
                LastEntrySignal = signal;
                return signal;
            }

            // Check for support/resistance clusters
            if (orderFlow.SupportLevel == 0 || orderFlow.ResistanceLevel == 0)
            {
                signal.CanEnterLong = false;
                signal.CanEnterShort = false;
                signal.RejectReason = "No S/R clusters detected";
                LastEntrySignal = signal;
                return signal;
            }

            // Evaluate LONG entry (at support)
            EvaluateLongEntry(signal, regimeSignal, orderFlow, currentPrice);

            // Evaluate SHORT entry (at resistance)
            if (!signal.CanEnterLong)
                EvaluateShortEntry(signal, regimeSignal, orderFlow, currentPrice);

            // Calculate profit target and stop loss
            if (signal.CanEnterLong || signal.CanEnterShort)
            {
                CalculateTargets(signal, regimeSignal);
            }

            LastEntrySignal = signal;
            return signal;
        }

        /// <summary>
        /// Check for LONG entry conditions (at support)
        /// </summary>
        private void EvaluateLongEntry(RangingEntrySignal signal, RegimeSignal regime, 
                                       OrderFlowMetrics flow, double currentPrice)
        {
            // Must be near support level
            double distanceToSupport = Math.Abs(currentPrice - flow.SupportLevel);
            double tolerance = currentPrice * (PriceTolerancePercent / 100.0);

            if (distanceToSupport > tolerance)
            {
                signal.RejectReason = $"Price not at support (distance: {distanceToSupport:F4})";
                return;
            }

            // Price must be bouncing UP from support (not falling further)
            if (flow.RejectionAtSupport)
            {
                signal.LongRejectionConfidence = 0.9; // Strong rejection detected
            }
            else
            {
                signal.LongRejectionConfidence = 0.5; // Weak rejection
            }

            // Order flow must be turning bullish or neutral (not strongly bearish)
            if (flow.OrderFlowBias > 30.0) // Too much selling pressure
            {
                signal.RejectReason = "Order flow too bearish for long entry";
                return;
            }

            // RSI should be oversold (mean reversion signal)
            if (!regime.RsiOversold && flow.OrderFlowBias > 0)
            {
                signal.RejectReason = "RSI not oversold and order flow not bullish";
                return;
            }

            // Support must be strong (significant volume)
            if (flow.SupportClusterVolume < 100) // Minimum volume at support
            {
                signal.RejectReason = "Support cluster too weak";
                return;
            }

            // All checks passed
            signal.CanEnterLong = true;
            signal.EntryDirection = "LONG";
            signal.SupportLevel = flow.SupportLevel;
            signal.ResistanceLevel = flow.ResistanceLevel;
            signal.OrderFlowConfidence = CalculateOrderFlowConfidence(flow.OrderFlowBias, true);
            signal.RsiConfidence = regime.RsiOversold ? 1.0 : 0.6;
            signal.RejectReason = null;
        }

        /// <summary>
        /// Check for SHORT entry conditions (at resistance)
        /// </summary>
        private void EvaluateShortEntry(RangingEntrySignal signal, RegimeSignal regime, 
                                        OrderFlowMetrics flow, double currentPrice)
        {
            // Must be near resistance level
            double distanceToResistance = Math.Abs(currentPrice - flow.ResistanceLevel);
            double tolerance = currentPrice * (PriceTolerancePercent / 100.0);

            if (distanceToResistance > tolerance)
            {
                signal.RejectReason = $"Price not at resistance (distance: {distanceToResistance:F4})";
                return;
            }

            // Price must be rejecting DOWN from resistance (not rising further)
            if (flow.RejectionAtResistance)
            {
                signal.ShortRejectionConfidence = 0.9; // Strong rejection detected
            }
            else
            {
                signal.ShortRejectionConfidence = 0.5; // Weak rejection
            }

            // Order flow must be turning bearish or neutral (not strongly bullish)
            if (flow.OrderFlowBias < -30.0) // Too much buying pressure
            {
                signal.RejectReason = "Order flow too bullish for short entry";
                return;
            }

            // RSI should be overbought (mean reversion signal)
            if (!regime.RsiOverbought && flow.OrderFlowBias < 0)
            {
                signal.RejectReason = "RSI not overbought and order flow not bearish";
                return;
            }

            // Resistance must be strong (significant volume)
            if (flow.ResistanceClusterVolume < 100) // Minimum volume at resistance
            {
                signal.RejectReason = "Resistance cluster too weak";
                return;
            }

            // All checks passed
            signal.CanEnterShort = true;
            signal.EntryDirection = "SHORT";
            signal.SupportLevel = flow.SupportLevel;
            signal.ResistanceLevel = flow.ResistanceLevel;
            signal.OrderFlowConfidence = CalculateOrderFlowConfidence(flow.OrderFlowBias, false);
            signal.RsiConfidence = regime.RsiOverbought ? 1.0 : 0.6;
            signal.RejectReason = null;
        }

        /// <summary>
        /// Calculate profit target and stop loss for ranging trades
        /// </summary>
        private void CalculateTargets(RangingEntrySignal signal, RegimeSignal regime)
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
        public RangingExitSignal EvaluateExit(double currentPrice, TimeSpan timeInTrade, 
                                              RegimeSignal regimeSignal, OrderFlowMetrics orderFlow)
        {
            if (!IsInTrade || EntryPrice == 0)
            {
                return null;
            }

            var signal = new RangingExitSignal
            {
                Timestamp = regimeSignal.Timestamp,
                EntryPrice = EntryPrice,
                CurrentPrice = currentPrice,
                TimeInTrade = timeInTrade,
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

            // 3. Range breakout (exit the range)
            if (RangeBreakoutDetected(signal, LastEntrySignal, orderFlow))
            {
                signal.ExitReason = "Range breakout detected";
                signal.ShouldExit = true;
                LastExitSignal = signal;
                return signal;
            }

            // 4. Regime changed from RANGING
            if (regimeSignal.Regime != RegimeState.Ranging)
            {
                signal.ExitReason = "Regime changed from RANGING to " + regimeSignal.Regime;
                signal.ShouldExit = true;
                LastExitSignal = signal;
                return signal;
            }

            // 5. Minimum hold time elapsed - check order flow
            if (timeInTrade.TotalSeconds >= MinimumHoldTimeSeconds)
            {
                if (HasOrderFlowFlipped(signal.IsLong, orderFlow))
                {
                    signal.ExitReason = "Order flow flipped after minimum hold";
                    signal.ShouldExit = true;
                    LastExitSignal = signal;
                    return signal;
                }
            }

            // 6. Maximum hold time - force exit
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

        private bool HitProfitTarget(RangingExitSignal signal, RangingEntrySignal entry)
        {
            if (entry == null || entry.ProfitTarget == 0)
                return false;

            double pnl = CalculatePnL(signal);
            return Math.Abs(pnl) >= entry.ProfitTarget;
        }

        private bool HitStopLoss(RangingExitSignal signal, RangingEntrySignal entry)
        {
            if (entry == null || entry.StopLoss == 0)
                return false;

            double pnl = CalculatePnL(signal);
            return pnl <= -(entry.StopLoss);
        }

        private bool RangeBreakoutDetected(RangingExitSignal signal, RangingEntrySignal entry, OrderFlowMetrics flow)
        {
            if (entry == null || flow == null)
                return false;

            if (signal.IsLong)
            {
                // Long entry at support, exit if price breaks above resistance
                return signal.CurrentPrice > entry.ResistanceLevel * 1.01; // 1% above resistance
            }
            else
            {
                // Short entry at resistance, exit if price breaks below support
                return signal.CurrentPrice < entry.SupportLevel * 0.99; // 1% below support
            }
        }

        private bool HasOrderFlowFlipped(bool isLong, OrderFlowMetrics flow)
        {
            if (flow == null)
                return false;

            if (isLong && flow.OrderFlowBias > 20.0) // Was buying, now bearish
                return true;

            if (!isLong && flow.OrderFlowBias < -20.0) // Was selling, now bullish
                return true;

            return false;
        }

        private double CalculatePnL(RangingExitSignal signal)
        {
            if (signal.IsLong)
                return signal.CurrentPrice - signal.EntryPrice;
            else
                return signal.EntryPrice - signal.CurrentPrice;
        }

        private double CalculateOrderFlowConfidence(double bias, bool isLong)
        {
            double absBias = Math.Abs(bias);
            double strength = Math.Min(1.0, absBias / 100.0);

            // Check direction alignment
            if ((isLong && bias < 0) || (!isLong && bias > 0))
                return strength; // Aligned
            else
                return strength * 0.5; // Not aligned, lower confidence
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
    /// Entry signal for ranging mode
    /// </summary>
    public class RangingEntrySignal
    {
        public DateTime Timestamp { get; set; }
        public double CurrentPrice { get; set; }
        public RegimeState Regime { get; set; }
        public bool CanEnterLong { get; set; }
        public bool CanEnterShort { get; set; }
        public string? EntryDirection { get; set; } // "LONG" or "SHORT"
        public string? RejectReason { get; set; }
        public double SupportLevel { get; set; }
        public double ResistanceLevel { get; set; }
        public double OrderFlowConfidence { get; set; }
        public double RsiConfidence { get; set; }
        public double LongRejectionConfidence { get; set; }
        public double ShortRejectionConfidence { get; set; }
        public double Atr { get; set; }
        public double StopLoss { get; set; }
        public double ProfitTarget { get; set; }
        public int MinimumHoldTimeSeconds { get; set; }
        public int MaximumHoldTimeSeconds { get; set; }
    }

    /// <summary>
    /// Exit signal for ranging mode
    /// </summary>
    public class RangingExitSignal
    {
        public DateTime Timestamp { get; set; }
        public double EntryPrice { get; set; }
        public double CurrentPrice { get; set; }
        public TimeSpan TimeInTrade { get; set; }
        public bool IsLong { get; set; }
        public bool ShouldExit { get; set; }
        public string? ExitReason { get; set; }
    }
}
