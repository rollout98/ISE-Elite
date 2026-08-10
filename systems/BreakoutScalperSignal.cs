using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.Systems
{
    /// <summary>
    /// Breakout Scalper Signal
    /// Trades breakouts of 5-bar highs/lows with volume confirmation
    /// </summary>
    public class BreakoutScalperSignal
    {
        public class Bar
        {
            public double Open { get; set; }
            public double High { get; set; }
            public double Low { get; set; }
            public double Close { get; set; }
            public long Volume { get; set; }
            public DateTime Time { get; set; }
        }

        public class SignalResult
        {
            public bool HasSignal { get; set; }
            public string Direction { get; set; } // "Long" or "Short"
            public double EntryPrice { get; set; }
            public double TargetPrice { get; set; }
            public double StopPrice { get; set; }
            public string Reason { get; set; }
            public int Bar { get; set; }
        }

        private List<Bar> bars = new List<Bar>();
        private const double TARGET_POINTS = 1.5;
        private const double STOP_POINTS = 0.75;
        private const int LOOKBACK_BARS = 5;
        private const double MIN_VOLUME_MULTIPLIER = 1.1;

        public void AddBar(Bar bar)
        {
            bars.Add(bar);
        }

        /// <summary>
        /// Analyze current bar for breakout signal
        /// </summary>
        public SignalResult GetSignal(int currentBarIndex)
        {
            if (currentBarIndex < LOOKBACK_BARS)
                return new SignalResult { HasSignal = false };

            var result = new SignalResult
            {
                Bar = currentBarIndex,
                HasSignal = false
            };

            var currentBar = bars[currentBarIndex];

            // Get 5-bar high and low (previous 5 bars, excluding current)
            double fiveBarHigh = double.MinValue;
            double fiveBarLow = double.MaxValue;
            long totalVolume = 0;

            for (int i = currentBarIndex - LOOKBACK_BARS; i < currentBarIndex; i++)
            {
                fiveBarHigh = Math.Max(fiveBarHigh, bars[i].High);
                fiveBarLow = Math.Min(fiveBarLow, bars[i].Low);
                totalVolume += bars[i].Volume;
            }

            double avgVolume = (double)totalVolume / LOOKBACK_BARS;
            double volumeMultiplier = currentBar.Volume / avgVolume;

            // === SIGNAL LOGIC ===

            // LONG: Current bar closes above 5-bar high + high volume
            if (currentBar.Close > fiveBarHigh && volumeMultiplier > MIN_VOLUME_MULTIPLIER)
            {
                result.HasSignal = true;
                result.Direction = "Long";
                result.EntryPrice = currentBar.Close;
                result.TargetPrice = currentBar.Close + TARGET_POINTS;
                result.StopPrice = currentBar.Close - STOP_POINTS;
                result.Reason = string.Format(
                    "Above 5-bar high ({0:F2}), Vol {1:F2}x",
                    fiveBarHigh, volumeMultiplier);
            }

            // SHORT: Current bar closes below 5-bar low + high volume
            else if (currentBar.Close < fiveBarLow && volumeMultiplier > MIN_VOLUME_MULTIPLIER)
            {
                result.HasSignal = true;
                result.Direction = "Short";
                result.EntryPrice = currentBar.Close;
                result.TargetPrice = currentBar.Close - TARGET_POINTS;
                result.StopPrice = currentBar.Close + STOP_POINTS;
                result.Reason = string.Format(
                    "Below 5-bar low ({0:F2}), Vol {1:F2}x",
                    fiveBarLow, volumeMultiplier);
            }

            return result;
        }

        public bool ShouldExit(double currentPrice, double entryPrice, double target, double stop, string direction)
        {
            if (direction == "Long")
            {
                return currentPrice >= target || currentPrice <= stop;
            }
            else if (direction == "Short")
            {
                return currentPrice <= target || currentPrice >= stop;
            }

            return false;
        }

        public double CalculatePnL(double entryPrice, double exitPrice, string direction, int contracts = 1)
        {
            double pointChange = exitPrice - entryPrice;
            if (direction == "Short")
                pointChange = -pointChange;

            return pointChange * 20 * contracts;
        }

        public List<Bar> GetBars() => bars;
        public void Clear() => bars.Clear();
    }
}
