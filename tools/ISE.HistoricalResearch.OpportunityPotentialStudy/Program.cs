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
    var potential = new MorningOpportunityPotentialAnalyzer();
    var potentialObservations = potential.Analyze(bars, candidates);
    var entry = new MorningEntryEfficiencyAnalyzer();
    var entryObservations = entry.Analyze(bars, potentialObservations);
    var matrix = entry.BuildMatrix(entryObservations);

    Console.WriteLine("ISE Elite V5.2 Potential x Entry Efficiency Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidate opportunities: {candidates.Count}");
    Console.WriteLine($"Potential observations: {potentialObservations.Count}");
    Console.WriteLine($"Entry-efficiency observations: {entryObservations.Count}");
    Console.WriteLine();
    Console.WriteLine("potential\tentry\tdecision\tcount\tavgMFE\tavgMAE\tavgRealized\tpositiveRate\tMFE>=300\tMFE>=500");

    foreach (var row in matrix)
    {
        Console.WriteLine(string.Join("\t", new[]
        {
            row.PotentialBand,
            row.EntryBand,
            row.DecisionClass.ToString(),
            row.Count.ToString(CultureInfo.InvariantCulture),
            row.AverageMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            row.AverageMaeTicks.ToString("F1", CultureInfo.InvariantCulture),
            row.AverageRealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            (row.PositiveRate * 100m).ToString("F1", CultureInfo.InvariantCulture) + "%",
            row.Hit300.ToString(CultureInfo.InvariantCulture),
            row.Hit500.ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("Decision-class summary");
    Console.WriteLine("decision\tcount\tavgPotential\tavgEntry\tavgMFE\tavgMAE\tavgRealized\tpositiveRate\tMFE>=300\tMFE>=500");

    foreach (MorningOpportunityDecisionClass decision in Enum.GetValues(typeof(MorningOpportunityDecisionClass)))
    {
        var members = entryObservations.Where(x => MorningEntryEfficiencyAnalyzer.DecisionFor(
            MorningEntryEfficiencyAnalyzer.PotentialBand(x.PotentialScore),
            MorningEntryEfficiencyAnalyzer.EntryBand(x.EntryEfficiencyScore)) == decision).ToList();

        Console.WriteLine(string.Join("\t", new[]
        {
            decision.ToString(),
            members.Count.ToString(CultureInfo.InvariantCulture),
            (members.Count == 0 ? 0m : members.Average(x => x.PotentialScore)).ToString("F1", CultureInfo.InvariantCulture),
            (members.Count == 0 ? 0m : members.Average(x => x.EntryEfficiencyScore)).ToString("F1", CultureInfo.InvariantCulture),
            (members.Count == 0 ? 0m : members.Average(x => x.Source.Source.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
            (members.Count == 0 ? 0m : members.Average(x => x.Source.Source.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
            (members.Count == 0 ? 0m : members.Average(x => x.Source.Source.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            (members.Count == 0 ? 0m : 100m * members.Count(x => x.Source.Source.RealizedDollars > 0m) / members.Count).ToString("F1", CultureInfo.InvariantCulture) + "%",
            members.Count(x => x.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture)
        }));
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
