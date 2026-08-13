using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ISE.HistoricalResearch.RiskControlDecompositionStudy -- <contract-aware-tsv-path>");
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

    var analyzer = new MorningRiskControlDecompositionAnalyzer(
        riskObjectiveDollars: 150m,
        dollarsPerTickPerContract: 0.50m);

    var fixedTwo = analyzer.Replay(
        bars,
        candidates,
        MorningRiskControlPolicy.FixedTwo);

    var sizeOnly = analyzer.Replay(
        bars,
        candidates,
        MorningRiskControlPolicy.SizeTwoOrOne);

    var strict = analyzer.Replay(
        bars,
        candidates,
        MorningRiskControlPolicy.StrictTwoOneZero);

    var fixedTrades = fixedTwo.SelectedTrades.ToList();
    var sizeTrades = sizeOnly.SelectedTrades.ToList();
    var strictTrades = strict.SelectedTrades.ToList();

    var fixedKeys = fixedTrades.Select(x => Key(x.Candidate)).ToList();
    var sizeKeys = sizeTrades.Select(x => Key(x.Candidate)).ToList();

    if (!fixedKeys.SequenceEqual(sizeKeys))
    {
        throw new InvalidOperationException(
            "Decomposition invalid: FixedTwo and SizeTwoOrOne changed trade identity. Quantity must not alter management or occupancy.");
    }

    var sessions = candidates
        .Select(x => x.SessionDateCentral)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    Console.WriteLine("ISE Elite V7.6 Risk-Control Decomposition Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Sessions: {sessions.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine("Frozen: Entry>=70, Potential>=80, one position, max 2 executed attempts, V7.3 management.");
    Console.WriteLine("A FixedTwo: always 2 MNQ.");
    Console.WriteLine("B SizeTwoOrOne: 2 MNQ if two-contract risk <= $150, otherwise 1 MNQ; DIAGNOSTIC ONLY.");
    Console.WriteLine("C StrictTwoOneZero: 2 / 1 / risk reject exactly as V7.5.");
    Console.WriteLine("No structural stop, entry threshold, Potential threshold, Core rule, or Runner rule is changed.");
    Console.WriteLine();

    Console.WriteLine("OVERALL");
    Console.WriteLine("policy\tselected\tqty2\tqty1\triskReject\tavgTrade\tpositive\ttotalPnL\tavgDaily\tdays300\tdays500\tworstTrade\tworstDay");

    PrintOverall("A_FixedTwo", fixedTwo, sessions);
    PrintOverall("B_SizeTwoOrOne", sizeOnly, sessions);
    PrintOverall("C_StrictTwoOneZero", strict, sessions);

    var fixedDaily = Daily(fixedTrades, sessions);
    var sizeDaily = Daily(sizeTrades, sessions);
    var strictDaily = Daily(strictTrades, sessions);

    Console.WriteLine();
    Console.WriteLine("DECOMPOSITION");
    Console.WriteLine($"Sizing-only total delta B-A: {(sizeTrades.Sum(x => x.RealizedDollars) - fixedTrades.Sum(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"Sizing-only avg-daily delta B-A: {(Avg(sizeDaily.Values) - Avg(fixedDaily.Values)):F2}");
    Console.WriteLine($"Hard-rejection total delta C-B: {(strictTrades.Sum(x => x.RealizedDollars) - sizeTrades.Sum(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"Hard-rejection avg-daily delta C-B: {(Avg(strictDaily.Values) - Avg(sizeDaily.Values)):F2}");
    Console.WriteLine($"Full strict total delta C-A: {(strictTrades.Sum(x => x.RealizedDollars) - fixedTrades.Sum(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"Full strict avg-daily delta C-A: {(Avg(strictDaily.Values) - Avg(fixedDaily.Values)):F2}");
    Console.WriteLine();

    var highRiskKeys = fixedTrades
        .Where(x => x.InitialRiskTicks * 0.50m > 150m)
        .Select(x => Key(x.Candidate))
        .ToHashSet();

    var highFixed = fixedTrades
        .Where(x => highRiskKeys.Contains(Key(x.Candidate)))
        .ToList();

    var highSize = sizeTrades
        .Where(x => highRiskKeys.Contains(Key(x.Candidate)))
        .ToList();

    Console.WriteLine("OVER-$150-AT-1-MNQ COHORT");
    Console.WriteLine($"n: {highRiskKeys.Count}");
    Console.WriteLine($"Average risk ticks: {Avg(highFixed.Select(x => x.InitialRiskTicks)):F1}");
    Console.WriteLine($"Average risk at 1 MNQ: {Avg(highFixed.Select(x => x.InitialRiskTicks * 0.50m)):F2}");
    Console.WriteLine($"A fixed-2 avg: {Avg(highFixed.Select(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"B one-contract avg: {Avg(highSize.Select(x => x.RealizedDollars)):F2}");
    Console.WriteLine($"B one-contract positive: {Positive(highSize.Select(x => x.RealizedDollars))}");
    Console.WriteLine($"B structural stops: {highSize.Count(x => x.ExitReason == MorningProtectedPositionExitReason.StructuralStop)}");
    Console.WriteLine($"B scalp captures: {highSize.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpCapture)}");
    Console.WriteLine($"B timeouts: {highSize.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpTimeout)}");
    Console.WriteLine($"B Core: {highSize.Count(x => x.FinalMode == MorningProtectedPositionMode.Core)}");
    Console.WriteLine($"B Runner: {highSize.Count(x => x.FinalMode == MorningProtectedPositionMode.Runner)}");
    Console.WriteLine();

    Console.WriteLine("OVER-$150-AT-1-MNQ DETAIL");
    Console.WriteLine("key\tdirection\triskTicks\trisk1\tentryScore\tpotential\tfixed2\toneMNQ\texit\tmode\tMFE\tMAE");

    foreach (var trade in highSize.OrderBy(x => x.Candidate.EntryUtc))
    {
        var source = trade.Candidate.Entry.Source.Source;
        var fixedTrade = fixedTrades.Single(x => Key(x.Candidate) == Key(trade.Candidate));

        Console.WriteLine(string.Join("\t", new[]
        {
            Key(trade.Candidate),
            source.Direction.ToString(),
            trade.InitialRiskTicks.ToString("F1", CultureInfo.InvariantCulture),
            (trade.InitialRiskTicks * 0.50m).ToString("F2", CultureInfo.InvariantCulture),
            trade.Candidate.EntryEfficiencyScore.ToString("F1", CultureInfo.InvariantCulture),
            trade.Candidate.PotentialScore.ToString("F1", CultureInfo.InvariantCulture),
            fixedTrade.RealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            trade.RealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            trade.ExitReason.ToString(),
            trade.FinalMode.ToString(),
            trade.ManagedTrade.MaxFavorableTicks.ToString("F1", CultureInfo.InvariantCulture),
            trade.ManagedTrade.MaxAdverseTicks.ToString("F1", CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("MONTHLY / HALF-MONTH");
    Console.WriteLine("period\tpolicy\tsessions\tselected\tqty2\tqty1\triskReject\tavgTrade\tpositive\tavgDaily\tdays300\tworstTrade\tworstDay");

    foreach (var period in BuildPeriods(sessions))
    {
        var periodDates = sessions.Where(period.Contains).ToList();

        PrintPeriod(
            period.Label,
            "A_FixedTwo",
            fixedTrades.Where(x => period.Contains(x.Candidate.SessionDateCentral)).ToList(),
            0,
            periodDates);

        PrintPeriod(
            period.Label,
            "B_SizeTwoOrOne",
            sizeTrades.Where(x => period.Contains(x.Candidate.SessionDateCentral)).ToList(),
            0,
            periodDates);

        PrintPeriod(
            period.Label,
            "C_StrictTwoOneZero",
            strictTrades.Where(x => period.Contains(x.Candidate.SessionDateCentral)).ToList(),
            strict.RiskRejectedCandidates.Count(x => period.Contains(x.SessionDateCentral)),
            periodDates);
    }

    Console.WriteLine();
    Console.WriteLine("STRICT REJECTION EFFECT / REPLACEMENTS");

    var sizeByKey = sizeTrades.ToDictionary(x => Key(x.Candidate));
    var strictByKey = strictTrades.ToDictionary(x => Key(x.Candidate));

    var sizeSet = sizeByKey.Keys.ToHashSet();
    var strictSet = strictByKey.Keys.ToHashSet();

    var removed = sizeSet.Except(strictSet).OrderBy(x => x).ToList();
    var added = strictSet.Except(sizeSet).OrderBy(x => x).ToList();

    Console.WriteLine($"Removed by strict risk control: {removed.Count}");
    Console.WriteLine($"Added later by strict risk control: {added.Count}");

    foreach (var key in removed)
    {
        var x = sizeByKey[key];

        Console.WriteLine(string.Join("\t", new[]
        {
            "REMOVED",
            key,
            $"qty={x.Quantity}",
            $"risk1={x.InitialRiskTicks * 0.50m:F2}",
            $"oneMNQ={x.RealizedDollars:F2}",
            $"exit={x.ExitReason}"
        }));
    }

    foreach (var key in added)
    {
        var x = strictByKey[key];

        Console.WriteLine(string.Join("\t", new[]
        {
            "ADDED",
            key,
            $"qty={x.Quantity}",
            $"realized={x.RealizedDollars:F2}",
            $"exit={x.ExitReason}"
        }));
    }

    Console.WriteLine();
    Console.WriteLine("V7.6 interpretation gate:");
    Console.WriteLine("- B is diagnostic only if 1 MNQ exceeds the $150 objective; do not promote it as production risk policy.");
    Console.WriteLine("- Use B-A to measure the pure effect of downsizing while preserving exactly the same trades.");
    Console.WriteLine("- Use C-B to measure the incremental effect of hard rejection and the later opportunities it exposes.");
    Console.WriteLine("- Do not invent a new risk threshold from this pass.");
    Console.WriteLine("- Keep structural stop, V6.1 selection, V5.6 Potential, and V7.3 Core/Runner management frozen.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintOverall(
    string label,
    MorningRiskSizedExecutionLifecycleResult replay,
    IReadOnlyList<DateTime> sessions)
{
    var trades = replay.SelectedTrades.ToList();
    var daily = Daily(trades, sessions);

    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 2).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 1).ToString(CultureInfo.InvariantCulture),
        replay.RejectedRisk.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        trades.Sum(x => x.RealizedDollars).ToString("F2", CultureInfo.InvariantCulture),
        Avg(daily.Values).ToString("F2", CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 500m).ToString(CultureInfo.InvariantCulture),
        Min(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Min(daily.Values).ToString("F2", CultureInfo.InvariantCulture)
    }));
}

static void PrintPeriod(
    string period,
    string policy,
    IReadOnlyList<MorningRiskSizedTrade> trades,
    int riskRejects,
    IReadOnlyList<DateTime> dates)
{
    var daily = Daily(trades, dates);

    Console.WriteLine(string.Join("\t", new[]
    {
        period,
        policy,
        dates.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count.ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 2).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.Quantity == 1).ToString(CultureInfo.InvariantCulture),
        riskRejects.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(daily.Values).ToString("F2", CultureInfo.InvariantCulture),
        daily.Values.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        Min(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Min(daily.Values).ToString("F2", CultureInfo.InvariantCulture)
    }));
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

static string Key(MorningDailySequencingCandidate candidate)
{
    var source = candidate.Entry.Source.Source;

    return $"{candidate.SessionDateCentral:yyyy-MM-dd}|{candidate.EntryUtc:O}|{source.Direction}";
}

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
