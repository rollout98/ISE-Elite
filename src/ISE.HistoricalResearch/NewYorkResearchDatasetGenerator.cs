using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class NewYorkResearchDatasetManifest
    {
        public NewYorkResearchDatasetManifest(
            string instrument,
            string contract,
            int intervalSeconds,
            DateTimeOffset requestedStartUtc,
            DateTimeOffset requestedEndUtc,
            TimeSpan sessionStartCentral,
            TimeSpan sessionEndCentral,
            int sourceBarCount,
            int selectedBarCount,
            int sessionCount,
            DateTime? firstSessionDateCentral,
            DateTime? lastSessionDateCentral,
            string outputPath)
        {
            Instrument = instrument;
            Contract = contract;
            IntervalSeconds = intervalSeconds;
            RequestedStartUtc = requestedStartUtc;
            RequestedEndUtc = requestedEndUtc;
            SessionStartCentral = sessionStartCentral;
            SessionEndCentral = sessionEndCentral;
            SourceBarCount = sourceBarCount;
            SelectedBarCount = selectedBarCount;
            SessionCount = sessionCount;
            FirstSessionDateCentral = firstSessionDateCentral;
            LastSessionDateCentral = lastSessionDateCentral;
            OutputPath = outputPath;
        }

        public string Instrument { get; }
        public string Contract { get; }
        public int IntervalSeconds { get; }
        public DateTimeOffset RequestedStartUtc { get; }
        public DateTimeOffset RequestedEndUtc { get; }
        public TimeSpan SessionStartCentral { get; }
        public TimeSpan SessionEndCentral { get; }
        public int SourceBarCount { get; }
        public int SelectedBarCount { get; }
        public int SessionCount { get; }
        public DateTime? FirstSessionDateCentral { get; }
        public DateTime? LastSessionDateCentral { get; }
        public string OutputPath { get; }
    }

    public sealed class NewYorkResearchDatasetGenerator
    {
        private readonly HistoricalDataAcquisitionService _acquisitionService;
        private readonly NewYorkSessionDatasetExtractor _extractor;
        private readonly HistoricalDataFileStore _fileStore;

        public NewYorkResearchDatasetGenerator()
            : this(
                new HistoricalDataAcquisitionService(),
                new NewYorkSessionDatasetExtractor(),
                new HistoricalDataFileStore())
        {
        }

        public NewYorkResearchDatasetGenerator(
            HistoricalDataAcquisitionService acquisitionService,
            NewYorkSessionDatasetExtractor extractor,
            HistoricalDataFileStore fileStore)
        {
            _acquisitionService = acquisitionService ?? throw new ArgumentNullException(nameof(acquisitionService));
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        }

        public NewYorkResearchDatasetManifest Generate(
            IHistoricalDataSource source,
            HistoricalDataAcquisitionRequest request,
            NewYorkResearchWindow window,
            string outputPath)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));

            var normalized = _acquisitionService.AcquireAndNormalize(source, request);
            var dataset = _extractor.Extract(normalized, window);
            var selected = Flatten(dataset.Sessions);

            _fileStore.Write(outputPath, selected);

            var firstSession = dataset.SessionCount == 0 ? (DateTime?)null : dataset.Sessions[0].SessionDateCentral;
            var lastSession = dataset.SessionCount == 0 ? (DateTime?)null : dataset.Sessions[dataset.SessionCount - 1].SessionDateCentral;

            return new NewYorkResearchDatasetManifest(
                request.Instrument,
                request.Contract,
                request.IntervalSeconds,
                request.StartUtc,
                request.EndUtc,
                window.StartCentral,
                window.EndCentral,
                dataset.SourceBarCount,
                dataset.SelectedBarCount,
                dataset.SessionCount,
                firstSession,
                lastSession,
                Path.GetFullPath(outputPath));
        }

        private static IReadOnlyList<HistoricalBar> Flatten(IReadOnlyList<NewYorkSessionSlice> sessions)
        {
            return sessions
                .SelectMany(x => x.Bars)
                .OrderBy(x => x.TimestampUtc)
                .ToList();
        }
    }
}
