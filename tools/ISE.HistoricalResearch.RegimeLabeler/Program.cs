using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.RegimeLabeler
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.RegimeLabeler -- <path-to-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var features = new NewYorkSessionResearchFeatureExtractor().Extract(bars);
                var classifier = new NewYorkRegimeSeedClassifier();
                var labeler = new NewYorkOpportunitySeedLabeler();
                var classifications = features.Select(classifier.Classify).ToList();
                var labels = classifications.SelectMany(labeler.Label).ToList();

                Console.WriteLine("ISE-REGIME-LABELER RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + features.Count
                    + " regimeClassifications=" + classifications.Count
                    + " opportunitySeeds=" + labels.Count);

                foreach (NewYorkResearchRegime regime in Enum.GetValues(typeof(NewYorkResearchRegime)))
                {
                    var subset = classifications.Where(x => x.Regime == regime).ToList();
                    Console.WriteLine("ISE-REGIME-LABELER REGIME"
                        + " name=" + regime
                        + " count=" + subset.Count
                        + " avgScore=" + AverageScore(subset).ToString("0.000"));
                }

                foreach (NewYorkOpportunitySeedType type in Enum.GetValues(typeof(NewYorkOpportunitySeedType)))
                {
                    if (type == NewYorkOpportunitySeedType.None) continue;
                    var subset = labels.Where(x => x.Type == type).ToList();
                    Console.WriteLine("ISE-REGIME-LABELER OPPORTUNITY"
                        + " type=" + type
                        + " count=" + subset.Count
                        + " avgScore=" + AverageLabelScore(subset).ToString("0.000"));
                }

                foreach (var c in classifications)
                {
                    var sessionLabels = labels.Where(x => x.SessionDateCentral == c.Features.SessionDateCentral).ToList();
                    Console.WriteLine("ISE-REGIME-LABELER SESSION"
                        + " date=" + c.Features.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " regime=" + c.Regime
                        + " score=" + c.Score.ToString("0.000")
                        + " openingDirection=" + c.Features.OpeningDirection
                        + " openingEfficiency=" + c.Features.OpeningEfficiency.ToString("0.000")
                        + " preOpenRange=" + c.Features.PreOpenRange.ToString("0.00")
                        + " openingRange=" + c.Features.OpeningRange.ToString("0.00")
                        + " earlyAdverse=" + c.Features.EarlyAdverseExcursion.ToString("0.00")
                        + " laterDisplacement=" + c.Features.LaterDisplacement.ToString("0.00")
                        + " seeds=" + sessionLabels.Count);
                }

                Console.WriteLine("ISE-REGIME-LABELER COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-REGIME-LABELER ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal AverageScore(IReadOnlyList<NewYorkRegimeClassification> values)
        {
            return values.Count == 0 ? 0m : values.Average(x => x.Score);
        }

        private static decimal AverageLabelScore(IReadOnlyList<NewYorkOpportunitySeedLabel> values)
        {
            return values.Count == 0 ? 0m : values.Average(x => x.Score);
        }
    }
}
