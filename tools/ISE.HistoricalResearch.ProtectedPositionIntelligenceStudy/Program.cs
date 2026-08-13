using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.ProtectedPositionIntelligenceStudy -- <contract-aware-tsv-path>");
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

    var v61Selected = v61Decisions
        .Where(x => x.Selected)
        .Select(x => x.Candidate)
        .ToList();

    var flawedV7 = new MorningVectorFlowPositionIntelligenceAnalyzer()
        .Analyze(bars, v61Selected)
        .ToList();

    var protectedAnalyzer = new MorningProtectedPositionIntelligenceAnalyzer();
    var replay = protectedAnalyzer.ReplayFrozenStrict(bars, candidates);
    var protectedTrades = replay.SelectedTrades.ToList();

    var baseline = v61Selected.Select(x => x.Entry.Source.Source).ToList();

    Console.WriteLine("ISE Elite V7.1 Protected Position Intelligence Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine($"V6.1 selected trades: {v61Selected.Count}");
    Console.WriteLine($"V7 flawed managed trades: {flawedV7.Count}");
    Console.WriteLine($"V7.1 protected replay trades: {protectedTrades.Count}");
    Console.WriteLine("Entry authority remains frozen: Entry Efficiency >=70 + V5.6 Potential >=80.");
    Console.WriteLine("VectorFlow alignment alone cannot promote a trade.");
    Console.WriteLine("Core is earned only when +150 ticks is reached while completed 5m VectorFlow is aligned.");
    Console.WriteLine("Runner is earned only after Core, >=300 MFE, and >=2 aligned completed 5m states.");
    Console.WriteLine();

    Console.WriteLine("OVERALL");
    Console.WriteLine("model\tselected\tavgRealized\tpositiveRate\tavgMFE\tavgMAE\tscalp\tcore\trunner\tstructStop\tscalpCapture\ttimeout\tbreakeven\textensionFloor\trunnerTrail\tbiasLoss");

    PrintBaseline("V6.1HistoricalBaseline", baseline);
    PrintV7("V7FlawedManager", flawedV7);
    PrintV71("V7.1Protected", protectedTrades);

    Console.WriteLine();
    Console.WriteLine("V7.1 replay lifecycle");
    Console.WriteLine($"position-open rejects: {replay.RejectedPositionOpen}");
    Console.WriteLine($"attempt-limit rejects: {replay.RejectedAttemptLimit}");
    Console.WriteLine($"entry-quality rejects: {replay.RejectedEntryQuality}");
    Console.WriteLine($"potential-below-80 rejects: {replay.RejectedPotential}");
    Console.WriteLine();

    Console.WriteLine("MONTHLY / HALF-MONTH");
    Console.WriteLine("period\tmodel\tselected\tavgRealized\tpositiveRate\tavgMFE\tavgMAE\tcore\trunner\tbreakeven\textensionFloor\trunnerTrail\tbiasLoss");

    var dates = candidates
        .Select(x => x.SessionDateCentral)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    foreach (var period in BuildPeriods(dates))
    {
        var b = baseline
            .Where(x => period.Contains(x.SessionDateCentral))
            .ToList();

        var f = flawedV7
            .Where(x => period.Contains(x.Candidate.SessionDateCentral))
            .ToList();

        var p = protectedTrades
            .Where(x => period.Contains(x.Candidate.SessionDateCentral))
            .ToList();

        PrintBaselinePeriod(period.Label, "V6.1HistoricalBaseline", b);
        PrintV7Period(period.Label, "V7FlawedManager", f);
        PrintV71Period(period.Label, "V7.1Protected", p);
    }

    Console.WriteLine();
    Console.WriteLine("V7.1 gate:");
    Console.WriteLine("- Protected management must materially recover the expectancy destroyed by V7.");
    Console.WriteLine("- Alignment alone must never create Core.");
    Console.WriteLine("- Core/Runner protection should reduce structural-stop giveback after extension.");
    Console.WriteLine("- Managed exit time is authoritative for whether a later same-day candidate is executable.");
    Console.WriteLine("- Do not tune the frozen 150/100/40%/300/2/250 protection seeds from one period alone.");

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
        "-", "-", "-", "-", "-", "-", "-", "-", "-", "-"
    }));
}

static void PrintV7(
    string label,
    IReadOnlyList<MorningVectorFlowManagedTrade> trades)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(trades.Select(x => x.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Scalp).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Core).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Runner).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.StructuralStop).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.ScalpCapture).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.ScalpTimeout).ToString(CultureInfo.InvariantCulture),
        "0", "0", "0",
        trades.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.VectorFlowBiasLoss).ToString(CultureInfo.InvariantCulture)
    }));
}

static void PrintV71(
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
        trades.Count(x => x.FinalMode == MorningProtectedPositionMode.Scalp).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningProtectedPositionMode.Core).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningProtectedPositionMode.Runner).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.StructuralStop).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpCapture).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ScalpTimeout).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.AdaptiveBreakeven).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ExtensionFloor).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.RunnerTrail).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.VectorFlowBiasLoss).ToString(CultureInfo.InvariantCulture)
    }));
}

static void PrintBaselinePeriod(
    string period,
    string model,
    IReadOnlyList<MorningAdaptiveTradeOutcome> trades)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        period, model,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(trades.Select(x => x.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
        "-", "-", "-", "-", "-", "-"
    }));
}

static void PrintV7Period(
    string period,
    string model,
    IReadOnlyList<MorningVectorFlowManagedTrade> trades)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        period, model,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(trades.Select(x => x.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Core).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningPositionIntelligenceMode.Runner).ToString(CultureInfo.InvariantCulture),
        "0", "0", "0",
        trades.Count(x => x.ExitReason == MorningPositionIntelligenceExitReason.VectorFlowBiasLoss).ToString(CultureInfo.InvariantCulture)
    }));
}

static void PrintV71Period(
    string period,
    string model,
    IReadOnlyList<MorningProtectedManagedTrade> trades)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        period, model,
        trades.Count.ToString(CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Positive(trades.Select(x => x.RealizedDollars)),
        Avg(trades.Select(x => x.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(trades.Select(x => x.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningProtectedPositionMode.Core).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.FinalMode == MorningProtectedPositionMode.Runner).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.AdaptiveBreakeven).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.ExtensionFloor).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.RunnerTrail).ToString(CultureInfo.InvariantCulture),
        trades.Count(x => x.ExitReason == MorningProtectedPositionExitReason.VectorFlowBiasLoss).ToString(CultureInfo.InvariantCulture)
    }));
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
