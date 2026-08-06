using System;
using System.Collections.Generic;

namespace ISE.HistoricalResearch
{
    public sealed class HistoricalDataAcquisitionRequest
    {
        public HistoricalDataAcquisitionRequest(
            string instrument,
            string contract,
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            int intervalSeconds,
            HistoricalDataSourceKind preferredSource)
        {
            if (string.IsNullOrWhiteSpace(instrument)) throw new ArgumentException("Instrument is required.", nameof(instrument));
            if (string.IsNullOrWhiteSpace(contract)) throw new ArgumentException("Contract is required.", nameof(contract));
            if (startUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Start must be UTC.", nameof(startUtc));
            if (endUtc.Offset != TimeSpan.Zero) throw new ArgumentException("End must be UTC.", nameof(endUtc));
            if (endUtc <= startUtc) throw new ArgumentException("End must be after start.", nameof(endUtc));
            if (intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));

            Instrument = instrument.Trim();
            Contract = contract.Trim();
            StartUtc = startUtc;
            EndUtc = endUtc;
            IntervalSeconds = intervalSeconds;
            PreferredSource = preferredSource;
        }

        public string Instrument { get; }
        public string Contract { get; }
        public DateTimeOffset StartUtc { get; }
        public DateTimeOffset EndUtc { get; }
        public int IntervalSeconds { get; }
        public HistoricalDataSourceKind PreferredSource { get; }
    }

    public interface IHistoricalDataSource
    {
        IReadOnlyList<HistoricalBar> Acquire(HistoricalDataAcquisitionRequest request);
    }

    public sealed class HistoricalDataAcquisitionService
    {
        private readonly HistoricalDataNormalizer _normalizer;

        public HistoricalDataAcquisitionService()
        {
            _normalizer = new HistoricalDataNormalizer();
        }

        public IReadOnlyList<HistoricalBar> AcquireAndNormalize(
            IHistoricalDataSource source,
            HistoricalDataAcquisitionRequest request)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (request == null) throw new ArgumentNullException(nameof(request));

            var bars = source.Acquire(request);
            if (bars == null) throw new InvalidOperationException("Historical data source returned null.");

            var normalized = _normalizer.Normalize(bars);
            foreach (var bar in normalized)
            {
                if (!string.Equals(bar.Instrument, request.Instrument, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Historical data source returned an unexpected instrument.");
                if (!string.Equals(bar.Contract, request.Contract, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Historical data source returned an unexpected contract.");
                if (bar.IntervalSeconds != request.IntervalSeconds)
                    throw new InvalidOperationException("Historical data source returned an unexpected bar interval.");
                if (bar.TimestampUtc < request.StartUtc || bar.TimestampUtc >= request.EndUtc)
                    throw new InvalidOperationException("Historical data source returned data outside the requested UTC range.");
            }

            return normalized;
        }
    }
}
