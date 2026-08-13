using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.GovernanceSequencingStudy -- <contract-aware-tsv-path>");
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
    var candidateBuilder = new MorningDailyOpportunitySequencer();
    var candidates = candidateBuilder.BuildCandidates(entry, weighted);

    var v61 = new MorningExecutionRealisticDailyOpportunitySequencer()
        .Sequence(candidates, MorningDailySequencingPolicy.StrictUpper80);

    var v62 = new MorningGovernanceAwareSequencer();
    var objective500 = v62.Sequence(candidates, MorningGovernanceSequencingPolicy.Objective500);
    var protectedGreen = v62.Sequence(candidates, MorningGovernanceSequencingPolicy.ProtectedGreen);

    var dates = candidates.Select(x => x.SessionDateCentral).Distinct().OrderBy(x => x).ToList();

    Console.WriteLine("ISE Elite V6.2 Governance-Aware Sequencing Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine("Frozen selection: Entry Efficiency >=70, V5.6 Potential >=80, one position at a time, max 2 attempts.");
    Console.WriteLine("Governance: lower objective=$500, upper objective=$1000, green threshold=$300, protected floor=$200, base risk=$150.");
    Console.WriteLine("A single loss may still take one later qualified second attempt; max consecutive losses remains 2.");
    Console.WriteLine("No new signal threshold and no post-loss time cooldown are introduced in this study.");
    Console.WriteLine();

    Console.WriteLine("OVERALL");
    Console.WriteLine("model\tsessions\ttradedDays\tselected\tavgDaily\tavgSelected\tpositiveRate\tavgMFE\tavgMAE\tdays>=300\tdays>=500\tdays>=1000\tobjectiveLocks\tgreenRiskBlocks\tpositionOpen\tattemptLocks");
    PrintV61("V6.1StrictRealistic", dates, v61);
    PrintV62("V6.2Objective500", dates, objective500);
    PrintV62("V6.2ProtectedGreen", dates, protectedGreen);

    Console.WriteLine();
    Console.WriteLine("MONTHLY / HALF-MONTH");
    Console.WriteLine("period\tmodel\tsessions\ttradedDays\tselected\tavgDaily\tavgSelected\tpositiveRate\tavgMFE\tavgMAE\tdays>=300\tdays>=500\tdays>=1000\tobjectiveLocks\tgreenRiskBlocks\tpositionOpen\tattemptLocks");

    foreach (var period in BuildPeriods(dates))
    {
        var periodDates = dates.Where(period.Contains).ToList();

        PrintV61(period.Label + "\tV6.1StrictRealistic", periodDates,
            v61.Where(x => period.Contains(x.Candidate.SessionDateCentral)).ToList());

        PrintV62(period.Label + "\tV6.2Objective500", periodDates,
            objective500.Where(x => period.Contains(x.Candidate.SessionDateCentral)).ToList());

        PrintV62(period.Label + "\tV6.2ProtectedGreen", periodDates,
            protectedGreen.Where(x => period.Contains(x.Candidate.SessionDateCentral)).ToList());
    }

    Console.WriteLine();
    Console.WriteLine("V6.2 gate:");
    Console.WriteLine("- Governance should preserve the V6.1 Strict expectancy advantage.");
    Console.WriteLine("- The $500 cutoff should prevent unnecessary second trades after the daily objective is already met.");
    Console.WriteLine("- The $300/$200 green-floor rule should reduce giveback without materially damaging large-opportunity capture.");
    Console.WriteLine("- If green protection has too little sample support, keep it as governance policy rather than optimize it from this dataset.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintV61(
    string label,
    IReadOnlyList<DateTime> dates,
    IReadOnlyList<MorningDailySequenceDecision> decisions)
{
    var selected = decisions.Where(x => x.Selected).ToList();
    PrintCore(
        label,
        dates,
        selected.Select(x => x.Candidate).ToList(),
        0, 0,
        decisions.Count(x => x.Reason == "position-open"),
        decisions.Count(x => x.Reason == "attempt-limit"));
}

static void PrintV62(
    string label,
    IReadOnlyList<DateTime> dates,
    IReadOnlyList<MorningGovernedSequenceDecision> decisions)
{
    var selected = decisions.Where(x => x.Selected).Select(x => x.Candidate).ToList();

    PrintCore(
        label,
        dates,
        selected,
        decisions.Count(x => x.Reason == "lower-objective-lock" || x.Reason == "upper-objective-lock"),
        decisions.Count(x => x.Reason == "green-floor-risk-block"),
        decisions.Count(x => x.Reason == "position-open"),
        decisions.Count(x => x.Reason == "attempt-limit"));
}

static void PrintCore(
    string label,
    IReadOnlyList<DateTime> dates,
    IReadOnlyList<MorningDailySequencingCandidate> selected,
    int objectiveLocks,
    int greenRiskBlocks,
    int positionOpen,
    int attemptLocks)
{
    var tradedDays = selected.Select(x => x.SessionDateCentral).Distinct().Count();
    var daily = dates.Select(date => selected
        .Where(x => x.SessionDateCentral == date)
        .Sum(x => x.Entry.Source.Source.RealizedDollars))
        .ToList();

    var avgDaily = dates.Count == 0 ? 0m : daily.Average();
    var avgSelected = selected.Count == 0 ? 0m : selected.Average(x => x.Entry.Source.Source.RealizedDollars);
    var positive = selected.Count == 0 ? 0m : 100m * selected.Count(x => x.Entry.Source.Source.RealizedDollars > 0m) / selected.Count;
    var avgMfe = selected.Count == 0 ? 0m : selected.Average(x => x.Entry.Source.Source.MaxFavorableTicks);
    var avgMae = selected.Count == 0 ? 0m : selected.Average(x => x.Entry.Source.Source.MaxAdverseTicks);

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
        daily.Count(x => x >= 1000m).ToString(CultureInfo.InvariantCulture),
        objectiveLocks.ToString(CultureInfo.InvariantCulture),
        greenRiskBlocks.ToString(CultureInfo.InvariantCulture),
        positionOpen.ToString(CultureInfo.InvariantCulture),
        attemptLocks.ToString(CultureInfo.InvariantCulture)
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
        result.Add(new Period(month.ToString("yyyy-MM") + "-H1", month, new DateTime(month.Year, month.Month, 16)));
        result.Add(new Period(month.ToString("yyyy-MM") + "-H2", new DateTime(month.Year, month.Month, 16), next));
    }

    return result;
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

    public bool Contains(DateTime date) => date >= Start && date < EndExclusive;
}
