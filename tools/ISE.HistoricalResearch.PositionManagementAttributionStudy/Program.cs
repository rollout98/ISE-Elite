using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.PositionManagementAttributionStudy -- <contract-aware-tsv-path>");
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

    var v61Decisions = new MorningExecutionRealisticDailyOpportunitySequencer()
        .Sequence(candidates, MorningDailySequencingPolicy.StrictUpper80);

    var pairedCandidates = v61Decisions
        .Where(x => x.Selected)
        .Select(x => x.Candidate)
        .ToList();

    var protectedAnalyzer = new MorningProtectedPositionIntelligenceAnalyzer();

    // Exact paired study: management is applied to the same frozen V6.1 entries.
    // Managed exits do NOT alter which entries belong to this comparison.
    var pairedManaged = pairedCandidates
        .Select(x => protectedAnalyzer.Manage(bars, x))
        .Where(x => x != null)
        .Cast<MorningProtectedManagedTrade>()
        .ToList();

    if (pairedManaged.Count != pairedCandidates.Count)
        throw new InvalidOperationException(
            $"Paired attribution invalid: {pairedCandidates.Count} V6.1 entries but {pairedManaged.Count} V7.1 managed outcomes.");

    var attribution = new MorningPositionManagementAttributionAnalyzer()
        .Analyze(bars, pairedManaged)
        .ToList();

    // Separate lifecycle replay: managed exit times are authoritative here.
    var lifecycle = protectedAnalyzer.ReplayFrozenStrict(bars, candidates);
    var lifecycleTrades = lifecycle.SelectedTrades.ToList();

    Console.WriteLine("ISE Elite V7.2 Position Management Attribution Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine($"Exact paired V6.1 entries: {pairedCandidates.Count}");
    Console.WriteLine($"Exact paired V7.1 managed: {pairedManaged.Count}");
    Console.WriteLine($"Lifecycle V7.1 managed: {lifecycleTrades.Count}");
    Console.WriteLine("Future full-path/post-exit excursion is diagnostic only and is never a decision input.");
    Console.WriteLine();

    var baseline = attribution.Select(x => x.Baseline).ToList();

    Console.WriteLine("PAIRED OVERALL");
    Console.WriteLine("model\tn\tavgRealized\tpositiveRate\tavgMFE\tavgMAE");
    Console.WriteLine(string.Join("\t", new[]
    {
        "V6.1HistoricalBaseline",
        baseline.Count.ToString(CultureInfo.InvariantCulture),
        Avg(baseline.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(baseline.Select(x => x.RealizedDollars)),
        Avg(baseline.Select(x => x.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(baseline.Select(x => x.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture)
    }));
    Console.WriteLine(string.Join("\t", new[]
    {
        "V7.1ProtectedPaired",
        attribution.Count.ToString(CultureInfo.InvariantCulture),
        Avg(attribution.Select(x => x.ManagedRealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(attribution.Select(x => x.ManagedRealizedDollars)),
        Avg(attribution.Select(x => x.ManagedTrade.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(attribution.Select(x => x.ManagedTrade.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture)
    }));

    Console.WriteLine();
    Console.WriteLine("PAIRED DELTA");
    Console.WriteLine($"Average managed delta: {Avg(attribution.Select(x => x.ManagedDeltaDollars)):F2}");
    Console.WriteLine($"Improved trades: {attribution.Count(x => x.ManagedDeltaDollars > 0m)}");
    Console.WriteLine($"Worsened trades: {attribution.Count(x => x.ManagedDeltaDollars < 0m)}");
    Console.WriteLine($"Unchanged trades: {attribution.Count(x => x.ManagedDeltaDollars == 0m)}");
    Console.WriteLine($"Average full-path MFE: {Avg(attribution.Select(x => x.FullPathMfeTicks)):F1}");
    Console.WriteLine($"Average full-path MAE: {Avg(attribution.Select(x => x.FullPathMaeTicks)):F1}");
    Console.WriteLine();

    Console.WriteLine("EXIT-REASON ATTRIBUTION");
    PrintGroups(attribution.GroupBy(x => x.ManagedTrade.ExitReason.ToString()));

    Console.WriteLine();
    Console.WriteLine("DIRECTION ATTRIBUTION");
    PrintGroups(attribution.GroupBy(x => x.Baseline.Direction.ToString()));

    Console.WriteLine();
    Console.WriteLine("FULL-PATH MFE-BAND ATTRIBUTION");
    PrintGroups(attribution.GroupBy(x => x.FullPathMfeBand));

    Console.WriteLine();
    Console.WriteLine("FINAL-MODE ATTRIBUTION");
    PrintGroups(attribution.GroupBy(x => x.ManagedTrade.FinalMode.ToString()));

    Console.WriteLine();
    Console.WriteLine("EXTENSION ATTRIBUTION");
    PrintGroups(attribution.GroupBy(x => x.ManagedTrade.ExtensionActivated ? "Extended" : "NotExtended"));

    Console.WriteLine();
    Console.WriteLine("MONTHLY / HALF-MONTH ATTRIBUTION");
    Console.WriteLine("period\tn\tbaselineAvg\tmanagedAvg\tdelta\tbasePositive\tmanagedPositive\tfullMFE\tmanagedCapture\tpost150\tpost300\tpost500");

    var dates = pairedCandidates
        .Select(x => x.SessionDateCentral)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    foreach (var period in BuildPeriods(dates))
    {
        var members = attribution
            .Where(x => period.Contains(x.Candidate.SessionDateCentral))
            .ToList();

        PrintAttributionRow(period.Label, members);
    }

    Console.WriteLine();
    Console.WriteLine("FOCUSED EXIT DIAGNOSTICS");
    Console.WriteLine("exitReason\tn\tbaselineAvg\tmanagedAvg\tdelta\tfullMFE\tpostExitMFE\tcapture\tpost150\tpost300\tpost500");

    foreach (var reason in new[]
    {
        MorningProtectedPositionExitReason.AdaptiveBreakeven,
        MorningProtectedPositionExitReason.ExtensionFloor,
        MorningProtectedPositionExitReason.RunnerTrail,
        MorningProtectedPositionExitReason.ScalpCapture,
        MorningProtectedPositionExitReason.StructuralStop,
        MorningProtectedPositionExitReason.ScalpTimeout,
        MorningProtectedPositionExitReason.VectorFlowBiasLoss
    })
    {
        var members = attribution
            .Where(x => x.ManagedTrade.ExitReason == reason)
            .ToList();

        PrintFocused(reason.ToString(), members);
    }

    Console.WriteLine();
    Console.WriteLine("LIFECYCLE EFFECT");
    var pairedKeys = pairedCandidates.Select(Key).ToHashSet();
    var lifecycleKeys = lifecycleTrades.Select(x => Key(x.Candidate)).ToHashSet();

    var added = lifecycleTrades
        .Where(x => !pairedKeys.Contains(Key(x.Candidate)))
        .ToList();

    var removed = pairedCandidates
        .Where(x => !lifecycleKeys.Contains(Key(x)))
        .ToList();

    Console.WriteLine($"V6.1 paired entries: {pairedCandidates.Count}");
    Console.WriteLine($"V7.1 lifecycle entries: {lifecycleTrades.Count}");
    Console.WriteLine($"Added by managed-exit availability: {added.Count}");
    Console.WriteLine($"Removed/replaced by managed-exit availability: {removed.Count}");
    Console.WriteLine($"Position-open rejects: {lifecycle.RejectedPositionOpen}");
    Console.WriteLine($"Attempt-limit rejects: {lifecycle.RejectedAttemptLimit}");
    Console.WriteLine($"Entry-quality rejects: {lifecycle.RejectedEntryQuality}");
    Console.WriteLine($"Potential-below-80 rejects: {lifecycle.RejectedPotential}");

    foreach (var trade in added.Take(10))
        Console.WriteLine($"ADDED\t{Key(trade.Candidate)}\tmanaged={trade.RealizedDollars:F2}\texit={trade.ExitReason}");

    foreach (var candidate in removed.Take(10))
        Console.WriteLine($"REMOVED\t{Key(candidate)}\tbaseline={candidate.Entry.Source.Source.RealizedDollars:F2}");

    Console.WriteLine();
    Console.WriteLine("V7.2 interpretation gate:");
    Console.WriteLine("- Do not tune thresholds in V7.2.");
    Console.WriteLine("- Use the exact 147-entry paired study to attribute management quality.");
    Console.WriteLine("- Use lifecycle replay only to measure second-opportunity availability caused by managed exit times.");
    Console.WriteLine("- If underperformance clusters in one exit mechanism, change that mechanism rather than global entry or Potential thresholds.");
    Console.WriteLine("- Post-exit MFE is counterfactual diagnostic evidence, never live authority.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintGroups(
    IEnumerable<IGrouping<string, MorningPositionManagementAttributionObservation>> groups)
{
    Console.WriteLine("group\tn\tbaselineAvg\tmanagedAvg\tdelta\tbasePositive\tmanagedPositive\tfullMFE\tmanagedCapture\tpost150\tpost300\tpost500");

    foreach (var group in groups.OrderBy(x => x.Key))
        PrintAttributionRow(group.Key, group.ToList());
}

static void PrintAttributionRow(
    string label,
    IReadOnlyList<MorningPositionManagementAttributionObservation> members)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        members.Count.ToString(CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.BaselineRealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedRealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedDeltaDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(members.Select(x => x.BaselineRealizedDollars)),
        Positive(members.Select(x => x.ManagedRealizedDollars)),
        Avg(members.Select(x => x.FullPathMfeTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedCaptureFraction)).ToString("F3", CultureInfo.InvariantCulture),
        members.Count(x => x.PostExitReached150).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.PostExitReached300).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.PostExitReached500).ToString(CultureInfo.InvariantCulture)
    }));
}

static void PrintFocused(
    string label,
    IReadOnlyList<MorningPositionManagementAttributionObservation> members)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        members.Count.ToString(CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.BaselineRealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedRealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedDeltaDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.FullPathMfeTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.PostExitMfeTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedCaptureFraction)).ToString("F3", CultureInfo.InvariantCulture),
        members.Count(x => x.PostExitReached150).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.PostExitReached300).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.PostExitReached500).ToString(CultureInfo.InvariantCulture)
    }));
}

static string Key(MorningDailySequencingCandidate candidate)
{
    var source = candidate.Entry.Source.Source;
    return $"{candidate.SessionDateCentral:yyyy-MM-dd}|{candidate.EntryUtc:O}|{source.Direction}";
}

static decimal Avg(IEnumerable<decimal> values)
{
    var list = values.ToList();
    return list.Count == 0 ? 0m : list.Average();
}

static string Positive(IEnumerable<decimal> values)
{
    var list = values.ToList();
    if (list.Count == 0) return "0.0%";

    return (100m * list.Count(x => x > 0m) / list.Count)
        .ToString("F1", CultureInfo.InvariantCulture) + "%";
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
