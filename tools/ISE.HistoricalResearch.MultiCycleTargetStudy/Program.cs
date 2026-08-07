using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.MultiCycleTargetStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.MultiCycleTargetStudy -- <path-to-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var config = new NewYorkMultiCycleTargetConfig();
                var studies = new NewYorkMultiCycleTargetAnalyzer(config).Analyze(bars);
                var cycles = studies.SelectMany(x => x.Cycles).ToList();

                var lowerOne = studies.Count(x => x.CyclesToLowerObjective == 1);
                var lowerTwo = studies.Count(x => x.CyclesToLowerObjective.HasValue && x.CyclesToLowerObjective.Value <= 2);
                var lowerThree = studies.Count(x => x.CyclesToLowerObjective.HasValue && x.CyclesToLowerObjective.Value <= 3);
                var upperOne = studies.Count(x => x.CyclesToUpperObjective == 1);
                var upperTwo = studies.Count(x => x.CyclesToUpperObjective.HasValue && x.CyclesToUpperObjective.Value <= 2);
                var upperThree = studies.Count(x => x.CyclesToUpperObjective.HasValue && x.CyclesToUpperObjective.Value <= 3);

                Console.WriteLine("ISE-MULTICYCLE-TARGET RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + studies.Count
                    + " cycles=" + cycles.Count
                    + " contracts=" + config.Contracts
                    + " lowerObjective=" + config.LowerDailyObjective.ToString("0")
                    + " upperObjective=" + config.UpperDailyObjective.ToString("0"));

                Console.WriteLine("ISE-MULTICYCLE-TARGET OBJECTIVE"
                    + " target=" + config.LowerDailyObjective.ToString("0")
                    + " oneCycle=" + lowerOne
                    + " withinTwoCycles=" + lowerTwo
                    + " withinThreeCycles=" + lowerThree);
                Console.WriteLine("ISE-MULTICYCLE-TARGET OBJECTIVE"
                    + " target=" + config.UpperDailyObjective.ToString("0")
                    + " oneCycle=" + upperOne
                    + " withinTwoCycles=" + upperTwo
                    + " withinThreeCycles=" + upperThree);

                foreach (var window in NewYorkMultiCycleTargetAnalyzer.DefaultWindows())
                {
                    var subset = cycles.Where(x => x.Window.CycleNumber == window.CycleNumber).ToList();
                    Console.WriteLine("ISE-MULTICYCLE-TARGET CYCLE"
                        + " number=" + window.CycleNumber
                        + " name=" + window.Name
                        + " window=" + window.StartCentral.ToString("hh\\:mm") + "-" + window.EndCentral.ToString("hh\\:mm")
                        + " count=" + subset.Count
                        + " avgEnvelopeDollars=" + Average(subset.Select(x => x.FavorableDollars)).ToString("0.00")
                        + " lowerAvailable=" + subset.Count(x => x.LowerObjectiveAvailable)
                        + " upperAvailable=" + subset.Count(x => x.UpperObjectiveAvailable)
                        + " long=" + subset.Count(x => x.Direction == NewYorkResearchDirection.Long)
                        + " short=" + subset.Count(x => x.Direction == NewYorkResearchDirection.Short));
                }

                foreach (var study in studies)
                {
                    Console.WriteLine("ISE-MULTICYCLE-TARGET SESSION"
                        + " date=" + study.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " cumulativeEnvelope=" + study.CumulativeEnvelopeDollars.ToString("0.00")
                        + " cyclesTo500=" + Value(study.CyclesToLowerObjective)
                        + " cyclesTo1000=" + Value(study.CyclesToUpperObjective));

                    foreach (var cycle in study.Cycles)
                    {
                        Console.WriteLine("ISE-MULTICYCLE-TARGET ROW"
                            + " date=" + study.SessionDateCentral.ToString("yyyy-MM-dd")
                            + " cycle=" + cycle.Window.CycleNumber
                            + " name=" + cycle.Window.Name
                            + " direction=" + cycle.Direction
                            + " entryCentral=" + ToCentral(cycle.EntryTimestampUtc).ToString("HH:mm")
                            + " exitCentral=" + ToCentral(cycle.ExitTimestampUtc).ToString("HH:mm")
                            + " points=" + cycle.FavorablePoints.ToString("0.00")
                            + " ticks=" + cycle.FavorableTicks.ToString("0.0")
                            + " dollars=" + cycle.FavorableDollars.ToString("0.00")
                            + " hit500=" + cycle.LowerObjectiveAvailable
                            + " hit500At=" + TimeValue(cycle.LowerObjectiveFirstHitUtc)
                            + " hit1000=" + cycle.UpperObjectiveAvailable
                            + " hit1000At=" + TimeValue(cycle.UpperObjectiveFirstHitUtc));
                    }
                }

                Console.WriteLine("ISE-MULTICYCLE-TARGET NOTE opportunity-envelope uses hindsight within each non-overlapping research window; it measures movement availability, not executable PnL.");
                Console.WriteLine("ISE-MULTICYCLE-TARGET COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-MULTICYCLE-TARGET ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal Average(IEnumerable<decimal> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? 0m : list.Average();
        }

        private static string Value(int? value) => value.HasValue ? value.Value.ToString() : "none";

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
