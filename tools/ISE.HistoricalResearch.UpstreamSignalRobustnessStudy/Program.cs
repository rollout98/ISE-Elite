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

Console.WriteLine("ISE Elite V7.8.4 Upstream Signal Robustness Attribution");
Console.WriteLine("Purpose: identify which causal entry/potential characteristics change behavior across independent windows.");
Console.WriteLine("Fixed execution reference only: Fixed2, one position, max 2 attempts, Entry>=70, Potential>=80, V7.3 management.");
Console.WriteLine("This study does NOT change thresholds and does NOT select a new threshold.");
Console.WriteLine();

var windows = specs.Select(Analyze).ToList();

Console.WriteLine("WINDOW BASELINE");
Console.WriteLine("window\tsessions\tselected\tavgTrade\tpositive\ttotalPnL\tavgMFE\tavgMAE");
foreach (var w in windows)
{
    var t = w.Trades;
    Console.WriteLine(string.Join("\t", new[]
    {
        w.Name,
        w.Sessions.Count.ToString(CultureInfo.InvariantCulture),
        t.Count.ToString(CultureInfo.InvariantCulture),
        F(Avg(t.Select(x => x.RealizedDollars))),
        Pct(t.Select(x => x.RealizedDollars)),
        F(t.Sum(x => x.RealizedDollars)),
        F(Avg(t.Select(Mfe))),
        F(Avg(t.Select(Mae)))
    }));
}

PrintBucketFamily(
    windows,
    "PotentialScore",
    t => Band(t.Candidate.PotentialScore, new[] { 85m, 90m, 95m }, new[] { "80-84", "85-89", "90-94", "95-100" }));

PrintBucketFamily(
    windows,
    "EntryEfficiency",
    t => Band(t.Candidate.EntryEfficiencyScore, new[] { 75m, 80m, 90m }, new[] { "70-74", "75-79", "80-89", "90-100" }));

PrintBucketFamily(
    windows,
    "MoveAgeBars",
    t =>
    {
        var v = t.Candidate.Potential.Source.Features.MoveAgeBars;
        if (v <= 6) return "0-6";
        if (v <= 12) return "7-12";
        if (v <= 18) return "13-18";
        if (v <= 24) return "19-24";
        return "25+";
    });

PrintBucketFamily(
    windows,
    "EfficiencyDelta",
    t =>
    {
        var v = t.Candidate.Potential.Source.Features.EfficiencyDelta;
        if (v < -0.10m) return "<-0.10";
        if (v < 0m) return "-0.10..0";
        if (v < 0.10m) return "0..0.10";
        return ">=0.10";
    });

PrintBucketFamily(
    windows,
    "AccelerationRatio",
    t =>
    {
        var v = t.Candidate.Potential.Source.Features.AccelerationRatio;
        if (v < 0.75m) return "<0.75";
        if (v < 1.00m) return "0.75-0.99";
        if (v < 1.50m) return "1.00-1.49";
        return ">=1.50";
    });

PrintBucketFamily(
    windows,
    "ExhaustionRisk",
    t =>
    {
        var v = t.Candidate.Potential.Source.Features.ExhaustionRisk;
        if (v < 0.20m) return "<0.20";
        if (v < 0.40m) return "0.20-0.39";
        if (v < 0.60m) return "0.40-0.59";
        return ">=0.60";
    });

PrintBucketFamily(
    windows,
    "StructuralRiskFraction",
    t =>
    {
        var v = t.Candidate.Entry.Features.StructuralRiskFraction;
        if (v < 0.25m) return "<0.25";
        if (v < 0.40m) return "0.25-0.39";
        if (v < 0.60m) return "0.40-0.59";
        return ">=0.60";
    });

PrintBucketFamily(
    windows,
    "PullbackDepthFraction",
    t =>
    {
        var v = t.Candidate.Entry.Features.PullbackDepthFraction;
        if (v < 0.10m) return "<0.10";
        if (v <= 0.40m) return "0.10-0.40";
        if (v <= 0.55m) return "0.41-0.55";
        return ">0.55";
    });

PrintBucketFamily(
    windows,
    "ResetCount",
    t =>
    {
        var v = t.Candidate.Entry.Features.ResetCount;
        if (v == 0) return "0";
        if (v <= 2) return "1-2";
        if (v <= 4) return "3-4";
        return "5+";
    });

PrintBucketFamily(
    windows,
    "BarsSinceLastReset",
    t =>
    {
        var v = t.Candidate.Entry.Features.BarsSinceLastReset;
        if (v <= 2) return "0-2";
        if (v <= 4) return "3-4";
        if (v <= 7) return "5-7";
        return "8+";
    });

PrintBucketFamily(
    windows,
    "EntryHourCentral",
    t =>
    {
        var central = ResolveCentral();
        var h = TimeZoneInfo.ConvertTime(t.Candidate.EntryUtc, central).Hour;
        if (h < 6) return "03-05";
        if (h < 8) return "06-07";
        if (h < 10) return "08-09";
        return "10+";
    });

PrintBucketFamily(
    windows,
    "Direction",
    t => t.Candidate.Entry.Source.Source.Direction.ToString());

Console.WriteLine();
Console.WriteLine("ROBUSTNESS FLAGS");
Console.WriteLine("family\tbucket\tpreAvg\tcalAvg\tpostAvg\tclassification");

var families = BuildFamilies(windows);
foreach (var family in families.OrderBy(x => x.Name))
{
    foreach (var bucket in family.Buckets.OrderBy(x => x))
    {
        var preAvg = BucketAvg(windows[0], family.Selector, bucket);
        var calAvg = BucketAvg(windows[1], family.Selector, bucket);
        var postAvg = BucketAvg(windows[2], family.Selector, bucket);

        var classification =
            preAvg < 0m && calAvg > 0m && postAvg > 0m ? "regime-sensitive-sign-flip" :
            preAvg < 0m && calAvg < 0m ? "persistently-weak" :
            preAvg > 0m && calAvg > 0m ? "cross-window-positive" :
            "mixed";

        Console.WriteLine($"{family.Name}\t{bucket}\t{F(preAvg)}\t{F(calAvg)}\t{F(postAvg)}\t{classification}");
    }
}

Console.WriteLine();
Console.WriteLine("DECISION GATE");
Console.WriteLine("- Do not change Entry>=70 or Potential>=80 from this study alone.");
Console.WriteLine("- Look for causal feature buckets that are persistently weak or show a clean pre/calibration sign flip.");
Console.WriteLine("- If weakness concentrates in a specific causal state, the next build should add a regime/state discriminator rather than raise a global threshold.");
Console.WriteLine("- If weakness is diffuse across all buckets, revisit the structural opportunity definition itself.");
Console.WriteLine("- Risk budgets remain frozen; V7.8.4 is upstream attribution only.");

return 0;

static Window Analyze(Spec s)
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

    var sessions = candidates
        .Select(x => x.SessionDateCentral.Date)
        .Distinct()
        .OrderBy(x => x)
        .ToList();

    var fixed2 = new MorningRiskControlDecompositionAnalyzer(150m, 0.50m)
        .Replay(
            bars,
            candidates,
            MorningRiskControlPolicy.FixedTwo,
            2,
            70m,
            80m);

    return new Window(
        s.Name,
        sessions,
        fixed2.SelectedTrades.ToList());
}

static void PrintBucketFamily(
    IReadOnlyList<Window> windows,
    string family,
    Func<MorningRiskSizedTrade, string> selector)
{
    Console.WriteLine();
    Console.WriteLine(family.ToUpperInvariant());
    Console.WriteLine("window\tbucket\tcount\tavgTrade\tpositive\ttotalPnL\tavgMFE\tavgMAE");

    foreach (var w in windows)
    {
        foreach (var g in w.Trades.GroupBy(selector).OrderBy(x => x.Key))
        {
            var t = g.ToList();
            Console.WriteLine(string.Join("\t", new[]
            {
                w.Name,
                g.Key,
                t.Count.ToString(CultureInfo.InvariantCulture),
                F(Avg(t.Select(x => x.RealizedDollars))),
                Pct(t.Select(x => x.RealizedDollars)),
                F(t.Sum(x => x.RealizedDollars)),
                F(Avg(t.Select(Mfe))),
                F(Avg(t.Select(Mae)))
            }));
        }
    }
}

static IReadOnlyList<Family> BuildFamilies(IReadOnlyList<Window> windows)
{
    var families = new List<Family>
    {
        new("PotentialScore", t => Band(t.Candidate.PotentialScore, new[] {85m,90m,95m}, new[] {"80-84","85-89","90-94","95-100"})),
        new("EntryEfficiency", t => Band(t.Candidate.EntryEfficiencyScore, new[] {75m,80m,90m}, new[] {"70-74","75-79","80-89","90-100"})),
        new("MoveAgeBars", t => {
            var v=t.Candidate.Potential.Source.Features.MoveAgeBars;
            return v<=6?"0-6":v<=12?"7-12":v<=18?"13-18":v<=24?"19-24":"25+";
        }),
        new("EfficiencyDelta", t => {
            var v=t.Candidate.Potential.Source.Features.EfficiencyDelta;
            return v < -0.10m ? "<-0.10" : v < 0m ? "-0.10..0" : v < 0.10m ? "0..0.10" : ">=0.10";
        }),
        new("AccelerationRatio", t => {
            var v=t.Candidate.Potential.Source.Features.AccelerationRatio;
            return v<0.75m?"<0.75":v<1m?"0.75-0.99":v<1.5m?"1.00-1.49":">=1.50";
        }),
        new("ExhaustionRisk", t => {
            var v=t.Candidate.Potential.Source.Features.ExhaustionRisk;
            return v<0.2m?"<0.20":v<0.4m?"0.20-0.39":v<0.6m?"0.40-0.59":">=0.60";
        }),
        new("StructuralRiskFraction", t => {
            var v=t.Candidate.Entry.Features.StructuralRiskFraction;
            return v<0.25m?"<0.25":v<0.4m?"0.25-0.39":v<0.6m?"0.40-0.59":">=0.60";
        }),
        new("PullbackDepthFraction", t => {
            var v=t.Candidate.Entry.Features.PullbackDepthFraction;
            return v<0.10m?"<0.10":v<=0.40m?"0.10-0.40":v<=0.55m?"0.41-0.55":">0.55";
        }),
        new("ResetCount", t => {
            var v=t.Candidate.Entry.Features.ResetCount;
            return v==0?"0":v<=2?"1-2":v<=4?"3-4":"5+";
        }),
        new("BarsSinceLastReset", t => {
            var v=t.Candidate.Entry.Features.BarsSinceLastReset;
            return v<=2?"0-2":v<=4?"3-4":v<=7?"5-7":"8+";
        })
    };

    foreach (var f in families)
    {
        f.Buckets = windows
            .SelectMany(w => w.Trades)
            .Select(f.Selector)
            .Distinct()
            .ToList();
    }

    return families;
}

static decimal BucketAvg(Window w, Func<MorningRiskSizedTrade,string> selector, string bucket)
{
    var t = w.Trades.Where(x => selector(x) == bucket).ToList();
    return t.Count == 0 ? 0m : t.Average(x => x.RealizedDollars);
}

static string Band(decimal v, decimal[] cuts, string[] labels)
{
    for (var i = 0; i < cuts.Length; i++)
        if (v < cuts[i]) return labels[i];
    return labels[labels.Length - 1];
}

static decimal Mfe(MorningRiskSizedTrade t) =>
    t.Candidate.Entry.Source.Source.MaxFavorableTicks;

static decimal Mae(MorningRiskSizedTrade t) =>
    t.Candidate.Entry.Source.Source.MaxAdverseTicks;

static decimal Avg(IEnumerable<decimal> values)
{
    var x = values.ToList();
    return x.Count == 0 ? 0m : x.Average();
}

static string Pct(IEnumerable<decimal> values)
{
    var x = values.ToList();
    if (x.Count == 0) return "0.0%";
    return (100m * x.Count(v => v > 0m) / x.Count)
        .ToString("F1", CultureInfo.InvariantCulture) + "%";
}

static string F(decimal v) =>
    v.ToString("F2", CultureInfo.InvariantCulture);

static TimeZoneInfo ResolveCentral()
{
    try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
}

sealed record Spec(string Name, string Path, DateTime Start, DateTime? EndExclusive);
sealed record Window(string Name, IReadOnlyList<DateTime> Sessions, IReadOnlyList<MorningRiskSizedTrade> Trades);

sealed class Family
{
    public Family(string name, Func<MorningRiskSizedTrade,string> selector)
    {
        Name = name;
        Selector = selector;
        Buckets = new List<string>();
    }

    public string Name { get; }
    public Func<MorningRiskSizedTrade,string> Selector { get; }
    public IReadOnlyList<string> Buckets { get; set; }
}
