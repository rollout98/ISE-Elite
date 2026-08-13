using System.Globalization;
using ISE.HistoricalResearch;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: <precal.tsv> <calibration.tsv> <postcal.tsv>");
    return 2;
}

var specs = new[]
{
    new WindowSpec("PreCalibration", Path.GetFullPath(args[0]), new DateTime(2025,12,1), new DateTime(2026,3,25)),
    new WindowSpec("Calibration", Path.GetFullPath(args[1]), new DateTime(2026,3,25), new DateTime(2026,8,1)),
    new WindowSpec("PostCalibration", Path.GetFullPath(args[2]), new DateTime(2026,8,1), null)
};

var filters = new[]
{
    new FilterSpec(
        "Baseline",
        "Frozen Entry>=70, Potential>=80 only",
        c => true),

    new FilterSpec(
        "RejectResetAge0to2",
        "Reject BarsSinceLastReset <= 2",
        c => c.Entry.Features.BarsSinceLastReset >= 3),

    new FilterSpec(
        "RejectResetCount3to4",
        "Reject ResetCount 3-4",
        c => c.Entry.Features.ResetCount < 3 || c.Entry.Features.ResetCount > 4),

    new FilterSpec(
        "RejectShallowPullback",
        "Reject PullbackDepthFraction < 0.10",
        c => c.Entry.Features.PullbackDepthFraction >= 0.10m),

    new FilterSpec(
        "ConservativeCombined",
        "Require BarsSinceLastReset>=3, reject ResetCount3-4, require PullbackDepth>=0.10",
        c =>
            c.Entry.Features.BarsSinceLastReset >= 3
            && (c.Entry.Features.ResetCount < 3 || c.Entry.Features.ResetCount > 4)
            && c.Entry.Features.PullbackDepthFraction >= 0.10m)
};

Console.WriteLine("ISE Elite V7.8.5 Causal Reset-Maturity Ablation");
Console.WriteLine("Predetermined ablation only. No threshold search.");
Console.WriteLine("Frozen: Entry>=70, Potential>=80, V7.3 management, one position, max 2 attempts.");
Console.WriteLine("Profiles reported: Fixed2, Funded175 strict 2/1/0, Combine250 strict 2/1/0.");
Console.WriteLine("August is NOT treated as untouched OOS for discriminator selection because V7.8.4 already inspected it.");
Console.WriteLine();

var windows = specs.Select(LoadWindow).ToList();

Console.WriteLine("FILTER DEFINITIONS");
foreach (var f in filters)
    Console.WriteLine($"{f.Name}\t{f.Description}");

Console.WriteLine();
Console.WriteLine("WINDOW RESULTS");
Console.WriteLine("window\tfilter\tprofile\teligibleCandidates\tsessions\tselected\tqty2\tqty1\trejectRisk\tavgTrade\tpositive\ttotalPnL\tavgDaily\tworstTrade\tworstDay\tmaxDrawdown");

foreach (var w in windows)
{
    foreach (var f in filters)
    {
        var eligible = w.Candidates.Where(f.Predicate).OrderBy(x => x.EntryUtc).ToList();

        PrintReplay(
            w,
            f,
            "Fixed2",
            eligible,
            150m,
            MorningRiskControlPolicy.FixedTwo);

        PrintReplay(
            w,
            f,
            "Funded175",
            eligible,
            175m,
            MorningRiskControlPolicy.StrictTwoOneZero);

        PrintReplay(
            w,
            f,
            "Combine250",
            eligible,
            250m,
            MorningRiskControlPolicy.StrictTwoOneZero);
    }
}

Console.WriteLine();
Console.WriteLine("FILTER DELTAS VS BASELINE - FIXED2");
Console.WriteLine("window\tfilter\tbaselinePnL\tfilteredPnL\tdeltaPnL\tbaselineTrades\tfilteredTrades\ttradeDelta");

foreach (var w in windows)
{
    var baselineCandidates = w.Candidates.ToList();
    var baseline = Replay(w.Bars, baselineCandidates, 150m, MorningRiskControlPolicy.FixedTwo);
    var baselinePnl = baseline.SelectedTrades.Sum(x => x.RealizedDollars);

    foreach (var f in filters.Where(x => x.Name != "Baseline"))
    {
        var eligible = w.Candidates.Where(f.Predicate).OrderBy(x => x.EntryUtc).ToList();
        var replay = Replay(w.Bars, eligible, 150m, MorningRiskControlPolicy.FixedTwo);
        var pnl = replay.SelectedTrades.Sum(x => x.RealizedDollars);

        Console.WriteLine(string.Join("\t", new[]
        {
            w.Name,
            f.Name,
            F(baselinePnl),
            F(pnl),
            F(pnl - baselinePnl),
            baseline.SelectedTrades.Count.ToString(CultureInfo.InvariantCulture),
            replay.SelectedTrades.Count.ToString(CultureInfo.InvariantCulture),
            (replay.SelectedTrades.Count - baseline.SelectedTrades.Count).ToString(CultureInfo.InvariantCulture)
        }));
    }
}

Console.WriteLine();
Console.WriteLine("HALF-MONTH FIXED2");
Console.WriteLine("window\tperiod\tfilter\tselected\ttotalPnL\tavgTrade\tpositive");

foreach (var w in windows)
{
    var periods = w.Sessions
        .Select(d => $"{d:yyyy-MM}-H{(d.Day <= 15 ? 1 : 2)}")
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    foreach (var f in filters)
    {
        var eligible = w.Candidates.Where(f.Predicate).OrderBy(x => x.EntryUtc).ToList();
        var replay = Replay(w.Bars, eligible, 150m, MorningRiskControlPolicy.FixedTwo);

        foreach (var p in periods)
        {
            var trades = replay.SelectedTrades
                .Where(x =>
                    $"{x.Candidate.SessionDateCentral:yyyy-MM}-H{(x.Candidate.SessionDateCentral.Day <= 15 ? 1 : 2)}" == p)
                .ToList();

            Console.WriteLine(string.Join("\t", new[]
            {
                w.Name,
                p,
                f.Name,
                trades.Count.ToString(CultureInfo.InvariantCulture),
                F(trades.Sum(x => x.RealizedDollars)),
                F(Avg(trades.Select(x => x.RealizedDollars))),
                Pct(trades.Select(x => x.RealizedDollars))
            }));
        }
    }
}

Console.WriteLine();
Console.WriteLine("ABLATION GATE");
Console.WriteLine("- Do not promote a filter solely because it improves calibration or August.");
Console.WriteLine("- A useful discriminator should improve the pre-calibration weakness without destroying calibration expectancy.");
Console.WriteLine("- Prefer the simplest causal rule that is directionally stable across multiple half-month blocks.");
Console.WriteLine("- If ConservativeCombined helps mainly because one component dominates, keep the dominant component and reject unnecessary complexity.");
Console.WriteLine("- Any chosen discriminator must be validated on new MNQ sessions after the current August sample.");
Console.WriteLine("- No production promotion in V7.8.5.");

return 0;

static LoadedWindow LoadWindow(WindowSpec spec)
{
    if (!File.Exists(spec.Path))
        throw new FileNotFoundException(spec.Path);

    var bars = new HistoricalDataFileStore().ReadContractAware(spec.Path);

    if (bars.Count == 0)
        throw new InvalidOperationException($"{spec.Name}: zero bars");

    if (bars.Any(x =>
        string.IsNullOrWhiteSpace(x.Instrument)
        || !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"{spec.Name}: non-MNQ data");

    var raw = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
    var potential = new MorningOpportunityPotentialAnalyzer().Analyze(bars, raw);
    var entry = new MorningEntryEfficiencyAnalyzer().Analyze(bars, potential);
    var weighted = new MorningStabilityWeightedPotentialAnalyzer().Analyze(potential);
    var all = new MorningDailyOpportunitySequencer().BuildCandidates(entry, weighted);

    var candidates = all
        .Where(x => x.SessionDateCentral.Date >= spec.Start.Date)
        .Where(x => !spec.EndExclusive.HasValue || x.SessionDateCentral.Date < spec.EndExclusive.Value.Date)
        .Where(x => x.EntryEfficiencyScore >= 70m && x.PotentialScore >= 80m)
        .OrderBy(x => x.EntryUtc)
        .ToList();

    var sessions = candidates
        .Select(x => x.SessionDateCentral.Date)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    if (sessions.Count == 0)
        throw new InvalidOperationException($"{spec.Name}: zero qualified sessions");

    return new LoadedWindow(
        spec.Name,
        bars,
        candidates,
        sessions);
}

static MorningRiskSizedExecutionLifecycleResult Replay(
    IReadOnlyList<HistoricalBar> bars,
    IReadOnlyList<MorningDailySequencingCandidate> candidates,
    decimal budget,
    MorningRiskControlPolicy policy)
{
    return new MorningRiskControlDecompositionAnalyzer(budget, 0.50m)
        .Replay(
            bars,
            candidates,
            policy,
            2,
            70m,
            80m);
}

static void PrintReplay(
    LoadedWindow w,
    FilterSpec filter,
    string profile,
    IReadOnlyList<MorningDailySequencingCandidate> eligible,
    decimal budget,
    MorningRiskControlPolicy policy)
{
    var r = Replay(w.Bars, eligible, budget, policy);
    var t = r.SelectedTrades.ToList();

    var daily = w.Sessions
        .Select(d =>
            t.Where(x => x.Candidate.SessionDateCentral.Date == d)
             .Sum(x => x.RealizedDollars))
        .ToList();

    Console.WriteLine(string.Join("\t", new[]
    {
        w.Name,
        filter.Name,
        profile,
        eligible.Count.ToString(CultureInfo.InvariantCulture),
        w.Sessions.Count.ToString(CultureInfo.InvariantCulture),
        t.Count.ToString(CultureInfo.InvariantCulture),
        t.Count(x => x.Quantity == 2).ToString(CultureInfo.InvariantCulture),
        t.Count(x => x.Quantity == 1).ToString(CultureInfo.InvariantCulture),
        r.RejectedRisk.ToString(CultureInfo.InvariantCulture),
        F(Avg(t.Select(x => x.RealizedDollars))),
        Pct(t.Select(x => x.RealizedDollars)),
        F(t.Sum(x => x.RealizedDollars)),
        F(Avg(daily)),
        F(Min(t.Select(x => x.RealizedDollars))),
        F(Min(daily)),
        F(MaxDrawdown(daily))
    }));
}

static decimal Avg(IEnumerable<decimal> values)
{
    var x = values.ToList();
    return x.Count == 0 ? 0m : x.Average();
}

static decimal Min(IEnumerable<decimal> values)
{
    var x = values.ToList();
    return x.Count == 0 ? 0m : x.Min();
}

static decimal MaxDrawdown(IReadOnlyList<decimal> values)
{
    decimal equity = 0m;
    decimal peak = 0m;
    decimal max = 0m;

    foreach (var value in values)
    {
        equity += value;
        if (equity > peak) peak = equity;
        var dd = peak - equity;
        if (dd > max) max = dd;
    }

    return max;
}

static string Pct(IEnumerable<decimal> values)
{
    var x = values.ToList();
    if (x.Count == 0) return "0.0%";

    return (100m * x.Count(v => v > 0m) / x.Count)
        .ToString("F1", CultureInfo.InvariantCulture)
        + "%";
}

static string F(decimal value) =>
    value.ToString("F2", CultureInfo.InvariantCulture);

sealed record WindowSpec(
    string Name,
    string Path,
    DateTime Start,
    DateTime? EndExclusive);

sealed record LoadedWindow(
    string Name,
    IReadOnlyList<HistoricalBar> Bars,
    IReadOnlyList<MorningDailySequencingCandidate> Candidates,
    IReadOnlyList<DateTime> Sessions);

sealed record FilterSpec(
    string Name,
    string Description,
    Func<MorningDailySequencingCandidate, bool> Predicate);
