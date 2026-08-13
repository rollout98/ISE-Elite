using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

const string calibrationFileName =
    "morning-MNQ-contract-aware-20251201-20260731-0300-1100-60s-causal-frontcontract.tsv";

if (args.Length < 1)
{
    Console.Error.WriteLine(
        "Usage:");
    Console.Error.WriteLine(
        "  --dataset <validation-tsv>");
    Console.Error.WriteLine(
        "  --discover <research-directory>");
    return 2;
}

var requested = new List<string>();

if (string.Equals(
    args[0],
    "--dataset",
    StringComparison.OrdinalIgnoreCase))
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine(
            "--dataset requires exactly one path.");
        return 2;
    }

    requested.Add(Path.GetFullPath(args[1]));
}
else if (string.Equals(
    args[0],
    "--discover",
    StringComparison.OrdinalIgnoreCase))
{
    if (args.Length != 2)
    {
        Console.Error.WriteLine(
            "--discover requires exactly one directory.");
        return 2;
    }

    var directory = Path.GetFullPath(args[1]);

    if (!Directory.Exists(directory))
    {
        Console.Error.WriteLine(
            $"Research directory not found: {directory}");
        return 3;
    }

    requested.AddRange(
        Directory
            .EnumerateFiles(
                directory,
                "*.tsv",
                SearchOption.AllDirectories)
            .Where(x =>
                !string.Equals(
                    Path.GetFileName(x),
                    calibrationFileName,
                    StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x));
}
else
{
    Console.Error.WriteLine(
        $"Unknown mode: {args[0]}");
    return 2;
}

var validator =
    new MorningFrozenRiskBudgetValidationAnalyzer();

var validDatasets =
    new List<ValidationDataset>();

Console.WriteLine(
    "ISE Elite V7.8 Frozen Risk-Budget Validation");
Console.WriteLine(
    "Frozen Funded candidate: strict 2/1/0 at $175.");
Console.WriteLine(
    "Frozen Combine candidate: strict 2/1/0 at $250.");
Console.WriteLine(
    "Calibration window: 2026-03-25 through 2026-07-31.");
Console.WriteLine(
    "Only datasets entirely before or entirely after that window qualify as independent validation.");
Console.WriteLine(
    "No parameter search or threshold tuning is performed.");
Console.WriteLine();

foreach (var file in requested.Distinct(
    StringComparer.OrdinalIgnoreCase))
{
    if (!File.Exists(file))
    {
        Console.WriteLine(
            $"SKIP missing: {file}");
        continue;
    }

    try
    {
        var bars =
            new HistoricalDataFileStore()
                .ReadContractAware(file);

        if (bars.Count == 0)
        {
            Console.WriteLine(
                $"SKIP empty: {file}");
            continue;
        }

        var classification =
            validator.ClassifyWindow(bars);

        var first =
            bars.Min(x => x.TradingDay.Date);

        var last =
            bars.Max(x => x.TradingDay.Date);

        Console.WriteLine(
            $"DATASET {Path.GetFileName(file)}");
        Console.WriteLine(
            $"  bars={bars.Count} first={first:yyyy-MM-dd} last={last:yyyy-MM-dd} classification={classification}");

        if (!validator.IsIndependent(bars))
        {
            Console.WriteLine(
                "  SKIP: overlaps V7.7 calibration window.");
            continue;
        }

        validDatasets.Add(
            new ValidationDataset(
                file,
                bars,
                first,
                last,
                classification));
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"SKIP unreadable: {file}");
        Console.WriteLine(
            $"  {ex.GetType().Name}: {ex.Message}");
    }
}

Console.WriteLine();

if (validDatasets.Count == 0)
{
    Console.WriteLine(
        "VALIDATION STATUS: NOT RUN");
    Console.WriteLine(
        "No independent contract-aware TSV dataset was found.");
    Console.WriteLine(
        "The V7.8 validation engine is ready, but $175 Funded and $250 Combine remain research candidates only.");
    Console.WriteLine(
        "Create or supply a dataset entirely before 2026-03-25 or after 2026-07-31, then rerun with --dataset.");
    return 0;
}

foreach (var dataset in validDatasets)
{
    Console.WriteLine(
        new string('=', 80));
    Console.WriteLine(
        $"VALIDATION DATASET: {dataset.Path}");
    Console.WriteLine(
        $"Window: {dataset.First:yyyy-MM-dd} through {dataset.Last:yyyy-MM-dd} ({dataset.Classification})");

    var raw =
        new MorningMarketStateAdaptiveAnalyzer()
            .Analyze(dataset.Bars);

    var potential =
        new MorningOpportunityPotentialAnalyzer()
            .Analyze(
                dataset.Bars,
                raw);

    var entry =
        new MorningEntryEfficiencyAnalyzer()
            .Analyze(
                dataset.Bars,
                potential);

    var weighted =
        new MorningStabilityWeightedPotentialAnalyzer()
            .Analyze(potential);

    var candidates =
        new MorningDailyOpportunitySequencer()
            .BuildCandidates(
                entry,
                weighted);

    var sessions =
        candidates
            .Select(x => x.SessionDateCentral)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

    Console.WriteLine(
        $"Candidates: {candidates.Count}");
    Console.WriteLine(
        $"Sessions: {sessions.Count}");

    var funded =
        validator.Validate(
            dataset.Bars,
            candidates,
            MorningFrozenRiskBudgetProfileKind.Funded175);

    var combine =
        validator.Validate(
            dataset.Bars,
            candidates,
            MorningFrozenRiskBudgetProfileKind.Combine250);

    var fixedTwo =
        new MorningRiskControlDecompositionAnalyzer(
            150m,
            0.50m)
            .Replay(
                dataset.Bars,
                candidates,
                MorningRiskControlPolicy.FixedTwo);

    Console.WriteLine();
    Console.WriteLine(
        "OVERALL");
    Console.WriteLine(
        "profile\tbudget\tselected\tqty2\tqty1\treject\tavgTrade\tpositive\ttotalPnL\tavgDaily\tdays300\tdays500\tworstTrade\tworstDay\tmaxDrawdown\tmaxConsecLossTrades\tmaxConsecLossDays");

    PrintFixed(
        "Fixed2Reference",
        fixedTwo,
        sessions);

    PrintProfile(
        "Funded175",
        funded,
        sessions);

    PrintProfile(
        "Combine250",
        combine,
        sessions);

    Console.WriteLine();
    Console.WriteLine(
        "MONTHLY");
    Console.WriteLine(
        "month\tprofile\tsessions\tselected\tqty2\tqty1\treject\tavgTrade\tpositive\tavgDaily\tworstTrade\tworstDay\tmaxDrawdown");

    foreach (var month in sessions
        .Select(x => new DateTime(
            x.Year,
            x.Month,
            1))
        .Distinct()
        .OrderBy(x => x))
    {
        var dates =
            sessions
                .Where(x =>
                    x.Year == month.Year
                    && x.Month == month.Month)
                .ToList();

        PrintMonth(
            month,
            "Funded175",
            funded.Lifecycle,
            dates);

        PrintMonth(
            month,
            "Combine250",
            combine.Lifecycle,
            dates);
    }

    Console.WriteLine();
    Console.WriteLine(
        "HALF-MONTH");
    Console.WriteLine(
        "period\tprofile\tsessions\tselected\tavgTrade\tpositive\tavgDaily\tworstTrade\tworstDay\tmaxDrawdown");

    foreach (var period in BuildHalfMonths(sessions))
    {
        var dates =
            sessions
                .Where(period.Contains)
                .ToList();

        PrintHalfMonth(
            period.Label,
            "Funded175",
            funded.Lifecycle,
            dates);

        PrintHalfMonth(
            period.Label,
            "Combine250",
            combine.Lifecycle,
            dates);
    }

    Console.WriteLine();
    Console.WriteLine(
        "VALIDATION INTERPRETATION GATE");
    Console.WriteLine(
        "- No budget is promoted merely for having the higher validation PnL.");
    Console.WriteLine(
        "- Funded must preserve lower tail risk and acceptable stability.");
    Console.WriteLine(
        "- Combine may accept more tail risk only if expectancy and period stability justify it.");
    Console.WriteLine(
        "- Any material breakdown sends the profile back to research; V7.8 itself does not retune.");
    Console.WriteLine(
        "- Structural stop, V6.1 selection, V5.6 Potential, and V7.3 management remain frozen.");
}

return 0;

static void PrintFixed(
    string label,
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> sessions)
{
    var metrics =
        Metrics(
            replay,
            sessions);

    Console.WriteLine(
        string.Join(
            "\t",
            new[]
            {
                label,
                "fixed2",
                metrics.Selected.ToString(
                    CultureInfo.InvariantCulture),
                metrics.Selected.ToString(
                    CultureInfo.InvariantCulture),
                "0",
                replay.RejectedRisk.ToString(
                    CultureInfo.InvariantCulture),
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

static void PrintProfile(
    string label,
    MorningFrozenRiskBudgetValidationResult result,
    IReadOnlyList<DateTime> sessions)
{
    var replay =
        result.Lifecycle;

    var trades =
        replay.SelectedTrades.ToList();

    var metrics =
        Metrics(
            replay,
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
                metrics.Selected.ToString(
                    CultureInfo.InvariantCulture),
                trades.Count(x => x.Quantity == 2)
                    .ToString(
                        CultureInfo.InvariantCulture),
                trades.Count(x => x.Quantity == 1)
                    .ToString(
                        CultureInfo.InvariantCulture),
                replay.RejectedRisk.ToString(
                    CultureInfo.InvariantCulture),
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

static void PrintMonth(
    DateTime month,
    string label,
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> dates)
{
    var trades =
        replay.SelectedTrades
            .Where(x =>
                x.Candidate.SessionDateCentral.Year
                    == month.Year
                && x.Candidate.SessionDateCentral.Month
                    == month.Month)
            .ToList();

    var rejects =
        replay.RiskRejectedCandidates
            .Count(x =>
                x.SessionDateCentral.Year
                    == month.Year
                && x.SessionDateCentral.Month
                    == month.Month);

    var daily =
        Daily(
            trades,
            dates);

    var values =
        dates
            .Select(x => daily[x])
            .ToList();

    Console.WriteLine(
        string.Join(
            "\t",
            new[]
            {
                month.ToString("yyyy-MM"),
                label,
                dates.Count.ToString(
                    CultureInfo.InvariantCulture),
                trades.Count.ToString(
                    CultureInfo.InvariantCulture),
                trades.Count(x => x.Quantity == 2)
                    .ToString(
                        CultureInfo.InvariantCulture),
                trades.Count(x => x.Quantity == 1)
                    .ToString(
                        CultureInfo.InvariantCulture),
                rejects.ToString(
                    CultureInfo.InvariantCulture),
                Avg(trades.Select(x => x.RealizedDollars))
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                Positive(
                    trades.Select(x => x.RealizedDollars)),
                Avg(values).ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                Min(trades.Select(x => x.RealizedDollars))
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                Min(values).ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                MaxDrawdown(values).ToString(
                    "F2",
                    CultureInfo.InvariantCulture)
            }));
}

static void PrintHalfMonth(
    string period,
    string label,
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> dates)
{
    var dateSet =
        dates.ToHashSet();

    var trades =
        replay.SelectedTrades
            .Where(x =>
                dateSet.Contains(
                    x.Candidate.SessionDateCentral))
            .ToList();

    var daily =
        Daily(
            trades,
            dates);

    var values =
        dates
            .Select(x => daily[x])
            .ToList();

    Console.WriteLine(
        string.Join(
            "\t",
            new[]
            {
                period,
                label,
                dates.Count.ToString(
                    CultureInfo.InvariantCulture),
                trades.Count.ToString(
                    CultureInfo.InvariantCulture),
                Avg(trades.Select(x => x.RealizedDollars))
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                Positive(
                    trades.Select(x => x.RealizedDollars)),
                Avg(values).ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                Min(trades.Select(x => x.RealizedDollars))
                    .ToString(
                        "F2",
                        CultureInfo.InvariantCulture),
                Min(values).ToString(
                    "F2",
                    CultureInfo.InvariantCulture),
                MaxDrawdown(values).ToString(
                    "F2",
                    CultureInfo.InvariantCulture)
            }));
}

static ValidationMetrics Metrics(
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> sessions)
{
    var trades =
        replay.SelectedTrades.ToList();

    var daily =
        Daily(
            trades,
            sessions);

    var dailyValues =
        sessions
            .Select(x => daily[x])
            .ToList();

    return new ValidationMetrics(
        trades.Count,
        Avg(trades.Select(x => x.RealizedDollars)),
        Positive(trades.Select(x => x.RealizedDollars)),
        trades.Sum(x => x.RealizedDollars),
        Avg(dailyValues),
        dailyValues.Count(x => x >= 300m),
        dailyValues.Count(x => x >= 500m),
        Min(trades.Select(x => x.RealizedDollars)),
        Min(dailyValues),
        MaxDrawdown(dailyValues),
        MaxConsecutiveLosses(
            trades.Select(x => x.RealizedDollars)),
        MaxConsecutiveLosses(dailyValues));
}

static Dictionary<DateTime, decimal> Daily(
    IReadOnlyList<MorningRiskSizedTrade> trades,
    IReadOnlyList<DateTime> dates)
{
    return dates.ToDictionary(
        date => date,
        date => trades
            .Where(x =>
                x.Candidate.SessionDateCentral == date)
            .Sum(x => x.RealizedDollars));
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
    IReadOnlyList<decimal> dailyValues)
{
    decimal equity = 0m;
    decimal peak = 0m;
    decimal drawdown = 0m;

    foreach (var value in dailyValues)
    {
        equity += value;

        if (equity > peak)
            peak = equity;

        var current =
            peak - equity;

        if (current > drawdown)
            drawdown = current;
    }

    return drawdown;
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

static IReadOnlyList<HalfMonth> BuildHalfMonths(
    IReadOnlyList<DateTime> dates)
{
    var result =
        new List<HalfMonth>();

    foreach (var month in dates
        .Select(x =>
            new DateTime(
                x.Year,
                x.Month,
                1))
        .Distinct()
        .OrderBy(x => x))
    {
        var next =
            month.AddMonths(1);

        result.Add(
            new HalfMonth(
                month.ToString("yyyy-MM")
                    + "-H1",
                month,
                new DateTime(
                    month.Year,
                    month.Month,
                    16)));

        result.Add(
            new HalfMonth(
                month.ToString("yyyy-MM")
                    + "-H2",
                new DateTime(
                    month.Year,
                    month.Month,
                    16),
                next));
    }

    return result;
}

sealed class ValidationDataset
{
    public ValidationDataset(
        string path,
        IReadOnlyList<HistoricalBar> bars,
        DateTime first,
        DateTime last,
        MorningValidationWindowClassification classification)
    {
        Path = path;
        Bars = bars;
        First = first;
        Last = last;
        Classification = classification;
    }

    public string Path { get; }
    public IReadOnlyList<HistoricalBar> Bars { get; }
    public DateTime First { get; }
    public DateTime Last { get; }
    public MorningValidationWindowClassification Classification { get; }
}

sealed class ValidationMetrics
{
    public ValidationMetrics(
        int selected,
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
        Selected = selected;
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

    public int Selected { get; }
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

sealed class HalfMonth
{
    public HalfMonth(
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

    public bool Contains(
        DateTime date)
        => date >= Start
            && date < EndExclusive;
}
