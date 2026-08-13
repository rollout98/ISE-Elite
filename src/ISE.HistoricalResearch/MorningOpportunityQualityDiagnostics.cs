using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    /// <summary>
    /// Research-only V5.1 diagnostics. These metrics are calculated after V5 has already
    /// assigned a causal potential score. They must never feed back into the V5 score.
    /// Their purpose is to explain whether high-potential opportunities are also efficient
    /// entries or whether they require excessive adverse excursion / structural risk first.
    /// </summary>
    public sealed class MorningOpportunityQualityBucket
    {
        public MorningOpportunityQualityBucket(
            string label,
            int count,
            decimal averageScore,
            decimal averageMfeTicks,
            decimal averageMaeTicks,
            decimal averageRealizedDollars,
            decimal positiveRate,
            decimal averageMfeMaeRatio,
            decimal averageMfeRiskRatio,
            decimal averageMoveAgeBars,
            decimal averageConsumedFraction,
            decimal averageExhaustionRisk,
            decimal averageAccelerationRatio,
            decimal averagePullbackResetCount,
            int hit300,
            int hit500,
            int hit300WithMaeAtLeast100,
            int hit500WithMaeAtLeast100)
        {
            Label = label;
            Count = count;
            AverageScore = averageScore;
            AverageMfeTicks = averageMfeTicks;
            AverageMaeTicks = averageMaeTicks;
            AverageRealizedDollars = averageRealizedDollars;
            PositiveRate = positiveRate;
            AverageMfeMaeRatio = averageMfeMaeRatio;
            AverageMfeRiskRatio = averageMfeRiskRatio;
            AverageMoveAgeBars = averageMoveAgeBars;
            AverageConsumedFraction = averageConsumedFraction;
            AverageExhaustionRisk = averageExhaustionRisk;
            AverageAccelerationRatio = averageAccelerationRatio;
            AveragePullbackResetCount = averagePullbackResetCount;
            Hit300 = hit300;
            Hit500 = hit500;
            Hit300WithMaeAtLeast100 = hit300WithMaeAtLeast100;
            Hit500WithMaeAtLeast100 = hit500WithMaeAtLeast100;
        }

        public string Label { get; }
        public int Count { get; }
        public decimal AverageScore { get; }
        public decimal AverageMfeTicks { get; }
        public decimal AverageMaeTicks { get; }
        public decimal AverageRealizedDollars { get; }
        public decimal PositiveRate { get; }
        public decimal AverageMfeMaeRatio { get; }
        public decimal AverageMfeRiskRatio { get; }
        public decimal AverageMoveAgeBars { get; }
        public decimal AverageConsumedFraction { get; }
        public decimal AverageExhaustionRisk { get; }
        public decimal AverageAccelerationRatio { get; }
        public decimal AveragePullbackResetCount { get; }
        public int Hit300 { get; }
        public int Hit500 { get; }
        public int Hit300WithMaeAtLeast100 { get; }
        public int Hit500WithMaeAtLeast100 { get; }
    }

    public sealed class MorningOpportunityQualityDimensionRow
    {
        public MorningOpportunityQualityDimensionRow(string dimension, string value, int count, decimal averageScore, decimal averageMfeTicks, decimal averageMaeTicks, decimal averageRealizedDollars)
        {
            Dimension = dimension;
            Value = value;
            Count = count;
            AverageScore = averageScore;
            AverageMfeTicks = averageMfeTicks;
            AverageMaeTicks = averageMaeTicks;
            AverageRealizedDollars = averageRealizedDollars;
        }

        public string Dimension { get; }
        public string Value { get; }
        public int Count { get; }
        public decimal AverageScore { get; }
        public decimal AverageMfeTicks { get; }
        public decimal AverageMaeTicks { get; }
        public decimal AverageRealizedDollars { get; }
    }

    public sealed class MorningOpportunityQualityDiagnosticsAnalyzer
    {
        private static readonly (string Label, decimal Min, decimal MaxExclusive)[] Buckets =
        {
            ("0-39", 0m, 40m),
            ("40-54", 40m, 55m),
            ("55-69", 55m, 70m),
            ("70-84", 70m, 85m),
            ("85-100", 85m, 101m)
        };

        public IReadOnlyList<MorningOpportunityQualityBucket> BuildBuckets(IReadOnlyList<MorningOpportunityPotentialObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));

            return Buckets.Select(x => BuildBucket(x.Label,
                observations.Where(o => o.PotentialScore >= x.Min && o.PotentialScore < x.MaxExclusive).ToList())).ToList();
        }

        public IReadOnlyList<MorningOpportunityQualityDimensionRow> BuildDimensions(IReadOnlyList<MorningOpportunityPotentialObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            var rows = new List<MorningOpportunityQualityDimensionRow>();

            AddDimension(rows, "direction", observations.GroupBy(x => x.Source.Direction.ToString()));
            AddDimension(rows, "state", observations.GroupBy(x => x.Source.State.ToString()));
            AddDimension(rows, "setup", observations.GroupBy(x => x.Source.SetupType.ToString()));
            AddDimension(rows, "hourCT", observations.GroupBy(x => ResolveCentralHour(x.Source.EntryUtc).ToString("00")));

            return rows.OrderBy(x => x.Dimension).ThenBy(x => x.Value).ToList();
        }

        private static MorningOpportunityQualityBucket BuildBucket(string label, IReadOnlyList<MorningOpportunityPotentialObservation> members)
        {
            if (members.Count == 0)
                return new MorningOpportunityQualityBucket(label, 0, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0, 0, 0, 0);

            decimal SafeMfeMae(MorningOpportunityPotentialObservation x)
                => x.Source.MaxAdverseTicks <= 0m ? x.Source.MaxFavorableTicks : x.Source.MaxFavorableTicks / x.Source.MaxAdverseTicks;
            decimal SafeMfeRisk(MorningOpportunityPotentialObservation x)
                => x.Source.InitialRiskTicks <= 0m ? 0m : x.Source.MaxFavorableTicks / x.Source.InitialRiskTicks;

            return new MorningOpportunityQualityBucket(
                label,
                members.Count,
                members.Average(x => x.PotentialScore),
                members.Average(x => x.Source.MaxFavorableTicks),
                members.Average(x => x.Source.MaxAdverseTicks),
                members.Average(x => x.Source.RealizedDollars),
                (decimal)members.Count(x => x.Source.RealizedDollars > 0m) / members.Count,
                members.Average(SafeMfeMae),
                members.Average(SafeMfeRisk),
                members.Average(x => (decimal)x.Features.MoveAgeBars),
                members.Average(x => x.Features.ConsumedDisplacementFraction),
                members.Average(x => x.Features.ExhaustionRisk),
                members.Average(x => x.Features.AccelerationRatio),
                members.Average(x => (decimal)x.Features.PullbackResetCount),
                members.Count(x => x.Source.MaxFavorableTicks >= 300m),
                members.Count(x => x.Source.MaxFavorableTicks >= 500m),
                members.Count(x => x.Source.MaxFavorableTicks >= 300m && x.Source.MaxAdverseTicks >= 100m),
                members.Count(x => x.Source.MaxFavorableTicks >= 500m && x.Source.MaxAdverseTicks >= 100m));
        }

        private static void AddDimension(List<MorningOpportunityQualityDimensionRow> rows, string dimension,
            IEnumerable<IGrouping<string, MorningOpportunityPotentialObservation>> groups)
        {
            foreach (var group in groups.OrderBy(x => x.Key))
            {
                var members = group.ToList();
                rows.Add(new MorningOpportunityQualityDimensionRow(
                    dimension,
                    group.Key,
                    members.Count,
                    members.Average(x => x.PotentialScore),
                    members.Average(x => x.Source.MaxFavorableTicks),
                    members.Average(x => x.Source.MaxAdverseTicks),
                    members.Average(x => x.Source.RealizedDollars)));
            }
        }

        private static int ResolveCentralHour(DateTimeOffset utc)
        {
            TimeZoneInfo central;
            try { central = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { central = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
            return TimeZoneInfo.ConvertTime(utc, central).Hour;
        }
    }
}
