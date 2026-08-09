using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.TradeableEntryStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.TradeableEntryStudy -- <path-to-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var transitionConfig = new NewYorkEightFortyFiveTransitionConfig();
                var transitions = new NewYorkEightFortyFiveTransitionAnalyzer(transitionConfig).Analyze(bars);
                var config = new NewYorkTradeableEntryConfig();
                var outcomes = new NewYorkTradeableEntryAnalyzer(config).Analyze(bars, transitions);
                var entries = outcomes.Where(x => x.HasEntry).ToList();

                Console.WriteLine("ISE-TRADEABLE-ENTRY RESULT"
                    + " bars=" + bars.Count
                    + " sessions=" + outcomes.Count
                    + " entries=" + entries.Count
                    + " continuation=" + entries.Count(x => x.EntryType == NewYorkTradeableEntryType.ContinuationAfterValidatedReset)
                    + " directReversal=" + entries.Count(x => x.EntryType == NewYorkTradeableEntryType.DirectReversal)
                    + " handoffReversal=" + entries.Count(x => x.EntryType == NewYorkTradeableEntryType.ContinuationFailureReversal)
                    + " invalidatedContinuations=" + outcomes.Count(x => x.ContinuationInvalidated)
                    + " stopFirst=" + entries.Count(x => x.SequenceResult == NewYorkTradeSequenceResult.StopFirst)
                    + " lowerFirst=" + entries.Count(x => x.SequenceResult == NewYorkTradeSequenceResult.LowerObjectiveFirst)
                    + " upperFirst=" + entries.Count(x => x.SequenceResult == NewYorkTradeSequenceResult.UpperObjectiveFirst)
                    + " timedOut=" + entries.Count(x => x.SequenceResult == NewYorkTradeSequenceResult.TimedOut));

                foreach (var type in new[]
                {
                    NewYorkTradeableEntryType.ContinuationAfterValidatedReset,
                    NewYorkTradeableEntryType.DirectReversal,
                    NewYorkTradeableEntryType.ContinuationFailureReversal
                })
                {
                    var subset = entries.Where(x => x.EntryType == type).ToList();
                    Console.WriteLine("ISE-TRADEABLE-ENTRY TYPE"
                        + " type=" + type
                        + " count=" + subset.Count
                        + " stopFirst=" + subset.Count(x => x.SequenceResult == NewYorkTradeSequenceResult.StopFirst)
                        + " lowerBeforeStop=" + subset.Count(x => x.LowerObjectiveBeforeStop)
                        + " upperBeforeStop=" + subset.Count(x => x.UpperObjectiveBeforeStop)
                        + " avgInitialRiskTicks=" + Average(subset.Select(x => x.InitialRiskTicks(config.TickSize))).ToString("0.0")
                        + " avgResetFraction=" + Average(subset.Where(x => x.ResetFraction > 0m).Select(x => x.ResetFraction)).ToString("0.000"));
                }

                foreach (var item in outcomes)
                {
                    Console.WriteLine("ISE-TRADEABLE-ENTRY ROW"
                        + " date=" + item.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " transition=" + item.TransitionState
                        + " type=" + item.EntryType
                        + " direction=" + item.Direction
                        + " invalidated=" + item.ContinuationInvalidated
                        + " invalidatedAt=" + TimeValue(item.ContinuationInvalidatedUtc)
                        + " pivotAt=" + TimeValue(item.PivotUtc)
                        + " setupAt=" + TimeValue(item.SetupCompleteUtc)
                        + " entryAt=" + TimeValue(item.EntryUtc)
                        + " resetFraction=" + item.ResetFraction.ToString("0.000")
                        + " riskTicks=" + item.InitialRiskTicks(config.TickSize).ToString("0.0")
                        + " result=" + item.SequenceResult
                        + " resolvedAt=" + TimeValue(item.SequenceResolvedUtc)
                        + " lowerAt=" + TimeValue(item.LowerObjectiveFirstHitUtc)
                        + " upperAt=" + TimeValue(item.UpperObjectiveFirstHitUtc)
                        + " stopAt=" + TimeValue(item.StopFirstHitUtc));
                }

                Console.WriteLine("ISE-TRADEABLE-ENTRY NOTE Continuation requires bounded reset, confirmed pivot, and structural resumption; destructive continuation resets hand off to reversal; same-bar stop/target ambiguity is resolved stop-first; outcome ends 09:30 CT.");
                Console.WriteLine("ISE-TRADEABLE-ENTRY COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-TRADEABLE-ENTRY ERROR " + ex.GetType().Name + ": " + ex.Message);
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
