using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class ContractSegment
    {
        public ContractSegment(string contract, DateTimeOffset firstTimestampUtc, DateTimeOffset lastTimestampUtc, int barCount)
        {
            Contract = contract;
            FirstTimestampUtc = firstTimestampUtc;
            LastTimestampUtc = lastTimestampUtc;
            BarCount = barCount;
        }

        public string Contract { get; }
        public DateTimeOffset FirstTimestampUtc { get; }
        public DateTimeOffset LastTimestampUtc { get; }
        public int BarCount { get; }
    }

    public sealed class ContractAwareDatasetCoverageReport
    {
        public ContractAwareDatasetCoverageReport(
            string instrument,
            int intervalSeconds,
            int barCount,
            int sessionCount,
            int completeSessionCount,
            int partialSessionCount,
            DateTime? firstSessionDateCentral,
            DateTime? lastSessionDateCentral,
            IReadOnlyList<DateTime> partialSessionDatesCentral,
            IReadOnlyList<ContractSegment> contractSegments)
        {
            Instrument = instrument;
            IntervalSeconds = intervalSeconds;
            BarCount = barCount;
            SessionCount = sessionCount;
            CompleteSessionCount = completeSessionCount;
            PartialSessionCount = partialSessionCount;
            FirstSessionDateCentral = firstSessionDateCentral;
            LastSessionDateCentral = lastSessionDateCentral;
            PartialSessionDatesCentral = partialSessionDatesCentral;
            ContractSegments = contractSegments;
        }

        public string Instrument { get; }
        public int IntervalSeconds { get; }
        public int BarCount { get; }
        public int SessionCount { get; }
        public int CompleteSessionCount { get; }
        public int PartialSessionCount { get; }
        public DateTime? FirstSessionDateCentral { get; }
        public DateTime? LastSessionDateCentral { get; }
        public IReadOnlyList<DateTime> PartialSessionDatesCentral { get; }
        public IReadOnlyList<ContractSegment> ContractSegments { get; }
    }

    public sealed class ContractAwareHistoricalDatasetValidator
    {
        private readonly TimeZoneInfo _centralTimeZone;

        public ContractAwareHistoricalDatasetValidator()
            : this(ResolveCentralTimeZone())
        {
        }

        public ContractAwareHistoricalDatasetValidator(TimeZoneInfo centralTimeZone)
        {
            _centralTimeZone = centralTimeZone ?? throw new ArgumentNullException(nameof(centralTimeZone));
        }

        public IReadOnlyList<HistoricalBar> ValidateAndOrder(IEnumerable<HistoricalBar> bars)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            var ordered = bars.OrderBy(x => x.TimestampUtc).ToList();
            if (ordered.Count == 0) return Array.Empty<HistoricalBar>();

            var first = ordered[0];
            if (ordered.Any(x => !string.Equals(x.Instrument, first.Instrument, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Contract-aware dataset may contain only one instrument.");
            if (ordered.Any(x => x.IntervalSeconds != first.IntervalSeconds))
                throw new InvalidOperationException("Contract-aware dataset may contain only one bar interval.");

            var seenTimestampIntervals = new HashSet<string>(StringComparer.Ordinal);
            var completedContracts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeContract = first.Contract;

            foreach (var bar in ordered)
            {
                var key = bar.TimestampUtc.UtcDateTime.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ":" + bar.IntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                if (!seenTimestampIntervals.Add(key))
                    throw new InvalidOperationException("Contract-aware dataset contains overlapping or duplicate bars at the same timestamp and interval.");

                if (!string.Equals(bar.Contract, activeContract, StringComparison.OrdinalIgnoreCase))
                {
                    completedContracts.Add(activeContract);
                    if (completedContracts.Contains(bar.Contract))
                        throw new InvalidOperationException("Contract-aware dataset cannot re-enter a previously completed futures contract segment.");
                    activeContract = bar.Contract;
                }
            }

            return ordered;
        }

        public ContractAwareDatasetCoverageReport BuildCoverageReport(
            IEnumerable<HistoricalBar> bars,
            TimeSpan sessionStartCentral,
            TimeSpan sessionEndCentral)
        {
            if (sessionStartCentral < TimeSpan.Zero || sessionStartCentral >= TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(sessionStartCentral));
            if (sessionEndCentral <= sessionStartCentral || sessionEndCentral > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(sessionEndCentral));

            var ordered = ValidateAndOrder(bars);
            if (ordered.Count == 0)
                return new ContractAwareDatasetCoverageReport(string.Empty, 0, 0, 0, 0, 0, null, null, Array.Empty<DateTime>(), Array.Empty<ContractSegment>());

            var expectedBarsPerSession = (int)((sessionEndCentral - sessionStartCentral).TotalSeconds / ordered[0].IntervalSeconds);
            if (expectedBarsPerSession <= 0)
                throw new InvalidOperationException("Research session window is shorter than the bar interval.");

            var sessions = ordered
                .Select(x => new { Bar = x, Central = TimeZoneInfo.ConvertTime(x.TimestampUtc, _centralTimeZone) })
                .GroupBy(x => x.Central.Date)
                .OrderBy(x => x.Key)
                .ToList();

            var partial = sessions.Where(x => x.Count() != expectedBarsPerSession).Select(x => x.Key).ToList();
            var segments = BuildSegments(ordered);

            return new ContractAwareDatasetCoverageReport(
                ordered[0].Instrument,
                ordered[0].IntervalSeconds,
                ordered.Count,
                sessions.Count,
                sessions.Count - partial.Count,
                partial.Count,
                sessions[0].Key,
                sessions[sessions.Count - 1].Key,
                partial,
                segments);
        }

        private static IReadOnlyList<ContractSegment> BuildSegments(IReadOnlyList<HistoricalBar> ordered)
        {
            var segments = new List<ContractSegment>();
            var start = 0;
            for (var i = 1; i <= ordered.Count; i++)
            {
                if (i < ordered.Count && string.Equals(ordered[i].Contract, ordered[start].Contract, StringComparison.OrdinalIgnoreCase))
                    continue;

                segments.Add(new ContractSegment(
                    ordered[start].Contract,
                    ordered[start].TimestampUtc,
                    ordered[i - 1].TimestampUtc,
                    i - start));
                start = i;
            }
            return segments;
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
