using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ISE.HistoricalResearch.RiskBudgetFrontierStudy -- <contract-aware-tsv-path>");
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
    var budgets = new[]
    {
        150m,
        175m,
        200m,
        225m,
        250m,
        300m
    };

    var bars = new HistoricalDataFileStore().ReadContractAware(path);
    var raw = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
    var potential = new MorningOpportunityPotentialAnalyzer().Analyze(bars, raw);
    var entry = new MorningEntryEfficiencyAnalyzer().Analyze(bars, potential);
    var weighted = new MorningStabilityWeightedPotentialAnalyzer().Analyze(potential);
    var candidates = new MorningDailyOpportunitySequencer().BuildCandidates(entry, weighted);

    var sessions = candidates
        .Select(x => x.SessionDateCentral)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    var fixedTwo = new MorningRiskControlDecompositionAnalyzer(
        150m,
        0.50m)
        .Replay(
            bars,
            candidates,
            MorningRiskControlPolicy.FixedTwo);

    var frontier = new MorningRiskBudgetFrontierAnalyzer(
        dollarsPerTickPerContract: 0.50m,
        maximumContracts: 2)
        .Analyze(
            bars,
            candidates,
            budgets)
        .ToList();

    Console.WriteLine("ISE Elite V7.7 Risk Budget Frontier Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Sessions: {sessions.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine("Predetermined budgets: $150, $175, $200, $225, $250, $300.");
    Console.WriteLine("Strict policy at every budget: 2 MNQ / 1 MNQ / reject from structural risk.");
    Console.WriteLine("Frozen: Entry>=70, Potential>=80, one position, max 2 executed attempts, V7.3 management.");
    Console.WriteLine("Structural stop is never moved.");
    Console.WriteLine("This study maps a frontier only; it does not automatically select a production budget.");
    Console.WriteLine();

    Console.WriteLine("FIXED-2 REFERENCE");
    PrintReference(fixedTwo, sessions);

    Console.WriteLine();
    Console.WriteLine("FRONTIER");
    Console.WriteLine("budget\tselected\tqty2\tqty1\treject\tavgTrade\tpositive\ttotalPnL\tavgDaily\tdays300\tdays500\tworstTrade\tworstDay\tmaxDrawdown\tavgPlannedRisk\tmaxPlannedRisk");

    foreach (var point in frontier)
        PrintFrontier(point, sessions);

    Console.WriteLine();
    Console.WriteLine("FRONTIER DELTAS VS $150 STRICT");
    Console.WriteLine("budget\tselectedDelta\trejectDelta\ttotalPnLDelta\tavgDailyDelta\tworstTradeDelta\tworstDayDelta\tmaxDrawdownDelta");

    var base150 = frontier.Single(x => x.RiskBudgetDollars == 150m);
    var base150Metrics = Metrics(base150.Lifecycle, sessions);

    foreach (var point in frontier)
    {
        var metrics = Metrics(point.Lifecycle, sessions);

        Console.WriteLine(string.Join("\t", new[]
        {
            Money(point.RiskBudgetDollars),
            (metrics.Selected - base150Metrics.Selected).ToString(CultureInfo.InvariantCulture),
            (metrics.Rejected - base150Metrics.Rejected).ToString(CultureInfo.InvariantCulture),
            (metrics.TotalPnl - base150Metrics.TotalPnl).ToString("F2", CultureInfo.InvariantCulture),
            (metrics.AvgDaily - base150Metrics.AvgDaily).ToString("F2", CultureInfo.InvariantCulture),
            (metrics.WorstTrade - base150Metrics.WorstTrade).ToString("F2", CultureInfo.InvariantCulture),
            (metrics.WorstDay - base150Metrics.WorstDay).ToString("F2", CultureInfo.InvariantCulture),
            (metrics.MaxDrawdown - base150Metrics.MaxDrawdown).ToString("F2", CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("MONTHLY / HALF-MONTH FRONTIER");
    Console.WriteLine("period\tbudget\tsessions\tselected\tqty2\tqty1\treject\tavgTrade\tpositive\tavgDaily\tdays300\tworstTrade\tworstDay\tmaxDrawdown");

    foreach (var period in BuildPeriods(sessions))
    {
        var periodDates = sessions
            .Where(period.Contains)
            .ToList();

        foreach (var point in frontier)
        {
            var trades = point.Lifecycle.SelectedTrades
                .Where(x => period.Contains(x.Candidate.SessionDateCentral))
                .ToList();

            var rejects = point.Lifecycle.RiskRejectedCandidates
                .Count(x => period.Contains(x.SessionDateCentral));

            PrintPeriod(
                period.Label,
                point.RiskBudgetDollars,
                periodDates,
                trades,
                rejects);
        }
    }

    Console.WriteLine();
    Console.WriteLine("LIFECYCLE CHANGES VS $150");
    Console.WriteLine("budget\taddedVs150\tremovedVs150");

    var keys150 = base150.Lifecycle.SelectedTrades
        .Select(x => Key(x.Candidate))
        .ToHashSet();

    foreach (var point in frontier.Where(x => x.RiskBudgetDollars > 150m))
    {
        var keys = point.Lifecycle.SelectedTrades
            .Select(x => Key(x.Candidate))
            .ToHashSet();

        Console.WriteLine(string.Join("\t", new[]
        {
            Money(point.RiskBudgetDollars),
            keys.Except(keys150).Count().ToString(CultureInfo.InvariantCulture),
            keys150.Except(keys).Count().ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("RISK-REJECT COUNTS BY BUDGET");
    Console.WriteLine("budget\trejected\tavgRiskTicks\tavgRisk1MNQ\tavgEntryScore\tavgPotential");

    foreach (var point in frontier)
    {
        var rejected = point.Lifecycle.RiskRejectedCandidates.ToList();

        Console.WriteLine(string.Join("\t", new[]
        {
            Money(point.RiskBudgetDollars),
            rejected.Count.ToString(CultureInfo.InvariantCulture),
            Avg(rejected.Select(x => x.Entry.Source.Source.InitialRiskTicks)).ToString("F1", CultureInfo.InvariantCulture),
            Avg(rejected.Select(x => x.Entry.Source.Source.InitialRiskTicks * 0.50m)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(rejected.Select(x => x.EntryEfficiencyScore)).ToString("F1", CultureInfo.InvariantCulture),
            Avg(rejected.Select(x => x.PotentialScore)).ToString("F1", CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("V7.7 interpretation gate:");
    Console.WriteLine("- Do not select the budget simply because it has the highest in-sample PnL.");
    Console.WriteLine("- Prefer a stable risk/return frontier across months and half-months.");
    Console.WriteLine("- Funded governance should favor lower tail risk; Combine governance may tolerate a higher budget only if stability supports it.");
    Console.WriteLine("- Keep structural stop, V6.1 entry selection, V5.6 Potential, and V7.3 management frozen.");
    Console.WriteLine("- No new setup-quality threshold is authorized by this study.");
    Console.WriteLine("- Any production budget decision must be validated out of sample before promotion.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintReference(
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> sessions)
{
    var metrics = Metrics(replay, sessions);

    Console.WriteLine(string.Join("\t", new[]
    {
        "Fixed2",
        $"selected={metrics.Selected}",
        $"avgTrade={metrics.AvgTrade:F2}",
        $"totalPnL={metrics.TotalPnl:F2}",
        $"avgDaily={metrics.AvgDaily:F2}",
        $"worstTrade={metrics.WorstTrade:F2}",
        $"worstDay={metrics.WorstDay:F2}",
        $"maxDrawdown={metrics.MaxDrawdown:F2}"
    }));
}

static void PrintFrontier(
    MorningRiskBudgetFrontierPoint point,
    IReadOnlyList<DateTime> sessions)
{
    var replay = point.Lifecycle;
    var trades = replay.SelectedTrades.ToList();
    var metrics = Metrics(replay, sessions);

    Console.WriteLine(string.Join("\t", new[]
    {
        Money(point.RiskBudgetDollars),
        metrics.Selected.ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 2).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 1).ToString(CultureInfo.InvariantCulture),
        metrics.Rejected.ToString(CultureInfo.InvariantCulture),
        metrics.AvgTrade.ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        metrics.TotalPnl.ToString("F2", CultureInfo.InvariantCulture),
        metrics.AvgDaily.ToString("F2", CultureInfo.InvariantCulture),
        metrics.Days300.ToString(CultureInfo.InvariantCulture),
        metrics.Days500.ToString(CultureInfo.InvariantCulture),
        metrics.WorstTrade.ToString("F2", CultureInfo.InvariantCulture),
        metrics.WorstDay.ToString("F2", CultureInfo.InvariantCulture),
        metrics.MaxDrawdown.ToString("F2", CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.PlannedRiskDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Max(trades.Select(x => x.PlannedRiskDollars)).ToString("F2", CultureInfo.InvariantCulture)
    }));
}

static void PrintPeriod(
    string period,
    decimal budget,
    IReadOnlyList<DateTime> dates,
    IReadOnlyList<MorningRiskSizedTrade> trades,
    int rejects)
{
    var daily = Daily(trades, dates);
    var values = dates.Select(x => daily[x]).ToList();

    Console.WriteLine(string.Join("\t", new[]
    {
        period,
        Money(budget),
        dates.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 2).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 1).ToString(CultureInfo.InvariantCulture),
        rejects.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(values).ToString("F2", CultureInfo.InvariantCulture),
        values.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        Min(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Min(values).ToString("F2", CultureInfo.InvariantCulture),
        MaxDrawdown(values).ToString("F2", CultureInfo.InvariantCulture)
    }));
}

static FrontierMetrics Metrics(
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> sessions)
{
    var trades = replay.SelectedTrades.ToList();
    var daily = Daily(trades, sessions);
    var dailyValues = sessions.Select(x => daily[x]).ToList();

    return new FrontierMetrics(
        trades.Count,
        replay.RejectedRisk,
        Avg(trades.Select(x => x.RealizedDollars)),
        trades.Sum(x => x.RealizedDollars),
        Avg(dailyValues),
        dailyValues.Count(x => x >= 300m),
        dailyValues.Count(x => x >= 500m),
        Min(trades.Select(x => x.RealizedDollars)),
        Min(dailyValues),
        MaxDrawdown(dailyValues));
}

static Dictionary<DateTime, decimal> Daily(
    IReadOnlyList<MorningRiskSizedTrade> trades,
    IReadOnlyList<DateTime> dates)
{
    return dates.ToDictionary(
        date => date,
        date => trades
            .Where(x => x.Candidate.SessionDateCentral == date)
            .Sum(x => x.RealizedDollars));
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

        var drawdown = peak - equity;

        if (drawdown > maxDrawdown)
            maxDrawdown = drawdown;
    }

    return maxDrawdown;
}

static string Key(
    MorningDailySequencingCandidate candidate)
{
    var source = candidate.Entry.Source.Source;

    return $"{candidate.SessionDateCentral:yyyy-MM-dd}|{candidate.EntryUtc:O}|{source.Direction}";
}

static string Money(decimal value)
    => "$" + value.ToString("0", CultureInfo.InvariantCulture);

static decimal Avg(IEnumerable<decimal> values)
{
    var list = values.ToList();

    return list.Count == 0
        ? 0m
        : list.Average();
}

static decimal Min(IEnumerable<decimal> values)
{
    var list = values.ToList();

    return list.Count == 0
        ? 0m
        : list.Min();
}

static decimal Max(IEnumerable<decimal> values)
{
    var list = values.ToList();

    return list.Count == 0
        ? 0m
        : list.Max();
}

static string Positive(IEnumerable<decimal> values)
{
    var list = values.ToList();

    if (list.Count == 0)
        return "0.0%";

    return (100m * list.Count(x => x > 0m) / list.Count)
        .ToString("F1", CultureInfo.InvariantCulture)
        + "%";
}

static IReadOnlyList<Period> BuildPeriods(
    IReadOnlyList<DateTime> dates)
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
            new DateTime(
                month.Year,
                month.Month,
                16)));

        result.Add(new Period(
            month.ToString("yyyy-MM") + "-H2",
            new DateTime(
                month.Year,
                month.Month,
                16),
            next));
    }

    return result;
}

sealed class FrontierMetrics
{
    public FrontierMetrics(
        int selected,
        int rejected,
        decimal avgTrade,
        decimal totalPnl,
        decimal avgDaily,
        int days300,
        int days500,
        decimal worstTrade,
        decimal worstDay,
        decimal maxDrawdown)
    {
        Selected = selected;
        Rejected = rejected;
        AvgTrade = avgTrade;
        TotalPnl = totalPnl;
        AvgDaily = avgDaily;
        Days300 = days300;
        Days500 = days500;
        WorstTrade = worstTrade;
        WorstDay = worstDay;
        MaxDrawdown = maxDrawdown;
    }

    public int Selected { get; }
    public int Rejected { get; }
    public decimal AvgTrade { get; }
    public decimal TotalPnl { get; }
    public decimal AvgDaily { get; }
    public int Days300 { get; }
    public int Days500 { get; }
    public decimal WorstTrade { get; }
    public decimal WorstDay { get; }
    public decimal MaxDrawdown { get; }
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
        => date >= Start
            && date < EndExclusive;
}
