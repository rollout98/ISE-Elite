using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ISE.HistoricalResearch.PreExtensionBreakevenAblationStudy -- <contract-aware-tsv-path>");
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
    var candidates = new MorningDailyOpportunitySequencer()
        .BuildCandidates(entry, weighted);

    var v61Decisions = new MorningExecutionRealisticDailyOpportunitySequencer()
        .Sequence(candidates, MorningDailySequencingPolicy.StrictUpper80);

    var pairedCandidates = v61Decisions
        .Where(x => x.Selected)
        .Select(x => x.Candidate)
        .ToList();

    var v71Analyzer = new MorningProtectedPositionIntelligenceAnalyzer(
        new MorningProtectedPositionConfig());

    var v73Analyzer = new MorningProtectedPositionIntelligenceAnalyzer(
        new MorningProtectedPositionConfig(
            enablePreExtensionAdaptiveBreakeven: false));

    var v71Paired = pairedCandidates
        .Select(x => v71Analyzer.Manage(bars, x))
        .Where(x => x != null)
        .Cast<MorningProtectedManagedTrade>()
        .ToList();

    var v73Paired = pairedCandidates
        .Select(x => v73Analyzer.Manage(bars, x))
        .Where(x => x != null)
        .Cast<MorningProtectedManagedTrade>()
        .ToList();

    if (v71Paired.Count != pairedCandidates.Count
        || v73Paired.Count != pairedCandidates.Count)
    {
        throw new InvalidOperationException(
            $"Paired study invalid: V6.1={pairedCandidates.Count}, V7.1={v71Paired.Count}, V7.3={v73Paired.Count}.");
    }

    var v71Attr = new MorningPositionManagementAttributionAnalyzer()
        .Analyze(bars, v71Paired)
        .ToList();

    var v73Attr = new MorningPositionManagementAttributionAnalyzer()
        .Analyze(bars, v73Paired)
        .ToList();

    var v71Lifecycle = v71Analyzer.ReplayFrozenStrict(bars, candidates);
    var v73Lifecycle = v73Analyzer.ReplayFrozenStrict(bars, candidates);

    var baseline = pairedCandidates
        .Select(x => x.Entry.Source.Source)
        .ToList();

    Console.WriteLine("ISE Elite V7.3 Pre-Extension Breakeven Ablation Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine($"Exact paired V6.1 entries: {pairedCandidates.Count}");
    Console.WriteLine("ONE VARIABLE ONLY: V7.3 disables pre-extension adaptive breakeven.");
    Console.WriteLine("Frozen: Entry>=70, Potential>=80, +150 Core qualification, +100 Core floor, 40% retention,");
    Console.WriteLine("        Runner>=300 + 2 aligned 5m states, 250-tick runner trail, same scalp timeout and structural stop.");
    Console.WriteLine();

    Console.WriteLine("PAIRED OVERALL");
    Console.WriteLine("model\tn\tavgRealized\tpositiveRate\tavgMFE\tavgMAE\tstructStop\tscalpCapture\ttimeout\tbreakeven\textensionFloor\trunnerTrail\tbiasLoss");

    PrintBaseline("V6.1HistoricalBaseline", baseline);
    PrintManaged("V7.1Protected", v71Paired);
    PrintManaged("V7.3NoPreExtensionBE", v73Paired);

    Console.WriteLine();
    Console.WriteLine("PAIRED DELTA");
    Console.WriteLine($"V7.1 vs baseline avg delta: {(Avg(v71Paired.Select(x => x.RealizedDollars)) - Avg(baseline.Select(x => x.RealizedDollars))):F2}");
    Console.WriteLine($"V7.3 vs baseline avg delta: {(Avg(v73Paired.Select(x => x.RealizedDollars)) - Avg(baseline.Select(x => x.RealizedDollars))):F2}");
    Console.WriteLine($"V7.3 vs V7.1 avg delta: {(Avg(v73Paired.Select(x => x.RealizedDollars)) - Avg(v71Paired.Select(x => x.RealizedDollars))):F2}");
    Console.WriteLine();

    var v71ByKey = v71Paired.ToDictionary(x => Key(x.Candidate));
    var v73ByKey = v73Paired.ToDictionary(x => Key(x.Candidate));

    var formerBeKeys = v71Paired
        .Where(x => x.ExitReason == MorningProtectedPositionExitReason.AdaptiveBreakeven)
        .Select(x => Key(x.Candidate))
        .ToHashSet();

    var beV71 = formerBeKeys
        .Select(x => v71ByKey[x])
        .ToList();

    var beV73 = formerBeKeys
        .Select(x => v73ByKey[x])
        .ToList();

    var beBaseline = pairedCandidates
        .Where(x => formerBeKeys.Contains(Key(x)))
        .Select(x => x.Entry.Source.Source)
        .ToList();

    Console.WriteLine("FORMER V7.1 BREAKEVEN COHORT");
    Console.WriteLine($"n: {formerBeKeys.Count}");
    Console.WriteLine($"Baseline avg: {Avg(beBaseline.Select(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"V7.1 avg: {Avg(beV71.Select(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"V7.3 avg: {Avg(beV73.Select(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"V7.3 positive rate: {Positive(beV73.Select(x => x.RealizedDollars))}");
    Console.WriteLine($"V7.3 structural stops: {beV73.Count(x => x.ExitReason == MorningProtectedPositionExitReason.StructuralStop)}");
    Console.WriteLine($"V7.3 scalp captures: {beV73.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpCapture)}");
    Console.WriteLine($"V7.3 timeouts: {beV73.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpTimeout)}");
    Console.WriteLine($"V7.3 extension floors: {beV73.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ExtensionFloor)}");
    Console.WriteLine($"V7.3 runner trails: {beV73.Count(x => x.ExitReason == MorningProtectedPositionExitReason.RunnerTrail)}");
    Console.WriteLine($"V7.3 bias-loss exits: {beV73.Count(x => x.ExitReason == MorningProtectedPositionExitReason.VectorFlowBiasLoss)}");
    Console.WriteLine();

    Console.WriteLine("FORMER-BE TRADE DETAIL");
    Console.WriteLine("key\tbaseline\tv71\tv73\tv73Exit\tv73Mode\tfullMFE\tpostExitMFE");

    var v73AttrByKey = v73Attr.ToDictionary(x => Key(x.Candidate));

    foreach (var key in formerBeKeys.OrderBy(x => x))
    {
        var candidate = pairedCandidates.Single(x => Key(x) == key);
        var b = candidate.Entry.Source.Source;
        var oldTrade = v71ByKey[key];
        var newTrade = v73ByKey[key];
        var attr = v73AttrByKey[key];

        Console.WriteLine(string.Join("\t", new[]
        {
            key,
            b.RealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            oldTrade.RealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            newTrade.RealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            newTrade.ExitReason.ToString(),
            newTrade.FinalMode.ToString(),
            attr.FullPathMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            attr.PostExitMfeTicks.ToString("F1", CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("DIRECTION PAIRED");
    Console.WriteLine("direction\tn\tbaselineAvg\tv71Avg\tv73Avg\tv73-v71\tbasePositive\tv71Positive\tv73Positive");

    foreach (var direction in new[]
    {
        NewYorkResearchDirection.Long,
        NewYorkResearchDirection.Short
    })
    {
        var keys = pairedCandidates
            .Where(x => x.Entry.Source.Source.Direction == direction)
            .Select(Key)
            .ToHashSet();

        var b = pairedCandidates
            .Where(x => keys.Contains(Key(x)))
            .Select(x => x.Entry.Source.Source)
            .ToList();

        var a71 = v71Paired
            .Where(x => keys.Contains(Key(x.Candidate)))
            .ToList();

        var a73 = v73Paired
            .Where(x => keys.Contains(Key(x.Candidate)))
            .ToList();

        Console.WriteLine(string.Join("\t", new[]
        {
            direction.ToString(),
            keys.Count.ToString(CultureInfo.InvariantCulture),
            Avg(b.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(a71.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(a73.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            (Avg(a73.Select(x => x.RealizedDollars)) - Avg(a71.Select(x => x.RealizedDollars))).ToString("F2", CultureInfo.InvariantCulture),
            Positive(b.Select(x => x.RealizedDollars)),
            Positive(a71.Select(x => x.RealizedDollars)),
            Positive(a73.Select(x => x.RealizedDollars))
        }));
    }

    Console.WriteLine();
    Console.WriteLine("MONTHLY / HALF-MONTH PAIRED");
    Console.WriteLine("period\tn\tbaselineAvg\tv71Avg\tv73Avg\tv73-v71\tbasePositive\tv71Positive\tv73Positive\tv71BE\tv73BE\tv73StructStop\tv73Core\tv73Runner");

    var dates = pairedCandidates
        .Select(x => x.SessionDateCentral)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    foreach (var period in BuildPeriods(dates))
    {
        var keys = pairedCandidates
            .Where(x => period.Contains(x.SessionDateCentral))
            .Select(Key)
            .ToHashSet();

        var b = pairedCandidates
            .Where(x => keys.Contains(Key(x)))
            .Select(x => x.Entry.Source.Source)
            .ToList();

        var a71 = v71Paired
            .Where(x => keys.Contains(Key(x.Candidate)))
            .ToList();

        var a73 = v73Paired
            .Where(x => keys.Contains(Key(x.Candidate)))
            .ToList();

        Console.WriteLine(string.Join("\t", new[]
        {
            period.Label,
            keys.Count.ToString(CultureInfo.InvariantCulture),
            Avg(b.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(a71.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(a73.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            (Avg(a73.Select(x => x.RealizedDollars)) - Avg(a71.Select(x => x.RealizedDollars))).ToString("F2", CultureInfo.InvariantCulture),
            Positive(b.Select(x => x.RealizedDollars)),
            Positive(a71.Select(x => x.RealizedDollars)),
            Positive(a73.Select(x => x.RealizedDollars)),
            a71.Count(x => x.ExitReason == MorningProtectedPositionExitReason.AdaptiveBreakeven).ToString(CultureInfo.InvariantCulture),
            a73.Count(x => x.ExitReason == MorningProtectedPositionExitReason.AdaptiveBreakeven).ToString(CultureInfo.InvariantCulture),
            a73.Count(x => x.ExitReason == MorningProtectedPositionExitReason.StructuralStop).ToString(CultureInfo.InvariantCulture),
            a73.Count(x => x.FinalMode == MorningProtectedPositionMode.Core).ToString(CultureInfo.InvariantCulture),
            a73.Count(x => x.FinalMode == MorningProtectedPositionMode.Runner).ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("LIFECYCLE REPLAY");
    PrintLifecycle("V7.1Protected", v71Lifecycle);
    PrintLifecycle("V7.3NoPreExtensionBE", v73Lifecycle);

    var v71LifeKeys = v71Lifecycle.SelectedTrades
        .Select(x => Key(x.Candidate))
        .ToHashSet();

    var v73LifeKeys = v73Lifecycle.SelectedTrades
        .Select(x => Key(x.Candidate))
        .ToHashSet();

    Console.WriteLine($"Lifecycle entries added by V7.3 vs V7.1: {v73LifeKeys.Except(v71LifeKeys).Count()}");
    Console.WriteLine($"Lifecycle entries removed by V7.3 vs V7.1: {v71LifeKeys.Except(v73LifeKeys).Count()}");
    Console.WriteLine();

    Console.WriteLine("V7.3 gate:");
    Console.WriteLine("- Promote the BE removal only if the exact 147-entry paired result materially improves V7.1 without creating disproportionate structural losses.");
    Console.WriteLine("- Confirm the former-BE cohort directly; do not infer from aggregate MFE alone.");
    Console.WriteLine("- Keep Core/Runner thresholds frozen in this experiment.");
    Console.WriteLine("- Keep Entry Efficiency and V5.6 Potential frozen.");
    Console.WriteLine("- Lifecycle replay is secondary; paired attribution is the primary causal comparison.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintBaseline(
    string label,
    IReadOnlyList<MorningAdaptiveTradeOutcome> trades)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(trades.Select(x => x.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
        "-", "-", "-", "-", "-", "-", "-"
    }));
}

static void PrintManaged(
    string label,
    IReadOnlyList<MorningProtectedManagedTrade> trades)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(trades.Select(x => x.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.StructuralStop).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpCapture).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpTimeout).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.AdaptiveBreakeven).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ExtensionFloor).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.RunnerTrail).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.VectorFlowBiasLoss).ToString(CultureInfo.InvariantCulture)
    }));
}

static void PrintLifecycle(
    string label,
    MorningProtectedReplayResult replay)
{
    var trades = replay.SelectedTrades.ToList();

    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        $"selected={trades.Count}",
        $"avg={Avg(trades.Select(x => x.RealizedDollars)):F2}",
        $"positive={Positive(trades.Select(x => x.RealizedDollars))}",
        $"positionOpen={replay.RejectedPositionOpen}",
        $"attemptLimit={replay.RejectedAttemptLimit}",
        $"entryReject={replay.RejectedEntryQuality}",
        $"potentialReject={replay.RejectedPotential}",
        $"structStop={trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.StructuralStop)}",
        $"BE={trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.AdaptiveBreakeven)}",
        $"core={trades.Count(x => x.FinalMode == MorningProtectedPositionMode.Core)}",
        $"runner={trades.Count(x => x.FinalMode == MorningProtectedPositionMode.Runner)}"
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

    if (list.Count == 0)
        return "0.0%";

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

        result.Add(new Period(
            month.ToString("yyyy-MM"),
            month,
            next));

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
    public Period(
        string label,
        DateTime start,
        DateTime endExclusive)
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
