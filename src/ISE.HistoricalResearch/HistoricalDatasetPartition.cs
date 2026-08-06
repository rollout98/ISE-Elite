using System;

namespace ISE.HistoricalResearch
{
    public enum HistoricalPartitionKind
    {
        Development = 0,
        Validation = 1,
        OutOfSample = 2
    }

    public sealed class HistoricalDatasetPartition
    {
        public HistoricalDatasetPartition(
            HistoricalPartitionKind kind,
            DateTime startTradingDate,
            DateTime endTradingDate)
        {
            var start = startTradingDate.Date;
            var end = endTradingDate.Date;

            if (end < start)
            {
                throw new ArgumentException("Partition end date must be on or after the start date.", nameof(endTradingDate));
            }

            Kind = kind;
            StartTradingDate = start;
            EndTradingDate = end;
        }

        public HistoricalPartitionKind Kind { get; }

        public DateTime StartTradingDate { get; }

        public DateTime EndTradingDate { get; }

        public bool Contains(DateTime tradingDate)
        {
            var date = tradingDate.Date;
            return date >= StartTradingDate && date <= EndTradingDate;
        }
    }
}
