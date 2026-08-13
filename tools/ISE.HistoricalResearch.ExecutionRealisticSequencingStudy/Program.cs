using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.ExecutionRealisticSequencingStudy -- <contract-aware-tsv-path>");
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
    var builder = new MorningDailyOpportunitySequencer();
    var candidates = builder.BuildCandidates(entry, weighted);

    var originalStrict = builder.Sequence(
        candidates,
        MorningDailySequencingPolicy.StrictUpper80);

    var realistic = new MorningExecutionRealisticDailyOpportunitySequencer();

    var realisticStrict = realistic.Sequence(
        candidates,
        MorningDailySequencingPolicy.StrictUpper80);

    var realisticBalanced = realistic.Sequence(
        candidates,
        MorningDailySequencingPolicy.BalancedReserve);

    var models = new[]
    {
        new Model("V6StrictOriginal", originalStrict),
        new Model("V6.1StrictRealistic", realisticStrict),
        new Model("V6.1BalancedRealistic", realisticBalanced)
    };

    var dates = candidates
        .Select(x => x.SessionDateCentral)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    Console.WriteLine("ISE Elite V6.1 Execution-Realistic Sequencing Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine("V5.6 Potential remains frozen at 80+ for Strict.");
    Console.WriteLine("V6.1 enforces one open position at a time and preserves the two-attempt maximum.");
    Console.WriteLine("position-open rejections are excluded from executable-miss diagnostics.");
    Console.WriteLine();

    Console.WriteLine("OVERALL");
    Console.WriteLine("model\tsessions\ttradedDays\tselected\tavgDaily\tavgSelected\tpositiveRate\tavgMFE\tavgMAE\tdays>=300\tdays>=500\tselected300\tselected500\toverlapRejected\toverlap300\toverlap500\tavailableMiss300\tavailableMiss500");
    foreach (var model in models)
        Print(model.Name, dates, model.Decisions);

    Console.WriteLine();
    Console.WriteLine("MONTHLY / HALF-MONTH");
    Console.WriteLine("period\tmodel\tsessions\ttradedDays\tselected\tavgDaily\tavgSelected\tpositiveRate\tavgMFE\tavgMAE\tdays>=300\tdays>=500\tselected300\tselected500\toverlapRejected\toverlap300\toverlap500\tavailableMiss300\tavailableMiss500");

    foreach (var period in BuildPeriods(dates))
    {
        var periodDates = dates.Where(period.Contains).ToList();
        foreach (var model in models)
        {
            var members = model.Decisions
                .Where(x => period.Contains(x.Candidate.SessionDateCentral))
                .ToList();

            Print(period.Label + "\t" + model.Name, periodDates, members);
        }
    }

    Console.WriteLine();
    Console.WriteLine("V6.1 gate:");
    Console.WriteLine("- StrictRealistic should remain superior to the original high-entry control profile.");
    Console.WriteLine("- One-position-at-a-time enforcement must not destroy the StrictUpper80 expectancy advantage.");
    Console.WriteLine("- Overlap-unavailable 300+/500+ observations are reported separately from genuinely executable misses.");
    Console.WriteLine("- BalancedReserve remains diagnostic unless it is more stable across periods without damaging June.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void Print(
    string label,
    IReadOnlyList<DateTime> dates,
    IReadOnlyList<MorningDailySequenceDecision> decisions)
{
    var selected = decisions.Where(x => x.Selected).ToList();
    var tradedDays = selected
        .Select(x => x.Candidate.SessionDateCentral)
        .Distinct()
        .Count();

    var daily = dates
        .Select(date => selected
            .Where(x => x.Candidate.SessionDateCentral == date)
            .Sum(x => x.Candidate.Entry.Source.Source.RealizedDollars))
        .ToList();

    var overlaps = decisions
        .Where(MorningExecutionRealisticDailyOpportunitySequencer.IsOverlapUnavailable)
        .ToList();

    var availableMisses = decisions
        .Where(x => MorningExecutionRealisticDailyOpportunitySequencer.IsExecutionAvailableMiss(x))
        .ToList();

    var avgDaily = dates.Count == 0 ? 0m : daily.Average();
    var avgSelected = selected.Count == 0
        ? 0m
        : selected.Average(x => x.Candidate.Entry.Source.Source.RealizedDollars);

    var positive = selected.Count == 0
        ? 0m
        : 100m * selected.Count(x => x.Candidate.Entry.Source.Source.RealizedDollars > 0m) / selected.Count;

    var avgMfe = selected.Count == 0
        ? 0m
        : selected.Average(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks);

    var avgMae = selected.Count == 0
        ? 0m
        : selected.Average(x => x.Candidate.Entry.Source.Source.MaxAdverseTicks);

    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        dates.Count.ToString(CultureInfo.InvariantCulture),
        tradedDays.ToString(CultureInfo.InvariantCulture),
        selected.Count.ToString(CultureInfo.InvariantCulture),
        avgDaily.ToString("F2", CultureInfo.InvariantCulture),
        avgSelected.ToString("F2", CultureInfo.InvariantCulture),
        positive.ToString("F1", CultureInfo.InvariantCulture) + "%",
        avgMfe.ToString("F1", CultureInfo.InvariantCulture),
        avgMae.ToString("F1", CultureInfo.InvariantCulture),
        daily.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        daily.Count(x => x >= 500m).ToString(CultureInfo.InvariantCulture),
        selected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
        selected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture),
        overlaps.Count.ToString(CultureInfo.InvariantCulture),
        overlaps.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
        overlaps.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture),
        availableMisses.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
        availableMisses.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture)
    }));
}

static IReadOnlyList<Period> BuildPeriods(IReadOnlyList<DateTime> dates)
{
    var result = new List<Period>();

    foreach (var month in dates
        .Select(x => new DateTime(x.Year, x.Month, 1))
        .Distinct()
        .OrderBy(x => x))
    {
        var next = month.AddMonths(1);
        result.Add(new Period(month.ToString("yyyy-MM"), month, next));
        result.Add(new Period(
            month.ToString("yyyy-MM") + "-H1",
            month,
            new DateTime(month.Year, month.Month, 16)));
        result.Add(new Period(
            month.ToString("yyyy-MM") + "-H2",
            new DateTime(month.Year, month.Month, 16),
            next));
    }

    return result;
}

sealed class Model
{
    public Model(string name, IReadOnlyList<MorningDailySequenceDecision> decisions)
    {
        Name = name;
        Decisions = decisions;
    }

    public string Name { get; }
    public IReadOnlyList<MorningDailySequenceDecision> Decisions { get; }
}

sealed class Period
{
    public Period(string label, DateTime start, DateTime endExclusive)
    {
        Label = label;
        Start = start;
        EndExclusive = endExclusive;
    }

    public string Label { get; }
    public DateTime Start { get; }
    public DateTime EndExclusive { get; }

    public bool Contains(DateTime date)
        => date >= Start && date < EndExclusive;
}

