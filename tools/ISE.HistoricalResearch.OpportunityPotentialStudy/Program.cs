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

    var sandbox = new MorningPotentialCalibrationSandbox();
    var calibrated = sandbox.Analyze(potentialObservations);
    var rows = sandbox.BuildRows(calibrated, highEntryOnly: true, entryObservations);

    Console.WriteLine("ISE Elite V5.4 Potential Calibration Sandbox");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidate opportunities: {candidates.Count}");
    Console.WriteLine($"Potential observations: {potentialObservations.Count}");
    Console.WriteLine($"Entry-efficiency observations: {entryObservations.Count}");
    Console.WriteLine("Scope: HIGH entry-efficiency only; June=calibration, July=validation");
    Console.WriteLine();
    Console.WriteLine("sample\tvariant\tscoreBand\tcount\tavgScore\tavgMFE\tavgMAE\tavgRealized\tpositiveRate\tMFE>=300\tMFE>=500");

    foreach (var row in rows)
    {
        Console.WriteLine(string.Join("\t", new[]
        {
            row.Sample,
            row.Variant.ToString(),
            row.ScoreBand,
            row.Count.ToString(CultureInfo.InvariantCulture),
            row.AverageScore.ToString("F1", CultureInfo.InvariantCulture),
            row.AverageMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            row.AverageMaeTicks.ToString("F1", CultureInfo.InvariantCulture),
            row.AverageRealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            (row.PositiveRate * 100m).ToString("F1", CultureInfo.InvariantCulture) + "%",
            row.Hit300.ToString(CultureInfo.InvariantCulture),
            row.Hit500.ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("Upper-tier summary (80-89 plus 90-100, HIGH entry only)");
    Console.WriteLine("sample\tvariant\tcount\tavgMFE\tavgMAE\tavgRealized\tpositiveRate\tMFE>=300\tMFE>=500");

    foreach (var sample in new[] { "JuneCalibration", "JulyValidation" })
    {
        foreach (MorningPotentialCalibrationVariant variant in Enum.GetValues(typeof(MorningPotentialCalibrationVariant)))
        {
            var members = calibrated
                .Where(x => x.Variant == variant)
                .Where(x => IsSample(x.Source.Source.SessionDateCentral, sample))
                .Where(x => x.CalibratedScore >= 80m)
                .Where(x => entryObservations.Any(e => ReferenceEquals(e.Source, x.Source) && MorningEntryEfficiencyAnalyzer.EntryBand(e.EntryEfficiencyScore) == "High"))
                .ToList();

            Console.WriteLine(string.Join("\t", new[]
            {
                sample,
                variant.ToString(),
                members.Count.ToString(CultureInfo.InvariantCulture),
                (members.Count == 0 ? 0m : members.Average(x => x.Source.Source.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
                (members.Count == 0 ? 0m : members.Average(x => x.Source.Source.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
                (members.Count == 0 ? 0m : members.Average(x => x.Source.Source.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
                (members.Count == 0 ? 0m : 100m * members.Count(x => x.Source.Source.RealizedDollars > 0m) / members.Count).ToString("F1", CultureInfo.InvariantCulture) + "%",
                members.Count(x => x.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
                members.Count(x => x.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture)
            }));
        }
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static bool IsSample(DateTime sessionDateCentral, string sample)
{
    if (sessionDateCentral.Year != 2026) return false;
    return sample == "JuneCalibration" ? sessionDateCentral.Month == 6
        : sample == "JulyValidation" && sessionDateCentral.Month == 7;
}
