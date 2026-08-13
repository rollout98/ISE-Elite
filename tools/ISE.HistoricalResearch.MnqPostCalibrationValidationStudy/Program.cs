using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ISE.HistoricalResearch.MnqPostCalibrationValidationStudy -- <full-mnq-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);

if (!File.Exists(path))
{
    Console.Error.WriteLine(
        $"Dataset not found: {path}");
    return 3;
}

try
{
    var bars =
        new HistoricalDataFileStore()
            .ReadContractAware(path);

    var validator =
        new MorningMnqPostCalibrationValidationAnalyzer();

    validator.RequireMnq(bars);

    var firstBarDay =
        bars.Min(x => x.TradingDay.Date);

    var lastBarDay =
        bars.Max(x => x.TradingDay.Date);

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
            allCandidates)
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
            "No MNQ post-calibration sessions were produced on or after 2026-08-01.");
    }

    var funded =
        validator.Validate(
            bars,
            allCandidates,
            MorningFrozenRiskBudgetProfileKind.Funded175);

    var combine =
        validator.Validate(
            bars,
            allCandidates,
            MorningFrozenRiskBudgetProfileKind.Combine250);

    var fixedTwo =
        new MorningRiskControlDecompositionAnalyzer(
            150m,
            0.50m)
            .Replay(
                bars,
                evaluationCandidates,
                MorningRiskControlPolicy.FixedTwo);

    Console.WriteLine(
        "ISE Elite V7.8.1 MNQ Post-Calibration Validation");
    Console.WriteLine(
        $"Dataset: {path}");
    Console.WriteLine(
        $"Full bars: {bars.Count}");
    Console.WriteLine(
        $"Full data window: {firstBarDay:yyyy-MM-dd} through {lastBarDay:yyyy-MM-dd}");
    Console.WriteLine(
        "Earlier MNQ bars are warmup/context only.");
    Console.WriteLine(
        "Evaluation candidates are restricted to Central session dates >= 2026-08-01.");
    Console.WriteLine(
        $"Evaluation sessions: {sessions.Count}");
    Console.WriteLine(
        $"First evaluation session: {sessions[0]:yyyy-MM-dd}");
    Console.WriteLine(
        $"Last evaluation session: {sessions[sessions.Count - 1]:yyyy-MM-dd}");
    Console.WriteLine(
        $"Evaluation candidates: {evaluationCandidates.Count}");
    Console.WriteLine(
        "Frozen Funded=$175; Combine=$250; strict 2/1/0.");
    Console.WriteLine(
        "No tuning. Structural stop, Entry>=70, Potential>=80, and V7.3 management remain frozen.");
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
        "DAILY");
    Console.WriteLine(
        "date\tfixedPnL\tfundedPnL\tcombinePnL\tfundedSelected\tcombineSelected\tfundedReject\tcombineReject");

    foreach (var date in sessions)
    {
        var fixedDay =
            fixedTwo.SelectedTrades
                .Where(x =>
                    x.Candidate.SessionDateCentral == date)
                .ToList();

        var fundedDay =
            funded.Lifecycle.SelectedTrades
                .Where(x =>
                    x.Candidate.SessionDateCentral == date)
                .ToList();

        var combineDay =
            combine.Lifecycle.SelectedTrades
                .Where(x =>
                    x.Candidate.SessionDateCentral == date)
                .ToList();

        Console.WriteLine(
            string.Join(
                "\t",
                new[]
                {
                    date.ToString("yyyy-MM-dd"),
                    fixedDay.Sum(x => x.RealizedDollars)
                        .ToString(
                            "F2",
                            CultureInfo.InvariantCulture),
                    fundedDay.Sum(x => x.RealizedDollars)
                        .ToString(
                            "F2",
                            CultureInfo.InvariantCulture),
                    combineDay.Sum(x => x.RealizedDollars)
                        .ToString(
                            "F2",
                            CultureInfo.InvariantCulture),
                    fundedDay.Count.ToString(
                        CultureInfo.InvariantCulture),
                    combineDay.Count.ToString(
                        CultureInfo.InvariantCulture),
                    funded.Lifecycle.RiskRejectedCandidates
                        .Count(x => x.SessionDateCentral == date)
                        .ToString(
                            CultureInfo.InvariantCulture),
                    combine.Lifecycle.RiskRejectedCandidates
                        .Count(x => x.SessionDateCentral == date)
                        .ToString(
                            CultureInfo.InvariantCulture)
                }));
    }

    Console.WriteLine();
    Console.WriteLine(
        "POST-CALIBRATION VALIDATION GATE");
    Console.WriteLine(
        "- This is the correct instrument: MNQ only.");
    Console.WriteLine(
        "- Earlier bars are used only as causal warmup/context; no pre-August candidate is evaluated.");
    Console.WriteLine(
        "- Do not promote either profile from a tiny sample solely because PnL is positive.");
    Console.WriteLine(
        "- A negative result is evidence against the frozen profile but does not authorize retuning in V7.8.1.");
    Console.WriteLine(
        "- Continue accumulating independent MNQ sessions before final promotion.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintFixed(
    string label,
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> sessions)
{
    var trades =
        replay.SelectedTrades.ToList();

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
                "fixed2",
                trades.Count.ToString(
                    CultureInfo.InvariantCulture),
                trades.Count.ToString(
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
    MorningMnqPostCalibrationValidationResult result,
    IReadOnlyList<DateTime> sessions)
{
    var replay =
        result.Lifecycle;

    var trades =
        replay.SelectedTrades.ToList();

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

static ValidationMetrics Metrics(
    IReadOnlyList<MorningRiskSizedTrade> trades,
    IReadOnlyList<DateTime> sessions)
{
    var daily =
        sessions.ToDictionary(
            date => date,
            date => trades
                .Where(x =>
                    x.Candidate.SessionDateCentral == date)
                .Sum(x => x.RealizedDollars));

    var dailyValues =
        sessions
            .Select(x => daily[x])
            .ToList();

    return new ValidationMetrics(
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
    decimal maxDrawdown = 0m;

    foreach (var pnl in dailyValues)
    {
        equity += pnl;

        if (equity > peak)
            peak = equity;

        var drawdown =
            peak - equity;

        if (drawdown > maxDrawdown)
            maxDrawdown = drawdown;
    }

    return maxDrawdown;
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

sealed class ValidationMetrics
{
    public ValidationMetrics(
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
