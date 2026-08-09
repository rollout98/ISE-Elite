using System;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.FamilyStopCapStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.FamilyStopCapStudy -- <path-to-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var transitions = new NewYorkEightFortyFiveTransitionAnalyzer().Analyze(bars);
                var tradeable = new NewYorkTradeableEntryAnalyzer().Analyze(bars, transitions);
                var quality = new NewYorkSetupQualityAnalyzer().Analyze(bars, transitions, tradeable);
                var results = new NewYorkFamilyStopCapAnalyzer().Analyze(quality);

                Console.WriteLine("ISE-FAMILY-STOP-CAP RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + transitions.Count
                    + " candidates=" + quality.Count
                    + " caps=150,200,250,300,350,400,500");

                foreach (var row in results)
                {
                    Console.WriteLine("ISE-FAMILY-STOP-CAP ROW"
                        + " type=" + row.EntryType
                        + " capTicks=" + row.CapTicks.ToString("0.0")
                        + " total=" + row.TotalCandidates
                        + " retained=" + row.RetainedCandidates
                        + " excluded=" + row.ExcludedCandidates
                        + " avgRetainedRiskTicks=" + row.AverageRetainedRiskTicks.ToString("0.0")
                        + " medianRetainedRiskTicks=" + row.MedianRetainedRiskTicks.ToString("0.0")
                        + " hit300=" + row.Hit300BeforeStop
                        + " hit300Rate=" + row.Hit300Rate.ToString("0.000")
                        + " hit500=" + row.Hit500BeforeStop
                        + " hit500Rate=" + row.Hit500Rate.ToString("0.000")
                        + " hit1000=" + row.Hit1000BeforeStop
                        + " stops=" + row.Stops
                        + " excludedHit300=" + row.ExcludedHit300
                        + " excludedHit500=" + row.ExcludedHit500
                        + " excludedHit1000=" + row.ExcludedHit1000);
                }

                foreach (var family in new[]
                {
                    NewYorkTradeableEntryType.ContinuationAfterValidatedReset,
                    NewYorkTradeableEntryType.DirectReversal,
                    NewYorkTradeableEntryType.ContinuationFailureReversal
                })
                {
                    var familyRows = quality.Where(x => x.EntryType == family).OrderBy(x => x.InitialRiskTicks).ToList();
                    Console.WriteLine("ISE-FAMILY-STOP-CAP DISTRIBUTION"
                        + " type=" + family
                        + " count=" + familyRows.Count
                        + " minRiskTicks=" + (familyRows.Count == 0 ? "0.0" : familyRows.First().InitialRiskTicks.ToString("0.0"))
                        + " medianRiskTicks=" + Median(familyRows.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " maxRiskTicks=" + (familyRows.Count == 0 ? "0.0" : familyRows.Last().InitialRiskTicks.ToString("0.0"))
                        + " totalHit300=" + familyRows.Count(x => x.Hit300BeforeStop)
                        + " totalHit500=" + familyRows.Count(x => x.Hit500BeforeStop)
                        + " totalHit1000=" + familyRows.Count(x => x.Hit1000BeforeStop));
                }

                Console.WriteLine("ISE-FAMILY-STOP-CAP NOTE common cap grid is a research sweep, not a production stop rule; compare retention, target-before-stop rates, and excluded winners separately by setup family before selecting any cap.");
                Console.WriteLine("ISE-FAMILY-STOP-CAP COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-FAMILY-STOP-CAP ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal Median(System.Collections.Generic.IEnumerable<decimal> values)
        {
            var list = values.OrderBy(x => x).ToList();
            if (list.Count == 0) return 0m;
            var middle = list.Count / 2;
            return list.Count % 2 == 1 ? list[middle] : (list[middle - 1] + list[middle]) / 2m;
        }
    }
}
