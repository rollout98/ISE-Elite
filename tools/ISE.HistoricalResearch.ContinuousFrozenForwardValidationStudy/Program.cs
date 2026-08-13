using System.Globalization;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: <continuous-forward-mnq-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Dataset not found: {path}");
    return 3;
}

var bars = new HistoricalDataFileStore().ReadContractAware(path);

if (bars.Count == 0)
    throw new InvalidOperationException("V7.8.7 dataset contains zero bars.");

if (bars.Any(x =>
    string.IsNullOrWhiteSpace(x.Instrument)
    || !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("V7.8.7 requires MNQ-only data.");

var raw = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
var potential = new MorningOpportunityPotentialAnalyzer().Analyze(bars, raw);
var entry = new MorningEntryEfficiencyAnalyzer().Analyze(bars, potential);
var weighted = new MorningStabilityWeightedPotentialAnalyzer().Analyze(potential);
var all = new MorningDailyOpportunitySequencer().BuildCandidates(entry, weighted);

var baselineCandidates = all
    .Where(x => x.SessionDateCentral.Date > new DateTime(2026,8,10))
    .Where(x => x.EntryEfficiencyScore >= 70m)
    .Where(x => x.PotentialScore >= 80m)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var frozenCandidates = baselineCandidates
    .Where(x => x.Entry.Features.BarsSinceLastReset >= 3)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var sessions = bars
    .Select(x => x.TradingDay.Date)
    .Where(x => x > new DateTime(2026,8,10))
    .Distinct()
    .OrderBy(x => x)
    .ToList();

Console.WriteLine("ISE Elite V7.8.7 Continuous Frozen Forward Validation");
Console.WriteLine("Frozen discriminator: BarsSinceLastReset >= 3.");
Console.WriteLine("Frozen Entry>=70, Potential>=80, V7.3 management, max 2 attempts.");
Console.WriteLine("Evaluation begins after 2026-08-10.");
Console.WriteLine("No tuning and no parameter changes.");
Console.WriteLine($"Bars: {bars.Count}");
Console.WriteLine($"Evaluation sessions: {sessions.Count}");
Console.WriteLine($"Baseline candidates: {baselineCandidates.Count}");
Console.WriteLine($"Frozen candidates: {frozenCandidates.Count}");
Console.WriteLine();

Console.WriteLine("CUMULATIVE RESULTS");
Console.WriteLine("variant\tprofile\tsessions\tselected\tqty2\tqty1\trejectRisk\tavgTrade\tpositive\ttotalPnL\tavgDaily\tworstTrade\tworstDay\tmaxDrawdown");

Print("Baseline", "Fixed2", bars, baselineCandidates, 150m, MorningRiskControlPolicy.FixedTwo, sessions);
Print("FrozenResetAge3Plus", "Fixed2", bars, frozenCandidates, 150m, MorningRiskControlPolicy.FixedTwo, sessions);
Print("FrozenResetAge3Plus", "Funded175", bars, frozenCandidates, 175m, MorningRiskControlPolicy.StrictTwoOneZero, sessions);
Print("FrozenResetAge3Plus", "Combine250", bars, frozenCandidates, 250m, MorningRiskControlPolicy.StrictTwoOneZero, sessions);

Console.WriteLine();
Console.WriteLine("DAILY FROZEN FIXED2");
Console.WriteLine("date\tselected\tpnl");

var frozenFixed = Replay(bars, frozenCandidates, 150m, MorningRiskControlPolicy.FixedTwo);

foreach (var day in sessions)
{
    var trades = frozenFixed.SelectedTrades
        .Where(x => x.Candidate.SessionDateCentral.Date == day)
        .ToList();

    Console.WriteLine($"{day:yyyy-MM-dd}\t{trades.Count}\t{F(trades.Sum(x => x.RealizedDollars))}");
}

Console.WriteLine();
Console.WriteLine("ROLLING BLOCKS");
Console.WriteLine("block\tselected\tpnl\tavgTrade\tpositive");

var selected = frozenFixed.SelectedTrades
    .OrderBy(x => x.Candidate.EntryUtc)
    .ToList();

var blockIndex = 1;
for (var i = 0; i < selected.Count; i += 10)
{
    var block = selected.Skip(i).Take(10).ToList();
    Console.WriteLine(
        $"{blockIndex}\t{block.Count}\t{F(block.Sum(x => x.RealizedDollars))}\t{F(Avg(block.Select(x => x.RealizedDollars)))}\t{Pct(block.Select(x => x.RealizedDollars))}");
    blockIndex++;
}

Console.WriteLine();
Console.WriteLine("CONTINUATION GATE");
Console.WriteLine("- Keep BarsSinceLastReset>=3 frozen.");
Console.WriteLine("- Keep Entry>=70 and Potential>=80 frozen.");
Console.WriteLine("- Keep risk profiles frozen.");
Console.WriteLine("- Continue adding new MNQ sessions without re-optimizing.");
Console.WriteLine("- Do not promote until the forward sample is materially larger.");
Console.WriteLine("- No merge.");

return 0;

static MorningRiskSizedExecutionLifecycleResult Replay(
    IReadOnlyList<HistoricalBar> bars,
    IReadOnlyList<MorningDailySequencingCandidate> candidates,
    decimal budget,
    MorningRiskControlPolicy policy)
{
    return new MorningRiskControlDecompositionAnalyzer(budget, 0.50m)
        .Replay(bars, candidates, policy, 2, 70m, 80m);
}

static void Print(
    string variant,
    string profile,
    IReadOnlyList<HistoricalBar> bars,
    IReadOnlyList<MorningDailySequencingCandidate> candidates,
    decimal budget,
    MorningRiskControlPolicy policy,
    IReadOnlyList<DateTime> sessions)
{
    var r = Replay(bars, candidates, budget, policy);
    var t = r.SelectedTrades.ToList();

    var daily = sessions
        .Select(d =>
            t.Where(x => x.Candidate.SessionDateCentral.Date == d)
             .Sum(x => x.RealizedDollars))
        .ToList();

    Console.WriteLine(string.Join("\t", new[]
    {
        variant,
        profile,
        sessions.Count.ToString(CultureInfo.InvariantCulture),
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

    foreach (var v in values)
    {
        equity += v;
        if (equity > peak) peak = equity;
        var dd = peak - equity;
        if (dd > max) max = dd;
    }

    return max;
}

static string F(decimal value) =>
    value.ToString("F2", CultureInfo.InvariantCulture);

static string Pct(IEnumerable<decimal> values)
{
    var x = values.ToList();
    if (x.Count == 0) return "0.0%";
    return (100m * x.Count(v => v > 0m) / x.Count)
        .ToString("F1", CultureInfo.InvariantCulture) + "%";
}
