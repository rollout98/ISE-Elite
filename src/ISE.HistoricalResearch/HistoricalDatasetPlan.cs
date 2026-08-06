using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class HistoricalDatasetPlan
    {
        private readonly IReadOnlyList<HistoricalDatasetPartition> _partitions;

        public HistoricalDatasetPlan(IEnumerable<HistoricalDatasetPartition> partitions)
        {
            if (partitions == null)
            {
                throw new ArgumentNullException(nameof(partitions));
            }

            var ordered = partitions
                .OrderBy(partition => partition.StartTradingDate)
                .ToArray();

            if (ordered.Length == 0)
            {
                throw new ArgumentException("At least one historical partition is required.", nameof(partitions));
            }

            for (var index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].StartTradingDate <= ordered[index - 1].EndTradingDate)
                {
                    throw new ArgumentException("Historical partitions must not overlap.", nameof(partitions));
                }
            }

            _partitions = ordered;
        }

        public IReadOnlyList<HistoricalDatasetPartition> Partitions => _partitions;

        public HistoricalPartitionKind? Resolve(DateTime tradingDate)
        {
            foreach (var partition in _partitions)
            {
                if (partition.Contains(tradingDate))
                {
                    return partition.Kind;
                }
            }

            return null;
        }
    }
}
