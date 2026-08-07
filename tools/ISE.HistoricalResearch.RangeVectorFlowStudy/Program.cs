using System;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.RangeVectorFlowStudy
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.RangeVectorFlowStudy -- <path-to-full-session-contract-aware-tsv>");
                return 2;
            }

            try
            {
                var bars = new HistoricalDataFileStore().ReadContractAware(args[0]);
                var central = ResolveCentralTimeZone();
                var hasEveningWarmup = bars.Any(x => TimeZoneInfo.ConvertTime(x.TimestampUtc, central).TimeOfDay >= new TimeSpan(17, 0, 0));
                if (!hasEveningWarmup)
                    throw new InvalidOperationException("Dataset does not contain the full-session evening warmup required for the 3-minute Range Filter and 5-minute VectorFlow state.");

                var rows = new RangeEntryVectorFlowHoldAnalyzer().Analyze(bars);
                Console.WriteLine("ISE-RANGE-VECTOR RESULT entries=" + rows.Count
                    + " alignedAtEntry=" + rows.Count(x => x.AlignedAtEntry)
                    + " alignedBeforeScalp=" + rows.Count(x => x.AlignedBeforeScalpExit)
                    + " avgRiskTicks=" + Avg(rows.Select(x => x.InitialRiskTicks)).ToString("0.0")
                    + " controlAvg=" + Avg(rows.Select(x => x.RangeOnlyControl.RealizedDollars)).ToString("0.0")
                    + " vectorAvg=" + Avg(rows.Select(x => x.VectorFlowHold.RealizedDollars)).ToString("0.0")
                    + " avgDelta=" + Avg(rows.Select(x => x.VectorFlowImprovementDollars)).ToString("0.0")
                    + " controlGreen=" + rows.Count(x => x.RangeOnlyControl.RealizedDollars > 0m)
                    + " vectorGreen=" + rows.Count(x => x.VectorFlowHold.RealizedDollars > 0m)
                    + " vectorBetter=" + rows.Count(x => x.VectorFlowImprovementDollars > 0m)
                    + " vectorWorse=" + rows.Count(x => x.VectorFlowImprovementDollars < 0m)
                    + " vectorCore=" + rows.Count(x => x.VectorFlowHold.FinalMode == RangeVectorManagementMode.Core)
                    + " vectorRunner=" + rows.Count(x => x.VectorFlowHold.FinalMode == RangeVectorManagementMode.Runner)
                    + " controlHit300=" + rows.Count(x => x.RangeOnlyControl.RealizedDollars >= 300m)
                    + " vectorHit300=" + rows.Count(x => x.VectorFlowHold.RealizedDollars >= 300m)
                    + " controlHit500=" + rows.Count(x => x.RangeOnlyControl.RealizedDollars >= 500m)
                    + " vectorHit500=" + rows.Count(x => x.VectorFlowHold.RealizedDollars >= 500m));

                foreach (var hourGroup in rows.GroupBy(x => TimeZoneInfo.ConvertTime(x.EntryUtc, central).Hour).OrderBy(x => x.Key))
                {
                    Console.WriteLine("ISE-RANGE-VECTOR HOUR hourCentral=" + hourGroup.Key.ToString("00")
                        + " count=" + hourGroup.Count()
                        + " aligned=" + hourGroup.Count(x => x.AlignedAtEntry || x.AlignedBeforeScalpExit)
                        + " controlAvg=" + Avg(hourGroup.Select(x => x.RangeOnlyControl.RealizedDollars)).ToString("0.0")
                        + " vectorAvg=" + Avg(hourGroup.Select(x => x.VectorFlowHold.RealizedDollars)).ToString("0.0")
                        + " delta=" + Avg(hourGroup.Select(x => x.VectorFlowImprovementDollars)).ToString("0.0"));
                }

                foreach (var row in rows)
                {
                    var local = TimeZoneInfo.ConvertTime(row.EntryUtc, central);
                    Console.WriteLine("ISE-RANGE-VECTOR ROW date=" + row.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " entryAt=" + local.ToString("HH:mm")
                        + " direction=" + row.Direction
                        + " biasAtEntry=" + row.VectorBiasAtEntry
                        + " alignedAtEntry=" + row.AlignedAtEntry
                        + " alignedBeforeScalp=" + row.AlignedBeforeScalpExit
                        + " riskTicks=" + row.InitialRiskTicks.ToString("0.0")
                        + " controlMode=" + row.RangeOnlyControl.FinalMode
                        + " controlExit=" + row.RangeOnlyControl.ExitReason
                        + " controlDollars=" + row.RangeOnlyControl.RealizedDollars.ToString("0.0")
                        + " controlMfeTicks=" + row.RangeOnlyControl.MaxFavorableTicks.ToString("0.0")
                        + " vectorMode=" + row.VectorFlowHold.FinalMode
                        + " vectorExit=" + row.VectorFlowHold.ExitReason
                        + " vectorDollars=" + row.VectorFlowHold.RealizedDollars.ToString("0.0")
                        + " vectorMfeTicks=" + row.VectorFlowHold.MaxFavorableTicks.ToString("0.0")
                        + " delta=" + row.VectorFlowImprovementDollars.ToString("0.0"));
                }

                Console.WriteLine("ISE-RANGE-VECTOR NOTE entry authority is confirmed 3-minute Range Filter flip only; fill is next one-minute bar open; 5-minute VectorFlow FTC+VIDYA has hold authority only; control is fixed scalp management; no commissions/slippage/copy latency yet; parameters are research seeds, not production settings.");
                Console.WriteLine("ISE-RANGE-VECTOR COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-RANGE-VECTOR ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static decimal Avg(System.Collections.Generic.IEnumerable<decimal> values)
        {
            var list = values.ToList();
            return list.Count == 0 ? 0m : list.Average();
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
