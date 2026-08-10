using System;

namespace ISE.OrderFlowAnalysis.Models
{
    /// <summary>
    /// Represents a single level in the order book (bid or ask side)
    /// </summary>
    public sealed class DomLevel
    {
        public DomLevel(decimal price, long volume)
        {
            if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
            if (volume < 0) throw new ArgumentOutOfRangeException(nameof(volume));
            
            Price = price;
            Volume = volume;
        }

        public decimal Price { get; }
        public long Volume { get; }
    }
}
