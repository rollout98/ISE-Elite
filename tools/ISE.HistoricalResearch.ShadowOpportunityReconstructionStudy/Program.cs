using System.Globalization;
using System.Text.Json;
using ISE.HistoricalResearch;

if (args.Length < 1 || args.Length > 2)
{
    Console.Error.WriteLine("Usage: <continuous-forward-mnq-tsv> [output-directory]");
    return 2;
}

var datasetPath = Path.GetFullPath(args[0]);
if (!File.Exists(datasetPath))
{
    Console.Error.WriteLine($"Dataset not found: {datasetPath}");
    return 3;
}

var outputDirectory = args.Length == 2
    ? Path.GetFullPath(args[1])
    : Path.Combine(Environment.CurrentDirectory, "shadow-opportunity-output");
Directory.CreateDirectory(outputDirectory);

var bars = new HistoricalDataFileStore().ReadContractAware(datasetPath);
if (bars.Count == 0)
    throw new InvalidOperationException("Dataset contains zero bars.");
if (bars.Any(x => string.IsNullOrWhiteSpace(x.Instrument) || !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Phase 2 shadow reconstruction requires MNQ-only data.");

var raw = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
var potential = new MorningOpportunityPotentialAnalyzer().Analyze(bars, raw);
var entry = new MorningEntryEfficiencyAnalyzer().Analyze(bars, potential);
var weighted = new MorningStabilityWeightedPotentialAnalyzer().Analyze(potential);
var allCandidates = new MorningDailyOpportunitySequencer().BuildCandidates(entry, weighted)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var baseline = allCandidates
    .Where(x => x.SessionDateCentral.Date > new DateTime(2026, 8, 10))
    .Where(x => x.EntryEfficiencyScore >= 70m)
    .Where(x => x.PotentialScore >= 80m)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var frozen = baseline
    .Where(x => x.Entry.Features.BarsSinceLastReset >= 3)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var replay = new MorningRiskControlDecompositionAnalyzer(150m, 0.50m)
    .Replay(bars, frozen, MorningRiskControlPolicy.FixedTwo, 2, 70m, 80m);

var selected = replay.SelectedTrades.OrderBy(x => x.Candidate.EntryUtc).ToList();
var resetRejected = baseline
    .Where(x => x.Entry.Features.BarsSinceLastReset < 3)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var central = ResolveCentralTimeZone();
var selectedRows = selected.Select(x => BuildSelectedRow(x, bars, central)).ToList();
var rejectedRows = resetRejected.Select(x => BuildRejectedRow(x, bars, central)).ToList();
var sessions = bars.Select(x => x.TradingDay.Date).Where(x => x > new DateTime(2026, 8, 10)).Distinct().OrderBy(x => x).ToList();
var sessionRows = sessions.Select(date => BuildSessionRow(date, selectedRows, rejectedRows)).ToList();

WriteSelected(Path.Combine(outputDirectory, "selected-trade-shadow.tsv"), selectedRows);
WriteRejected(Path.Combine(outputDirectory, "reset-age-rejected-shadow.tsv"), rejectedRows);
WriteSessions(Path.Combine(outputDirectory, "session-summary.tsv"), sessionRows);
WriteSummaryJson(Path.Combine(outputDirectory, "summary.json"), datasetPath, sessions, selectedRows, rejectedRows);
WriteSummaryText(Path.Combine(outputDirectory, "summary.txt"), datasetPath, sessions, selectedRows, rejectedRows);

Console.WriteLine("ISE Elite Phase 2 Shadow Opportunity Reconstruction");
Console.WriteLine("Diagnostic only. Frozen V7.8.7 authority is unchanged.");
Console.WriteLine($"Dataset: {datasetPath}");
Console.WriteLine($"Output: {outputDirectory}");
Console.WriteLine($"Sessions: {sessions.Count}");
Console.WriteLine($"Authoritative selected trades: {selectedRows.Count}");
Console.WriteLine($"Authoritative total P&L: {F(selectedRows.Sum(x => x.RealizedDollars))}");
Console.WriteLine($"Authoritative average trade: {F(selectedRows.Count == 0 ? 0m : selectedRows.Average(x => x.RealizedDollars))}");
Console.WriteLine($"Authoritative average daily P&L: {F(sessions.Count == 0 ? 0m : selectedRows.Sum(x => x.RealizedDollars) / sessions.Count)}");
Console.WriteLine($"Scalp/Core/Runner: {selectedRows.Count(x => x.FinalMode == "Scalp")}/{selectedRows.Count(x => x.ReachedCore)}/{selectedRows.Count(x => x.ReachedRunner)}");
Console.WriteLine($"Average MFE: {F(selectedRows.Count == 0 ? 0m : selectedRows.Average(x => x.AuthoritativeMfeTicks))}");
Console.WriteLine($"Average MAE: {F(selectedRows.Count == 0 ? 0m : selectedRows.Average(x => x.AuthoritativeMaeTicks))}");
Console.WriteLine($"Average capture ratio: {F(selectedRows.Count == 0 ? 0m : selectedRows.Average(x => x.CaptureRatio))}");
Console.WriteLine($"Median capture ratio: {F(Median(selectedRows.Select(x => x.CaptureRatio)))}");
Console.WriteLine($"Positive MFE but non-positive realized: {selectedRows.Count(x => x.AuthoritativeMfeTicks > 0m && x.RealizedTicks <= 0m)}");
Console.WriteLine($"Reset-age-only rejected candidates: {rejectedRows.Count}");
Console.WriteLine($"Rejected stop-before-favorable-extreme: {rejectedRows.Count(x => x.StopTouchedBeforeSessionFavorableExtreme)}");
Console.WriteLine($"Rejected favorable-before-stop: {rejectedRows.Count(x => x.FavorableBeforeFirstStopTicks > 0m)}");
Console.WriteLine("No shadow observation is realizable P&L and none feeds back into V7.8.7.");

return 0;

static SelectedShadowRow BuildSelectedRow(MorningRiskSizedTrade trade, IReadOnlyList<HistoricalBar> bars, TimeZoneInfo central)
{
    const decimal tickSize = 0.25m;
    var managed = trade.ManagedTrade;
    var source = trade.Candidate.Entry.Source.Source;
    var sessionBars = SessionBars(bars, source.EntryUtc, central);
    var postExit = sessionBars.Where(x => x.TimestampUtc > managed.ExitUtc).OrderBy(x => x.TimestampUtc).ToList();

    var post = ExcursionFromPrice(postExit, source.Direction, managed.ExitPrice, tickSize);
    var whole = ExcursionFromPrice(sessionBars.Where(x => x.TimestampUtc >= source.EntryUtc).ToList(), source.Direction, source.EntryPrice, tickSize);

    decimal adverseBeforeLaterFavorable = 0m;
    bool adverseFirst = false;
    DateTimeOffset? maxFavUtc = post.MaxFavorableUtc;
    if (maxFavUtc.HasValue)
    {
        var beforeFav = postExit.Where(x => x.TimestampUtc <= maxFavUtc.Value).ToList();
        var before = ExcursionFromPrice(beforeFav, source.Direction, managed.ExitPrice, tickSize);
        adverseBeforeLaterFavorable = before.MaxAdverseTicks;
        var firstAdverse = FirstAdverseUtc(beforeFav, source.Direction, managed.ExitPrice);
        adverseFirst = firstAdverse.HasValue && firstAdverse.Value < maxFavUtc.Value;
    }

    return new SelectedShadowRow(
        source.SessionDateCentral.Date,
        TimeZoneInfo.ConvertTime(source.EntryUtc, central).DateTime,
        source.Direction.ToString(),
        source.EntryPrice,
        TimeZoneInfo.ConvertTime(managed.ExitUtc, central).DateTime,
        managed.ExitPrice,
        managed.ExitReason.ToString(),
        trade.RealizedTicks,
        trade.RealizedDollars,
        managed.MaxFavorableTicks,
        managed.MaxAdverseTicks,
        managed.FinalMode.ToString(),
        managed.ExtensionActivated,
        managed.ExtensionActivated || managed.FinalMode >= MorningProtectedPositionMode.Core,
        managed.FinalMode == MorningProtectedPositionMode.Runner,
        managed.MaxFavorableTicks > 0m ? trade.RealizedTicks / managed.MaxFavorableTicks : 0m,
        post.MaxFavorableTicks,
        post.MaxAdverseTicks,
        ToCentral(maxFavUtc, central),
        ToCentral(post.MaxAdverseUtc, central),
        whole.MaxFavorableTicks,
        whole.MaxAdverseTicks,
        adverseFirst,
        adverseBeforeLaterFavorable);
}

static RejectedShadowRow BuildRejectedRow(MorningDailySequencingCandidate candidate, IReadOnlyList<HistoricalBar> bars, TimeZoneInfo central)
{
    const decimal tickSize = 0.25m;
    var source = candidate.Entry.Source.Source;
    var sessionBars = SessionBars(bars, source.EntryUtc, central).Where(x => x.TimestampUtc >= source.EntryUtc).OrderBy(x => x.TimestampUtc).ToList();

    var h15 = Horizon(sessionBars, source.EntryUtc, 15, source.Direction, source.EntryPrice, tickSize);
    var h30 = Horizon(sessionBars, source.EntryUtc, 30, source.Direction, source.EntryPrice, tickSize);
    var h60 = Horizon(sessionBars, source.EntryUtc, 60, source.Direction, source.EntryPrice, tickSize);
    var h120 = Horizon(sessionBars, source.EntryUtc, 120, source.Direction, source.EntryPrice, tickSize);
    var end = ExcursionFromPrice(sessionBars, source.Direction, source.EntryPrice, tickSize);

    var stopTouch = FirstStopTouch(sessionBars, source.Direction, source.StopPrice);
    var beforeStopBars = stopTouch.HasValue
        ? sessionBars.Where(x => x.TimestampUtc <= stopTouch.Value).ToList()
        : sessionBars;
    var beforeStop = ExcursionFromPrice(beforeStopBars, source.Direction, source.EntryPrice, tickSize);
    var stopBeforeFavExtreme = stopTouch.HasValue && end.MaxFavorableUtc.HasValue && stopTouch.Value < end.MaxFavorableUtc.Value;

    return new RejectedShadowRow(
        source.SessionDateCentral.Date,
        TimeZoneInfo.ConvertTime(source.EntryUtc, central).DateTime,
        source.Direction.ToString(),
        source.EntryPrice,
        source.StopPrice,
        source.InitialRiskTicks,
        candidate.EntryEfficiencyScore,
        candidate.PotentialScore,
        candidate.Entry.Features.BarsSinceLastReset,
        h15.MaxFavorableTicks, h15.MaxAdverseTicks,
        h30.MaxFavorableTicks, h30.MaxAdverseTicks,
        h60.MaxFavorableTicks, h60.MaxAdverseTicks,
        h120.MaxFavorableTicks, h120.MaxAdverseTicks,
        end.MaxFavorableTicks, end.MaxAdverseTicks,
        stopTouch.HasValue,
        stopBeforeFavExtreme,
        beforeStop.MaxFavorableTicks);
}

static SessionSummaryRow BuildSessionRow(DateTime date, IReadOnlyList<SelectedShadowRow> selected, IReadOnlyList<RejectedShadowRow> rejected)
{
    var s = selected.Where(x => x.SessionDateCentral == date).ToList();
    var r = rejected.Where(x => x.SessionDateCentral == date).ToList();
    return new SessionSummaryRow(
        date,
        s.Count,
        s.Sum(x => x.RealizedDollars),
        s.Count(x => x.FinalMode == "Scalp"),
        s.Count(x => x.ReachedCore),
        s.Count(x => x.ReachedRunner),
        r.Count,
        r.Count(x => x.StopTouchedBeforeSessionFavorableExtreme),
        r.Count(x => x.FavorableBeforeFirstStopTicks > 0m));
}

static IReadOnlyList<HistoricalBar> SessionBars(IReadOnlyList<HistoricalBar> bars, DateTimeOffset referenceUtc, TimeZoneInfo central)
{
    var date = TimeZoneInfo.ConvertTime(referenceUtc, central).Date;
    return bars.Where(x =>
    {
        var local = TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime;
        return local.Date == date && local.TimeOfDay < new TimeSpan(11, 0, 0);
    }).OrderBy(x => x.TimestampUtc).ToList();
}

static Excursion Horizon(IReadOnlyList<HistoricalBar> sessionBars, DateTimeOffset entryUtc, int minutes, NewYorkResearchDirection direction, decimal entryPrice, decimal tickSize)
{
    var end = entryUtc.AddMinutes(minutes);
    return ExcursionFromPrice(sessionBars.Where(x => x.TimestampUtc >= entryUtc && x.TimestampUtc <= end).ToList(), direction, entryPrice, tickSize);
}

static Excursion ExcursionFromPrice(IReadOnlyList<HistoricalBar> bars, NewYorkResearchDirection direction, decimal referencePrice, decimal tickSize)
{
    if (bars.Count == 0) return new Excursion(0m, 0m, null, null);

    HistoricalBar favBar;
    HistoricalBar advBar;
    decimal favTicks;
    decimal advTicks;
    if (direction == NewYorkResearchDirection.Long)
    {
        favBar = bars.OrderByDescending(x => x.High).First();
        advBar = bars.OrderBy(x => x.Low).First();
        favTicks = Math.Max(0m, (favBar.High - referencePrice) / tickSize);
        advTicks = Math.Max(0m, (referencePrice - advBar.Low) / tickSize);
    }
    else
    {
        favBar = bars.OrderBy(x => x.Low).First();
        advBar = bars.OrderByDescending(x => x.High).First();
        favTicks = Math.Max(0m, (referencePrice - favBar.Low) / tickSize);
        advTicks = Math.Max(0m, (advBar.High - referencePrice) / tickSize);
    }
    return new Excursion(favTicks, advTicks, favBar.TimestampUtc, advBar.TimestampUtc);
}

static DateTimeOffset? FirstStopTouch(IReadOnlyList<HistoricalBar> bars, NewYorkResearchDirection direction, decimal stopPrice)
{
    foreach (var bar in bars)
    {
        if (direction == NewYorkResearchDirection.Long && bar.Low <= stopPrice) return bar.TimestampUtc;
        if (direction == NewYorkResearchDirection.Short && bar.High >= stopPrice) return bar.TimestampUtc;
    }
    return null;
}

static DateTimeOffset? FirstAdverseUtc(IReadOnlyList<HistoricalBar> bars, NewYorkResearchDirection direction, decimal referencePrice)
{
    foreach (var bar in bars)
    {
        if (direction == NewYorkResearchDirection.Long && bar.Low < referencePrice) return bar.TimestampUtc;
        if (direction == NewYorkResearchDirection.Short && bar.High > referencePrice) return bar.TimestampUtc;
    }
    return null;
}

static DateTime? ToCentral(DateTimeOffset? utc, TimeZoneInfo central) => utc.HasValue ? TimeZoneInfo.ConvertTime(utc.Value, central).DateTime : null;
static string F(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
static decimal Median(IEnumerable<decimal> values)
{
    var a = values.OrderBy(x => x).ToList();
    if (a.Count == 0) return 0m;
    return a.Count % 2 == 1 ? a[a.Count / 2] : (a[a.Count / 2 - 1] + a[a.Count / 2]) / 2m;
}

static void WriteSelected(string path, IReadOnlyList<SelectedShadowRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("sessionDateCT\tentryCT\tdirection\tentryPrice\texitCT\texitPrice\texitReason\trealizedTicks\trealizedDollars\tauthoritativeMfeTicks\tauthoritativeMaeTicks\tfinalMode\textensionActivated\treachedCore\treachedRunner\tcaptureRatio\tpostExitMaxFavorableTicks\tpostExitMaxAdverseTicks\tpostExitMaxFavorableCT\tpostExitMaxAdverseCT\twindowEndMfeFromEntryTicks\twindowEndMaeFromEntryTicks\tadverseBeforeLaterFavorable\tadverseRequiredBeforeLaterFavorableTicks");
    foreach (var r in rows)
        w.WriteLine(string.Join("\t", r.ToFields()));
}

static void WriteRejected(string path, IReadOnlyList<RejectedShadowRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("sessionDateCT\tentryCT\tdirection\tentryPrice\tstopPrice\tinitialRiskTicks\tentryScore\tpotentialScore\tbarsSinceLastReset\tmfe15\tmae15\tmfe30\tmae30\tmfe60\tmae60\tmfe120\tmae120\tmfeSessionEnd\tmaeSessionEnd\tstructuralStopTouched\tstopTouchedBeforeSessionFavorableExtreme\tfavorableBeforeFirstStopTicks");
    foreach (var r in rows)
        w.WriteLine(string.Join("\t", r.ToFields()));
}

static void WriteSessions(string path, IReadOnlyList<SessionSummaryRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("sessionDateCT\tselectedTrades\tauthoritativePnL\tscalpCount\tcoreCount\trunnerCount\tresetAgeRejected\tstopBeforeFavExtreme\tfavorableBeforeStop");
    foreach (var r in rows)
        w.WriteLine(string.Join("\t", new[] { r.SessionDateCentral.ToString("yyyy-MM-dd"), r.SelectedTrades.ToString(), F(r.AuthoritativePnl), r.ScalpCount.ToString(), r.CoreCount.ToString(), r.RunnerCount.ToString(), r.ResetAgeRejected.ToString(), r.StopBeforeFavExtreme.ToString(), r.FavorableBeforeStop.ToString() }));
}

static void WriteSummaryJson(string path, string datasetPath, IReadOnlyList<DateTime> sessions, IReadOnlyList<SelectedShadowRow> selected, IReadOnlyList<RejectedShadowRow> rejected)
{
    var payload = new
    {
        schemaVersion = 1,
        datasetPath,
        governance = new { frozen = true, entryMinimum = 70, potentialMinimum = 80, barsSinceLastResetMinimum = 3, management = "V7.3", maximumAttempts = 2 },
        authoritative = new
        {
            sessions = sessions.Count,
            selectedTrades = selected.Count,
            totalPnl = selected.Sum(x => x.RealizedDollars),
            averageTrade = selected.Count == 0 ? 0m : selected.Average(x => x.RealizedDollars),
            averageDailyPnl = sessions.Count == 0 ? 0m : selected.Sum(x => x.RealizedDollars) / sessions.Count,
            scalpCount = selected.Count(x => x.FinalMode == "Scalp"),
            coreCount = selected.Count(x => x.ReachedCore),
            runnerCount = selected.Count(x => x.ReachedRunner),
            averageMfe = selected.Count == 0 ? 0m : selected.Average(x => x.AuthoritativeMfeTicks),
            averageMae = selected.Count == 0 ? 0m : selected.Average(x => x.AuthoritativeMaeTicks),
            averageCaptureRatio = selected.Count == 0 ? 0m : selected.Average(x => x.CaptureRatio),
            medianCaptureRatio = Median(selected.Select(x => x.CaptureRatio))
        },
        shadow = new
        {
            selectedPositiveMfeNonPositiveRealized = selected.Count(x => x.AuthoritativeMfeTicks > 0m && x.RealizedTicks <= 0m),
            resetAgeRejected = rejected.Count,
            rejectedStopBeforeSessionFavorableExtreme = rejected.Count(x => x.StopTouchedBeforeSessionFavorableExtreme),
            rejectedFavorableBeforeFirstStop = rejected.Count(x => x.FavorableBeforeFirstStopTicks > 0m)
        }
    };
    File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteSummaryText(string path, string datasetPath, IReadOnlyList<DateTime> sessions, IReadOnlyList<SelectedShadowRow> selected, IReadOnlyList<RejectedShadowRow> rejected)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("ISE Elite Phase 2 Shadow Opportunity Reconstruction");
    w.WriteLine("Diagnostic only. No shadow value is realizable P&L and no result feeds back into frozen V7.8.7.");
    w.WriteLine($"Dataset: {datasetPath}");
    w.WriteLine($"Sessions: {sessions.Count}");
    w.WriteLine($"Authoritative selected trades: {selected.Count}");
    w.WriteLine($"Authoritative total P&L: {F(selected.Sum(x => x.RealizedDollars))}");
    w.WriteLine($"Authoritative average trade: {F(selected.Count == 0 ? 0m : selected.Average(x => x.RealizedDollars))}");
    w.WriteLine($"Authoritative average daily P&L: {F(sessions.Count == 0 ? 0m : selected.Sum(x => x.RealizedDollars) / sessions.Count)}");
    w.WriteLine($"Scalp/Core/Runner: {selected.Count(x => x.FinalMode == "Scalp")}/{selected.Count(x => x.ReachedCore)}/{selected.Count(x => x.ReachedRunner)}");
    w.WriteLine($"Average MFE: {F(selected.Count == 0 ? 0m : selected.Average(x => x.AuthoritativeMfeTicks))}");
    w.WriteLine($"Average MAE: {F(selected.Count == 0 ? 0m : selected.Average(x => x.AuthoritativeMaeTicks))}");
    w.WriteLine($"Average capture ratio: {F(selected.Count == 0 ? 0m : selected.Average(x => x.CaptureRatio))}");
    w.WriteLine($"Median capture ratio: {F(Median(selected.Select(x => x.CaptureRatio)))}");
    w.WriteLine($"Reset-age-only rejected candidates: {rejected.Count}");
    w.WriteLine($"Rejected stop-before-favorable-extreme: {rejected.Count(x => x.StopTouchedBeforeSessionFavorableExtreme)}");
    w.WriteLine($"Rejected favorable-before-stop: {rejected.Count(x => x.FavorableBeforeFirstStopTicks > 0m)}");
}

static TimeZoneInfo ResolveCentralTimeZone()
{
    try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
}

sealed record Excursion(decimal MaxFavorableTicks, decimal MaxAdverseTicks, DateTimeOffset? MaxFavorableUtc, DateTimeOffset? MaxAdverseUtc);

sealed record SelectedShadowRow(
    DateTime SessionDateCentral, DateTime EntryCentral, string Direction, decimal EntryPrice, DateTime ExitCentral, decimal ExitPrice,
    string ExitReason, decimal RealizedTicks, decimal RealizedDollars, decimal AuthoritativeMfeTicks, decimal AuthoritativeMaeTicks,
    string FinalMode, bool ExtensionActivated, bool ReachedCore, bool ReachedRunner, decimal CaptureRatio,
    decimal PostExitMaxFavorableTicks, decimal PostExitMaxAdverseTicks, DateTime? PostExitMaxFavorableCentral, DateTime? PostExitMaxAdverseCentral,
    decimal WindowEndMfeFromEntryTicks, decimal WindowEndMaeFromEntryTicks, bool AdverseBeforeLaterFavorable, decimal AdverseRequiredBeforeLaterFavorableTicks)
{
    public IEnumerable<string> ToFields() => new[]
    {
        SessionDateCentral.ToString("yyyy-MM-dd"), EntryCentral.ToString("yyyy-MM-dd HH:mm:ss"), Direction, F(EntryPrice), ExitCentral.ToString("yyyy-MM-dd HH:mm:ss"), F(ExitPrice), ExitReason,
        F(RealizedTicks), F(RealizedDollars), F(AuthoritativeMfeTicks), F(AuthoritativeMaeTicks), FinalMode, ExtensionActivated ? "yes" : "no", ReachedCore ? "yes" : "no", ReachedRunner ? "yes" : "no", F(CaptureRatio),
        F(PostExitMaxFavorableTicks), F(PostExitMaxAdverseTicks), PostExitMaxFavorableCentral?.ToString("yyyy-MM-dd HH:mm:ss") ?? "", PostExitMaxAdverseCentral?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
        F(WindowEndMfeFromEntryTicks), F(WindowEndMaeFromEntryTicks), AdverseBeforeLaterFavorable ? "yes" : "no", F(AdverseRequiredBeforeLaterFavorableTicks)
    };
}

sealed record RejectedShadowRow(
    DateTime SessionDateCentral, DateTime EntryCentral, string Direction, decimal EntryPrice, decimal StopPrice, decimal InitialRiskTicks,
    decimal EntryScore, decimal PotentialScore, int BarsSinceLastReset,
    decimal Mfe15, decimal Mae15, decimal Mfe30, decimal Mae30, decimal Mfe60, decimal Mae60, decimal Mfe120, decimal Mae120,
    decimal MfeSessionEnd, decimal MaeSessionEnd, bool StructuralStopTouched, bool StopTouchedBeforeSessionFavorableExtreme, decimal FavorableBeforeFirstStopTicks)
{
    public IEnumerable<string> ToFields() => new[]
    {
        SessionDateCentral.ToString("yyyy-MM-dd"), EntryCentral.ToString("yyyy-MM-dd HH:mm:ss"), Direction, F(EntryPrice), F(StopPrice), F(InitialRiskTicks), F(EntryScore), F(PotentialScore), BarsSinceLastReset.ToString(),
        F(Mfe15), F(Mae15), F(Mfe30), F(Mae30), F(Mfe60), F(Mae60), F(Mfe120), F(Mae120), F(MfeSessionEnd), F(MaeSessionEnd), StructuralStopTouched ? "yes" : "no", StopTouchedBeforeSessionFavorableExtreme ? "yes" : "no", F(FavorableBeforeFirstStopTicks)
    };
}

sealed record SessionSummaryRow(DateTime SessionDateCentral, int SelectedTrades, decimal AuthoritativePnl, int ScalpCount, int CoreCount, int RunnerCount, int ResetAgeRejected, int StopBeforeFavExtreme, int FavorableBeforeStop);
