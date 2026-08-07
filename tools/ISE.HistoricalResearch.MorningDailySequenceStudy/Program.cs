using System;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.MorningDailySequenceStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.MorningDailySequenceStudy -- <path-to-0300-1100-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var candidates = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
                Run("COMBINE", MorningDailySequencingConfig.CombineDefault, candidates);
                Run("FUNDED", MorningDailySequencingConfig.FundedDefault, candidates);
                Console.WriteLine("ISE-MORNING-DAILY NOTE selection uses only entry-time state/setup/efficiency/risk/time context; realized/MFE/MAE are evaluation only; no costs/slippage yet; runner-miss count is diagnostic only.");
                Console.WriteLine("ISE-MORNING-DAILY COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-MORNING-DAILY ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static void Run(string label, MorningDailySequencingConfig config,
            System.Collections.Generic.IReadOnlyList<MorningAdaptiveTradeOutcome> candidates)
        {
            var days = new MorningDailySequencingAnalyzer(config).Analyze(candidates);
            var selected = days.SelectMany(x => x.SelectedTrades).ToList();
            Console.WriteLine("ISE-MORNING-DAILY RESULT stage=" + label
                + " sessions=" + days.Count
                + " candidates=" + candidates.Count
                + " selected=" + selected.Count
                + " avgTradesPerDay=" + (days.Count == 0 ? 0m : (decimal)selected.Count / days.Count).ToString("0.00")
                + " greenDays=" + days.Count(x => x.RealizedDollars > 0m)
                + " hit500Days=" + days.Count(x => x.RealizedDollars >= 500m)
                + " hit1000Days=" + days.Count(x => x.RealizedDollars >= 1000m)
                + " avgDaily=" + (days.Count == 0 ? 0m : days.Average(x => x.RealizedDollars)).ToString("0.0")
                + " medianDaily=" + Median(days.Select(x => x.RealizedDollars).ToArray()).ToString("0.0")
                + " runnerMiss=" + days.Sum(x => x.RunnerCapableButNotRunner)
                + " rejectQuality=" + days.Sum(x => x.RejectedByQuality)
                + " rejectRisk=" + days.Sum(x => x.RejectedByRisk)
                + " rejectOverlap=" + days.Sum(x => x.RejectedWhilePositionOpen)
                + " rejectGovernance=" + days.Sum(x => x.RejectedByGovernance));

            foreach (var day in days)
            {
                Console.WriteLine("ISE-MORNING-DAILY DAY stage=" + label
                    + " date=" + day.SessionDateCentral.ToString("yyyy-MM-dd")
                    + " attempts=" + day.Attempts
                    + " realized=" + day.RealizedDollars.ToString("0.0")
                    + " hit500=" + (day.RealizedDollars >= 500m)
                    + " hit1000=" + (day.RealizedDollars >= 1000m)
                    + " runnerMiss=" + day.RunnerCapableButNotRunner);

                foreach (var trade in day.SelectedTrades)
                {
                    var t = trade.Source;
                    var central = TimeZoneInfo.ConvertTime(t.EntryUtc, ResolveCentralTimeZone());
                    Console.WriteLine("ISE-MORNING-DAILY TRADE stage=" + label
                        + " date=" + day.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " entryAt=" + central.ToString("HH:mm")
                        + " score=" + trade.SelectionScore.ToString("0.0")
                        + " state=" + t.State
                        + " setup=" + t.SetupType
                        + " riskTicks=" + t.InitialRiskTicks.ToString("0.0")
                        + " mode=" + t.FinalMode
                        + " realized=" + t.RealizedDollars.ToString("0.0")
                        + " cumulative=" + trade.CumulativeAfterTrade.ToString("0.0")
                        + " mfeTicks=" + t.MaxFavorableTicks.ToString("0.0")
                        + " maeTicks=" + t.MaxAdverseTicks.ToString("0.0"));
                }
            }
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
