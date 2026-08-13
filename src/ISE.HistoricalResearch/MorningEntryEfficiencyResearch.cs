using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    /// <summary>
    /// V5.2 research-only entry-efficiency layer. V5 asks how much expansion may remain;
    /// V5.2 asks whether the current entry location is structurally efficient.
    /// All inputs are causal and known by EntryUtc. Future outcome fields and descriptive
    /// state/setup labels are intentionally excluded from the score.
    /// </summary>
    public sealed class MorningEntryEfficiencyConfig
    {
        public MorningEntryEfficiencyConfig(int lookbackBars = 24, int resetLookbackBars = 10, decimal tickSize = 0.25m)
        {
            if (lookbackBars < 12) throw new ArgumentOutOfRangeException(nameof(lookbackBars));
            if (resetLookbackBars < 3 || resetLookbackBars > lookbackBars) throw new ArgumentOutOfRangeException(nameof(resetLookbackBars));
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            LookbackBars = lookbackBars;
            ResetLookbackBars = resetLookbackBars;
            TickSize = tickSize;
        }
        public int LookbackBars { get; }
        public int ResetLookbackBars { get; }
        public decimal TickSize { get; }
    }

    public sealed class MorningEntryEfficiencyFeatures
    {
        public MorningEntryEfficiencyFeatures(decimal initialRiskTicks, decimal contextRangeTicks, decimal structuralRiskFraction,
            decimal pullbackDepthFraction, decimal entryLocationFraction, int resetCount, int barsSinceLastReset,
            decimal reclaimTicks, decimal shortRangeFraction, decimal setupToEntryMinutes)
        {
            InitialRiskTicks = initialRiskTicks;
            ContextRangeTicks = contextRangeTicks;
            StructuralRiskFraction = structuralRiskFraction;
            PullbackDepthFraction = pullbackDepthFraction;
            EntryLocationFraction = entryLocationFraction;
            ResetCount = resetCount;
            BarsSinceLastReset = barsSinceLastReset;
            ReclaimTicks = reclaimTicks;
            ShortRangeFraction = shortRangeFraction;
            SetupToEntryMinutes = setupToEntryMinutes;
        }
        public decimal InitialRiskTicks { get; }
        public decimal ContextRangeTicks { get; }
        public decimal StructuralRiskFraction { get; }
        public decimal PullbackDepthFraction { get; }
        public decimal EntryLocationFraction { get; }
        public int ResetCount { get; }
        public int BarsSinceLastReset { get; }
        public decimal ReclaimTicks { get; }
        public decimal ShortRangeFraction { get; }
        public decimal SetupToEntryMinutes { get; }
    }

    public sealed class MorningEntryEfficiencyObservation
    {
        public MorningEntryEfficiencyObservation(MorningOpportunityPotentialObservation source, MorningEntryEfficiencyFeatures features, decimal entryEfficiencyScore)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Features = features ?? throw new ArgumentNullException(nameof(features));
            EntryEfficiencyScore = entryEfficiencyScore;
        }
        public MorningOpportunityPotentialObservation Source { get; }
        public MorningEntryEfficiencyFeatures Features { get; }
        public decimal EntryEfficiencyScore { get; }
        public decimal PotentialScore => Source.PotentialScore;
    }

    public enum MorningOpportunityDecisionClass { Reject = 0, Scalp = 1, Wait = 2, Qualify = 3, Good = 4, Prime = 5 }

    public sealed class MorningOpportunityMatrixRow
    {
        public MorningOpportunityMatrixRow(string potentialBand, string entryBand, MorningOpportunityDecisionClass decisionClass,
            int count, decimal averageMfeTicks, decimal averageMaeTicks, decimal averageRealizedDollars,
            decimal positiveRate, int hit300, int hit500)
        {
            PotentialBand = potentialBand;
            EntryBand = entryBand;
            DecisionClass = decisionClass;
            Count = count;
            AverageMfeTicks = averageMfeTicks;
            AverageMaeTicks = averageMaeTicks;
            AverageRealizedDollars = averageRealizedDollars;
            PositiveRate = positiveRate;
            Hit300 = hit300;
            Hit500 = hit500;
        }
        public string PotentialBand { get; }
        public string EntryBand { get; }
        public MorningOpportunityDecisionClass DecisionClass { get; }
        public int Count { get; }
        public decimal AverageMfeTicks { get; }
        public decimal AverageMaeTicks { get; }
        public decimal AverageRealizedDollars { get; }
        public decimal PositiveRate { get; }
        public int Hit300 { get; }
        public int Hit500 { get; }
    }

    public sealed class MorningEntryEfficiencyAnalyzer
    {
        private readonly MorningEntryEfficiencyConfig config;
        public MorningEntryEfficiencyAnalyzer(MorningEntryEfficiencyConfig? config = null) { this.config = config ?? new MorningEntryEfficiencyConfig(); }

        public IReadOnlyList<MorningEntryEfficiencyObservation> Analyze(IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<MorningOpportunityPotentialObservation> potentialObservations)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (potentialObservations == null) throw new ArgumentNullException(nameof(potentialObservations));
            var ordered = bars.OrderBy(x => x.TimestampUtc).ToList();
            var result = new List<MorningEntryEfficiencyObservation>();
            foreach (var observation in potentialObservations.OrderBy(x => x.Source.EntryUtc))
            {
                var features = BuildFeatures(ordered, observation.Source);
                if (features != null) result.Add(new MorningEntryEfficiencyObservation(observation, features, Score(features)));
            }
            return result;
        }

        public MorningEntryEfficiencyFeatures? BuildFeatures(IReadOnlyList<HistoricalBar> bars, MorningAdaptiveTradeOutcome candidate)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (candidate.Direction == NewYorkResearchDirection.None) return null;
            var prior = bars.Where(x => x.TimestampUtc <= candidate.EntryUtc).OrderByDescending(x => x.TimestampUtc)
                .Take(config.LookbackBars).OrderBy(x => x.TimestampUtc).ToList();
            if (prior.Count < config.LookbackBars) return null;

            var sign = candidate.Direction == NewYorkResearchDirection.Long ? 1m : -1m;
            var contextHigh = prior.Max(x => x.High);
            var contextLow = prior.Min(x => x.Low);
            var contextRange = Math.Max(config.TickSize, contextHigh - contextLow);
            var contextRangeTicks = contextRange / config.TickSize;
            var initialRiskTicks = Math.Max(0m, candidate.InitialRiskTicks);
            var structuralRiskFraction = Clamp(initialRiskTicks / Math.Max(1m, contextRangeTicks), 0m, 3m);

            var directionalExtreme = candidate.Direction == NewYorkResearchDirection.Long ? prior.Max(x => x.High) : prior.Min(x => x.Low);
            var pullbackDistance = candidate.Direction == NewYorkResearchDirection.Long
                ? Math.Max(0m, directionalExtreme - candidate.EntryPrice)
                : Math.Max(0m, candidate.EntryPrice - directionalExtreme);
            var pullbackDepthFraction = Clamp(pullbackDistance / contextRange, 0m, 2m);
            var entryLocationFraction = candidate.Direction == NewYorkResearchDirection.Long
                ? Clamp((candidate.EntryPrice - contextLow) / contextRange, 0m, 1.5m)
                : Clamp((contextHigh - candidate.EntryPrice) / contextRange, 0m, 1.5m);

            var resetWindow = prior.Skip(Math.Max(0, prior.Count - config.ResetLookbackBars)).ToList();
            var resetCount = CountResetEpisodes(resetWindow, sign);
            var barsSinceLastReset = BarsSinceLastReset(resetWindow, sign);
            var lastKnownClose = prior[prior.Count - 1].Close;
            var reclaimTicks = sign * (candidate.EntryPrice - lastKnownClose) / config.TickSize;
            var shortWindow = prior.Skip(prior.Count - Math.Min(5, prior.Count)).ToList();
            var shortRange = Math.Max(config.TickSize, shortWindow.Max(x => x.High) - shortWindow.Min(x => x.Low));
            var shortRangeFraction = Clamp(shortRange / contextRange, 0m, 2m);
            var setupToEntryMinutes = Math.Max(0m, (decimal)(candidate.EntryUtc - candidate.SetupUtc).TotalMinutes);

            return new MorningEntryEfficiencyFeatures(initialRiskTicks, contextRangeTicks, structuralRiskFraction,
                pullbackDepthFraction, entryLocationFraction, resetCount, barsSinceLastReset, reclaimTicks,
                shortRangeFraction, setupToEntryMinutes);
        }

        public decimal Score(MorningEntryEfficiencyFeatures f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            decimal score = 50m;
            if (f.StructuralRiskFraction <= 0.35m) score += 15m; else if (f.StructuralRiskFraction <= 0.55m) score += 8m; else if (f.StructuralRiskFraction >= 0.90m) score -= 15m;
            if (f.PullbackDepthFraction >= 0.10m && f.PullbackDepthFraction <= 0.40m) score += 14m; else if (f.PullbackDepthFraction > 0.55m) score -= 8m; else if (f.PullbackDepthFraction < 0.03m) score -= 7m;
            if (f.EntryLocationFraction >= 0.35m && f.EntryLocationFraction <= 0.80m) score += 10m; else if (f.EntryLocationFraction >= 0.95m) score -= 12m;
            if (f.ResetCount >= 1 && f.ResetCount <= 3) score += 12m; else if (f.ResetCount == 0) score -= 12m; else if (f.ResetCount >= 5) score -= 6m;
            if (f.BarsSinceLastReset <= 3) score += 8m; else if (f.BarsSinceLastReset >= 8) score -= 7m;
            if (f.ReclaimTicks >= 0m && f.ReclaimTicks <= 40m) score += 8m; else if (f.ReclaimTicks < -20m) score -= 8m; else if (f.ReclaimTicks > 80m) score -= 5m;
            if (f.ShortRangeFraction <= 0.40m) score += 7m; else if (f.ShortRangeFraction >= 0.80m) score -= 7m;
            if (f.SetupToEntryMinutes >= 1m && f.SetupToEntryMinutes <= 20m) score += 6m; else if (f.SetupToEntryMinutes > 30m) score -= 8m;
            return Clamp(score, 0m, 100m);
        }

        public IReadOnlyList<MorningOpportunityMatrixRow> BuildMatrix(IReadOnlyList<MorningEntryEfficiencyObservation> observations)
        {
            if (observations == null) throw new ArgumentNullException(nameof(observations));
            var result = new List<MorningOpportunityMatrixRow>();
            foreach (var p in new[] { "Low", "Medium", "High" }) foreach (var e in new[] { "Low", "Medium", "High" })
            {
                var members = observations.Where(x => PotentialBand(x.PotentialScore) == p && EntryBand(x.EntryEfficiencyScore) == e).ToList();
                result.Add(new MorningOpportunityMatrixRow(p, e, DecisionFor(p, e), members.Count,
                    members.Count == 0 ? 0m : members.Average(x => x.Source.Source.MaxFavorableTicks),
                    members.Count == 0 ? 0m : members.Average(x => x.Source.Source.MaxAdverseTicks),
                    members.Count == 0 ? 0m : members.Average(x => x.Source.Source.RealizedDollars),
                    members.Count == 0 ? 0m : (decimal)members.Count(x => x.Source.Source.RealizedDollars > 0m) / members.Count,
                    members.Count(x => x.Source.Source.MaxFavorableTicks >= 300m), members.Count(x => x.Source.Source.MaxFavorableTicks >= 500m)));
            }
            return result;
        }

        public static string PotentialBand(decimal score) => score >= 70m ? "High" : score >= 40m ? "Medium" : "Low";
        public static string EntryBand(decimal score) => score >= 70m ? "High" : score >= 40m ? "Medium" : "Low";
        public static MorningOpportunityDecisionClass DecisionFor(string p, string e)
        {
            if (p == "High" && e == "High") return MorningOpportunityDecisionClass.Prime;
            if (p == "High" && e == "Medium") return MorningOpportunityDecisionClass.Qualify;
            if (p == "High" && e == "Low") return MorningOpportunityDecisionClass.Wait;
            if (p == "Medium" && e == "High") return MorningOpportunityDecisionClass.Good;
            if (p == "Medium" && e == "Medium") return MorningOpportunityDecisionClass.Qualify;
            if (p == "Low" && e == "High") return MorningOpportunityDecisionClass.Scalp;
            return MorningOpportunityDecisionClass.Reject;
        }

        private static int CountResetEpisodes(IReadOnlyList<HistoricalBar> bars, decimal sign)
        {
            var count = 0; var inReset = false;
            foreach (var bar in bars)
            {
                var opposite = sign * (bar.Close - bar.Open) < 0m;
                if (opposite && !inReset) { count++; inReset = true; }
                else if (!opposite) inReset = false;
            }
            return count;
        }
        private static int BarsSinceLastReset(IReadOnlyList<HistoricalBar> bars, decimal sign)
        {
            for (var i = bars.Count - 1; i >= 0; i--) if (sign * (bars[i].Close - bars[i].Open) < 0m) return bars.Count - 1 - i;
            return bars.Count;
        }
        private static decimal Clamp(decimal value, decimal min, decimal max) => value < min ? min : value > max ? max : value;
    }
}
