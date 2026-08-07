using System;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.EightFortyFiveTransitionStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.EightFortyFiveTransitionStudy -- <path-to-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var config = new NewYorkEightFortyFiveTransitionConfig();
                var rows = new NewYorkEightFortyFiveTransitionAnalyzer(config).Analyze(bars);

                Console.WriteLine("ISE-0845-TRANSITION RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + rows.Count
                    + " continue=" + rows.Count(x => x.State == NewYorkEightFortyFiveState.Continue)
                    + " reverse=" + rows.Count(x => x.State == NewYorkEightFortyFiveState.Reverse)
                    + " standAside=" + rows.Count(x => x.State == NewYorkEightFortyFiveState.StandAside)
                    + " hit500=" + rows.Count(x => x.LowerObjectiveAvailable)
                    + " hit1000=" + rows.Count(x => x.UpperObjectiveAvailable));

                foreach (var state in new[] { NewYorkEightFortyFiveState.Continue, NewYorkEightFortyFiveState.Reverse, NewYorkEightFortyFiveState.StandAside })
                {
                    var subset = rows.Where(x => x.State == state).ToList();
                    Console.WriteLine("ISE-0845-TRANSITION STATE"
                        + " state=" + state
                        + " count=" + subset.Count
                        + " hit500=" + subset.Count(x => x.LowerObjectiveAvailable)
                        + " hit1000=" + subset.Count(x => x.UpperObjectiveAvailable)
                        + " avgFavorableTicks=" + Average(subset.Select(x => x.FavorableTicks(config.TickSize))).ToString("0.0")
                        + " avgAdverseTicks=" + Average(subset.Select(x => x.AdverseTicks(config.TickSize))).ToString("0.0"));
                }

                foreach (var row in rows)
                {
                    Console.WriteLine("ISE-0845-TRANSITION ROW"
                        + " date=" + row.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " state=" + row.State
                        + " openingDirection=" + row.OpeningDirection
                        + " tradeDirection=" + row.TradeDirection
                        + " openingEfficiency=" + row.OpeningEfficiency.ToString("0.000")
                        + " signalCentral=" + TimeValue(row.SignalTimestampUtc)
                        + " entryCentral=" + TimeValue(row.ReferenceEntryTimestampUtc)
                        + " favorableTicks=" + row.FavorableTicks(config.TickSize).ToString("0.0")
                        + " adverseTicks=" + row.AdverseTicks(config.TickSize).ToString("0.0")
                        + " hit500=" + row.LowerObjectiveAvailable
                        + " hit500At=" + TimeValue(row.LowerObjectiveFirstHitUtc)
                        + " hit1000=" + row.UpperObjectiveAvailable
                        + " hit1000At=" + TimeValue(row.UpperObjectiveFirstHitUtc));
                }

                Console.WriteLine("ISE-0845-TRANSITION NOTE signal uses completed one-minute bars from 08:45-09:05; reference entry is the next bar open; outcome is measured only after that entry through 09:30 CT.");
                Console.WriteLine("ISE-0845-TRANSITION COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-0845-TRANSITION ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal Average(System.Collections.Generic.IEnumerable<decimal> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? 0m : list.Average();
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
