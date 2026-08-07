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

                Console.WriteLine("ISE-RANGE-VECTOR-V2 WARMUP oneMinuteBarsBeforeResearch=" + warmupBars);
                RunV2(bars, central);
                RunV3(bars, central);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ISE-RANGE-VECTOR ERROR " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }

        private static void RunV2(IReadOnlyList<HistoricalBar> bars, TimeZoneInfo central)
        {
            var allRows = new ProtectedRangeVectorAnalyzer().Analyze(bars);
            var rows = allRows.Where(x => x.Source.SessionDateCentral >= ResearchStartCentral.Date
                && x.Source.SessionDateCentral < ResearchEndCentral.Date).ToList();

            PrintV2Overall(rows);
            PrintV2Stage("Combine", rows.Where(x => x.CombineRiskQualified).ToList(), rows.Count(x => !x.CombineRiskQualified));
            PrintV2Stage("Funded", rows.Where(x => x.FundedRiskQualified).ToList(), rows.Count(x => !x.FundedRiskQualified));

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

            Console.WriteLine("ISE-RANGE-VECTOR-V2 NOTE entry authority remains confirmed 3-minute Range Filter only; extension is allowed only after the normal scalp objective is reached with completed 5-minute VectorFlow alignment; 100-tick breakeven, 75% peak-pullback protection, and 250-tick runner trail are research seeds from the supplied VectorFlow workflow; Combine/Funded risk flags are eligibility diagnostics only.");
            Console.WriteLine("ISE-RANGE-VECTOR-V2 COMPLETE");
        }

        private static void RunV3(IReadOnlyList<HistoricalBar> bars, TimeZoneInfo central)
        {
            var combineConfig = EfficientAdaptiveRangeVectorConfig.CombineDefault;
            var fundedConfig = EfficientAdaptiveRangeVectorConfig.FundedDefault;
            var combine = new EfficientAdaptiveRangeVectorAnalyzer(combineConfig).Analyze(bars)
                .Where(IsResearchRow).ToList();
            var funded = new EfficientAdaptiveRangeVectorAnalyzer(fundedConfig).Analyze(bars)
                .Where(IsResearchRow).ToList();

            Console.WriteLine("ISE-RANGE-VECTOR-V3 START efficientEntryMaxWaitMinutes=" + combineConfig.MaximumDeferralMinutes
                + " combineRiskCapTicks=" + combineConfig.MaximumStructuralRiskTicks.ToString("0.0")
                + " fundedRiskCapTicks=" + fundedConfig.MaximumStructuralRiskTicks.ToString("0.0")
                + " nonAlignedBE=" + combineConfig.NonAlignedBreakevenTriggerTicks
                + " extensionFloor=" + combineConfig.ExtensionProfitFloorTicks
                + " coreRetention=" + combineConfig.CoreRetentionFraction.ToString("0.00")
                + " runnerThreshold=" + combineConfig.RunnerThresholdTicks
                + " runnerTrail=" + combineConfig.RunnerTrailTicks);

            PrintV3Stage(combineConfig, combine, central);
            PrintV3Stage(fundedConfig, funded, central);
            Console.WriteLine("ISE-RANGE-VECTOR-V3 NOTE 3-minute Range Filter creates the directional opportunity; over-risk next-bar entries may defer up to 20 minutes for a pullback toward the original signal structural stop, never beyond the next Range Filter flip and never by widening the stop. Five-minute VectorFlow has hold authority only. The +100 breakeven is suppressed while completed VectorFlow remains aligned; aligned trades earn extension only after the normal 150-tick scalp objective, then use a 100-tick minimum profit floor, 40% core peak retention, and 250-tick runner trail. No daily attempt sequencing, commissions, slippage, or copy latency yet; all v3 parameters are research seeds.");
            Console.WriteLine("ISE-RANGE-VECTOR-V3 COMPLETE");
        }

        private static bool IsResearchRow(EfficientAdaptiveRangeVectorOutcome row)
        {
            return row.Source.SessionDateCentral >= ResearchStartCentral.Date
                && row.Source.SessionDateCentral < ResearchEndCentral.Date;
        }

        private static void PrintV3Stage(EfficientAdaptiveRangeVectorConfig config,
            IReadOnlyList<EfficientAdaptiveRangeVectorOutcome> rows, TimeZoneInfo central)
        {
            var selected = rows.Where(x => x.Selected).ToList();
            var managed = selected.Select(x => x.ManagedOutcome!).ToList();
            Console.WriteLine("ISE-RANGE-VECTOR-V3 STAGE stage=" + config.Stage
                + " signals=" + rows.Count
                + " selected=" + selected.Count
                + " immediate=" + selected.Count(x => x.Disposition == EfficientAdaptiveEntryDisposition.Immediate)
                + " deferred=" + selected.Count(x => x.Disposition == EfficientAdaptiveEntryDisposition.Deferred)
                + " convertedOverRisk=" + selected.Count(x => x.Source.InitialRiskTicks > config.MaximumStructuralRiskTicks)
                + " rejectedStructure=" + rows.Count(x => x.Disposition == EfficientAdaptiveEntryDisposition.RejectedStructure)
                + " rejectedNoEfficient=" + rows.Count(x => x.Disposition == EfficientAdaptiveEntryDisposition.RejectedNoEfficientEntry)
                + " avgSourceRiskTicks=" + Avg(selected.Select(x => x.Source.InitialRiskTicks)).ToString("0.0")
                + " avgEntryRiskTicks=" + Avg(selected.Select(x => x.InitialRiskTicks ?? 0m)).ToString("0.0")
                + " avgDollars=" + Avg(managed.Select(x => x.RealizedDollars)).ToString("0.0")
                + " green=" + managed.Count(x => x.RealizedDollars > 0m)
                + " adaptiveBE=" + managed.Count(x => x.AdaptiveBreakevenActivated)
                + " extended=" + managed.Count(x => x.ExtensionActivated)
                + " core=" + managed.Count(x => x.FinalMode == RangeVectorManagementMode.Core)
                + " runner=" + managed.Count(x => x.FinalMode == RangeVectorManagementMode.Runner)
                + " hit300=" + managed.Count(x => x.RealizedDollars >= 300m)
                + " hit500=" + managed.Count(x => x.RealizedDollars >= 500m)
                + " hit1000=" + managed.Count(x => x.RealizedDollars >= 1000m));

            foreach (var hourGroup in selected.GroupBy(x => TimeZoneInfo.ConvertTime(x.EntryUtc!.Value, central).Hour).OrderBy(x => x.Key))
            {
                Console.WriteLine("ISE-RANGE-VECTOR-V3 HOUR stage=" + config.Stage
                    + " hourCentral=" + hourGroup.Key.ToString("00")
                    + " count=" + hourGroup.Count()
                    + " deferred=" + hourGroup.Count(x => x.Deferred)
                    + " avgRiskTicks=" + Avg(hourGroup.Select(x => x.InitialRiskTicks ?? 0m)).ToString("0.0")
                    + " avgDollars=" + Avg(hourGroup.Select(x => x.ManagedOutcome!.RealizedDollars)).ToString("0.0")
                    + " extended=" + hourGroup.Count(x => x.ManagedOutcome!.ExtensionActivated)
                    + " runner=" + hourGroup.Count(x => x.ManagedOutcome!.FinalMode == RangeVectorManagementMode.Runner));
            }

            foreach (var row in selected)
            {
                var local = TimeZoneInfo.ConvertTime(row.EntryUtc!.Value, central);
                Console.WriteLine("ISE-RANGE-VECTOR-V3 ROW stage=" + config.Stage
                    + " date=" + row.Source.SessionDateCentral.ToString("yyyy-MM-dd")
                    + " sourceAt=" + TimeZoneInfo.ConvertTime(row.Source.EntryUtc, central).ToString("HH:mm")
                    + " entryAt=" + local.ToString("HH:mm")
                    + " direction=" + row.Source.Direction
                    + " disposition=" + row.Disposition
                    + " deferralMinutes=" + row.DeferralMinutes
                    + " sourceRiskTicks=" + row.Source.InitialRiskTicks.ToString("0.0")
                    + " entryRiskTicks=" + (row.InitialRiskTicks ?? 0m).ToString("0.0")
                    + " biasAtEntry=" + row.VectorBiasAtEntry
                    + " mode=" + row.ManagedOutcome!.FinalMode
                    + " exit=" + row.ManagedOutcome.ExitReason
                    + " dollars=" + row.ManagedOutcome.RealizedDollars.ToString("0.0")
                    + " mfeTicks=" + row.ManagedOutcome.MaxFavorableTicks.ToString("0.0")
                    + " maeTicks=" + row.ManagedOutcome.MaxAdverseTicks.ToString("0.0")
                    + " protectedTicks=" + row.ManagedOutcome.BestProtectedTicks.ToString("0.0")
                    + " adaptiveBE=" + row.ManagedOutcome.AdaptiveBreakevenActivated
                    + " extended=" + row.ManagedOutcome.ExtensionActivated);
            }
        }

        private static void PrintV2Overall(IReadOnlyList<ProtectedRangeVectorComparison> rows)
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

        private static void PrintV2Stage(string name, IReadOnlyList<ProtectedRangeVectorComparison> eligible, int rejectedRisk)
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
