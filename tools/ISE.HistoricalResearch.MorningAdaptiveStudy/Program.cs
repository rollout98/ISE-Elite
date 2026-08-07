using System;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.MorningAdaptiveStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.MorningAdaptiveStudy -- <path-to-0300-1100-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var outcomes = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
                Console.WriteLine("ISE-MORNING-ADAPTIVE RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + bars.Select(x => x.TradingDay.Date).Distinct().Count()
                    + " opportunities=" + outcomes.Count
                    + " profitable=" + outcomes.Count(x => x.Profitable)
                    + " realized500=" + outcomes.Count(x => x.ReachedFundedObjective)
                    + " realized1000=" + outcomes.Count(x => x.ReachedUpperObjective)
                    + " avgRiskTicks=" + Avg(outcomes.Select(x => x.InitialRiskTicks)).ToString("0.0")
                    + " medianRiskTicks=" + Median(outcomes.Select(x => x.InitialRiskTicks).ToArray()).ToString("0.0")
                    + " avgRealizedDollars=" + Avg(outcomes.Select(x => x.RealizedDollars)).ToString("0.0"));

                foreach (var type in Enum.GetValues(typeof(MorningAdaptiveSetupType)).Cast<MorningAdaptiveSetupType>().Where(x => x != MorningAdaptiveSetupType.None))
                {
                    var subset = outcomes.Where(x => x.SetupType == type).ToList();
                    Console.WriteLine("ISE-MORNING-ADAPTIVE TYPE"
                        + " type=" + type
                        + " count=" + subset.Count
                        + " avgRiskTicks=" + Avg(subset.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " avgRealizedDollars=" + Avg(subset.Select(x => x.RealizedDollars)).ToString("0.0")
                        + " profitable=" + subset.Count(x => x.Profitable)
                        + " scalp=" + subset.Count(x => x.FinalMode == MorningAdaptiveManagementMode.Scalp)
                        + " core=" + subset.Count(x => x.FinalMode == MorningAdaptiveManagementMode.Core)
                        + " runner=" + subset.Count(x => x.FinalMode == MorningAdaptiveManagementMode.Runner));
                }

                foreach (var state in Enum.GetValues(typeof(MorningMarketState)).Cast<MorningMarketState>())
                {
                    var subset = outcomes.Where(x => x.State == state).ToList();
                    if (subset.Count == 0) continue;
                    Console.WriteLine("ISE-MORNING-ADAPTIVE STATE"
                        + " state=" + state
                        + " count=" + subset.Count
                        + " avgRealizedDollars=" + Avg(subset.Select(x => x.RealizedDollars)).ToString("0.0")
                        + " avgMfeTicks=" + Avg(subset.Select(x => x.MaxFavorableTicks)).ToString("0.0")
                        + " avgMaeTicks=" + Avg(subset.Select(x => x.MaxAdverseTicks)).ToString("0.0"));
                }

                foreach (var group in outcomes.GroupBy(x => TimeZoneInfo.ConvertTime(x.EntryUtc, ResolveCentralTimeZone()).Hour).OrderBy(x => x.Key))
                {
                    var subset = group.ToList();
                    Console.WriteLine("ISE-MORNING-ADAPTIVE HOUR"
                        + " hourCentral=" + group.Key.ToString("00")
                        + " count=" + subset.Count
                        + " profitable=" + subset.Count(x => x.Profitable)
                        + " avgRiskTicks=" + Avg(subset.Select(x => x.InitialRiskTicks)).ToString("0.0")
                        + " avgRealizedDollars=" + Avg(subset.Select(x => x.RealizedDollars)).ToString("0.0")
                        + " runners=" + subset.Count(x => x.FinalMode == MorningAdaptiveManagementMode.Runner));
                }

                foreach (var item in outcomes)
                {
                    var entryCentral = TimeZoneInfo.ConvertTime(item.EntryUtc, ResolveCentralTimeZone());
                    Console.WriteLine("ISE-MORNING-ADAPTIVE ROW"
                        + " date=" + item.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " entryAt=" + entryCentral.ToString("HH:mm")
                        + " state=" + item.State
                        + " setup=" + item.SetupType
                        + " direction=" + item.Direction
                        + " riskTicks=" + item.InitialRiskTicks.ToString("0.0")
                        + " mode=" + item.FinalMode
                        + " exit=" + item.ExitReason
                        + " realizedDollars=" + item.RealizedDollars.ToString("0.0")
                        + " mfeTicks=" + item.MaxFavorableTicks.ToString("0.0")
                        + " maeTicks=" + item.MaxAdverseTicks.ToString("0.0"));
                }

                Console.WriteLine("ISE-MORNING-ADAPTIVE NOTE research only; 03:00-11:00 CT; multiple causal setup families; next-bar-open entries; conservative same-bar stop-first; adaptive scalp/core/runner management uses completed-bar evidence and is not production-tuned.");
                Console.WriteLine("ISE-MORNING-ADAPTIVE COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-MORNING-ADAPTIVE ERROR " + ex.GetType().Name + ": " + ex.Message);
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
