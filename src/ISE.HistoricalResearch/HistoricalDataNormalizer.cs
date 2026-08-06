using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class HistoricalDataNormalizer
    {
        public IReadOnlyList<HistoricalBar> Normalize(IEnumerable<HistoricalBar> bars)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));

            var materialized = bars.ToList();
            if (materialized.Count == 0) return Array.Empty<HistoricalBar>();

            var first = materialized[0];
            if (materialized.Any(x => !string.Equals(x.Instrument, first.Instrument, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A normalized batch may contain only one instrument.");
            }

            if (materialized.Any(x => !string.Equals(x.Contract, first.Contract, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("A normalized batch may contain only one futures contract.");
            }

            var ordered = materialized
                .OrderBy(x => x.TimestampUtc)
                .ThenBy(x => x.IntervalSeconds)
                .ToList();

            var result = new List<HistoricalBar>(ordered.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var bar in ordered)
            {
                var key = bar.TimestampUtc.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ":" + bar.IntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (!seen.Add(key))
                {
                    throw new InvalidOperationException("Duplicate historical bar detected for the same timestamp and interval.");
                }

                result.Add(bar);
            }

            return result;
        }
    }
}
