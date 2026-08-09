namespace ISE.OrderFlowIntelligence.Models
{
    /// <summary>
    /// Depth of Market (DOM) snapshot at a point in time
    /// Contains bid/ask ladder with volume at each price level
    /// </summary>
    public class DomSnapshot
    {
        /// <summary>
        /// Timestamp of this DOM snapshot
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Current bid price (top of book)
        /// </summary>
        public double BidPrice { get; set; }

        /// <summary>
        /// Current ask price (top of book)
        /// </summary>
        public double AskPrice { get; set; }

        /// <summary>
        /// Total volume at bid price
        /// </summary>
        public long BidVolume { get; set; }

        /// <summary>
        /// Total volume at ask price
        /// </summary>
        public long AskVolume { get; set; }

        /// <summary>
        /// Bid/ask spread in ticks or price units
        /// </summary>
        public double Spread { get; set; }

        /// <summary>
        /// Bid levels [price -> volume]
        /// Index 0 = top of book, index N = deeper levels
        /// </summary>
        public List<(double price, long volume)> BidLevels { get; set; } = new();

        /// <summary>
        /// Ask levels [price -> volume]
        /// Index 0 = top of book, index N = deeper levels
        /// </summary>
        public List<(double price, long volume)> AskLevels { get; set; } = new();

        /// <summary>
        /// Total cumulative volume on bid side
        /// </summary>
        public long TotalBidVolume => BidLevels.Sum(x => x.volume);

        /// <summary>
        /// Total cumulative volume on ask side
        /// </summary>
        public long TotalAskVolume => AskLevels.Sum(x => x.volume);

        /// <summary>
        /// Imbalance ratio: (Ask Volume - Bid Volume) / (Bid Volume + Ask Volume)
        /// Positive = more selling, Negative = more buying
        /// </summary>
        public double VolumeImbalance 
        {
            get
            {
                long totalBid = TotalBidVolume;
                long totalAsk = TotalAskVolume;
                long total = totalBid + totalAsk;
                
                if (total == 0) return 0.0;
                return (double)(totalAsk - totalBid) / total;
            }
        }

        public DomSnapshot()
        {
            Timestamp = DateTime.UtcNow;
        }

        public DomSnapshot(DateTime timestamp, double bidPrice, double askPrice, 
                          long bidVolume, long askVolume)
        {
            Timestamp = timestamp;
            BidPrice = bidPrice;
            AskPrice = askPrice;
            BidVolume = bidVolume;
            AskVolume = askVolume;
            Spread = askPrice - bidPrice;
        }

        /// <summary>
        /// Get total volume within N price levels of the spread
        /// Used to detect liquidity clusters
        /// </summary>
        public long GetVolumeWithinSpread(int levels)
        {
            long total = BidVolume + AskVolume;
            
            for (int i = 1; i < Math.Min(levels, BidLevels.Count); i++)
                total += BidLevels[i].volume;
            
            for (int i = 1; i < Math.Min(levels, AskLevels.Count); i++)
                total += AskLevels[i].volume;

            return total;
        }

        /// <summary>
        /// Get bid/ask imbalance as a score (-100 to +100)
        /// -100 = all buying pressure, +100 = all selling pressure
        /// </summary>
        public double GetImbalanceScore()
        {
            long totalBid = TotalBidVolume;
            long totalAsk = TotalAskVolume;
            long total = totalBid + totalAsk;

            if (total == 0) return 0.0;

            return ((double)(totalAsk - totalBid) / total) * 100.0;
        }

        public override string ToString()
        {
            return $"{Timestamp:HH:mm:ss} | Bid: {BidPrice:F2}x{BidVolume} Ask: {AskPrice:F2}x{AskVolume} | Spread: {Spread:F4}";
        }
    }
}
