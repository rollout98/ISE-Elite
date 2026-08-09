using System;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.SetupQualityStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.SetupQualityStudy -- <path-to-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var transitions = new NewYorkEightFortyFiveTransitionAnalyzer().Analyze(bars);
                var tradeable = new NewYorkTradeableEntryAnalyzer().Analyze(bars, transitions);
                var quality = new NewYorkSetupQualityAnalyzer().Analyze(bars, transitions, tradeable);

                Console.WriteLine("ISE-SETUP-QUALITY RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + transitions.Count
                    + " candidates=" + quality.Count
                    + " gradeA=" + quality.Count(x => x.Grade == NewYorkSetupQualityGrade.A)
                    + " gradeB=" + quality.Count(x => x.Grade == NewYorkSetupQualityGrade.B)
                    + " gradeC=" + quality.Count(x => x.Grade == NewYorkSetupQualityGrade.C)
                    + " preferred=" + quality.Count(x => x.Preferred)
                    + " preferredHit300=" + quality.Count(x => x.Preferred && x.Hit300BeforeStop)
                    + " preferredHit500=" + quality.Count(x => x.Preferred && x.Hit500BeforeStop)
                    + " preferredHit1000=" + quality.Count(x => x.Preferred && x.Hit1000BeforeStop));

                foreach (var grade in new[] { NewYorkSetupQualityGrade.A, NewYorkSetupQualityGrade.B, NewYorkSetupQualityGrade.C })
                {
                    var subset = quality.Where(x => x.Grade == grade).ToList();
                    Console.WriteLine("ISE-SETUP-QUALITY GRADE"
                        + " grade=" + grade
                        + " count=" + subset.Count
                        + " avgScore=" + Avg(subset.Select(x => x.TotalScore)).ToString("0.0")
                        + " avgRiskTicks=" + Avg(subset.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " hit300=" + subset.Count(x => x.Hit300BeforeStop)
                        + " hit500=" + subset.Count(x => x.Hit500BeforeStop)
                        + " hit1000=" + subset.Count(x => x.Hit1000BeforeStop)
                        + " stops=" + subset.Count(x => x.StopUtc.HasValue));
                }

                foreach (var type in new[] { NewYorkTradeableEntryType.ContinuationAfterValidatedReset, NewYorkTradeableEntryType.DirectReversal, NewYorkTradeableEntryType.ContinuationFailureReversal })
                {
                    var subset = quality.Where(x => x.EntryType == type).ToList();
                    Console.WriteLine("ISE-SETUP-QUALITY TYPE"
                        + " type=" + type
                        + " count=" + subset.Count
                        + " preferred=" + subset.Count(x => x.Preferred)
                        + " avgScore=" + Avg(subset.Select(x => x.TotalScore)).ToString("0.0")
                        + " avgRiskTicks=" + Avg(subset.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " hit300=" + subset.Count(x => x.Preferred && x.Hit300BeforeStop)
                        + " hit500=" + subset.Count(x => x.Preferred && x.Hit500BeforeStop));
                }

                foreach (var item in quality)
                {
                    Console.WriteLine("ISE-SETUP-QUALITY ROW"
                        + " date=" + item.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " type=" + item.EntryType
                        + " direction=" + item.Direction
                        + " grade=" + item.Grade
                        + " score=" + item.TotalScore.ToString("0.0")
                        + " riskTicks=" + item.InitialRiskTicks.ToString("0.0")
                        + " riskScore=" + item.RiskScore.ToString("0.0")
                        + " bodyScore=" + item.BodyScore.ToString("0.0")
                        + " closeScore=" + item.CloseLocationScore.ToString("0.0")
                        + " impulseScore=" + item.ImpulseScore.ToString("0.0")
                        + " separationScore=" + item.SeparationScore.ToString("0.0")
                        + " hit300=" + item.Hit300BeforeStop
                        + " hit500=" + item.Hit500BeforeStop
                        + " hit1000=" + item.Hit1000BeforeStop
                        + " stop=" + item.StopUtc.HasValue);
                }

                Console.WriteLine("ISE-SETUP-QUALITY NOTE no hard 200-tick cutoff; scores are transparent research labels only; preferred=A/B; objectives are tracked through 09:30 CT with same-bar stop-first handling.");
                Console.WriteLine("ISE-SETUP-QUALITY COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-SETUP-QUALITY ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal Avg(System.Collections.Generic.IEnumerable<decimal> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? 0m : list.Average();
        }
    }
}
