using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.RiskQualifiedEntryStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.RiskQualifiedEntryStudy -- <path-to-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var transitions = new NewYorkEightFortyFiveTransitionAnalyzer().Analyze(bars);
                var tradeable = new NewYorkTradeableEntryAnalyzer().Analyze(bars, transitions);
                var config = new NewYorkRiskQualifiedEntryConfig();
                var qualified = new NewYorkRiskQualifiedEntryAnalyzer(config).Analyze(bars, transitions, tradeable);
                var accepted = qualified.Where(x => x.HasAcceptedEntry).ToList();
                var paths = new NewYorkObjectivePathAnalyzer(config).Analyze(bars, qualified);

                Console.WriteLine("ISE-RISK-QUALIFIED RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + qualified.Count
                    + " priorEntries=" + tradeable.Count(x => x.HasEntry)
                    + " accepted=" + accepted.Count
                    + " riskRejected=" + qualified.Count(x => x.Disposition == NewYorkRiskQualifiedDisposition.RejectedRisk)
                    + " handoffNoSetup=" + qualified.Count(x => x.Disposition == NewYorkRiskQualifiedDisposition.HandoffNoSetup)
                    + " noEntry=" + qualified.Count(x => x.Disposition == NewYorkRiskQualifiedDisposition.NoEntry)
                    + " hit300BeforeStop=" + paths.Count(x => x.Hit300BeforeStop)
                    + " hit500BeforeStop=" + paths.Count(x => x.Hit500BeforeStop)
                    + " hit1000BeforeStop=" + paths.Count(x => x.Hit1000BeforeStop)
                    + " stopOccurred=" + paths.Count(x => x.StopOccurred));

                foreach (var type in new[]
                {
                    NewYorkTradeableEntryType.ContinuationAfterValidatedReset,
                    NewYorkTradeableEntryType.DirectReversal,
                    NewYorkTradeableEntryType.ContinuationFailureReversal
                })
                {
                    var subset = accepted.Where(x => x.EntryType == type).ToList();
                    var subsetPaths = paths.Where(x => x.EntryType == type).ToList();
                    Console.WriteLine("ISE-RISK-QUALIFIED TYPE"
                        + " type=" + type
                        + " accepted=" + subset.Count
                        + " avgRiskTicks=" + Average(subset.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " medianRiskTicks=" + Median(subset.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " hit300BeforeStop=" + subsetPaths.Count(x => x.Hit300BeforeStop)
                        + " hit500BeforeStop=" + subsetPaths.Count(x => x.Hit500BeforeStop)
                        + " hit1000BeforeStop=" + subsetPaths.Count(x => x.Hit1000BeforeStop)
                        + " stops=" + subsetPaths.Count(x => x.StopOccurred));
                }

                var pathByDate = paths.ToDictionary(x => x.SessionDateCentral.Date, x => x);
                foreach (var item in qualified)
                {
                    pathByDate.TryGetValue(item.SessionDateCentral.Date, out var path);
                    Console.WriteLine("ISE-RISK-QUALIFIED ROW"
                        + " date=" + item.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " disposition=" + item.Disposition
                        + " type=" + item.EntryType
                        + " direction=" + item.Direction
                        + " setupAt=" + TimeValue(item.SetupCompleteUtc)
                        + " entryAt=" + TimeValue(item.EntryUtc)
                        + " riskTicks=" + item.InitialRiskTicks.ToString("0.0")
                        + " hit300BeforeStop=" + (path?.Hit300BeforeStop ?? false)
                        + " hit300At=" + TimeValue(path?.First300Utc)
                        + " hit500BeforeStop=" + (path?.Hit500BeforeStop ?? false)
                        + " hit500At=" + TimeValue(path?.First500Utc)
                        + " hit1000BeforeStop=" + (path?.Hit1000BeforeStop ?? false)
                        + " hit1000At=" + TimeValue(path?.First1000Utc)
                        + " stopAt=" + TimeValue(path?.StopUtc));
                }

                Console.WriteLine("ISE-RISK-QUALIFIED NOTE risk ceiling=200 ticks; continuation-failure reversals require opposite impulse/retest/resumption; $300/$500/$1000 are tracked through 09:30 CT; same-bar stop/target ambiguity is stop-first.");
                Console.WriteLine("ISE-RISK-QUALIFIED COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-RISK-QUALIFIED ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
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
            var mid = list.Count / 2;
            return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) / 2m;
        }

        private static string TimeValue(DateTimeOffset? value)
        {
            return value.HasValue ? ToCentral(value.Value).ToString("HH:mm") : "none";
        }

        private static DateTime ToCentral(DateTimeOffset utc)
        {
            var central = ResolveCentralTimeZone();
            return TimeZoneInfo.ConvertTime(utc, central).DateTime;
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
