using System;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.PositionIntelligenceStudy -- <contract-aware-tsv-path>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Dataset not found: {path}");
    return 3;
}

try
{
    var bars = new HistoricalDataFileStore().ReadContractAware(path);
    var raw = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
    var potential = new MorningOpportunityPotentialAnalyzer().Analyze(bars, raw);
    var entry = new MorningEntryEfficiencyAnalyzer().Analyze(bars, potential);
    var weighted = new MorningStabilityWeightedPotentialAnalyzer().Analyze(potential);
    var candidates = new MorningDailyOpportunitySequencer().BuildCandidates(entry, weighted);

    var strict = new MorningExecutionRealisticDailyOpportunitySequencer()
        .Sequence(candidates, MorningDailySequencingPolicy.StrictUpper80)
        .Where(x => x.Selected)
        .Select(x => x.Candidate)
        .ToList();

    var managed = new MorningVectorFlowPositionIntelligenceAnalyzer()
        .Analyze(bars, strict)
        .ToList();

    Console.WriteLine("ISE Elite V7 Position Intelligence Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"V6.1 selected trades: {strict.Count}");
    Console.WriteLine($"V7 managed trades: {managed.Count}");
    Console.WriteLine("Entry authority remains frozen V6.1 StrictUpper80.");
    Console.WriteLine("Five-minute VectorFlow is post-entry management only.");
    Console.WriteLine();

    var baseline = strict.Select(x => x.Entry.Source.Source).ToList();

    Console.WriteLine("BASELINE VS V7");
    Console.WriteLine($"Baseline avg realized: {(baseline.Count == 0 ? 0m : baseline.Average(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"V7 avg realized: {(managed.Count == 0 ? 0m : managed.Average(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"Baseline positive rate: {(baseline.Count == 0 ? 0m : 100m * baseline.Count(x => x.RealizedDollars > 0m) / baseline.Count):F1}%");
    Console.WriteLine($"V7 positive rate: {(managed.Count == 0 ? 0m : 100m * managed.Count(x => x.RealizedDollars > 0m) / managed.Count):F1}%");
    Console.WriteLine($"V7 Scalp: {managed.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Scalp)}");
    Console.WriteLine($"V7 Core: {managed.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Core)}");
    Console.WriteLine($"V7 Runner: {managed.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Runner)}");
    Console.WriteLine($"V7 VectorFlow bias-loss exits: {managed.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.VectorFlowBiasLoss)}");
    Console.WriteLine($"V7 structural-stop exits: {managed.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.StructuralStop)}");
    Console.WriteLine($"V7 scalp captures: {managed.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.ScalpCapture)}");
    Console.WriteLine($"V7 scalp timeouts: {managed.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.ScalpTimeout)}");
    Console.WriteLine();

    Console.WriteLine("MONTHLY");
    Console.WriteLine("month\tbaselineN\tbaselineAvg\tv7N\tv7Avg\tv7Positive\tcore\trunner\tbiasLoss");

    foreach (var month in managed
        .Select(x => new DateTime(x.Candidate.SessionDateCentral.Year, x.Candidate.SessionDateCentral.Month, 1))
        .Distinct()
        .OrderBy(x => x))
    {
        var b = baseline.Where(x => x.SessionDateCentral.Year == month.Year && x.SessionDateCentral.Month == month.Month).ToList();
        var m = managed.Where(x => x.Candidate.SessionDateCentral.Year == month.Year && x.Candidate.SessionDateCentral.Month == month.Month).ToList();

        Console.WriteLine(string.Join("\t", new[]
        {
            month.ToString("yyyy-MM"),
            b.Count.ToString(CultureInfo.InvariantCulture),
            (b.Count == 0 ? 0m : b.Average(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            m.Count.ToString(CultureInfo.InvariantCulture),
            (m.Count == 0 ? 0m : m.Average(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            (m.Count == 0 ? 0m : 100m * m.Count(x => x.RealizedDollars > 0m) / m.Count).ToString("F1", CultureInfo.InvariantCulture) + "%",
            m.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Core).ToString(CultureInfo.InvariantCulture),
            m.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Runner).ToString(CultureInfo.InvariantCulture),
            m.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.VectorFlowBiasLoss).ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("V7 gate:");
    Console.WriteLine("- V7 must not create, defer, or re-rank entries.");
    Console.WriteLine("- VectorFlow may only change post-entry hold/exit behavior.");
    Console.WriteLine("- Runner must be earned after entry through aligned completed 5-minute state plus >=300 ticks MFE.");
    Console.WriteLine("- Promote only if management improves expectancy and/or protects winners across multiple periods.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
