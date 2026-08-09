using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.StructuralEntryStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.StructuralEntryStudy -- <path-to-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var transitions = new NewYorkEightFortyFiveTransitionAnalyzer().Analyze(bars);
                var config = new NewYorkStructuralEntryConfig();
                var outcomes = new NewYorkStructuralEntryAnalyzer(config).Analyze(bars, transitions);
                var entries = outcomes.Where(x => x.HasEntry).ToList();

                Console.WriteLine("ISE-STRUCTURAL-ENTRY RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + outcomes.Count
                    + " entries=" + entries.Count
                    + " continueEntries=" + entries.Count(x => x.EntryType == NewYorkCausalEntryType.ContinuationAfterReset)
                    + " reverseEntries=" + entries.Count(x => x.EntryType == NewYorkCausalEntryType.ReversalAfterConfirmation)
                    + " invalidatedContinuations=" + outcomes.Count(x => x.Disposition == NewYorkStructuralEntryDisposition.ContinuationInvalidated)
                    + " hit500=" + entries.Count(x => x.LowerObjectiveAvailable)
                    + " hit1000=" + entries.Count(x => x.UpperObjectiveAvailable));

                foreach (var type in new[] { NewYorkCausalEntryType.ContinuationAfterReset, NewYorkCausalEntryType.ReversalAfterConfirmation })
                {
                    var subset = entries.Where(x => x.EntryType == type).ToList();
                    Console.WriteLine("ISE-STRUCTURAL-ENTRY TYPE"
                        + " type=" + type
                        + " count=" + subset.Count
                        + " hit500=" + subset.Count(x => x.LowerObjectiveAvailable)
                        + " hit1000=" + subset.Count(x => x.UpperObjectiveAvailable)
                        + " avgFavorableTicks=" + Average(subset.Select(x => x.FavorableTicks(config.TickSize))).ToString("0.0")
                        + " avgAdverseTicks=" + Average(subset.Select(x => x.AdverseTicks(config.TickSize))).ToString("0.0"));
                }

                foreach (var item in outcomes)
                {
                    Console.WriteLine("ISE-STRUCTURAL-ENTRY ROW"
                        + " date=" + item.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " transition=" + item.TransitionState
                        + " disposition=" + item.Disposition
                        + " type=" + item.EntryType
                        + " direction=" + item.Direction
                        + " transitionAt=" + TimeValue(item.TransitionSignalUtc)
                        + " invalidatedAt=" + TimeValue(item.InvalidatedUtc)
                        + " setupAt=" + TimeValue(item.SetupCompleteUtc)
                        + " entryAt=" + TimeValue(item.EntryUtc)
                        + " favorableTicks=" + item.FavorableTicks(config.TickSize).ToString("0.0")
                        + " adverseTicks=" + item.AdverseTicks(config.TickSize).ToString("0.0")
                        + " hit500=" + item.LowerObjectiveAvailable
                        + " hit500At=" + TimeValue(item.LowerObjectiveFirstHitUtc)
                        + " hit1000=" + item.UpperObjectiveAvailable
                        + " hit1000At=" + TimeValue(item.UpperObjectiveFirstHitUtc));
                }

                Console.WriteLine("ISE-STRUCTURAL-ENTRY NOTE Continue must survive opening-midpoint structure, complete a reset, and break a two-bar micro-swing before next-bar entry; Reverse retains completed-bar confirmation; outcome ends 09:30 CT.");
                Console.WriteLine("ISE-STRUCTURAL-ENTRY COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-STRUCTURAL-ENTRY ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal Average(IEnumerable<decimal> values)
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
