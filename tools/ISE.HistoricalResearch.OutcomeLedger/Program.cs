using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.OutcomeLedger
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 4)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.OutcomeLedger -- <path-to-contract-aware-tsv> [roundTripCommission] [slippageTicksPerSide] [pointValue]");
                return 2;
            }

            try
            {
                var roundTripCommission = args.Length >= 2 ? decimal.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 0m;
                var slippageTicksPerSide = args.Length >= 3 ? decimal.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 0m;
                var pointValue = args.Length >= 4 ? decimal.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture) : 2m;

                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var features = new NewYorkSessionResearchFeatureExtractor().Extract(bars);
                var classifier = new NewYorkRegimeSeedClassifier();
                var seedLabeler = new NewYorkOpportunitySeedLabeler();
                var classifications = features.Select(classifier.Classify).ToList();
                var seeds = classifications.SelectMany(seedLabeler.Label).ToList();
                var outcomeConfig = new NewYorkOpportunityOutcomeConfig(0.25m, pointValue, roundTripCommission, slippageTicksPerSide);
                var outcomes = new NewYorkOpportunityOutcomeLabeler(outcomeConfig).Label(bars, classifications, seeds);

                Console.WriteLine("ISE-OUTCOME-LEDGER RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + features.Count
                    + " seeds=" + seeds.Count
                    + " outcomes=" + outcomes.Count
                    + " runnerCandidates=" + outcomes.Count(x => x.RunnerCandidate)
                    + " sessionClosedFavorable=" + outcomes.Count(x => x.SessionClosedFavorable)
                    + " fullOpeningRangeReached=" + outcomes.Count(x => x.ReachedFullOpeningRange));

                foreach (NewYorkOpportunitySeedType type in Enum.GetValues(typeof(NewYorkOpportunitySeedType)))
                {
                    if (type == NewYorkOpportunitySeedType.None) continue;
                    var subset = outcomes.Where(x => x.SeedType == type).ToList();
                    Console.WriteLine("ISE-OUTCOME-LEDGER TYPE"
                        + " type=" + type
                        + " count=" + subset.Count
                        + " avgMfeTicks=" + Average(subset.Select(x => x.MfeTicks)).ToString("0.0")
                        + " avgMaeTicks=" + Average(subset.Select(x => x.MaeTicks)).ToString("0.0")
                        + " avgSessionMovePoints=" + Average(subset.Select(x => x.SessionEndMovePoints)).ToString("0.00")
                        + " favorableCloseRate=" + Rate(subset.Count(x => x.SessionClosedFavorable), subset.Count).ToString("0.000")
                        + " runnerRate=" + Rate(subset.Count(x => x.RunnerCandidate), subset.Count).ToString("0.000"));
                }

                foreach (var outcome in outcomes)
                {
                    Console.WriteLine("ISE-OUTCOME-LEDGER ROW"
                        + " date=" + outcome.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " regime=" + outcome.Regime
                        + " type=" + outcome.SeedType
                        + " direction=" + outcome.Direction
                        + " score=" + outcome.SeedScore.ToString("0.000")
                        + " entry=" + outcome.EntryPrice.ToString("0.00")
                        + " mfeTicks=" + outcome.MfeTicks.ToString("0.0")
                        + " maeTicks=" + outcome.MaeTicks.ToString("0.0")
                        + " minutesToMfe=" + outcome.MinutesToMfe
                        + " minutesToMae=" + outcome.MinutesToMae
                        + " windowMovePoints=" + outcome.OpportunityWindowCloseMovePoints.ToString("0.00")
                        + " sessionMovePoints=" + outcome.SessionEndMovePoints.ToString("0.00")
                        + " grossSessionPnl=" + outcome.GrossSessionEndPnlPerContract.ToString("0.00")
                        + " afterCostSessionPnl=" + outcome.AfterCostSessionEndPnlPerContract.ToString("0.00")
                        + " favorableOR=" + outcome.FavorableOpeningRangeMultiple.ToString("0.00")
                        + " adverseOR=" + outcome.AdverseOpeningRangeMultiple.ToString("0.00")
                        + " runner=" + outcome.RunnerCandidate);
                }

                Console.WriteLine("ISE-OUTCOME-LEDGER COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-OUTCOME-LEDGER ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal Average(IEnumerable<decimal> values)
        {
            var materialized = values.ToList();
            return materialized.Count == 0 ? 0m : materialized.Average();
        }

        private static decimal Rate(int numerator, int denominator)
        {
            return denominator == 0 ? 0m : (decimal)numerator / denominator;
        }
    }
}
