using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    var observations = potential.Analyze(bars, candidates);
    var buckets = potential.BuildBuckets(observations);

    Console.WriteLine("ISE Elite V5 Opportunity Potential Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidate opportunities: {candidates.Count}");
    Console.WriteLine($"Scored observations: {observations.Count}");
    Console.WriteLine();
    Console.WriteLine("bucket\tcount\tavgScore\tavgMFEticks\tavgMAEticks\tavgRealized\tpositiveRate\tMFE>=300\tMFE>=500");

    foreach (var bucket in buckets)
    {
        var members = MembersFor(bucket.Label, observations);
        var avgScore = members.Count == 0 ? 0m : members.Average(x => x.PotentialScore);
        var avgMae = members.Count == 0 ? 0m : members.Average(x => x.Source.MaxAdverseTicks);
        var hit300 = members.Count(x => x.Source.MaxFavorableTicks >= 300m);
        var hit500 = members.Count(x => x.Source.MaxFavorableTicks >= 500m);

        Console.WriteLine(string.Join("\t", new[]
        {
            bucket.Label,
            bucket.Count.ToString(CultureInfo.InvariantCulture),
            avgScore.ToString("F1", CultureInfo.InvariantCulture),
            bucket.AverageMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            avgMae.ToString("F1", CultureInfo.InvariantCulture),
            bucket.AverageRealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            (bucket.PositiveOutcomeRate * 100m).ToString("F1", CultureInfo.InvariantCulture) + "%",
            hit300.ToString(CultureInfo.InvariantCulture),
            hit500.ToString(CultureInfo.InvariantCulture)
        }));
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static List<MorningOpportunityPotentialObservation> MembersFor(string label, IReadOnlyList<MorningOpportunityPotentialObservation> observations)
{
    var bounds = label switch
    {
        "0-39" => (Min: 0m, MaxExclusive: 40m),
        "40-54" => (Min: 40m, MaxExclusive: 55m),
        "55-69" => (Min: 55m, MaxExclusive: 70m),
        "70-84" => (Min: 70m, MaxExclusive: 85m),
        "85-100" => (Min: 85m, MaxExclusive: 101m),
        _ => throw new ArgumentOutOfRangeException(nameof(label))
    };

    return observations.Where(x => x.PotentialScore >= bounds.Min && x.PotentialScore < bounds.MaxExclusive).ToList();
}
