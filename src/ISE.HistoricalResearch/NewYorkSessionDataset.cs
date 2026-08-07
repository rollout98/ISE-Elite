using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class NewYorkResearchWindow
    {
        public NewYorkResearchWindow(TimeSpan startCentral, TimeSpan endCentral)
        {
            if (startCentral < TimeSpan.Zero || startCentral >= TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(startCentral));
            if (endCentral <= TimeSpan.Zero || endCentral > TimeSpan.FromDays(1)) throw new ArgumentOutOfRangeException(nameof(endCentral));
            if (endCentral <= startCentral) throw new ArgumentException("New York research window must end after it starts.");

            StartCentral = startCentral;
            EndCentral = endCentral;
        }

        public TimeSpan StartCentral { get; }
        public TimeSpan EndCentral { get; }
    }

    public sealed class NewYorkSessionSlice
    {
        public NewYorkSessionSlice(DateTime sessionDateCentral, IReadOnlyList<HistoricalBar> bars)
        {
            if (sessionDateCentral.TimeOfDay != TimeSpan.Zero) throw new ArgumentException("Session date must be date-only.", nameof(sessionDateCentral));
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (bars.Count == 0) throw new ArgumentException("Session slice must contain at least one bar.", nameof(bars));

            SessionDateCentral = sessionDateCentral.Date;
            Bars = bars;
        }

        public DateTime SessionDateCentral { get; }
        public IReadOnlyList<HistoricalBar> Bars { get; }
        public DateTimeOffset FirstTimestampUtc => Bars[0].TimestampUtc;
        public DateTimeOffset LastTimestampUtc => Bars[Bars.Count - 1].TimestampUtc;
    }

    public sealed class NewYorkSessionDataset
    {
        public NewYorkSessionDataset(IReadOnlyList<NewYorkSessionSlice> sessions, int sourceBarCount, int selectedBarCount)
        {
            Sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            SourceBarCount = sourceBarCount;
            SelectedBarCount = selectedBarCount;
        }

        public IReadOnlyList<NewYorkSessionSlice> Sessions { get; }
        public int SourceBarCount { get; }
        public int SelectedBarCount { get; }
        public int SessionCount => Sessions.Count;
    }

    public sealed class NewYorkSessionDatasetExtractor
    {
        private readonly TimeZoneInfo _centralTimeZone;

        public NewYorkSessionDatasetExtractor()
            : this(ResolveCentralTimeZone())
        {
        }

        public NewYorkSessionDatasetExtractor(TimeZoneInfo centralTimeZone)
        {
            _centralTimeZone = centralTimeZone ?? throw new ArgumentNullException(nameof(centralTimeZone));
        }

        public NewYorkSessionDataset Extract(IReadOnlyList<HistoricalBar> bars, NewYorkResearchWindow window)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (window == null) throw new ArgumentNullException(nameof(window));

            if (bars.Count == 0)
                return new NewYorkSessionDataset(Array.Empty<NewYorkSessionSlice>(), 0, 0);

            var ordered = bars.OrderBy(x => x.TimestampUtc).ToList();
            ValidateSingleSeries(ordered);

            var selected = new List<(DateTime SessionDate, HistoricalBar Bar)>();
            foreach (var bar in ordered)
            {
                var central = TimeZoneInfo.ConvertTime(bar.TimestampUtc, _centralTimeZone);
                var time = central.TimeOfDay;
                if (time < window.StartCentral || time >= window.EndCentral)
                    continue;

                selected.Add((central.Date, bar));
            }

            var sessions = selected
                .GroupBy(x => x.SessionDate)
                .OrderBy(x => x.Key)
                .Select(group => new NewYorkSessionSlice(
                    group.Key,
                    group.Select(x => x.Bar).OrderBy(x => x.TimestampUtc).ToList()))
                .ToList();

            return new NewYorkSessionDataset(sessions, bars.Count, selected.Count);
        }

        private static void ValidateSingleSeries(IReadOnlyList<HistoricalBar> bars)
        {
            var first = bars[0];
            for (var i = 1; i < bars.Count; i++)
            {
                var bar = bars[i];
                if (!string.Equals(bar.Instrument, first.Instrument, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("New York dataset extraction requires a single instrument.");
                if (!string.Equals(bar.Contract, first.Contract, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("New York dataset extraction requires a single futures contract.");
                if (bar.IntervalSeconds != first.IntervalSeconds)
                    throw new InvalidOperationException("New York dataset extraction requires a single bar interval.");
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
            }
        }
    }
}
