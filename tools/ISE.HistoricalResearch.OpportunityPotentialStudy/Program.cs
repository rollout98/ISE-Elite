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
    var v5 = new MorningOpportunityPotentialAnalyzer();
    var potentialObservations = v5.Analyze(bars, candidates);
    var entry = new MorningEntryEfficiencyAnalyzer();
    var entryObservations = entry.Analyze(bars, potentialObservations);

    var highEntrySources = new HashSet<MorningOpportunityPotentialObservation>(
        entryObservations
            .Where(x => MorningEntryEfficiencyAnalyzer.EntryBand(x.EntryEfficiencyScore) == "High")
            .Select(x => x.Source));

    var v56 = new MorningStabilityWeightedPotentialAnalyzer();
    var weighted = v56.Analyze(potentialObservations)
        .Where(x => highEntrySources.Contains(x.Source))
        .ToList();

    var windows = new[]
    {
        (Label: "Jun01-15", Start: new DateTime(2026, 6, 1), EndExclusive: new DateTime(2026, 6, 16)),
        (Label: "Jun16-30", Start: new DateTime(2026, 6, 16), EndExclusive: new DateTime(2026, 7, 1)),
        (Label: "Jul01-15", Start: new DateTime(2026, 7, 1), EndExclusive: new DateTime(2026, 7, 16)),
        (Label: "Jul16-31", Start: new DateTime(2026, 7, 16), EndExclusive: new DateTime(2026, 8, 1)),
        (Label: "June", Start: new DateTime(2026, 6, 1), EndExclusive: new DateTime(2026, 7, 1)),
        (Label: "July", Start: new DateTime(2026, 7, 1), EndExclusive: new DateTime(2026, 8, 1))
    };

    var bands = new[]
    {
        (Label: "0-39", Min: 0m, MaxExclusive: 40m),
        (Label: "40-54", Min: 40m, MaxExclusive: 55m),
        (Label: "55-69", Min: 55m, MaxExclusive: 70m),
        (Label: "70-79", Min: 70m, MaxExclusive: 80m),
        (Label: "80-89", Min: 80m, MaxExclusive: 90m),
        (Label: "90-100", Min: 90m, MaxExclusive: 101m)
    };

    Console.WriteLine("ISE Elite V5.6 Stability-Weighted Potential Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidate opportunities: {candidates.Count}");
    Console.WriteLine($"Potential observations: {potentialObservations.Count}");
    Console.WriteLine($"HIGH entry-efficiency observations: {weighted.Count}");
    Console.WriteLine("Models: Baseline V5 vs V5.6 stability-weighted candidate");
    Console.WriteLine();
    Console.WriteLine("window\tmodel\tscoreBand\tcount\tavgScore\tavgMFE\tavgMAE\tavgRealized\tpositiveRate\tMFE>=300\tMFE>=500");

    foreach (var window in windows)
    {
        foreach (var model in new[] { "BaselineV5", "V5.6" })
        {
            foreach (var band in bands)
            {
                var members = weighted
                    .Where(x => x.Source.Source.SessionDateCentral >= window.Start &&
                                x.Source.Source.SessionDateCentral < window.EndExclusive)
                    .Where(x => ScoreFor(model, x) >= band.Min &&
                                ScoreFor(model, x) < band.MaxExclusive)
                    .ToList();

                PrintRow(window.Label, model, band.Label, members);
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine("Upper-tier summary (score >= 80, HIGH entry only)");
    Console.WriteLine("window\tmodel\tcount\tavgScore\tavgMFE\tavgMAE\tavgRealized\tpositiveRate\tMFE>=300\tMFE>=500");

    foreach (var window in windows)
    {
        foreach (var model in new[] { "BaselineV5", "V5.6" })
        {
            var members = weighted
                .Where(x => x.Source.Source.SessionDateCentral >= window.Start &&
                            x.Source.Source.SessionDateCentral < window.EndExclusive)
                .Where(x => ScoreFor(model, x) >= 80m)
                .ToList();

            PrintSummary(window.Label, model, members);
        }
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static decimal ScoreFor(string model, MorningStabilityWeightedPotentialObservation x)
    => model == "BaselineV5" ? x.Source.PotentialScore : x.StabilityWeightedScore;

static void PrintRow(
    string window,
    string model,
    string band,
    List<MorningStabilityWeightedPotentialObservation> members)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        window,
        model,
        band,
        members.Count.ToString(CultureInfo.InvariantCulture),
        Average(members, x => ScoreFor(model, x)).ToString("F1", CultureInfo.InvariantCulture),
        Average(members, x => x.Source.Source.MaxFavorableTicks).ToString("F1", CultureInfo.InvariantCulture),
        Average(members, x => x.Source.Source.MaxAdverseTicks).ToString("F1", CultureInfo.InvariantCulture),
        Average(members, x => x.Source.Source.RealizedDollars).ToString("F2", CultureInfo.InvariantCulture),
        PositiveRate(members).ToString("F1", CultureInfo.InvariantCulture) + "%",
        members.Count(x => x.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture)
    }));
}

static void PrintSummary(
    string window,
    string model,
    List<MorningStabilityWeightedPotentialObservation> members)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        window,
        model,
        members.Count.ToString(CultureInfo.InvariantCulture),
        Average(members, x => ScoreFor(model, x)).ToString("F1", CultureInfo.InvariantCulture),
        Average(members, x => x.Source.Source.MaxFavorableTicks).ToString("F1", CultureInfo.InvariantCulture),
        Average(members, x => x.Source.Source.MaxAdverseTicks).ToString("F1", CultureInfo.InvariantCulture),
        Average(members, x => x.Source.Source.RealizedDollars).ToString("F2", CultureInfo.InvariantCulture),
        PositiveRate(members).ToString("F1", CultureInfo.InvariantCulture) + "%",
        members.Count(x => x.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture)
    }));
}

static decimal Average(
    List<MorningStabilityWeightedPotentialObservation> members,
    Func<MorningStabilityWeightedPotentialObservation, decimal> selector)
    => members.Count == 0 ? 0m : members.Average(selector);

static decimal PositiveRate(List<MorningStabilityWeightedPotentialObservation> members)
    => members.Count == 0
        ? 0m
        : 100m * members.Count(x => x.Source.Source.RealizedDollars > 0m) / members.Count;
