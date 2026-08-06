using System;

namespace ISE.HistoricalResearch
{
    public enum HistoricalDataSourceKind
    {
        Unknown = 0,
        NinjaTraderProvider = 1,
        NinjaTraderRepository = 2,
        ImportedFile = 3,
        MarketReplay = 4
    }

    public sealed class HistoricalBar
    {
        public HistoricalBar(
            string instrument,
            string contract,
            DateTimeOffset timestampUtc,
            DateTime tradingDay,
            int intervalSeconds,
            decimal open,
            decimal high,
            decimal low,
            decimal close,
            long volume,
            HistoricalDataSourceKind sourceKind,
            string sourceName,
            decimal? bid = null,
            decimal? ask = null)
        {
            if (string.IsNullOrWhiteSpace(instrument)) throw new ArgumentException("Instrument is required.", nameof(instrument));
            if (string.IsNullOrWhiteSpace(contract)) throw new ArgumentException("Contract is required.", nameof(contract));
            if (timestampUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be normalized to UTC.", nameof(timestampUtc));
            if (tradingDay.TimeOfDay != TimeSpan.Zero) throw new ArgumentException("Trading day must be a date-only value.", nameof(tradingDay));
            if (intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            if (high < low) throw new ArgumentException("High cannot be below low.");
            if (open < low || open > high) throw new ArgumentException("Open must be within low/high range.");
            if (close < low || close > high) throw new ArgumentException("Close must be within low/high range.");
            if (volume < 0) throw new ArgumentOutOfRangeException(nameof(volume));
            if (bid.HasValue && ask.HasValue && bid.Value > ask.Value) throw new ArgumentException("Bid cannot exceed ask.");
            if (string.IsNullOrWhiteSpace(sourceName)) throw new ArgumentException("Source name is required.", nameof(sourceName));

            Instrument = instrument.Trim();
            Contract = contract.Trim();
            TimestampUtc = timestampUtc;
            TradingDay = tradingDay.Date;
            IntervalSeconds = intervalSeconds;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
            SourceKind = sourceKind;
            SourceName = sourceName.Trim();
            Bid = bid;
            Ask = ask;
        }

        public string Instrument { get; }
        public string Contract { get; }
        public DateTimeOffset TimestampUtc { get; }
        public DateTime TradingDay { get; }
        public int IntervalSeconds { get; }
        public decimal Open { get; }
        public decimal High { get; }
        public decimal Low { get; }
        public decimal Close { get; }
        public long Volume { get; }
        public HistoricalDataSourceKind SourceKind { get; }
        public string SourceName { get; }
        public decimal? Bid { get; }
        public decimal? Ask { get; }
    }
}
