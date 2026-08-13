using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ISE.HistoricalResearch.RiskSizedExecutionLifecycleStudy -- <contract-aware-tsv-path>");
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

    var v73Manager = new MorningProtectedPositionIntelligenceAnalyzer(
        new MorningProtectedPositionConfig(
            enablePreExtensionAdaptiveBreakeven: false));

    var fixedTwo = v73Manager.ReplayFrozenStrict(bars, candidates);

    var riskSized = new MorningRiskSizedExecutionLifecycleAnalyzer(
        riskObjectiveDollars: 150m,
        dollarsPerTickPerContract: 0.50m,
        maximumContracts: 2)
        .Replay(bars, candidates);

    var sessionDates = candidates
        .Select(x => x.SessionDateCentral)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    var fixedTrades = fixedTwo.SelectedTrades.ToList();
    var sizedTrades = riskSized.SelectedTrades.ToList();

    Console.WriteLine("ISE Elite V7.5 Risk-Sized Execution Lifecycle Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Sessions: {sessionDates.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine("Frozen: Entry>=70, Potential>=80, one position, max 2 executed attempts, V7.3 management.");
    Console.WriteLine("New variable only: size 2/1/0 MNQ from structural risk against $150 risk objective.");
    Console.WriteLine("Structural stop is never moved.");
    Console.WriteLine("Risk rejection consumes neither attempt nor position occupancy.");
    Console.WriteLine();

    Console.WriteLine("OVERALL");
    Console.WriteLine("model\tselected\tqty2\tqty1\triskReject\tavgTrade\tpositive\tavgDaily\tdays300\tdays500\tmaxTradeLoss\tpositionOpen\tattemptLimit");

    PrintFixedOverall("V7.3Fixed2MNQ", fixedTwo, sessionDates);
    PrintSizedOverall("V7.5RiskSized", riskSized, sessionDates);

    Console.WriteLine();
    Console.WriteLine("RISK REJECTIONS");
    Console.WriteLine($"Risk-rejected opportunities: {riskSized.RejectedRisk}");

    foreach (var candidate in riskSized.RiskRejectedCandidates)
    {
        var source = candidate.Entry.Source.Source;

        Console.WriteLine(string.Join("\t", new[]
        {
            "RISK_REJECT",
            Key(candidate),
            $"riskTicks={source.InitialRiskTicks:F1}",
            $"risk1MNQ={source.InitialRiskTicks * 0.50m:F2}",
            $"baseline={source.RealizedDollars:F2}"
        }));
    }

    Console.WriteLine();
    Console.WriteLine("LIFECYCLE REPLACEMENTS");

    var fixedByKey = fixedTrades.ToDictionary(x => Key(x.Candidate));
    var sizedByKey = sizedTrades.ToDictionary(x => Key(x.Candidate));

    var fixedKeys = fixedByKey.Keys.ToHashSet();
    var sizedKeys = sizedByKey.Keys.ToHashSet();

    var added = sizedKeys.Except(fixedKeys).OrderBy(x => x).ToList();
    var removed = fixedKeys.Except(sizedKeys).OrderBy(x => x).ToList();

    Console.WriteLine($"Added later opportunities: {added.Count}");
    Console.WriteLine($"Removed fixed-2 opportunities: {removed.Count}");

    foreach (var key in added)
    {
        var trade = sizedByKey[key];

        Console.WriteLine(string.Join("\t", new[]
        {
            "ADDED",
            key,
            $"qty={trade.Quantity}",
            $"realized={trade.RealizedDollars:F2}",
            $"exit={trade.ExitReason}"
        }));
    }

    foreach (var key in removed)
    {
        var trade = fixedByKey[key];

        Console.WriteLine(string.Join("\t", new[]
        {
            "REMOVED",
            key,
            $"fixed2={trade.RealizedDollars:F2}",
            $"riskTicks={trade.Candidate.Entry.Source.Source.InitialRiskTicks:F1}",
            $"exit={trade.ExitReason}"
        }));
    }

    Console.WriteLine();
    Console.WriteLine("DAILY");
    Console.WriteLine("date\tfixedSelected\tfixedPnL\tsizedSelected\tsizedPnL\tdelta\triskRejects");

    foreach (var date in sessionDates)
    {
        var fixedDay = fixedTrades
            .Where(x => x.Candidate.SessionDateCentral == date)
            .ToList();

        var sizedDay = sizedTrades
            .Where(x => x.Candidate.SessionDateCentral == date)
            .ToList();

        var rejects = riskSized.RiskRejectedCandidates
            .Count(x => x.SessionDateCentral == date);

        var fixedPnl = fixedDay.Sum(x => x.RealizedDollars);
        var sizedPnl = sizedDay.Sum(x => x.RealizedDollars);

        Console.WriteLine(string.Join("\t", new[]
        {
            date.ToString("yyyy-MM-dd"),
            fixedDay.Count.ToString(CultureInfo.InvariantCulture),
            fixedPnl.ToString("F2", CultureInfo.InvariantCulture),
            sizedDay.Count.ToString(CultureInfo.InvariantCulture),
            sizedPnl.ToString("F2", CultureInfo.InvariantCulture),
            (sizedPnl - fixedPnl).ToString("F2", CultureInfo.InvariantCulture),
            rejects.ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("MONTHLY / HALF-MONTH");
    Console.WriteLine("period\tmodel\tsessions\tselected\tqty2\tqty1\triskReject\tavgTrade\tpositive\tavgDaily\tdays300\tdays500\tmaxTradeLoss");

    foreach (var period in BuildPeriods(sessionDates))
    {
        var periodDates = sessionDates.Where(period.Contains).ToList();

        var fixedPeriod = fixedTrades
            .Where(x => period.Contains(x.Candidate.SessionDateCentral))
            .ToList();

        var sizedPeriod = sizedTrades
            .Where(x => period.Contains(x.Candidate.SessionDateCentral))
            .ToList();

        var rejectCount = riskSized.RiskRejectedCandidates
            .Count(x => period.Contains(x.SessionDateCentral));

        PrintFixedPeriod(period.Label, periodDates, fixedPeriod);
        PrintSizedPeriod(period.Label, periodDates, sizedPeriod, rejectCount);
    }

    Console.WriteLine();
    Console.WriteLine("QUANTITY ATTRIBUTION");
    Console.WriteLine("quantity\tn\tavgRiskTicks\tavgPlannedRisk\tavgRealized\tpositive\tstructStop\ttimeout\tcore\trunner");

    foreach (var group in sizedTrades
        .GroupBy(x => x.Quantity)
        .OrderByDescending(x => x.Key))
    {
        var members = group.ToList();

        Console.WriteLine(string.Join("\t", new[]
        {
            group.Key.ToString(CultureInfo.InvariantCulture),
            members.Count.ToString(CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.InitialRiskTicks)).ToString("F1", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.PlannedRiskDollars)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            Positive(members.Select(x => x.RealizedDollars)),
            members.Count(x => x.ExitReason == MorningProtectedPositionExitReason.StructuralStop).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpTimeout).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.FinalMode == MorningProtectedPositionMode.Core).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.FinalMode == MorningProtectedPositionMode.Runner).ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("V7.5 gate:");
    Console.WriteLine("- Promote risk sizing only if it materially improves drawdown/loss containment without destroying daily expectancy.");
    Console.WriteLine("- Risk reject must remain a Risk-layer decision; do not move the structural stop.");
    Console.WriteLine("- Risk reject must not consume an attempt or position slot.");
    Console.WriteLine("- Keep V6.1 entry selection, V5.6 Potential, and V7.3 management frozen.");
    Console.WriteLine("- Evaluate replacement opportunities explicitly because rejected trades can expose a later valid setup.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintFixedOverall(
    string label,
    MorningProtectedReplayResult replay,
    IReadOnlyList<DateTime> sessionDates)
{
    var trades = replay.SelectedTrades.ToList();
    var daily = DailyFixed(trades, sessionDates);

    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count.ToString(CultureInfo.InvariantCulture),
        "0",
        "0",
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(daily.Values).ToString("F2", CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 500m).ToString(CultureInfo.InvariantCulture),
        Min(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        replay.RejectedPositionOpen.ToString(CultureInfo.InvariantCulture),
        replay.RejectedAttemptLimit.ToString(CultureInfo.InvariantCulture)
    }));
}

static void PrintSizedOverall(
    string label,
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> sessionDates)
{
    var trades = replay.SelectedTrades.ToList();
    var daily = DailySized(trades, sessionDates);

    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 2).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 1).ToString(CultureInfo.InvariantCulture),
        replay.RejectedRisk.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(daily.Values).ToString("F2", CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 500m).ToString(CultureInfo.InvariantCulture),
        Min(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        replay.RejectedPositionOpen.ToString(CultureInfo.InvariantCulture),
        replay.RejectedAttemptLimit.ToString(CultureInfo.InvariantCulture)
    }));
}

static void PrintFixedPeriod(
    string label,
    IReadOnlyList<DateTime> dates,
    IReadOnlyList<MorningProtectedManagedTrade> trades)
{
    var daily = DailyFixed(trades, dates);

    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        "V7.3Fixed2MNQ",
        dates.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count.ToString(CultureInfo.InvariantCulture),
        "0",
        "0",
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(daily.Values).ToString("F2", CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 500m).ToString(CultureInfo.InvariantCulture),
        Min(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture)
    }));
}

static void PrintSizedPeriod(
    string label,
    IReadOnlyList<DateTime> dates,
    IReadOnlyList<MorningRiskSizedTrade> trades,
    int rejectCount)
{
    var daily = DailySized(trades, dates);

    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        "V7.5RiskSized",
        dates.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 2).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 1).ToString(CultureInfo.InvariantCulture),
        rejectCount.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(daily.Values).ToString("F2", CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 500m).ToString(CultureInfo.InvariantCulture),
        Min(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture)
    }));
}

static Dictionary<DateTime, decimal> DailyFixed(
    IReadOnlyList<MorningProtectedManagedTrade> trades,
    IReadOnlyList<DateTime> dates)
{
    return dates.ToDictionary(
        date => date,
        date => trades
            .Where(x => x.Candidate.SessionDateCentral == date)
            .Sum(x => x.RealizedDollars));
}

static Dictionary<DateTime, decimal> DailySized(
    IReadOnlyList<MorningRiskSizedTrade> trades,
    IReadOnlyList<DateTime> dates)
{
    return dates.ToDictionary(
        date => date,
        date => trades
            .Where(x => x.Candidate.SessionDateCentral == date)
            .Sum(x => x.RealizedDollars));
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

static decimal Min(IEnumerable<decimal> values)
{
    var list = values.ToList();
    return list.Count == 0 ? 0m : list.Min();
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
