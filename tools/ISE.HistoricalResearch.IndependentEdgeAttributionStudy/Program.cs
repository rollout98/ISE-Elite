using System.Globalization;
using ISE.HistoricalResearch;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: <precal.tsv> <calibration.tsv> <postcal.tsv>");
    return 2;
}

var specs = new[]
{
    new Spec("PreCalibration", Path.GetFullPath(args[0]), new DateTime(2025,12,1), new DateTime(2026,3,25)),
    new Spec("Calibration", Path.GetFullPath(args[1]), new DateTime(2026,3,25), new DateTime(2026,8,1)),
    new Spec("PostCalibration", Path.GetFullPath(args[2]), new DateTime(2026,8,1), null)
};

Console.WriteLine("ISE Elite V7.8.3 Independent Edge Attribution");
Console.WriteLine("Frozen: Entry>=70, Potential>=80, V7.3 management, max 2 attempts.");
Console.WriteLine("Profiles: Fixed2, Funded175 strict 2/1/0, Combine250 strict 2/1/0.");
Console.WriteLine("No tuning.");
Console.WriteLine();

var results = specs.Select(Run).ToList();

Console.WriteLine("WINDOW SUMMARY");
Console.WriteLine("window\tprofile\tsessions\tselected\tqty2\tqty1\treject\tavgTrade\tpositive\ttotalPnL\tavgDaily\tworstTrade\tworstDay\tmaxDrawdown");
foreach (var r in results)
{
    Print(r.Name, "Fixed2", r.Fixed, r.Sessions);
    Print(r.Name, "Funded175", r.Funded, r.Sessions);
    Print(r.Name, "Combine250", r.Combine, r.Sessions);
}

Console.WriteLine();
Console.WriteLine("ATTRIBUTION DELTAS");
Console.WriteLine("window\tfixed2PnL\tfundedPnL\tcombinePnL\tfundedMinusFixed\tcombineMinusFixed");
foreach (var r in results)
{
    var f = Pnl(r.Fixed);
    var a = Pnl(r.Funded);
    var c = Pnl(r.Combine);
    Console.WriteLine($"{r.Name}\t{F(f)}\t{F(a)}\t{F(c)}\t{F(a-f)}\t{F(c-f)}");
}

Console.WriteLine();
Console.WriteLine("HALF-MONTH");
Console.WriteLine("window\tperiod\tprofile\tselected\ttotalPnL\tavgTrade\tpositive");
foreach (var r in results)
{
    var periods = r.Sessions
        .Select(d => $"{d:yyyy-MM}-H{(d.Day <= 15 ? 1 : 2)}")
        .Distinct()
        .OrderBy(x => x);

    foreach (var p in periods)
    {
        PrintPeriod(r.Name, p, "Fixed2", r.Fixed);
        PrintPeriod(r.Name, p, "Funded175", r.Funded);
        PrintPeriod(r.Name, p, "Combine250", r.Combine);
    }
}

Console.WriteLine();
Console.WriteLine("CONTRACT");
Console.WriteLine("window\tcontract\tprofile\tselected\ttotalPnL\tavgTrade\tpositive");
foreach (var r in results)
{
    foreach (var contract in r.ContractByDay.Values.Distinct().OrderBy(x => x))
    {
        var days = r.ContractByDay.Where(x => x.Value == contract).Select(x => x.Key).ToHashSet();
        PrintContract(r.Name, contract, "Fixed2", r.Fixed, days);
        PrintContract(r.Name, contract, "Funded175", r.Funded, days);
        PrintContract(r.Name, contract, "Combine250", r.Combine, days);
    }
}

Console.WriteLine();
Console.WriteLine("DECISION GATE");
Console.WriteLine("- Fixed2 negative pre-calibration => primary weakness is upstream of risk sizing.");
Console.WriteLine("- Fixed2 positive while sized profiles are negative => primary weakness is downstream sizing/governance.");
Console.WriteLine("- Fixed2 weak and sizing worse => both layers need separate work; do not tune together.");
Console.WriteLine("- V7.8.3 is attribution only; no profile promotion.");
return 0;

static Result Run(Spec s)
{
    if (!File.Exists(s.Path)) throw new FileNotFoundException(s.Path);

    var bars = new HistoricalDataFileStore().ReadContractAware(s.Path);
    if (bars.Count == 0) throw new InvalidOperationException($"{s.Name}: zero bars");

    if (bars.Any(x => string.IsNullOrWhiteSpace(x.Instrument) ||
                      !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"{s.Name}: non-MNQ data");

    var raw = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
    var pot = new MorningOpportunityPotentialAnalyzer().Analyze(bars, raw);
    var ent = new MorningEntryEfficiencyAnalyzer().Analyze(bars, pot);
    var weighted = new MorningStabilityWeightedPotentialAnalyzer().Analyze(pot);
    var all = new MorningDailyOpportunitySequencer().BuildCandidates(ent, weighted);

    var candidates = all
        .Where(x => x.SessionDateCentral.Date >= s.Start.Date)
        .Where(x => !s.EndExclusive.HasValue || x.SessionDateCentral.Date < s.EndExclusive.Value.Date)
        .OrderBy(x => x.EntryUtc)
        .ToList();

    if (candidates.Count == 0) throw new InvalidOperationException($"{s.Name}: zero candidates");

    var sessions = candidates
        .Select(x => x.SessionDateCentral.Date)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    var fixed2 = new MorningRiskControlDecompositionAnalyzer(150m, 0.50m)
        .Replay(bars, candidates, MorningRiskControlPolicy.FixedTwo, 2, 70m, 80m);

    var funded = new MorningRiskControlDecompositionAnalyzer(175m, 0.50m)
        .Replay(bars, candidates, MorningRiskControlPolicy.StrictTwoOneZero, 2, 70m, 80m);

    var combine = new MorningRiskControlDecompositionAnalyzer(250m, 0.50m)
        .Replay(bars, candidates, MorningRiskControlPolicy.StrictTwoOneZero, 2, 70m, 80m);

    var contractByDay = bars
        .GroupBy(x => x.TradingDay.Date)
        .ToDictionary(
            g => g.Key,
            g => g.GroupBy(x => x.Contract)
                  .OrderByDescending(x => x.Count())
                  .First()
                  .Key);

    return new Result(s.Name, sessions, fixed2, funded, combine, contractByDay);
}

static void Print(string window, string profile, MorningRiskSizedExecutionLifecycleResult r, IReadOnlyList<DateTime> sessions)
{
    var t = r.SelectedTrades.ToList();
    var daily = sessions
        .Select(d => t.Where(x => x.Candidate.SessionDateCentral.Date == d).Sum(x => x.RealizedDollars))
        .ToList();

    Console.WriteLine(string.Join("\t", new[]
    {
        window,
        profile,
        sessions.Count.ToString(),
        t.Count.ToString(),
        t.Count(x => x.Quantity == 2).ToString(),
        t.Count(x => x.Quantity == 1).ToString(),
        r.RejectedRisk.ToString(),
        F(Avg(t.Select(x => x.RealizedDollars))),
        Pct(t.Select(x => x.RealizedDollars)),
        F(t.Sum(x => x.RealizedDollars)),
        F(Avg(daily)),
        F(Min(t.Select(x => x.RealizedDollars))),
        F(Min(daily)),
        F(MaxDD(daily))
    }));
}

static void PrintPeriod(string window, string period, string profile, MorningRiskSizedExecutionLifecycleResult r)
{
    var t = r.SelectedTrades
        .Where(x => $"{x.Candidate.SessionDateCentral:yyyy-MM}-H{(x.Candidate.SessionDateCentral.Day <= 15 ? 1 : 2)}" == period)
        .ToList();

    Console.WriteLine($"{window}\t{period}\t{profile}\t{t.Count}\t{F(t.Sum(x => x.RealizedDollars))}\t{F(Avg(t.Select(x => x.RealizedDollars)))}\t{Pct(t.Select(x => x.RealizedDollars))}");
}

static void PrintContract(string window, string contract, string profile, MorningRiskSizedExecutionLifecycleResult r, HashSet<DateTime> days)
{
    var t = r.SelectedTrades
        .Where(x => days.Contains(x.Candidate.SessionDateCentral.Date))
        .ToList();

    Console.WriteLine($"{window}\t{contract}\t{profile}\t{t.Count}\t{F(t.Sum(x => x.RealizedDollars))}\t{F(Avg(t.Select(x => x.RealizedDollars)))}\t{Pct(t.Select(x => x.RealizedDollars))}");
}

static decimal Pnl(MorningRiskSizedExecutionLifecycleResult r) =>
    r.SelectedTrades.Sum(x => x.RealizedDollars);

static decimal Avg(IEnumerable<decimal> v)
{
    var x = v.ToList();
    return x.Count == 0 ? 0m : x.Average();
}

static decimal Min(IEnumerable<decimal> v)
{
    var x = v.ToList();
    return x.Count == 0 ? 0m : x.Min();
}

static decimal MaxDD(IReadOnlyList<decimal> v)
{
    decimal e = 0m, p = 0m, m = 0m;
    foreach (var x in v)
    {
        e += x;
        if (e > p) p = e;
        var d = p - e;
        if (d > m) m = d;
    }
    return m;
}

static string F(decimal v) =>
    v.ToString("F2", CultureInfo.InvariantCulture);

static string Pct(IEnumerable<decimal> v)
{
    var x = v.ToList();
    if (x.Count == 0) return "0.0%";
    return (100m * x.Count(y => y > 0m) / x.Count)
        .ToString("F1", CultureInfo.InvariantCulture) + "%";
}

sealed record Spec(string Name, string Path, DateTime Start, DateTime? EndExclusive);
sealed record Result(
    string Name,
    IReadOnlyList<DateTime> Sessions,
    MorningRiskSizedExecutionLifecycleResult Fixed,
    MorningRiskSizedExecutionLifecycleResult Funded,
    MorningRiskSizedExecutionLifecycleResult Combine,
    IReadOnlyDictionary<DateTime,string> ContractByDay);
