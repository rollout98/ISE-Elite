using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ISE.HistoricalResearch;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/ISE.HistoricalResearch.DailySequencingStudy -- <contract-aware-tsv-path>");
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
    var opportunityAnalyzer = new MorningMarketStateAdaptiveAnalyzer();
    var rawOpportunities = opportunityAnalyzer.Analyze(bars);

    var v5 = new MorningOpportunityPotentialAnalyzer();
    var potential = v5.Analyze(bars, rawOpportunities);

    var entryAnalyzer = new MorningEntryEfficiencyAnalyzer();
    var entry = entryAnalyzer.Analyze(bars, potential);

    var v56 = new MorningStabilityWeightedPotentialAnalyzer();
    var weighted = v56.Analyze(potential);

    var sequencer = new MorningDailyOpportunitySequencer();
    var candidates = sequencer.BuildCandidates(entry, weighted);

    var allDecisions = new List<MorningDailySequenceDecision>();
    foreach (MorningDailySequencingPolicy policy in Enum.GetValues(typeof(MorningDailySequencingPolicy)))
        allDecisions.AddRange(sequencer.Sequence(candidates, policy));

    var summaries = sequencer.Summarize(allDecisions);

    Console.WriteLine("ISE Elite V6 Daily Opportunity Sequencing Study");
    Console.WriteLine($"Dataset: {path}");
    Console.WriteLine($"Bars: {bars.Count}");
    Console.WriteLine($"Raw opportunities: {rawOpportunities.Count}");
    Console.WriteLine($"Entry/Potential candidates: {candidates.Count}");
    Console.WriteLine("Maximum attempts/day: 2");
    Console.WriteLine("Frozen V5.6 Potential: unchanged");
    Console.WriteLine("Entry authority: upstream structural opportunity + V5.2 Entry Efficiency");
    Console.WriteLine("VectorFlow: excluded from selection; remains post-entry management intelligence");
    Console.WriteLine();
    Console.WriteLine("Policies:");
    Console.WriteLine("  ControlFirstTwoHighEntry = first two Entry Efficiency >=70");
    Console.WriteLine("  StrictUpper80 = first two Entry Efficiency >=70 AND V5.6 Potential >=80");
    Console.WriteLine("  BalancedReserve = one strong 70-79 fallback allowed for slot 1 (Entry>=85), remaining slot reserved for 80+");
    Console.WriteLine();

    Console.WriteLine("OVERALL");
    Console.WriteLine("policy\tsessions\ttradedDays\tselected\tavgDailyRealized\tavgSelectedRealized\tpositiveRate\tavgMFE\tavgMAE\tdays>=300\tdays>=500\tdays>=1000\tselected300\tselected500\tmissedEligible300\tmissedEligible500");
    foreach (MorningDailySequencingPolicy policy in Enum.GetValues(typeof(MorningDailySequencingPolicy)))
    {
        var decisions = allDecisions.Where(x => x.Policy == policy).ToList();
        PrintOverall(policy, decisions);
    }

    Console.WriteLine();
    Console.WriteLine("MONTHLY / HALF-MONTH");
    Console.WriteLine("period\tpolicy\tsessions\ttradedDays\tselected\tavgDailyRealized\tavgSelectedRealized\tpositiveRate\tavgMFE\tavgMAE\tdays>=300\tdays>=500\tdays>=1000\tselected300\tselected500\tmissedEligible300\tmissedEligible500");
    foreach (var s in summaries.Where(x => x.Sessions > 0).OrderBy(x => x.Period).ThenBy(x => x.Policy))
    {
        Console.WriteLine(string.Join("\t", new[]
        {
            s.Period,
            s.Policy.ToString(),
            s.Sessions.ToString(CultureInfo.InvariantCulture),
            s.SessionsTraded.ToString(CultureInfo.InvariantCulture),
            s.SelectedTrades.ToString(CultureInfo.InvariantCulture),
            s.AverageDailyRealized.ToString("F2", CultureInfo.InvariantCulture),
            s.AverageSelectedRealized.ToString("F2", CultureInfo.InvariantCulture),
            (s.SelectedPositiveRate * 100m).ToString("F1", CultureInfo.InvariantCulture) + "%",
            s.AverageMfeTicks.ToString("F1", CultureInfo.InvariantCulture),
            s.AverageMaeTicks.ToString("F1", CultureInfo.InvariantCulture),
            s.DaysAtLeast300.ToString(CultureInfo.InvariantCulture),
            s.DaysAtLeast500.ToString(CultureInfo.InvariantCulture),
            s.DaysAtLeast1000.ToString(CultureInfo.InvariantCulture),
            s.SelectedHit300.ToString(CultureInfo.InvariantCulture),
            s.SelectedHit500.ToString(CultureInfo.InvariantCulture),
            s.MissedEligibleHit300.ToString(CultureInfo.InvariantCulture),
            s.MissedEligibleHit500.ToString(CultureInfo.InvariantCulture)
        }));
    }

    Console.WriteLine();
    Console.WriteLine("Interpretation gate:");
    Console.WriteLine("- V6 must improve daily expectancy and/or reduce wasted attempts versus Control.");
    Console.WriteLine("- It should preserve meaningful 300+/500+ capture.");
    Console.WriteLine("- No policy is promoted merely for looking best on one month or half-month.");
    Console.WriteLine("- Future outcomes remain diagnostics only; sequencing is chronological and causal.");

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void PrintOverall(
    MorningDailySequencingPolicy policy,
    List<MorningDailySequenceDecision> decisions)
{
    var dates = decisions.Select(x => x.Candidate.SessionDateCentral).Distinct().OrderBy(x => x).ToList();
    var selected = decisions.Where(x => x.Selected).ToList();
    var tradedDays = selected.Select(x => x.Candidate.SessionDateCentral).Distinct().Count();

    var daily = dates.Select(date => selected
        .Where(x => x.Candidate.SessionDateCentral == date)
        .Sum(x => x.Candidate.Entry.Source.Source.RealizedDollars))
        .ToList();

    var eligibleUnselected = decisions
        .Where(x => !x.Selected && x.Candidate.EntryEfficiencyScore >= 70m)
        .ToList();

    decimal avgDaily = dates.Count == 0 ? 0m : daily.Average();
    decimal avgSelected = selected.Count == 0 ? 0m : selected.Average(x => x.Candidate.Entry.Source.Source.RealizedDollars);
    decimal positive = selected.Count == 0 ? 0m : 100m * selected.Count(x => x.Candidate.Entry.Source.Source.RealizedDollars > 0m) / selected.Count;
    decimal avgMfe = selected.Count == 0 ? 0m : selected.Average(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks);
    decimal avgMae = selected.Count == 0 ? 0m : selected.Average(x => x.Candidate.Entry.Source.Source.MaxAdverseTicks);

    Console.WriteLine(string.Join("\t", new[]
    {
        policy.ToString(),
        dates.Count.ToString(CultureInfo.InvariantCulture),
        tradedDays.ToString(CultureInfo.InvariantCulture),
        selected.Count.ToString(CultureInfo.InvariantCulture),
        avgDaily.ToString("F2", CultureInfo.InvariantCulture),
        avgSelected.ToString("F2", CultureInfo.InvariantCulture),
        positive.ToString("F1", CultureInfo.InvariantCulture) + "%",
        avgMfe.ToString("F1", CultureInfo.InvariantCulture),
        avgMae.ToString("F1", CultureInfo.InvariantCulture),
        daily.Count(x => x >= 300m).ToString(CultureInfo.InvariantCulture),
        daily.Count(x => x >= 500m).ToString(CultureInfo.InvariantCulture),
        daily.Count(x => x >= 1000m).ToString(CultureInfo.InvariantCulture),
        selected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
        selected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture),
        eligibleUnselected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 300m).ToString(CultureInfo.InvariantCulture),
        eligibleUnselected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 500m).ToString(CultureInfo.InvariantCulture)
    }));
}
