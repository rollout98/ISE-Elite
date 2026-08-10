using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.OrderFlowAnalysis.Models
{
    /// <summary>
    /// Snapshot of the order book (bid and ask sides) at a point in time
    /// </summary>
    public sealed class DomSnapshot
    {
        public DomSnapshot(
            DateTime timestamp,
            IReadOnlyList<DomLevel> bidLevels,
            IReadOnlyList<DomLevel> askLevels)
        {
            if (bidLevels == null) throw new ArgumentNullException(nameof(bidLevels));
            if (askLevels == null) throw new ArgumentNullException(nameof(askLevels));
            
            Timestamp = timestamp;
            BidLevels = bidLevels;
            AskLevels = askLevels;
        }

        public DateTime Timestamp { get; }
        public IReadOnlyList<DomLevel> BidLevels { get; }
        public IReadOnlyList<DomLevel> AskLevels { get; }

        /// <summary>Get best bid price</summary>
        public decimal BestBid => BidLevels.Count > 0 ? BidLevels[0].Price : 0m;

        /// <summary>Get best ask price</summary>
        public decimal BestAsk => AskLevels.Count > 0 ? AskLevels[0].Price : 0m;

        /// <summary>Get bid-ask spread in ticks (0.25 for most futures)</summary>
        public decimal Spread => BestAsk - BestBid;

        /// <summary>Total bid volume (all levels)</summary>
        public long TotalBidVolume => BidLevels.Sum(l => l.Volume);

        /// <summary>Total ask volume (all levels)</summary>
        public long TotalAskVolume => AskLevels.Sum(l => l.Volume);

        /// <summary>Bid/ask imbalance ratio (bid volume / ask volume)</summary>
        public double Ratio => TotalAskVolume > 0 ? (double)TotalBidVolume / TotalAskVolume : 0;
    }
}
