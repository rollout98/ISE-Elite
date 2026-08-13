using System;
using System.Globalization;
using System.IO;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.OpportunityPotentialStudy -- <contract-aware-tsv-path>");
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
    var candidates = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
    var v5 = new MorningOpportunityPotentialAnalyzer();
    var potentialObservations = v5.Analyze(bars, candidates);
    var entry = new MorningEntryEfficiencyAnalyzer();
    var entryObservations = entry.Analyze(bars, potentialObservations);
    var v56 = new MorningStabilityWeightedPotentialAnalyzer();
    var weighted = v56.Analyze(potentialObservations);
    var v57 = new MorningExtendedWalkForwardAnalyzer();
    var comparisons = v57.Analyze(weighted, entryObservations);

    Console.WriteLine("ISE Elite V5.7 Extended Walk-Forward Validation");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidate opportunities: {candidates.Count}");
    Console.WriteLine($"Potential observations: {potentialObservations.Count}");
    Console.WriteLine($"Entry-efficiency observations: {entryObservations.Count}");
    Console.WriteLine("Frozen model: V5.6 stability-weighted score; upper tier is fixed at 80+");
    Console.WriteLine("No score fitting or threshold tuning is performed by V5.7.");
    Console.WriteLine();
    Console.WriteLine("cadence\twindow\tbelowCount\tupperCount\tbelowMFE\tupperMFE\tdeltaMFE\tbelowMAE\tupperMAE\tdeltaMAE\tbelowRealized\tupperRealized\tdeltaRealized\tbelowPositive\tupperPositive\tdeltaPositive\tupper300\tupper500");

    foreach (var c in comparisons)
    {
        Console.WriteLine(string.Join("\t", new[]
        {
            c.Window.Cadence,
            c.Window.Label,
            c.Below80.Count.ToString(CultureInfo.InvariantCulture),
            c.Upper80.Count.ToString(CultureInfo.InvariantCulture),
            c.Below80.AverageMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            c.Upper80.AverageMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            c.DeltaMfe.ToString("F1", CultureInfo.InvariantCulture),
            c.Below80.AverageMaeTicks.ToString("F1", CultureInfo.InvariantCulture),
            c.Upper80.AverageMaeTicks.ToString("F1", CultureInfo.InvariantCulture),
            c.DeltaMae.ToString("F1", CultureInfo.InvariantCulture),
            c.Below80.AverageRealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            c.Upper80.AverageRealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            c.DeltaRealized.ToString("F2", CultureInfo.InvariantCulture),
            (c.Below80.PositiveRate * 100m).ToString("F1", CultureInfo.InvariantCulture) + "%",
            (c.Upper80.PositiveRate * 100m).ToString("F1", CultureInfo.InvariantCulture) + "%",
            (c.DeltaPositiveRate * 100m).ToString("F1", CultureInfo.InvariantCulture) + "%",
            c.Upper80.Hit300.ToString(CultureInfo.InvariantCulture),
            c.Upper80.Hit500.ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("Interpretation: positive deltaMFE/deltaRealized favors V5.6 80+; negative deltaMAE means lower adverse excursion for 80+.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
