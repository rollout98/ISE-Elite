using System;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.MorningOpportunityStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.MorningOpportunityStudy -- <path-to-0300-1100-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var opportunities = new MorningOpportunityDiscoveryAnalyzer().Analyze(bars);
                Console.WriteLine("ISE-MORNING-OPPORTUNITY RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + bars.Select(x => x.TradingDay.Date).Distinct().Count()
                    + " opportunities=" + opportunities.Count
                    + " continuations=" + opportunities.Count(x => x.Type == MorningOpportunityType.ContinuationResumption)
                    + " transitions=" + opportunities.Count(x => x.Type == MorningOpportunityType.DirectionalTransition)
                    + " hit300=" + opportunities.Count(x => x.Hit300BeforeStop)
                    + " hit500=" + opportunities.Count(x => x.Hit500BeforeStop)
                    + " hit1000=" + opportunities.Count(x => x.Hit1000BeforeStop));

                foreach (var type in new[] { MorningOpportunityType.ContinuationResumption, MorningOpportunityType.DirectionalTransition })
                {
                    var subset = opportunities.Where(x => x.Type == type).ToList();
                    Console.WriteLine("ISE-MORNING-OPPORTUNITY TYPE"
                        + " type=" + type
                        + " count=" + subset.Count
                        + " avgRiskTicks=" + Avg(subset.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " medianRiskTicks=" + Median(subset.Select(x => x.InitialRiskTicks).ToArray()).ToString("0.0")
                        + " avgMoveAgeMinutes=" + Avg(subset.Select(x => (decimal)x.EstimatedMoveAgeMinutes)).ToString("0.0")
                        + " hit300=" + subset.Count(x => x.Hit300BeforeStop)
                        + " hit500=" + subset.Count(x => x.Hit500BeforeStop)
                        + " hit1000=" + subset.Count(x => x.Hit1000BeforeStop)
                        + " stops=" + subset.Count(x => x.StopUtc.HasValue));
                }

                foreach (var group in opportunities.GroupBy(x => TimeZoneInfo.ConvertTime(x.EntryUtc, ResolveCentralTimeZone()).Hour).OrderBy(x => x.Key))
                {
                    var subset = group.ToList();
                    Console.WriteLine("ISE-MORNING-OPPORTUNITY HOUR"
                        + " hourCentral=" + group.Key.ToString("00")
                        + " count=" + subset.Count
                        + " continuation=" + subset.Count(x => x.Type == MorningOpportunityType.ContinuationResumption)
                        + " transition=" + subset.Count(x => x.Type == MorningOpportunityType.DirectionalTransition)
                        + " avgRiskTicks=" + Avg(subset.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " avgMoveAgeMinutes=" + Avg(subset.Select(x => (decimal)x.EstimatedMoveAgeMinutes)).ToString("0.0")
                        + " hit300=" + subset.Count(x => x.Hit300BeforeStop)
                        + " hit500=" + subset.Count(x => x.Hit500BeforeStop)
                        + " hit1000=" + subset.Count(x => x.Hit1000BeforeStop));
                }

                foreach (var item in opportunities)
                {
                    var entryCentral = TimeZoneInfo.ConvertTime(item.EntryUtc, ResolveCentralTimeZone());
                    var originCentral = TimeZoneInfo.ConvertTime(item.EstimatedOriginUtc, ResolveCentralTimeZone());
                    Console.WriteLine("ISE-MORNING-OPPORTUNITY ROW"
                        + " date=" + item.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " type=" + item.Type
                        + " direction=" + item.Direction
                        + " originAt=" + originCentral.ToString("HH:mm")
                        + " entryAt=" + entryCentral.ToString("HH:mm")
                        + " ageMinutes=" + item.EstimatedMoveAgeMinutes
                        + " trendEfficiency=" + item.TrendEfficiency.ToString("0.000")
                        + " riskTicks=" + item.InitialRiskTicks.ToString("0.0")
                        + " hit300=" + item.Hit300BeforeStop
                        + " hit500=" + item.Hit500BeforeStop
                        + " hit1000=" + item.Hit1000BeforeStop
                        + " stop=" + item.StopUtc.HasValue);
                }

                Console.WriteLine("ISE-MORNING-OPPORTUNITY NOTE discovery window=03:00-11:00 CT; setup search ends 10:30; entries are next-bar-open; candidate times are discovered from causal structure, not prescribed clock windows; thresholds are research seeds only.");
                Console.WriteLine("ISE-MORNING-OPPORTUNITY COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-MORNING-OPPORTUNITY ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal Avg(System.Collections.Generic.IEnumerable<decimal> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? 0m : list.Average();
        }

        private static decimal Median(decimal[] values)
        {
            if (values.Length == 0) return 0m;
            Array.Sort(values);
            var m = values.Length / 2;
            return values.Length % 2 == 1 ? values[m] : (values[m - 1] + values[m]) / 2m;
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
