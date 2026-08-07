using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;

namespace ISE.HistoricalResearch.RangeVectorFlowStudy
{
    internal static class Program
    {
        private static readonly DateTime ResearchStartCentral = new DateTime(2026, 6, 1, 3, 0, 0, DateTimeKind.Unspecified);
        private static readonly DateTime ResearchEndCentral = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Unspecified);

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
                var warmupBars = bars.Count(x => TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime < ResearchStartCentral);
                if (warmupBars < 1200)
                    throw new InvalidOperationException("Dataset needs at least 1,200 one-minute pre-June warmup bars before 2026-06-01 03:00 CT. Regenerate it with ISEEliteMNQIndicatorLogicDatasetProbe.");

                var allRows = new ProtectedRangeVectorAnalyzer().Analyze(bars);
                var rows = allRows.Where(x => x.Source.SessionDateCentral >= ResearchStartCentral.Date
                    && x.Source.SessionDateCentral < ResearchEndCentral.Date).ToList();

                Console.WriteLine("ISE-RANGE-VECTOR-V2 WARMUP oneMinuteBarsBeforeResearch=" + warmupBars);
                PrintOverall(rows);
                PrintStage("Combine", rows.Where(x => x.CombineRiskQualified).ToList(), rows.Count(x => !x.CombineRiskQualified));
                PrintStage("Funded", rows.Where(x => x.FundedRiskQualified).ToList(), rows.Count(x => !x.FundedRiskQualified));

                foreach (var hourGroup in rows.GroupBy(x => TimeZoneInfo.ConvertTime(x.Source.EntryUtc, central).Hour).OrderBy(x => x.Key))
                {
                    Console.WriteLine("ISE-RANGE-VECTOR-V2 HOUR hourCentral=" + hourGroup.Key.ToString("00")
                        + " count=" + hourGroup.Count()
                        + " extended=" + hourGroup.Count(x => x.ProtectedHold.ExtensionActivated)
                        + " controlAvg=" + Avg(hourGroup.Select(x => x.Source.RangeOnlyControl.RealizedDollars)).ToString("0.0")
                        + " v1Avg=" + Avg(hourGroup.Select(x => x.Source.VectorFlowHold.RealizedDollars)).ToString("0.0")
                        + " protectedAvg=" + Avg(hourGroup.Select(x => x.ProtectedHold.RealizedDollars)).ToString("0.0")
                        + " deltaVsControl=" + Avg(hourGroup.Select(x => x.ImprovementVsControlDollars)).ToString("0.0"));
                }

                foreach (var row in rows)
                {
                    var local = TimeZoneInfo.ConvertTime(row.Source.EntryUtc, central);
                    Console.WriteLine("ISE-RANGE-VECTOR-V2 ROW date=" + row.Source.SessionDateCentral.ToString("yyyy-MM-dd")
                        + " entryAt=" + local.ToString("HH:mm")
                        + " direction=" + row.Source.Direction
                        + " biasAtEntry=" + row.Source.VectorBiasAtEntry
                        + " riskTicks=" + row.Source.InitialRiskTicks.ToString("0.0")
                        + " combineRiskOK=" + row.CombineRiskQualified
                        + " fundedRiskOK=" + row.FundedRiskQualified
                        + " controlDollars=" + row.Source.RangeOnlyControl.RealizedDollars.ToString("0.0")
                        + " v1Dollars=" + row.Source.VectorFlowHold.RealizedDollars.ToString("0.0")
                        + " protectedMode=" + row.ProtectedHold.FinalMode
                        + " protectedExit=" + row.ProtectedHold.ExitReason
                        + " protectedDollars=" + row.ProtectedHold.RealizedDollars.ToString("0.0")
                        + " protectedMfeTicks=" + row.ProtectedHold.MaxFavorableTicks.ToString("0.0")
                        + " protectedFloorTicks=" + row.ProtectedHold.BestProtectedTicks.ToString("0.0")
                        + " extended=" + row.ProtectedHold.ExtensionActivated
                        + " deltaVsControl=" + row.ImprovementVsControlDollars.ToString("0.0")
                        + " deltaVsV1=" + row.ImprovementVsV1Dollars.ToString("0.0"));
                }

                Console.WriteLine("ISE-RANGE-VECTOR-V2 NOTE entry authority remains confirmed 3-minute Range Filter only; extension is allowed only after the normal scalp objective is reached with completed 5-minute VectorFlow alignment; 100-tick breakeven, 75% peak-pullback protection, and 250-tick runner trail are research seeds from the supplied VectorFlow workflow; Combine/Funded risk flags are eligibility diagnostics only; no daily attempt sequencing, commissions, slippage, or copy latency in this pass.");
                Console.WriteLine("ISE-RANGE-VECTOR-V2 COMPLETE");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-RANGE-VECTOR-V2 ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static void PrintOverall(IReadOnlyList<ProtectedRangeVectorComparison> rows)
        {
            Console.WriteLine("ISE-RANGE-VECTOR-V2 RESULT entries=" + rows.Count
                + " extended=" + rows.Count(x => x.ProtectedHold.ExtensionActivated)
                + " breakeven=" + rows.Count(x => x.ProtectedHold.BreakevenActivated)
                + " avgRiskTicks=" + Avg(rows.Select(x => x.Source.InitialRiskTicks)).ToString("0.0")
                + " controlAvg=" + Avg(rows.Select(x => x.Source.RangeOnlyControl.RealizedDollars)).ToString("0.0")
                + " v1Avg=" + Avg(rows.Select(x => x.Source.VectorFlowHold.RealizedDollars)).ToString("0.0")
                + " protectedAvg=" + Avg(rows.Select(x => x.ProtectedHold.RealizedDollars)).ToString("0.0")
                + " protectedGreen=" + rows.Count(x => x.ProtectedHold.RealizedDollars > 0m)
                + " betterVsControl=" + rows.Count(x => x.ImprovementVsControlDollars > 0m)
                + " worseVsControl=" + rows.Count(x => x.ImprovementVsControlDollars < 0m)
                + " betterVsV1=" + rows.Count(x => x.ImprovementVsV1Dollars > 0m)
                + " protectedCore=" + rows.Count(x => x.ProtectedHold.FinalMode == RangeVectorManagementMode.Core)
                + " protectedRunner=" + rows.Count(x => x.ProtectedHold.FinalMode == RangeVectorManagementMode.Runner)
                + " protectedHit300=" + rows.Count(x => x.ProtectedHold.RealizedDollars >= 300m)
                + " protectedHit500=" + rows.Count(x => x.ProtectedHold.RealizedDollars >= 500m));
        }

        private static void PrintStage(string name, IReadOnlyList<ProtectedRangeVectorComparison> eligible, int rejectedRisk)
        {
            Console.WriteLine("ISE-RANGE-VECTOR-V2 STAGE stage=" + name
                + " eligible=" + eligible.Count
                + " rejectedRisk=" + rejectedRisk
                + " avgRiskTicks=" + Avg(eligible.Select(x => x.Source.InitialRiskTicks)).ToString("0.0")
                + " controlAvg=" + Avg(eligible.Select(x => x.Source.RangeOnlyControl.RealizedDollars)).ToString("0.0")
                + " protectedAvg=" + Avg(eligible.Select(x => x.ProtectedHold.RealizedDollars)).ToString("0.0")
                + " green=" + eligible.Count(x => x.ProtectedHold.RealizedDollars > 0m)
                + " extended=" + eligible.Count(x => x.ProtectedHold.ExtensionActivated)
                + " hit300=" + eligible.Count(x => x.ProtectedHold.RealizedDollars >= 300m)
                + " hit500=" + eligible.Count(x => x.ProtectedHold.RealizedDollars >= 500m));
        }

        private static decimal Avg(IEnumerable<decimal> values)
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
