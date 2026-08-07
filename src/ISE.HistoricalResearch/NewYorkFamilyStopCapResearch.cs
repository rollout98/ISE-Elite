using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class NewYorkFamilyStopCapConfig
    {
        private static readonly decimal[] DefaultCaps = { 150m, 200m, 250m, 300m, 350m, 400m, 500m };

        public NewYorkFamilyStopCapConfig(IEnumerable<decimal>? capTicks = null)
        {
            var values = (capTicks ?? DefaultCaps).Distinct().OrderBy(x => x).ToArray();
            if (values.Length == 0) throw new ArgumentException("At least one stop-cap value is required.", nameof(capTicks));
            if (values.Any(x => x <= 0m)) throw new ArgumentOutOfRangeException(nameof(capTicks));
            CapTicks = values;
        }

        public IReadOnlyList<decimal> CapTicks { get; }
    }

    public sealed class NewYorkFamilyStopCapOutcome
    {
        public NewYorkFamilyStopCapOutcome(NewYorkTradeableEntryType entryType, decimal capTicks,
            int totalCandidates, int retainedCandidates, int excludedCandidates, decimal averageRetainedRiskTicks,
            decimal medianRetainedRiskTicks, int hit300BeforeStop, int hit500BeforeStop, int hit1000BeforeStop,
            int stops, int excludedHit300, int excludedHit500, int excludedHit1000)
        {
            EntryType = entryType;
            CapTicks = capTicks;
            TotalCandidates = totalCandidates;
            RetainedCandidates = retainedCandidates;
            ExcludedCandidates = excludedCandidates;
            AverageRetainedRiskTicks = averageRetainedRiskTicks;
            MedianRetainedRiskTicks = medianRetainedRiskTicks;
            Hit300BeforeStop = hit300BeforeStop;
            Hit500BeforeStop = hit500BeforeStop;
            Hit1000BeforeStop = hit1000BeforeStop;
            Stops = stops;
            ExcludedHit300 = excludedHit300;
            ExcludedHit500 = excludedHit500;
            ExcludedHit1000 = excludedHit1000;
        }

        public NewYorkTradeableEntryType EntryType { get; }
        public decimal CapTicks { get; }
        public int TotalCandidates { get; }
        public int RetainedCandidates { get; }
        public int ExcludedCandidates { get; }
        public decimal AverageRetainedRiskTicks { get; }
        public decimal MedianRetainedRiskTicks { get; }
        public int Hit300BeforeStop { get; }
        public int Hit500BeforeStop { get; }
        public int Hit1000BeforeStop { get; }
        public int Stops { get; }
        public int ExcludedHit300 { get; }
        public int ExcludedHit500 { get; }
        public int ExcludedHit1000 { get; }
        public decimal Hit300Rate => RetainedCandidates == 0 ? 0m : (decimal)Hit300BeforeStop / RetainedCandidates;
        public decimal Hit500Rate => RetainedCandidates == 0 ? 0m : (decimal)Hit500BeforeStop / RetainedCandidates;
    }

    /// <summary>
    /// Research-only family-specific structural-stop-cap study. It does not choose a production cap.
    /// For each setup family it sweeps a transparent common grid of candidate maximum stop sizes and
    /// reports retained opportunity, target-before-stop rates, stop counts, and the target opportunity
    /// that would have been excluded by each cap. This is intended to expose family differences without
    /// tuning one cap to the current 42-session development sample.
    /// </summary>
    public sealed class NewYorkFamilyStopCapAnalyzer
    {
        private readonly NewYorkFamilyStopCapConfig config;

        public NewYorkFamilyStopCapAnalyzer(NewYorkFamilyStopCapConfig? config = null)
        {
            this.config = config ?? new NewYorkFamilyStopCapConfig();
        }

        public IReadOnlyList<NewYorkFamilyStopCapOutcome> Analyze(IReadOnlyList<NewYorkSetupQualityOutcome> quality)
        {
            if (quality == null) throw new ArgumentNullException(nameof(quality));
            if (quality.Count == 0) return Array.Empty<NewYorkFamilyStopCapOutcome>();

            var result = new List<NewYorkFamilyStopCapOutcome>();
            var families = new[]
            {
                NewYorkTradeableEntryType.ContinuationAfterValidatedReset,
                NewYorkTradeableEntryType.DirectReversal,
                NewYorkTradeableEntryType.ContinuationFailureReversal
            };

            foreach (var family in families)
            {
                var familyRows = quality.Where(x => x.EntryType == family).OrderBy(x => x.InitialRiskTicks).ToList();
                foreach (var cap in config.CapTicks)
                {
                    var retained = familyRows.Where(x => x.InitialRiskTicks <= cap).ToList();
                    var excluded = familyRows.Where(x => x.InitialRiskTicks > cap).ToList();
                    result.Add(new NewYorkFamilyStopCapOutcome(
                        family,
                        cap,
                        familyRows.Count,
                        retained.Count,
                        excluded.Count,
                        Average(retained.Select(x => x.InitialRiskTicks)),
                        Median(retained.Select(x => x.InitialRiskTicks)),
                        retained.Count(x => x.Hit300BeforeStop),
                        retained.Count(x => x.Hit500BeforeStop),
                        retained.Count(x => x.Hit1000BeforeStop),
                        retained.Count(x => x.StopUtc.HasValue),
                        excluded.Count(x => x.Hit300BeforeStop),
                        excluded.Count(x => x.Hit500BeforeStop),
                        excluded.Count(x => x.Hit1000BeforeStop)));
                }
            }

            return result;
        }

        private static decimal Average(IEnumerable<decimal> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? 0m : list.Average();
        }

        private static decimal Median(IEnumerable<decimal> values)
        {
            var list = values.OrderBy(x => x).ToList();
            if (list.Count == 0) return 0m;
            var middle = list.Count / 2;
            return list.Count % 2 == 1 ? list[middle] : (list[middle - 1] + list[middle]) / 2m;
        }
    }
}
