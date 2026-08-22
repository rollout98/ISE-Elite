using System.Globalization;
using System.Text.Json;
using ISE.HistoricalResearch;

if (args.Length < 1 || args.Length > 3)
{
    Console.Error.WriteLine("Usage: <continuous-forward-mnq-tsv> [yyyy-MM-dd] [output-directory]");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Dataset not found: {path}");
    return 3;
}

var bars = new HistoricalDataFileStore().ReadContractAware(path);
if (bars.Count == 0)
    throw new InvalidOperationException("Forensics dataset contains zero bars.");

if (bars.Any(x => string.IsNullOrWhiteSpace(x.Instrument) || !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Frozen V7.8.7 forensics requires MNQ-only data.");

var raw = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
var potential = new MorningOpportunityPotentialAnalyzer().Analyze(bars, raw);
var entry = new MorningEntryEfficiencyAnalyzer().Analyze(bars, potential);
var weighted = new MorningStabilityWeightedPotentialAnalyzer().Analyze(potential);
var all = new MorningDailyOpportunitySequencer().BuildCandidates(entry, weighted)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var evaluationSessions = bars
    .Select(x => x.TradingDay.Date)
    .Where(x => x > new DateTime(2026, 8, 10))
    .Distinct()
    .OrderBy(x => x)
    .ToList();

if (evaluationSessions.Count == 0)
    throw new InvalidOperationException("Dataset contains no V7.8.7 evaluation sessions after 2026-08-10.");

var targetDate = args.Length >= 2
    ? DateTime.ParseExact(args[1], "yyyy-MM-dd", CultureInfo.InvariantCulture).Date
    : evaluationSessions[evaluationSessions.Count - 1];

var baselineCandidates = all
    .Where(x => x.SessionDateCentral.Date > new DateTime(2026, 8, 10))
    .Where(x => x.EntryEfficiencyScore >= 70m)
    .Where(x => x.PotentialScore >= 80m)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var frozenCandidates = baselineCandidates
    .Where(x => x.Entry.Features.BarsSinceLastReset >= 3)
    .OrderBy(x => x.EntryUtc)
    .ToList();

// This is the exact Fixed2 replay used by the V7.8.7 control study.
var fixedReplay = new MorningRiskControlDecompositionAnalyzer(150m, 0.50m)
    .Replay(bars, frozenCandidates, MorningRiskControlPolicy.FixedTwo, 2, 70m, 80m);

var fundedSizer = new MorningRiskControlDecompositionAnalyzer(175m, 0.50m);
var combineSizer = new MorningRiskControlDecompositionAnalyzer(250m, 0.50m);
var central = ResolveCentralTimeZone();

var targetAll = all.Where(x => x.SessionDateCentral.Date == targetDate).OrderBy(x => x.EntryUtc).ToList();
var targetBaseline = baselineCandidates.Where(x => x.SessionDateCentral.Date == targetDate).OrderBy(x => x.EntryUtc).ToList();
var targetFrozen = frozenCandidates.Where(x => x.SessionDateCentral.Date == targetDate).OrderBy(x => x.EntryUtc).ToList();
var targetSelected = fixedReplay.SelectedTrades
    .Where(x => x.Candidate.SessionDateCentral.Date == targetDate)
    .OrderBy(x => x.Candidate.EntryUtc)
    .ToList();

var candidateRows = new List<CandidateRow>();
foreach (var c in targetAll)
{
    var baselineEligible = c.EntryEfficiencyScore >= 70m && c.PotentialScore >= 80m;
    var frozenEligible = baselineEligible && c.Entry.Features.BarsSinceLastReset >= 3;
    var selected = targetSelected.FirstOrDefault(x => ReferenceEquals(x.Candidate, c));

    candidateRows.Add(new CandidateRow(
        c,
        baselineEligible,
        frozenEligible,
        selected != null,
        ClassifyDisposition(c, baselineEligible, frozenEligible, targetSelected),
        2,
        fundedSizer.ResolveQuantity(c.Entry.Source.Source.InitialRiskTicks, MorningRiskControlPolicy.StrictTwoOneZero),
        combineSizer.ResolveQuantity(c.Entry.Source.Source.InitialRiskTicks, MorningRiskControlPolicy.StrictTwoOneZero),
        central));
}

var lifecycleRows = targetSelected.Select(t => BuildLifecycle(t, bars, central)).ToList();

Console.WriteLine("ISE Elite Phase 1 Frozen Trade Forensics");
Console.WriteLine("Diagnostic only. V7.8.7 remains frozen; no tuning or parameter changes.");
Console.WriteLine($"Dataset: {path}");
Console.WriteLine($"Target session CT: {targetDate:yyyy-MM-dd}");
Console.WriteLine($"Bars: {bars.Count}");
Console.WriteLine();

Console.WriteLine("CANDIDATES");
Console.WriteLine("entryCT\tdirection\tentryPrice\tstopPrice\triskTicks\tentryScore\tpotentialScore\tbarsSinceReset\tbaseline\tfrozen\tselected\tfixedQty\tfunded175Qty\tcombine250Qty\tdisposition");
foreach (var r in candidateRows)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        r.EntryCentral.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        r.Direction,
        F(r.EntryPrice),
        F(r.StopPrice),
        F(r.InitialRiskTicks),
        F(r.EntryScore),
        F(r.PotentialScore),
        r.BarsSinceLastReset.ToString(CultureInfo.InvariantCulture),
        Yn(r.BaselineEligible),
        Yn(r.FrozenEligible),
        Yn(r.Selected),
        r.FixedQty.ToString(CultureInfo.InvariantCulture),
        r.Funded175Qty.ToString(CultureInfo.InvariantCulture),
        r.Combine250Qty.ToString(CultureInfo.InvariantCulture),
        r.Disposition
    }));
}

Console.WriteLine();
Console.WriteLine("SELECTED TRADE LIFECYCLE — FIXED2");
Console.WriteLine("entryCT\tdirection\texitCT\tdurationMin\trealizedTicks\trealizedDollars\tMFE\tMAE\tfinalMode\texitReason\textension\tcore\trunner\tmaxAligned5m\tbestProtectedTicks\tcaptureRatio\tpostFavTicks\tpostAdvTicks\tadditionalFavTicks");
foreach (var r in lifecycleRows)
{
    Console.WriteLine(string.Join("\t", new[]
    {
        r.EntryCentral.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        r.Direction,
        r.ExitCentral.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        F(r.DurationMinutes),
        F(r.RealizedTicks),
        F(r.RealizedDollars),
        F(r.MfeTicks),
        F(r.MaeTicks),
        r.FinalMode,
        r.ExitReason,
        Yn(r.ExtensionActivated),
        Yn(r.ReachedCore),
        Yn(r.ReachedRunner),
        r.MaximumAlignedFiveMinuteBars.ToString(CultureInfo.InvariantCulture),
        F(r.BestProtectedTicks),
        F(r.CaptureRatio),
        F(r.PostExitFavorableTicks),
        F(r.PostExitAdverseTicks),
        F(r.AdditionalFavorableTicks)
    }));
}

Console.WriteLine();
Console.WriteLine("SESSION SUMMARY");
Console.WriteLine($"rawCandidates={targetAll.Count}");
Console.WriteLine($"baselineEligible={targetBaseline.Count}");
Console.WriteLine($"frozenEligible={targetFrozen.Count}");
Console.WriteLine($"selected={targetSelected.Count}");
Console.WriteLine($"rejectedResetAge={candidateRows.Count(x => x.Disposition == \"RejectedResetAge\")}");
Console.WriteLine($"rejectedPositionOpen={candidateRows.Count(x => x.Disposition == \"RejectedPositionOpen\")}");
Console.WriteLine($"rejectedAttemptLimit={candidateRows.Count(x => x.Disposition == \"RejectedAttemptLimit\")}");
Console.WriteLine($"selectedPnL={F(targetSelected.Sum(x => x.RealizedDollars))}");
Console.WriteLine($"selectedAverageMFE={F(lifecycleRows.Count == 0 ? 0m : lifecycleRows.Average(x => x.MfeTicks))}");
Console.WriteLine($"averageCaptureRatio={F(lifecycleRows.Count == 0 ? 0m : lifecycleRows.Average(x => x.CaptureRatio))}");
Console.WriteLine($"coreCount={lifecycleRows.Count(x => x.ReachedCore)}");
Console.WriteLine($"runnerCount={lifecycleRows.Count(x => x.ReachedRunner)}");
foreach (var g in lifecycleRows.GroupBy(x => x.ExitReason).OrderBy(x => x.Key))
    Console.WriteLine($"exitReason.{g.Key}={g.Count()}");

Console.WriteLine();
Console.WriteLine("DIAGNOSIS FLAGS");
Console.WriteLine($"normalSelectedLoss={Yn(targetSelected.Any(x => x.RealizedDollars < 0m))}");
Console.WriteLine($"profitableBaselineRejectedByResetAge={Yn(candidateRows.Any(x => x.Disposition == \"RejectedResetAge\" && x.SourceRealizedDollars > 0m))}");
Console.WriteLine($"baselineOpportunitiesPresent={Yn(targetBaseline.Count > 0)}");
Console.WriteLine($"blockedByPositionOrAttempt={Yn(candidateRows.Any(x => x.Disposition is \"RejectedPositionOpen\" or \"RejectedAttemptLimit\"))}");
Console.WriteLine($"selectedMeaningfulMfeNotRetained={Yn(lifecycleRows.Any(x => x.MfeTicks >= 150m && x.RealizedTicks < x.MfeTicks * 0.50m))}");
Console.WriteLine($"selectedRemainedScalp={Yn(lifecycleRows.Any(x => !x.ReachedCore))}");
Console.WriteLine($"runnerReached={Yn(lifecycleRows.Any(x => x.ReachedRunner))}");
Console.WriteLine($"researchWindowEndExit={Yn(lifecycleRows.Any(x => x.ExitReason == nameof(MorningProtectedPositionExitReason.ResearchWindowEnd)))}");

if (args.Length == 3)
{
    var outputDirectory = Path.GetFullPath(args[2]);
    Directory.CreateDirectory(outputDirectory);
    var stem = $"frozen-forensics-{targetDate:yyyyMMdd}";
    WriteCandidates(Path.Combine(outputDirectory, stem + "-candidates.tsv"), candidateRows);
    WriteLifecycle(Path.Combine(outputDirectory, stem + "-lifecycle.tsv"), lifecycleRows);
    WriteSummary(Path.Combine(outputDirectory, stem + "-summary.json"), targetDate, path, candidateRows, lifecycleRows);
    Console.WriteLine();
    Console.WriteLine($"OUTPUT DIRECTORY {outputDirectory}");
}

return 0;

static string ClassifyDisposition(
    MorningDailySequencingCandidate candidate,
    bool baselineEligible,
    bool frozenEligible,
    IReadOnlyList<MorningRiskSizedTrade> selected)
{
    if (candidate.EntryEfficiencyScore < 70m) return "RejectedEntry";
    if (candidate.PotentialScore < 80m) return "RejectedPotential";
    if (baselineEligible && !frozenEligible) return "RejectedResetAge";

    if (selected.Any(x => ReferenceEquals(x.Candidate, candidate))) return "Selected";
    if (!frozenEligible) return "Rejected";

    var priorSelected = selected.Where(x => x.Candidate.EntryUtc < candidate.EntryUtc).OrderBy(x => x.Candidate.EntryUtc).ToList();
    if (priorSelected.Any(x => candidate.EntryUtc < x.ExitUtc)) return "RejectedPositionOpen";
    if (priorSelected.Count >= 2) return "RejectedAttemptLimit";
    return "ManagedNull";
}

static LifecycleRow BuildLifecycle(MorningRiskSizedTrade trade, IReadOnlyList<HistoricalBar> bars, TimeZoneInfo central)
{
    const decimal tickSize = 0.25m;
    var managed = trade.ManagedTrade;
    var source = trade.Candidate.Entry.Source.Source;
    var entryCentral = TimeZoneInfo.ConvertTime(source.EntryUtc, central).DateTime;
    var exitCentral = TimeZoneInfo.ConvertTime(managed.ExitUtc, central).DateTime;

    var after = bars
        .Where(x => x.TimestampUtc > managed.ExitUtc)
        .Where(x =>
        {
            var local = TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime;
            return local.Date == entryCentral.Date && local.TimeOfDay < new TimeSpan(11, 0, 0);
        })
        .OrderBy(x => x.TimestampUtc)
        .ToList();

    decimal postFav = 0m;
    decimal postAdv = 0m;
    if (after.Count > 0)
    {
        if (source.Direction == NewYorkResearchDirection.Long)
        {
            postFav = Math.Max(0m, (after.Max(x => x.High) - managed.ExitPrice) / tickSize);
            postAdv = Math.Max(0m, (managed.ExitPrice - after.Min(x => x.Low)) / tickSize);
        }
        else
        {
            postFav = Math.Max(0m, (managed.ExitPrice - after.Min(x => x.Low)) / tickSize);
            postAdv = Math.Max(0m, (after.Max(x => x.High) - managed.ExitPrice) / tickSize);
        }
    }

    return new LifecycleRow(
        entryCentral,
        source.Direction.ToString(),
        exitCentral,
        (decimal)(managed.ExitUtc - source.EntryUtc).TotalMinutes,
        trade.RealizedTicks,
        trade.RealizedDollars,
        managed.MaxFavorableTicks,
        managed.MaxAdverseTicks,
        managed.FinalMode.ToString(),
        managed.ExitReason.ToString(),
        managed.ExtensionActivated,
        managed.ExtensionActivated || managed.FinalMode >= MorningProtectedPositionMode.Core,
        managed.FinalMode == MorningProtectedPositionMode.Runner,
        managed.MaximumAlignedFiveMinuteBars,
        managed.BestProtectedTicks,
        managed.MaxFavorableTicks > 0m ? trade.RealizedTicks / managed.MaxFavorableTicks : 0m,
        postFav,
        postAdv,
        postFav);
}

static void WriteCandidates(string path, IReadOnlyList<CandidateRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("entryCentral\tdirection\tentryPrice\tstopPrice\tinitialRiskTicks\tentryScore\tpotentialScore\tbarsSinceLastReset\tbaselineEligible\tfrozenEligible\tselected\tfixedQty\tfunded175Qty\tcombine250Qty\tdisposition\tsourceRealizedDollars");
    foreach (var r in rows)
        w.WriteLine(string.Join("\t", new[] { r.EntryCentral.ToString("O", CultureInfo.InvariantCulture), r.Direction, F(r.EntryPrice), F(r.StopPrice), F(r.InitialRiskTicks), F(r.EntryScore), F(r.PotentialScore), r.BarsSinceLastReset.ToString(CultureInfo.InvariantCulture), Yn(r.BaselineEligible), Yn(r.FrozenEligible), Yn(r.Selected), r.FixedQty.ToString(CultureInfo.InvariantCulture), r.Funded175Qty.ToString(CultureInfo.InvariantCulture), r.Combine250Qty.ToString(CultureInfo.InvariantCulture), r.Disposition, F(r.SourceRealizedDollars) }));
}

static void WriteLifecycle(string path, IReadOnlyList<LifecycleRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("entryCentral\tdirection\texitCentral\tdurationMinutes\trealizedTicks\trealizedDollars\tmfeTicks\tmaeTicks\tfinalMode\texitReason\textensionActivated\treachedCore\treachedRunner\tmaximumAlignedFiveMinuteBars\tbestProtectedTicks\tcaptureRatio\tpostExitFavorableTicks\tpostExitAdverseTicks\tadditionalFavorableTicks");
    foreach (var r in rows)
        w.WriteLine(string.Join("\t", new[] { r.EntryCentral.ToString("O", CultureInfo.InvariantCulture), r.Direction, r.ExitCentral.ToString("O", CultureInfo.InvariantCulture), F(r.DurationMinutes), F(r.RealizedTicks), F(r.RealizedDollars), F(r.MfeTicks), F(r.MaeTicks), r.FinalMode, r.ExitReason, Yn(r.ExtensionActivated), Yn(r.ReachedCore), Yn(r.ReachedRunner), r.MaximumAlignedFiveMinuteBars.ToString(CultureInfo.InvariantCulture), F(r.BestProtectedTicks), F(r.CaptureRatio), F(r.PostExitFavorableTicks), F(r.PostExitAdverseTicks), F(r.AdditionalFavorableTicks) }));
}

static void WriteSummary(string path, DateTime targetDate, string datasetPath, IReadOnlyList<CandidateRow> candidates, IReadOnlyList<LifecycleRow> lifecycle)
{
    var summary = new
    {
        schemaVersion = 1,
        targetSessionCentral = targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        datasetPath,
        frozen = new { barsSinceLastResetMinimum = 3, entryMinimum = 70, potentialMinimum = 80, management = "V7.3", maximumAttempts = 2 },
        counts = new
        {
            rawCandidates = candidates.Count,
            baselineEligible = candidates.Count(x => x.BaselineEligible),
            frozenEligible = candidates.Count(x => x.FrozenEligible),
            selected = candidates.Count(x => x.Selected),
            rejectedResetAge = candidates.Count(x => x.Disposition == "RejectedResetAge"),
            rejectedPositionOpen = candidates.Count(x => x.Disposition == "RejectedPositionOpen"),
            rejectedAttemptLimit = candidates.Count(x => x.Disposition == "RejectedAttemptLimit")
        },
        selected = new
        {
            totalPnl = lifecycle.Sum(x => x.RealizedDollars),
            coreCount = lifecycle.Count(x => x.ReachedCore),
            runnerCount = lifecycle.Count(x => x.ReachedRunner),
            averageMfeTicks = lifecycle.Count == 0 ? 0m : lifecycle.Average(x => x.MfeTicks),
            averageCaptureRatio = lifecycle.Count == 0 ? 0m : lifecycle.Average(x => x.CaptureRatio)
        }
    };
    File.WriteAllText(path, JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
}

static TimeZoneInfo ResolveCentralTimeZone()
{
    try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
}

static string F(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);
static string Yn(bool value) => value ? "Y" : "N";

sealed class CandidateRow
{
    public CandidateRow(MorningDailySequencingCandidate candidate, bool baselineEligible, bool frozenEligible, bool selected, string disposition, int fixedQty, int funded175Qty, int combine250Qty, TimeZoneInfo central)
    {
        var source = candidate.Entry.Source.Source;
        EntryCentral = TimeZoneInfo.ConvertTime(source.EntryUtc, central).DateTime;
        Direction = source.Direction.ToString();
        EntryPrice = source.EntryPrice;
        StopPrice = source.StopPrice;
        InitialRiskTicks = source.InitialRiskTicks;
        EntryScore = candidate.EntryEfficiencyScore;
        PotentialScore = candidate.PotentialScore;
        BarsSinceLastReset = candidate.Entry.Features.BarsSinceLastReset;
        BaselineEligible = baselineEligible;
        FrozenEligible = frozenEligible;
        Selected = selected;
        Disposition = disposition;
        FixedQty = fixedQty;
        Funded175Qty = funded175Qty;
        Combine250Qty = combine250Qty;
        SourceRealizedDollars = source.RealizedDollars;
    }
    public DateTime EntryCentral { get; }
    public string Direction { get; }
    public decimal EntryPrice { get; }
    public decimal StopPrice { get; }
    public decimal InitialRiskTicks { get; }
    public decimal EntryScore { get; }
    public decimal PotentialScore { get; }
    public int BarsSinceLastReset { get; }
    public bool BaselineEligible { get; }
    public bool FrozenEligible { get; }
    public bool Selected { get; }
    public string Disposition { get; }
    public int FixedQty { get; }
    public int Funded175Qty { get; }
    public int Combine250Qty { get; }
    public decimal SourceRealizedDollars { get; }
}

sealed record LifecycleRow(
    DateTime EntryCentral,
    string Direction,
    DateTime ExitCentral,
    decimal DurationMinutes,
    decimal RealizedTicks,
    decimal RealizedDollars,
    decimal MfeTicks,
    decimal MaeTicks,
    string FinalMode,
    string ExitReason,
    bool ExtensionActivated,
    bool ReachedCore,
    bool ReachedRunner,
    int MaximumAlignedFiveMinuteBars,
    decimal BestProtectedTicks,
    decimal CaptureRatio,
    decimal PostExitFavorableTicks,
    decimal PostExitAdverseTicks,
    decimal AdditionalFavorableTicks);
