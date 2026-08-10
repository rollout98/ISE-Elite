using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.Systems
{
    /// <summary>
    /// Mean Reversion Scalper Signal
    /// Trades price extremes that reject off 2-bar highs/lows
    /// </summary>
    public class MeanReversionSignal
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
        private const double TARGET_POINTS = 0.5;
        private const double STOP_POINTS = 0.5;
        private const int MIN_VOLUME_BARS = 5;

        /// <summary>
        /// Add a bar to the analysis
        /// </summary>
        public void AddBar(Bar bar)
        {
            bars.Add(bar);
        }

        /// <summary>
        /// Analyze current bar for mean reversion signal
        /// </summary>
        public SignalResult GetSignal(int currentBarIndex)
        {
            if (currentBarIndex < 2)
                return new SignalResult { HasSignal = false };

            var result = new SignalResult
            {
                Bar = currentBarIndex,
                HasSignal = false
            };

            // Get last 3 bars: [i-2], [i-1], [i]
            var bar2Ago = bars[currentBarIndex - 2];
            var bar1Ago = bars[currentBarIndex - 1];
            var currentBar = bars[currentBarIndex];

            // Get 2-bar high and low (previous 2 bars)
            double twoBarHigh = Math.Max(bar2Ago.High, bar1Ago.High);
            double twoBarLow = Math.Min(bar2Ago.Low, bar1Ago.Low);

            // Calculate average volume over last 5 bars
            double avgVolume = CalculateAverageVolume(currentBarIndex, MIN_VOLUME_BARS);
            double volumeMultiplier = currentBar.Volume / avgVolume;

            // === SIGNAL LOGIC ===

            // SHORT: Current bar closes below 2-bar low (rejection down) + high volume
            if (currentBar.Close < twoBarLow && volumeMultiplier > 1.2)
            {
                result.HasSignal = true;
                result.Direction = "Short";
                result.EntryPrice = currentBar.Close;
                result.TargetPrice = currentBar.Close - TARGET_POINTS;
                result.StopPrice = currentBar.Close + STOP_POINTS;
                result.Reason = string.Format(
                    "Below 2-bar low ({0:F2}), Vol {1:F2}x",
                    twoBarLow, volumeMultiplier);
            }

            // LONG: Current bar closes above 2-bar high (rejection up) + high volume
            else if (currentBar.Close > twoBarHigh && volumeMultiplier > 1.2)
            {
                result.HasSignal = true;
                result.Direction = "Long";
                result.EntryPrice = currentBar.Close;
                result.TargetPrice = currentBar.Close + TARGET_POINTS;
                result.StopPrice = currentBar.Close - STOP_POINTS;
                result.Reason = string.Format(
                    "Above 2-bar high ({0:F2}), Vol {1:F2}x",
                    twoBarHigh, volumeMultiplier);
            }

            return result;
        }

        /// <summary>
        /// Calculate average volume over N bars
        /// </summary>
        private double CalculateAverageVolume(int barIndex, int period)
        {
            if (barIndex < period)
                period = barIndex;

            long totalVolume = 0;
            for (int i = barIndex - period; i < barIndex; i++)
            {
                totalVolume += bars[i].Volume;
            }

            return (double)totalVolume / period;
        }

        /// <summary>
        /// Check if trade should exit
        /// </summary>
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

        /// <summary>
        /// Calculate P&L for trade
        /// </summary>
        public double CalculatePnL(double entryPrice, double exitPrice, string direction, int contracts = 1)
        {
            double pointChange = exitPrice - entryPrice;
            if (direction == "Short")
                pointChange = -pointChange;

            // $20 per point for MNQ
            return pointChange * 20 * contracts;
        }

        /// <summary>
        /// Get all bars
        /// </summary>
        public List<Bar> GetBars() => bars;

        /// <summary>
        /// Clear bars (for next test)
        /// </summary>
        public void Clear() => bars.Clear();
    }
}
