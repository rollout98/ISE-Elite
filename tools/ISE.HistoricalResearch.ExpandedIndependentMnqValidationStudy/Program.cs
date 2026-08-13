using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 2)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ISE.HistoricalResearch.ExpandedIndependentMnqValidationStudy -- <precal-mnq-tsv> <postcal-mnq-tsv>");
    return 2;
}

var prePath = Path.GetFullPath(args[0]);
var postPath = Path.GetFullPath(args[1]);

if (!File.Exists(prePath))
{
    Console.Error.WriteLine(
        $"Pre-calibration dataset not found: {prePath}");
    return 3;
}

if (!File.Exists(postPath))
{
    Console.Error.WriteLine(
        $"Post-calibration dataset not found: {postPath}");
    return 3;
}

try
{
    var validator =
        new MorningExpandedIndependentMnqValidationAnalyzer();

    var pre =
        LoadWindow(
            prePath,
            MorningIndependentMnqWindowKind.PreCalibration,
            validator);

    var post =
        LoadWindow(
            postPath,
            MorningIndependentMnqWindowKind.PostCalibration,
            validator);

    Console.WriteLine(
        "ISE Elite V7.8.2 Expanded Independent MNQ Validation");
    Console.WriteLine(
        "Frozen Funded profile: $175 strict 2/1/0.");
    Console.WriteLine(
        "Frozen Combine profile: $250 strict 2/1/0.");
    Console.WriteLine(
        "Pre-cal evaluation: 2025-12-01 through 2026-03-24.");
    Console.WriteLine(
        "Post-cal evaluation: 2026-08-01 and later.");
    Console.WriteLine(
        "Warmup/context bars outside those evaluation windows are allowed but cannot create evaluated trades.");
    Console.WriteLine(
        "No parameter tuning is performed.");
    Console.WriteLine();

    PrintWindow(pre);
    Console.WriteLine();
    PrintWindow(post);

    Console.WriteLine();
    Console.WriteLine(
        "COMBINED INDEPENDENT WINDOWS");
    Console.WriteLine(
        "profile\twindows\tsessions\tselected\tqty2\tqty1\treject\tavgTrade\tpositive\ttotalPnL\tavgDaily\tworstTrade\tworstDay\tmaxWindowDrawdown\tmaxConsecLossTrades\tmaxConsecLossDays");

    PrintCombined(
        "Funded175",
        new[] { pre.Funded, post.Funded },
        new[] { pre, post });

    PrintCombined(
        "Combine250",
        new[] { pre.Combine, post.Combine },
        new[] { pre, post });

    Console.WriteLine();
    Console.WriteLine(
        "V7.8.2 VALIDATION GATE");
    Console.WriteLine(
        "- Do not retune either budget from this pass.");
    Console.WriteLine(
        "- Funded must demonstrate lower tail risk and stable behavior across independent windows.");
    Console.WriteLine(
        "- Combine must justify higher risk with durable expectancy across both windows.");
    Console.WriteLine(
        "- A profile that depends on only one independent window is not promoted.");
    Console.WriteLine(
        "- Structural stop, Entry>=70, Potential>=80, and V7.3 management remain frozen.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static LoadedWindow LoadWindow(
    string path,
    MorningIndependentMnqWindowKind window,
    MorningExpandedIndependentMnqValidationAnalyzer validator)
{
    var bars =
        new HistoricalDataFileStore()
            .ReadContractAware(path);

    validator.RequireMnq(bars);

    var raw =
        new MorningMarketStateAdaptiveAnalyzer()
            .Analyze(bars);

    var potential =
        new MorningOpportunityPotentialAnalyzer()
            .Analyze(
                bars,
                raw);

    var entry =
        new MorningEntryEfficiencyAnalyzer()
            .Analyze(
                bars,
                potential);

    var weighted =
        new MorningStabilityWeightedPotentialAnalyzer()
            .Analyze(potential);

    var allCandidates =
        new MorningDailyOpportunitySequencer()
            .BuildCandidates(
                entry,
                weighted);

    var evaluationCandidates =
        validator.EvaluationCandidates(
            bars,
            allCandidates,
            window)
        .ToList();

    var sessions =
        evaluationCandidates
            .Select(x => x.SessionDateCentral)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

    if (sessions.Count == 0)
    {
        throw new InvalidOperationException(
            $"No evaluation sessions produced for {window} from {path}.");
    }

    var funded =
        validator.Validate(
            bars,
            allCandidates,
            window,
            MorningFrozenRiskBudgetProfileKind.Funded175);

    var combine =
        validator.Validate(
            bars,
            allCandidates,
            window,
            MorningFrozenRiskBudgetProfileKind.Combine250);

    return new LoadedWindow(
        window,
        path,
        bars.Count,
        sessions,
        funded,
        combine);
}

static void PrintWindow(
    LoadedWindow window)
{
    Console.WriteLine(
        new string('=', 80));
    Console.WriteLine(
        $"WINDOW: {window.Kind}");
    Console.WriteLine(
        $"Dataset: {window.Path}");
    Console.WriteLine(
        $"Bars: {window.BarCount}");
    Console.WriteLine(
        $"Sessions: {window.Sessions.Count}");
    Console.WriteLine(
        $"First session: {window.Sessions[0]:yyyy-MM-dd}");
    Console.WriteLine(
        $"Last session: {window.Sessions[window.Sessions.Count - 1]:yyyy-MM-dd}");

    Console.WriteLine();
    Console.WriteLine(
        "profile\tbudget\tselected\tqty2\tqty1\treject\tavgTrade\tpositive\ttotalPnL\tavgDaily\tdays300\tdays500\tworstTrade\tworstDay\tmaxDrawdown\tmaxConsecLossTrades\tmaxConsecLossDays");

    PrintProfile(
        "Funded175",
        window.Funded,
        window.Sessions);

    PrintProfile(
        "Combine250",
        window.Combine,
        window.Sessions);
}

static void PrintProfile(
    string label,
    MorningExpandedIndependentMnqWindowResult result,
    IReadOnlyList<DateTime> sessions)
{
    var trades =
        result.Lifecycle.SelectedTrades.ToList();

    var metrics =
        Metrics(
            trades,
            sessions);

    Console.WriteLine(
        string.Join(
            "\t",
            new[]
            {
                label,
                result.RiskBudgetDollars.ToString(
                    "F0",
                    CultureInfo.InvariantCulture),
                trades.Count.ToString(
                    CultureInfo.InvariantCulture),
                trades.Count(x => x.Quantity == 2)
                    .ToString(CultureInfo.InvariantCulture),
                trades.Count(x => x.Quantity == 1)
                    .ToString(CultureInfo.InvariantCulture),
                result.Lifecycle.RejectedRisk
                    .ToString(CultureInfo.InvariantCulture),
                metrics.AvgTrade.ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                metrics.Positive,
                metrics.TotalPnl.ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                metrics.AvgDaily.ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                metrics.Days300.ToString(
                    CultureInfo.InvariantCulture),
                metrics.Days500.ToString(
                    CultureInfo.InvariantCulture),
                metrics.WorstTrade.ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                metrics.WorstDay.ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                metrics.MaxDrawdown.ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                metrics.MaxConsecutiveLosingTrades.ToString(
                    CultureInfo.InvariantCulture),
                metrics.MaxConsecutiveLosingDays.ToString(
                    CultureInfo.InvariantCulture)
            }));
}

static void PrintCombined(
    string label,
    IReadOnlyList<MorningExpandedIndependentMnqWindowResult> results,
    IReadOnlyList<LoadedWindow> windows)
{
    var allTrades =
        results
            .SelectMany(x => x.Lifecycle.SelectedTrades)
            .ToList();

    var dailyValues =
        new List<decimal>();

    foreach (var pair in results.Zip(
        windows,
        (result, window) => new { result, window }))
    {
        foreach (var date in pair.window.Sessions)
        {
            dailyValues.Add(
                pair.result.Lifecycle.SelectedTrades
                    .Where(x =>
                        x.Candidate.SessionDateCentral == date)
                    .Sum(x => x.RealizedDollars));
        }
    }

    Console.WriteLine(
        string.Join(
            "\t",
            new[]
            {
                label,
                results.Count.ToString(
                    CultureInfo.InvariantCulture),
                windows.Sum(x => x.Sessions.Count)
                    .ToString(CultureInfo.InvariantCulture),
                allTrades.Count.ToString(
                    CultureInfo.InvariantCulture),
                allTrades.Count(x => x.Quantity == 2)
                    .ToString(CultureInfo.InvariantCulture),
                allTrades.Count(x => x.Quantity == 1)
                    .ToString(CultureInfo.InvariantCulture),
                results.Sum(x => x.Lifecycle.RejectedRisk)
                    .ToString(CultureInfo.InvariantCulture),
                Avg(allTrades.Select(x => x.RealizedDollars))
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                Positive(allTrades.Select(x => x.RealizedDollars)),
                allTrades.Sum(x => x.RealizedDollars)
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                Avg(dailyValues)
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                Min(allTrades.Select(x => x.RealizedDollars))
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                Min(dailyValues)
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                MaxWindowDrawdown(
                    results,
                    windows)
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                MaxConsecutiveLosses(
                    allTrades
                        .OrderBy(x => x.Candidate.EntryUtc)
                        .Select(x => x.RealizedDollars))
                    .ToString(CultureInfo.InvariantCulture),
                MaxConsecutiveLosses(dailyValues)
                    .ToString(CultureInfo.InvariantCulture)
            }));
}

static decimal MaxWindowDrawdown(
    IReadOnlyList<MorningExpandedIndependentMnqWindowResult> results,
    IReadOnlyList<LoadedWindow> windows)
{
    decimal max = 0m;

    for (var i = 0; i < results.Count; i++)
    {
        var values =
            windows[i].Sessions
                .Select(date =>
                    results[i].Lifecycle.SelectedTrades
                        .Where(x =>
                            x.Candidate.SessionDateCentral == date)
                        .Sum(x => x.RealizedDollars))
                .ToList();

        max = Math.Max(
            max,
            MaxDrawdown(values));
    }

    return max;
}

static WindowMetrics Metrics(
    IReadOnlyList<MorningRiskSizedTrade> trades,
    IReadOnlyList<DateTime> sessions)
{
    var values =
        sessions
            .Select(date =>
                trades
                    .Where(x =>
                        x.Candidate.SessionDateCentral == date)
                    .Sum(x => x.RealizedDollars))
            .ToList();

    return new WindowMetrics(
        Avg(trades.Select(x => x.RealizedDollars)),
        Positive(trades.Select(x => x.RealizedDollars)),
        trades.Sum(x => x.RealizedDollars),
        Avg(values),
        values.Count(x => x >= 300m),
        values.Count(x => x >= 500m),
        Min(trades.Select(x => x.RealizedDollars)),
        Min(values),
        MaxDrawdown(values),
        MaxConsecutiveLosses(
            trades
                .OrderBy(x => x.Candidate.EntryUtc)
                .Select(x => x.RealizedDollars)),
        MaxConsecutiveLosses(values));
}

static int MaxConsecutiveLosses(
    IEnumerable<decimal> values)
{
    var max = 0;
    var current = 0;

    foreach (var value in values)
    {
        if (value < 0m)
        {
            current++;

            if (current > max)
                max = current;
        }
        else
        {
            current = 0;
        }
    }

    return max;
}

static decimal MaxDrawdown(
    IReadOnlyList<decimal> values)
{
    decimal equity = 0m;
    decimal peak = 0m;
    decimal max = 0m;

    foreach (var value in values)
    {
        equity += value;

        if (equity > peak)
            peak = equity;

        var drawdown =
            peak - equity;

        if (drawdown > max)
            max = drawdown;
    }

    return max;
}

static decimal Avg(
    IEnumerable<decimal> values)
{
    var list = values.ToList();

    return list.Count == 0
        ? 0m
        : list.Average();
}

static decimal Min(
    IEnumerable<decimal> values)
{
    var list = values.ToList();

    return list.Count == 0
        ? 0m
        : list.Min();
}

static string Positive(
    IEnumerable<decimal> values)
{
    var list = values.ToList();

    if (list.Count == 0)
        return "0.0%";

    return (
        100m
        * list.Count(x => x > 0m)
        / list.Count)
        .ToString(
            "F1",
            CultureInfo.InvariantCulture)
        + "%";
}

sealed class LoadedWindow
{
    public LoadedWindow(
        MorningIndependentMnqWindowKind kind,
        string path,
        int barCount,
        IReadOnlyList<DateTime> sessions,
        MorningExpandedIndependentMnqWindowResult funded,
        MorningExpandedIndependentMnqWindowResult combine)
    {
        Kind = kind;
        Path = path;
        BarCount = barCount;
        Sessions = sessions;
        Funded = funded;
        Combine = combine;
    }

    public MorningIndependentMnqWindowKind Kind { get; }
    public string Path { get; }
    public int BarCount { get; }
    public IReadOnlyList<DateTime> Sessions { get; }
    public MorningExpandedIndependentMnqWindowResult Funded { get; }
    public MorningExpandedIndependentMnqWindowResult Combine { get; }
}

sealed class WindowMetrics
{
    public WindowMetrics(
        decimal avgTrade,
        string positive,
        decimal totalPnl,
        decimal avgDaily,
        int days300,
        int days500,
        decimal worstTrade,
        decimal worstDay,
        decimal maxDrawdown,
        int maxConsecutiveLosingTrades,
        int maxConsecutiveLosingDays)
    {
        AvgTrade = avgTrade;
        Positive = positive;
        TotalPnl = totalPnl;
        AvgDaily = avgDaily;
        Days300 = days300;
        Days500 = days500;
        WorstTrade = worstTrade;
        WorstDay = worstDay;
        MaxDrawdown = maxDrawdown;
        MaxConsecutiveLosingTrades = maxConsecutiveLosingTrades;
        MaxConsecutiveLosingDays = maxConsecutiveLosingDays;
    }

    public decimal AvgTrade { get; }
    public string Positive { get; }
    public decimal TotalPnl { get; }
    public decimal AvgDaily { get; }
    public int Days300 { get; }
    public int Days500 { get; }
    public decimal WorstTrade { get; }
    public decimal WorstDay { get; }
    public decimal MaxDrawdown { get; }
    public int MaxConsecutiveLosingTrades { get; }
    public int MaxConsecutiveLosingDays { get; }
}
