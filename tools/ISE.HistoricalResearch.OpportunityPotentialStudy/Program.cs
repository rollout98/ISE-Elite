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
    var observations = potential.Analyze(bars, candidates);
    var quality = new MorningOpportunityQualityDiagnosticsAnalyzer();
    var buckets = quality.BuildBuckets(observations);
    var dimensions = quality.BuildDimensions(observations);

    Console.WriteLine("ISE Elite V5.1 Opportunity Quality Diagnostic Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidate opportunities: {candidates.Count}");
    Console.WriteLine($"Scored observations: {observations.Count}");
    Console.WriteLine();
    Console.WriteLine("bucket\tcount\tavgScore\tavgMFE\tavgMAE\tavgRealized\tpositiveRate\tMFE/MAE\tMFE/Risk\tmoveAge\tconsumedFrac\texhaustRisk\taccel\tresets\tMFE>=300\tMFE>=500\t300+&MAE>=100\t500+&MAE>=100");

    foreach (var bucket in buckets)
    {
        Console.WriteLine(string.Join("\t", new[]
        {
            bucket.Label,
            bucket.Count.ToString(CultureInfo.InvariantCulture),
            bucket.AverageScore.ToString("F1", CultureInfo.InvariantCulture),
            bucket.AverageMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            bucket.AverageMaeTicks.ToString("F1", CultureInfo.InvariantCulture),
            bucket.AverageRealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            (bucket.PositiveRate * 100m).ToString("F1", CultureInfo.InvariantCulture) + "%",
            bucket.AverageMfeMaeRatio.ToString("F2", CultureInfo.InvariantCulture),
            bucket.AverageMfeRiskRatio.ToString("F2", CultureInfo.InvariantCulture),
            bucket.AverageMoveAgeBars.ToString("F1", CultureInfo.InvariantCulture),
            bucket.AverageConsumedFraction.ToString("F2", CultureInfo.InvariantCulture),
            bucket.AverageExhaustionRisk.ToString("F2", CultureInfo.InvariantCulture),
            bucket.AverageAccelerationRatio.ToString("F2", CultureInfo.InvariantCulture),
            bucket.AveragePullbackResetCount.ToString("F2", CultureInfo.InvariantCulture),
            bucket.Hit300.ToString(CultureInfo.InvariantCulture),
            bucket.Hit500.ToString(CultureInfo.InvariantCulture),
            bucket.Hit300WithMaeAtLeast100.ToString(CultureInfo.InvariantCulture),
            bucket.Hit500WithMaeAtLeast100.ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("V5.1 diagnostic dimensions (outcome diagnostics only; not score inputs)");
    Console.WriteLine("dimension\tvalue\tcount\tavgScore\tavgMFE\tavgMAE\tavgRealized");
    foreach (var row in dimensions)
    {
        Console.WriteLine(string.Join("\t", new[]
        {
            row.Dimension,
            row.Value,
            row.Count.ToString(CultureInfo.InvariantCulture),
            row.AverageScore.ToString("F1", CultureInfo.InvariantCulture),
            row.AverageMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            row.AverageMaeTicks.ToString("F1", CultureInfo.InvariantCulture),
            row.AverageRealizedDollars.ToString("F2", CultureInfo.InvariantCulture)
        }));
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
