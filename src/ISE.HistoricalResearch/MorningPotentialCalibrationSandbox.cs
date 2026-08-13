using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningPotentialCalibrationVariant
    {
        Baseline = 0,
        StrongConsumedPenalty = 1,
        StrongExhaustionPenalty = 2,
        UpperTierRiskGate = 3,
        Combined = 4
    }

    public sealed class MorningPotentialCalibrationObservation
    {
        public MorningPotentialCalibrationObservation(
            MorningOpportunityPotentialObservation source,
            MorningPotentialCalibrationVariant variant,
            decimal calibratedScore)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Variant = variant;
            CalibratedScore = calibratedScore;
        }

        public MorningOpportunityPotentialObservation Source { get; }
        public MorningPotentialCalibrationVariant Variant { get; }
        public decimal CalibratedScore { get; }
    }

    public sealed class MorningPotentialCalibrationRow
    {
        public MorningPotentialCalibrationRow(
            string sample,
            MorningPotentialCalibrationVariant variant,
            string scoreBand,
            int count,
            decimal averageScore,
            decimal averageMfeTicks,
            decimal averageMaeTicks,
            decimal averageRealizedDollars,
            decimal positiveRate,
            int hit300,
            int hit500)
        {
            Sample = sample;
            Variant = variant;
            ScoreBand = scoreBand;
            Count = count;
            AverageScore = averageScore;
            AverageMfeTicks = averageMfeTicks;
            AverageMaeTicks = averageMaeTicks;
            AverageRealizedDollars = averageRealizedDollars;
            PositiveRate = positiveRate;
            Hit300 = hit300;
            Hit500 = hit500;
        }

        public string Sample { get; }
        public MorningPotentialCalibrationVariant Variant { get; }
        public string ScoreBand { get; }
        public int Count { get; }
        public decimal AverageScore { get; }
        public decimal AverageMfeTicks { get; }
        public decimal AverageMaeTicks { get; }
        public decimal AverageRealizedDollars { get; }
        public decimal PositiveRate { get; }
        public int Hit300 { get; }
        public int Hit500 { get; }
    }

    /// <summary>
    /// Research-only V5.4 calibration sandbox. It never mutates the baseline V5 score.
    /// Alternative scores are computed from the already-causal V5 feature object and are
    /// evaluated separately on June calibration and July validation samples.
    /// Future MFE/MAE/P&amp;L are diagnostics only and do not feed the score.
    /// </summary>
    public sealed class MorningPotentialCalibrationSandbox
    {
        public IReadOnlyList<MorningPotentialCalibrationObservation> Analyze(
            IReadOnlyList<MorningOpportunityPotentialObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));

            var result = new List<MorningPotentialCalibrationObservation>();
            foreach (var observation in observations)
            {
                foreach (MorningPotentialCalibrationVariant variant in Enum.GetValues(typeof(MorningPotentialCalibrationVariant)))
                {
                    result.Add(new MorningPotentialCalibrationObservation(
                        observation,
                        variant,
                        Score(observation, variant)));
                }
            }
            return result;
        }

        public decimal Score(
            MorningOpportunityPotentialObservation observation,
            MorningPotentialCalibrationVariant variant)
        {
            if (observation == null) throw new ArgumentNullException(nameof(observation));
            var score = observation.PotentialScore;
            var f = observation.Features;

            switch (variant)
            {
                case MorningPotentialCalibrationVariant.Baseline:
                    return score;

                case MorningPotentialCalibrationVariant.StrongConsumedPenalty:
                    score -= ConsumedPenalty(f.ConsumedDisplacementFraction);
                    break;

                case MorningPotentialCalibrationVariant.StrongExhaustionPenalty:
                    score -= ExhaustionPenalty(f.ExhaustionRisk);
                    break;

                case MorningPotentialCalibrationVariant.UpperTierRiskGate:
                    score = ApplyUpperTierRiskGate(score, f.RiskEfficiency);
                    break;

                case MorningPotentialCalibrationVariant.Combined:
                    score -= ConsumedPenalty(f.ConsumedDisplacementFraction);
                    score -= ExhaustionPenalty(f.ExhaustionRisk);
                    score = ApplyUpperTierRiskGate(score, f.RiskEfficiency);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(variant));
            }

            return Clamp(score, 0m, 100m);
        }

        public IReadOnlyList<MorningPotentialCalibrationRow> BuildRows(
            IReadOnlyList<MorningPotentialCalibrationObservation> calibrated,
            bool highEntryOnly,
            IReadOnlyList<MorningEntryEfficiencyObservation>? entryObservations = null)
        {
            if (calibrated == null) throw new ArgumentNullException(nameof(calibrated));
            if (highEntryOnly && entryObservations == null) throw new ArgumentNullException(nameof(entryObservations));

            HashSet<MorningOpportunityPotentialObservation>? highEntrySources = null;
            if (highEntryOnly)
            {
                highEntrySources = new HashSet<MorningOpportunityPotentialObservation>(
                    entryObservations!
                        .Where(x => MorningEntryEfficiencyAnalyzer.EntryBand(x.EntryEfficiencyScore) == "High")
                        .Select(x => x.Source));
            }

            var filtered = calibrated
                .Where(x => !highEntryOnly || highEntrySources!.Contains(x.Source))
                .ToList();

            var result = new List<MorningPotentialCalibrationRow>();
            foreach (var sample in new[] { "JuneCalibration", "JulyValidation" })
            {
                var sampleRows = filtered.Where(x => InSample(x.Source.Source.SessionDateCentral, sample)).ToList();
                foreach (MorningPotentialCalibrationVariant variant in Enum.GetValues(typeof(MorningPotentialCalibrationVariant)))
                {
                    var variantRows = sampleRows.Where(x => x.Variant == variant).ToList();
                    foreach (var band in Bands)
                    {
                        var members = variantRows
                            .Where(x => x.CalibratedScore >= band.Min && x.CalibratedScore < band.MaxExclusive)
                            .ToList();
                        result.Add(BuildRow(sample, variant, band.Label, members));
                    }
                }
            }
            return result;
        }

        private static readonly (string Label, decimal Min, decimal MaxExclusive)[] Bands =
        {
            ("40-54", 40m, 55m),
            ("55-69", 55m, 70m),
            ("70-79", 70m, 80m),
            ("80-89", 80m, 90m),
            ("90-100", 90m, 101m)
        };

        private static MorningPotentialCalibrationRow BuildRow(
            string sample,
            MorningPotentialCalibrationVariant variant,
            string band,
            IReadOnlyList<MorningPotentialCalibrationObservation> members)
        {
            if (members.Count == 0)
                return new MorningPotentialCalibrationRow(sample, variant, band, 0, 0m, 0m, 0m, 0m, 0m, 0, 0);

            return new MorningPotentialCalibrationRow(
                sample,
                variant,
                band,
                members.Count,
                members.Average(x => x.CalibratedScore),
                members.Average(x => x.Source.Source.MaxFavorableTicks),
                members.Average(x => x.Source.Source.MaxAdverseTicks),
                members.Average(x => x.Source.Source.RealizedDollars),
                (decimal)members.Count(x => x.Source.Source.RealizedDollars > 0m) / members.Count,
                members.Count(x => x.Source.Source.MaxFavorableTicks >= 300m),
                members.Count(x => x.Source.Source.MaxFavorableTicks >= 500m));
        }

        private static decimal ConsumedPenalty(decimal consumedFraction)
        {
            if (consumedFraction >= 1.00m) return 18m;
            if (consumedFraction >= 0.90m) return 12m;
            if (consumedFraction >= 0.85m) return 8m;
            if (consumedFraction >= 0.80m) return 4m;
            return 0m;
        }

        private static decimal ExhaustionPenalty(decimal exhaustionRisk)
        {
            if (exhaustionRisk >= 0.40m) return 16m;
            if (exhaustionRisk >= 0.30m) return 11m;
            if (exhaustionRisk >= 0.25m) return 7m;
            if (exhaustionRisk >= 0.20m) return 4m;
            return 0m;
        }

        private static decimal ApplyUpperTierRiskGate(decimal score, decimal riskEfficiency)
        {
            if (score >= 90m && riskEfficiency < 2.75m) return Math.Min(score, 89m);
            if (score >= 80m && riskEfficiency < 2.25m) return Math.Min(score, 79m);
            return score;
        }

        private static bool InSample(DateTime sessionDateCentral, string sample)
        {
            if (sessionDateCentral.Year != 2026) return false;
            if (sample == "JuneCalibration") return sessionDateCentral.Month == 6;
            if (sample == "JulyValidation") return sessionDateCentral.Month == 7;
            return false;
        }

        private static decimal Clamp(decimal value, decimal min, decimal max)
            => value < min ? min : value > max ? max : value;
    }
}
