using System;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.MorningOpportunityReadinessStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.MorningOpportunityReadinessStudy -- <path-to-0300-1100-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var candidates = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
                Run("COMBINE", MorningOpportunityReadinessConfig.CombineDefault, candidates);
                Run("FUNDED", MorningOpportunityReadinessConfig.FundedDefault, candidates);
                Console.WriteLine("ISE-MORNING-READINESS NOTE selection uses causal entry-time readiness only; missed-better and missed-runner diagnostics use future outcomes after sequencing and are evaluation-only.");
                Console.WriteLine("ISE-MORNING-READINESS COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-MORNING-READINESS ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static void Run(string label, MorningOpportunityReadinessConfig config,
            System.Collections.Generic.IReadOnlyList<MorningAdaptiveTradeOutcome> candidates)
        {
            var days = new MorningOpportunityReadinessAnalyzer(config).Analyze(candidates);
            var selected = days.SelectMany(x => x.SelectedTrades).ToList();
            var decisions = days.SelectMany(x => x.Decisions).ToList();

            Console.WriteLine("ISE-MORNING-READINESS RESULT stage=" + label
                + " sessions=" + days.Count
                + " candidates=" + candidates.Count
                + " selected=" + selected.Count
                + " avgTradesPerDay=" + (days.Count == 0 ? 0m : (decimal)selected.Count / days.Count).ToString("0.00")
                + " greenDays=" + days.Count(x => x.RealizedDollars > 0m)
                + " hit500Days=" + days.Count(x => x.RealizedDollars >= 500m)
                + " hit1000Days=" + days.Count(x => x.RealizedDollars >= 1000m)
                + " avgDaily=" + (days.Count == 0 ? 0m : days.Average(x => x.RealizedDollars)).ToString("0.0")
                + " medianDaily=" + Median(days.Select(x => x.RealizedDollars).ToArray()).ToString("0.0")
                + " observe=" + decisions.Count(x => x.Readiness == MorningOpportunityReadiness.Observe)
                + " tradeable=" + decisions.Count(x => x.Readiness == MorningOpportunityReadiness.Tradeable)
                + " actionable=" + decisions.Count(x => x.Readiness == MorningOpportunityReadiness.Actionable)
                + " exceptional=" + decisions.Count(x => x.Readiness == MorningOpportunityReadiness.Exceptional)
                + " deferred=" + decisions.Count(x => x.Reason == "Deferred")
                + " missedBetter=" + days.Sum(x => x.MissedBetterOpportunities)
                + " missedRunner=" + days.Sum(x => x.MissedRunnerCapableOpportunities));

            foreach (var day in days)
            {
                Console.WriteLine("ISE-MORNING-READINESS DAY stage=" + label
                    + " date=" + day.SessionDateCentral.ToString("yyyy-MM-dd")
                    + " attempts=" + day.Attempts
                    + " realized=" + day.RealizedDollars.ToString("0.0")
                    + " missedBetter=" + day.MissedBetterOpportunities
                    + " missedRunner=" + day.MissedRunnerCapableOpportunities);

                foreach (var trade in day.SelectedTrades)
                {
                    var source = trade.Source;
                    var central = TimeZoneInfo.ConvertTime(source.EntryUtc, ResolveCentralTimeZone());
                    Console.WriteLine("ISE-MORNING-READINESS TRADE stage=" + label
                        + " date=" + day.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " entryAt=" + central.ToString("HH:mm")
                        + " score=" + trade.SelectionScore.ToString("0.0")
                        + " state=" + source.State
                        + " setup=" + source.SetupType
                        + " riskTicks=" + source.InitialRiskTicks.ToString("0.0")
                        + " mode=" + source.FinalMode
                        + " realized=" + source.RealizedDollars.ToString("0.0")
                        + " cumulative=" + trade.CumulativeAfterTrade.ToString("0.0")
                        + " mfeTicks=" + source.MaxFavorableTicks.ToString("0.0")
                        + " maeTicks=" + source.MaxAdverseTicks.ToString("0.0"));
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
