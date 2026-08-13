using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine(
        "Usage: dotnet run --project tools/ISE.HistoricalResearch.PreExtensionRiskAttributionStudy -- <contract-aware-tsv-path>");
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

    var v61 = new MorningExecutionRealisticDailyOpportunitySequencer()
        .Sequence(candidates, MorningDailySequencingPolicy.StrictUpper80)
        .Where(x => x.Selected)
        .Select(x => x.Candidate)
        .ToList();

    var v73Analyzer = new MorningProtectedPositionIntelligenceAnalyzer(
        new MorningProtectedPositionConfig(
            enablePreExtensionAdaptiveBreakeven: false));

    var managed = v61
        .Select(x => v73Analyzer.Manage(bars, x))
        .Where(x => x != null)
        .Cast<MorningProtectedManagedTrade>()
        .ToList();

    if (managed.Count != v61.Count)
        throw new InvalidOperationException(
            $"V7.4 attribution invalid: V6.1 entries={v61.Count}, V7.3 managed={managed.Count}.");

    var attribution = new MorningPreExtensionRiskAttributionAnalyzer(
        riskObjectiveDollars: 150m,
        dollarsPerTickPerContract: 0.50m)
        .Analyze(managed)
        .ToList();

    var preExtension = attribution
        .Where(x => x.IsPreExtension)
        .ToList();

    var structuralStops = preExtension
        .Where(x => x.IsStructuralStop)
        .ToList();

    var timeouts = preExtension
        .Where(x => x.IsScalpTimeout)
        .ToList();

    Console.WriteLine("ISE Elite V7.4 Pre-Extension Risk Attribution Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Candidates: {candidates.Count}");
    Console.WriteLine($"Exact paired V6.1 / V7.3 trades: {managed.Count}");
    Console.WriteLine($"Pre-extension trades: {preExtension.Count}");
    Console.WriteLine($"Pre-extension structural stops: {structuralStops.Count}");
    Console.WriteLine($"Pre-extension timeouts: {timeouts.Count}");
    Console.WriteLine("Risk objective used only for diagnosis: $150 per trade.");
    Console.WriteLine("MNQ sizing diagnostic: $0.50 per tick per contract; maximum modeled quantity = 2.");
    Console.WriteLine("No structural stop is moved and no entry is rejected in V7.4.");
    Console.WriteLine();

    Console.WriteLine("PRE-EXTENSION OVERALL");
    PrintSummary("AllPreExtension", preExtension);
    PrintSummary("StructuralStop", structuralStops);
    PrintSummary("ScalpTimeout", timeouts);

    Console.WriteLine();
    Console.WriteLine("RISK-SIZING COVERAGE");
    Console.WriteLine("cohort\tn\t2MNQWithin150\t1MNQOnlyWithin150\tOver150EvenAt1MNQ\tavgRiskTicks\tavgRisk$2\tavgRisk$1\tavgManaged\tavgRiskSized");

    PrintSizing("AllPreExtension", preExtension);
    PrintSizing("StructuralStop", structuralStops);
    PrintSizing("ScalpTimeout", timeouts);

    Console.WriteLine();
    Console.WriteLine("STRUCTURAL-STOP RISK BANDS");
    PrintGroups(structuralStops.GroupBy(x => x.RiskBand));

    Console.WriteLine();
    Console.WriteLine("TIMEOUT RISK BANDS");
    PrintGroups(timeouts.GroupBy(x => x.RiskBand));

    Console.WriteLine();
    Console.WriteLine("STRUCTURAL-STOP DIRECTION");
    PrintGroups(structuralStops.GroupBy(x => x.Baseline.Direction.ToString()));

    Console.WriteLine();
    Console.WriteLine("TIMEOUT DIRECTION");
    PrintGroups(timeouts.GroupBy(x => x.Baseline.Direction.ToString()));

    Console.WriteLine();
    Console.WriteLine("STRUCTURAL-STOP ENTRY TIME");
    PrintGroups(structuralStops.GroupBy(x => x.EntryTimeSegment));

    Console.WriteLine();
    Console.WriteLine("TIMEOUT ENTRY TIME");
    PrintGroups(timeouts.GroupBy(x => x.EntryTimeSegment));

    Console.WriteLine();
    Console.WriteLine("STRUCTURAL-STOP MFE/RISK");
    Console.WriteLine("band\tn\tavgRiskTicks\tavgMFE\tavgMAE\tavgMfeRiskFraction\tavgManaged\t2MNQWithin150\t1MNQOnly\tNoQtyWithin150");

    foreach (var group in structuralStops
        .GroupBy(x => MfeRiskBand(x.MfeRiskFraction))
        .OrderBy(x => x.Key))
    {
        PrintMfeRisk(group.Key, group.ToList());
    }

    Console.WriteLine();
    Console.WriteLine("MONTHLY PRE-EXTENSION RISK");
    Console.WriteLine("month\tn\tstructStops\ttimeouts\tavgRiskTicks\t2MNQWithin150\t1MNQOnly\tNoQtyWithin150\tavgManaged\tavgRiskSized");

    foreach (var month in preExtension
        .Select(x => new DateTime(
            x.Candidate.SessionDateCentral.Year,
            x.Candidate.SessionDateCentral.Month,
            1))
        .Distinct()
        .OrderBy(x => x))
    {
        var members = preExtension
            .Where(x =>
                x.Candidate.SessionDateCentral.Year == month.Year
                && x.Candidate.SessionDateCentral.Month == month.Month)
            .ToList();

        Console.WriteLine(string.Join("\t", new[]
        {
            month.ToString("yyyy-MM"),
            members.Count.ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.IsStructuralStop).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.IsScalpTimeout).ToString(CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.InitialRiskTicks)).ToString("F1", CultureInfo.InvariantCulture),
            members.Count(x => x.MaximumContractsWithinRisk == 2).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.MaximumContractsWithinRisk == 1).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.MaximumContractsWithinRisk == 0).ToString(CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.ManagedTrade.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.RiskSizedRealizedDollars)).ToString("F2", CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("STRUCTURAL-STOP DETAIL");
    Console.WriteLine("key\tdirection\tentryCT\triskTicks\trisk$2\trisk$1\tmaxQty150\tMFE\tMAE\tmfeRisk\tmanaged\tbaseline");

    foreach (var x in structuralStops
        .OrderByDescending(x => x.InitialRiskTicks)
        .ThenBy(x => x.Candidate.EntryUtc))
    {
        Console.WriteLine(string.Join("\t", new[]
        {
            Key(x.Candidate),
            x.Baseline.Direction.ToString(),
            x.EntryTimeSegment,
            x.InitialRiskTicks.ToString("F1", CultureInfo.InvariantCulture),
            x.RiskDollarsTwoContracts.ToString("F2", CultureInfo.InvariantCulture),
            x.RiskDollarsOneContract.ToString("F2", CultureInfo.InvariantCulture),
            x.MaximumContractsWithinRisk.ToString(CultureInfo.InvariantCulture),
            x.ManagedTrade.MaxFavorableTicks.ToString("F1", CultureInfo.InvariantCulture),
            x.ManagedTrade.MaxAdverseTicks.ToString("F1", CultureInfo.InvariantCulture),
            x.MfeRiskFraction.ToString("F2", CultureInfo.InvariantCulture),
            x.ManagedTrade.RealizedDollars.ToString("F2", CultureInfo.InvariantCulture),
            x.Baseline.RealizedDollars.ToString("F2", CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("V7.4 interpretation gate:");
    Console.WriteLine("- Do not tighten the structural stop merely to force the $150 risk objective.");
    Console.WriteLine("- If 1 MNQ makes a structurally valid trade fit the risk objective, sizing is the preferred control.");
    Console.WriteLine("- If risk exceeds $150 even at 1 MNQ, treat that as a Risk-layer eligibility question, not a stop-placement change.");
    Console.WriteLine("- Separate structural-stop behavior from timeout behavior before modifying either mechanism.");
    Console.WriteLine("- Keep V6.1 selection, frozen V5.6 Potential, and V7.3 Core/Runner management unchanged.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintSummary(
    string label,
    IReadOnlyList<MorningPreExtensionRiskAttributionObservation> members)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        $"n={members.Count}",
        $"avgRiskTicks={Avg(members.Select(x => x.InitialRiskTicks)):F1}",
        $"avgMFE={Avg(members.Select(x => x.ManagedTrade.MaxFavorableTicks)):F1}",
        $"avgMAE={Avg(members.Select(x => x.ManagedTrade.MaxAdverseTicks)):F1}",
        $"avgManaged={Avg(members.Select(x => x.ManagedTrade.RealizedDollars)):F2}",
        $"positive={Positive(members.Select(x => x.ManagedTrade.RealizedDollars))}"
    }));
}

static void PrintSizing(
    string label,
    IReadOnlyList<MorningPreExtensionRiskAttributionObservation> members)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        members.Count.ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.MaximumContractsWithinRisk == 2).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.MaximumContractsWithinRisk == 1).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.MaximumContractsWithinRisk == 0).ToString(CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.InitialRiskTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.RiskDollarsTwoContracts)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.RiskDollarsOneContract)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedTrade.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.RiskSizedRealizedDollars)).ToString("F2", CultureInfo.InvariantCulture)
    }));
}

static void PrintGroups(
    IEnumerable<IGrouping<string, MorningPreExtensionRiskAttributionObservation>> groups)
{
    Console.WriteLine("group\tn\tavgRiskTicks\tavgRisk$2\tavgMFE\tavgMAE\tavgMfeRisk\tavgManaged\tbaselineAvg\t2MNQWithin150\t1MNQOnly\tNoQtyWithin150");

    foreach (var group in groups.OrderBy(x => x.Key))
    {
        var members = group.ToList();

        Console.WriteLine(string.Join("\t", new[]
        {
            group.Key,
            members.Count.ToString(CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.InitialRiskTicks)).ToString("F1", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.RiskDollarsTwoContracts)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.ManagedTrade.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.ManagedTrade.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.MfeRiskFraction)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.ManagedTrade.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            Avg(members.Select(x => x.Baseline.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
            members.Count(x => x.MaximumContractsWithinRisk == 2).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.MaximumContractsWithinRisk == 1).ToString(CultureInfo.InvariantCulture),
            members.Count(x => x.MaximumContractsWithinRisk == 0).ToString(CultureInfo.InvariantCulture)
        }));
    }
}

static void PrintMfeRisk(
    string label,
    IReadOnlyList<MorningPreExtensionRiskAttributionObservation> members)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        label,
        members.Count.ToString(CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.InitialRiskTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedTrade.MaxFavorableTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedTrade.MaxAdverseTicks)).ToString("F1", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.MfeRiskFraction)).ToString("F2", CultureInfo.InvariantCulture),
        Avg(members.Select(x => x.ManagedTrade.RealizedDollars)).ToString("F2", CultureInfo.InvariantCulture),
        members.Count(x => x.MaximumContractsWithinRisk == 2).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.MaximumContractsWithinRisk == 1).ToString(CultureInfo.InvariantCulture),
        members.Count(x => x.MaximumContractsWithinRisk == 0).ToString(CultureInfo.InvariantCulture)
    }));
}

static string MfeRiskBand(decimal fraction)
{
    if (fraction < 0.25m) return "<0.25R";
    if (fraction < 0.50m) return "0.25-0.49R";
    if (fraction < 1.00m) return "0.50-0.99R";
    if (fraction < 2.00m) return "1.00-1.99R";
    return "2.00R+";
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
