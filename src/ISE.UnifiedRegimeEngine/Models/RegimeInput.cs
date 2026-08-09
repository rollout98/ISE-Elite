namespace ISE.UnifiedRegimeEngine.Models
{
    /// <summary>
    /// Unified input for regime calculation engines
    /// Contains OHLCV bar data + state tracking
    /// </summary>
    public class RegimeInput
    {
        /// <summary>
        /// Bar timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// OHLCV values for current bar
        /// </summary>
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public long Volume { get; set; }

        /// <summary>
        /// Previous bar close (for True Range calculation)
        /// </summary>
        public double? PreviousClose { get; set; }

        /// <summary>
        /// Bar period name for reference (e.g., "1-Minute", "5-Minute")
        /// </summary>
        public string? BarPeriod { get; set; }

        /// <summary>
        /// Session context (e.g., "NY", "RTH")
        /// </summary>
        public string? SessionContext { get; set; }

        public RegimeInput()
        {
            Timestamp = DateTime.UtcNow;
            BarPeriod = "1-Minute";
        }

        public RegimeInput(DateTime timestamp, double open, double high, double low, double close, long volume, double? previousClose = null)
        {
            Timestamp = timestamp;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            PreviousClose = previousClose;
            BarPeriod = "1-Minute";
        }

        /// <summary>
        /// Validation: ensure OHLC are logically consistent
        /// </summary>
        public bool IsValid()
        {
            if (High < Low || High < Open || High < Close || Low > Open || Low > Close)
                return false;

            if (Open <= 0 || High <= 0 || Low <= 0 || Close <= 0)
                return false;

            return true;
        }

        public override string ToString()
        {
            return $"{Timestamp:HH:mm:ss} | O:{Open:F2} H:{High:F2} L:{Low:F2} C:{Close:F2} | V:{Volume}";
        }
    }
}
