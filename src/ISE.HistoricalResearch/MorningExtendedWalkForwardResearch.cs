using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class MorningExtendedWalkForwardWindow
    {
        public MorningExtendedWalkForwardWindow(string label, DateTime start, DateTime endExclusive, string cadence)
        {
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Start = start.Date;
            EndExclusive = endExclusive.Date;
            Cadence = cadence ?? throw new ArgumentNullException(nameof(cadence));
        }

        public string Label { get; }
        public DateTime Start { get; }
        public DateTime EndExclusive { get; }
        public string Cadence { get; }
    }

    public sealed class MorningExtendedWalkForwardRow
    {
        public MorningExtendedWalkForwardRow(
            MorningExtendedWalkForwardWindow window,
            string tier,
            int count,
            decimal averageScore,
            decimal averageMfeTicks,
            decimal averageMaeTicks,
            decimal averageRealizedDollars,
            decimal positiveRate,
            int hit300,
            int hit500)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            Tier = tier ?? throw new ArgumentNullException(nameof(tier));
            Count = count;
            AverageScore = averageScore;
            AverageMfeTicks = averageMfeTicks;
            AverageMaeTicks = averageMaeTicks;
            AverageRealizedDollars = averageRealizedDollars;
            PositiveRate = positiveRate;
            Hit300 = hit300;
            Hit500 = hit500;
        }

        public MorningExtendedWalkForwardWindow Window { get; }
        public string Tier { get; }
        public int Count { get; }
        public decimal AverageScore { get; }
        public decimal AverageMfeTicks { get; }
        public decimal AverageMaeTicks { get; }
        public decimal AverageRealizedDollars { get; }
        public decimal PositiveRate { get; }
        public int Hit300 { get; }
        public int Hit500 { get; }
    }

    public sealed class MorningExtendedWalkForwardComparison
    {
        public MorningExtendedWalkForwardComparison(
            MorningExtendedWalkForwardWindow window,
            MorningExtendedWalkForwardRow below80,
            MorningExtendedWalkForwardRow upper80,
            decimal deltaMfe,
            decimal deltaMae,
            decimal deltaRealized,
            decimal deltaPositiveRate)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            Below80 = below80 ?? throw new ArgumentNullException(nameof(below80));
            Upper80 = upper80 ?? throw new ArgumentNullException(nameof(upper80));
            DeltaMfe = deltaMfe;
            DeltaMae = deltaMae;
            DeltaRealized = deltaRealized;
            DeltaPositiveRate = deltaPositiveRate;
        }

        public MorningExtendedWalkForwardWindow Window { get; }
        public MorningExtendedWalkForwardRow Below80 { get; }
        public MorningExtendedWalkForwardRow Upper80 { get; }
        public decimal DeltaMfe { get; }
        public decimal DeltaMae { get; }
        public decimal DeltaRealized { get; }
        public decimal DeltaPositiveRate { get; }
    }

    /// <summary>
    /// V5.7 research-only walk-forward evaluator for the frozen V5.6 score.
    /// It never changes the score or tunes thresholds. It only evaluates the unchanged
    /// V5.6 80+ tier against the below-80 population over dynamic monthly and half-month
    /// blocks derived from whatever historical dataset is supplied.
    /// </summary>
    public sealed class MorningExtendedWalkForwardAnalyzer
    {
        public const decimal UpperTierThreshold = 80m;

        public IReadOnlyList<MorningExtendedWalkForwardWindow> BuildWindows(IEnumerable<DateTime> sessionDates)
        {
            if (sessionDates == null) throw new ArgumentNullException(nameof(sessionDates));
            var dates = sessionDates.Select(x => x.Date).Distinct().OrderBy(x => x).ToList();
            if (dates.Count == 0) return Array.Empty<MorningExtendedWalkForwardWindow>();

            var firstMonth = new DateTime(dates[0].Year, dates[0].Month, 1);
            var lastMonth = new DateTime(dates[dates.Count - 1].Year, dates[dates.Count - 1].Month, 1);
            var result = new List<MorningExtendedWalkForwardWindow>();

            for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
            {
                var nextMonth = month.AddMonths(1);
                if (!dates.Any(x => x >= month && x < nextMonth)) continue;

                result.Add(new MorningExtendedWalkForwardWindow(
                    month.ToString("yyyy-MM"), month, nextMonth, "Monthly"));

                var secondHalf = new DateTime(month.Year, month.Month, 16);
                if (dates.Any(x => x >= month && x < secondHalf))
                {
                    result.Add(new MorningExtendedWalkForwardWindow(
                        month.ToString("yyyy-MM") + "-H1", month, secondHalf, "HalfMonth"));
                }
                if (dates.Any(x => x >= secondHalf && x < nextMonth))
                {
                    result.Add(new MorningExtendedWalkForwardWindow(
                        month.ToString("yyyy-MM") + "-H2", secondHalf, nextMonth, "HalfMonth"));
                }
            }

            return result
                .OrderBy(x => x.Start)
                .ThenBy(x => x.Cadence == "Monthly" ? 0 : 1)
                .ToList();
        }

        public IReadOnlyList<MorningExtendedWalkForwardComparison> Analyze(
            IReadOnlyList<MorningStabilityWeightedPotentialObservation> weighted,
            IReadOnlyList<MorningEntryEfficiencyObservation> entryObservations)
        {
            if (weighted == null) throw new ArgumentNullException(nameof(weighted));
            if (entryObservations == null) throw new ArgumentNullException(nameof(entryObservations));

            var highEntrySources = new HashSet<MorningOpportunityPotentialObservation>(
                entryObservations
                    .Where(x => MorningEntryEfficiencyAnalyzer.EntryBand(x.EntryEfficiencyScore) == "High")
                    .Select(x => x.Source));

            var eligible = weighted
                .Where(x => highEntrySources.Contains(x.Source))
                .OrderBy(x => x.Source.Source.SessionDateCentral)
                .ToList();

            var windows = BuildWindows(eligible.Select(x => x.Source.Source.SessionDateCentral));
            var result = new List<MorningExtendedWalkForwardComparison>();

            foreach (var window in windows)
            {
                var inWindow = eligible
                    .Where(x => x.Source.Source.SessionDateCentral >= window.Start &&
                                x.Source.Source.SessionDateCentral < window.EndExclusive)
                    .ToList();

                var below = BuildRow(window, "Below80", inWindow.Where(x => x.StabilityWeightedScore < UpperTierThreshold).ToList());
                var upper = BuildRow(window, "Upper80Plus", inWindow.Where(x => x.StabilityWeightedScore >= UpperTierThreshold).ToList());

                result.Add(new MorningExtendedWalkForwardComparison(
                    window,
                    below,
                    upper,
                    upper.AverageMfeTicks - below.AverageMfeTicks,
                    upper.AverageMaeTicks - below.AverageMaeTicks,
                    upper.AverageRealizedDollars - below.AverageRealizedDollars,
                    upper.PositiveRate - below.PositiveRate));
            }

            return result;
        }

        public static string TierForScore(decimal score)
            => score >= UpperTierThreshold ? "Upper80Plus" : "Below80";

        private static MorningExtendedWalkForwardRow BuildRow(
            MorningExtendedWalkForwardWindow window,
            string tier,
            IReadOnlyList<MorningStabilityWeightedPotentialObservation> members)
        {
            if (members.Count == 0)
                return new MorningExtendedWalkForwardRow(window, tier, 0, 0m, 0m, 0m, 0m, 0m, 0, 0);

            return new MorningExtendedWalkForwardRow(
                window,
                tier,
                members.Count,
                members.Average(x => x.StabilityWeightedScore),
                members.Average(x => x.Source.Source.MaxFavorableTicks),
                members.Average(x => x.Source.Source.MaxAdverseTicks),
                members.Average(x => x.Source.Source.RealizedDollars),
                (decimal)members.Count(x => x.Source.Source.RealizedDollars > 0m) / members.Count,
                members.Count(x => x.Source.Source.MaxFavorableTicks >= 300m),
                members.Count(x => x.Source.Source.MaxFavorableTicks >= 500m));
        }
    }
}
