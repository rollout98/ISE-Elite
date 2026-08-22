using System.Globalization;
using System.Text.Json;
using ISE.HistoricalResearch;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: <continuous-forward-mnq-tsv> <output-directory>");
    return 2;
}

var datasetPath = Path.GetFullPath(args[0]);
var outputDirectory = Path.GetFullPath(args[1]);
if (!File.Exists(datasetPath))
{
    Console.Error.WriteLine($"Dataset not found: {datasetPath}");
    return 3;
}

Directory.CreateDirectory(outputDirectory);
var bars = new HistoricalDataFileStore().ReadContractAware(datasetPath);
if (bars.Count == 0)
    throw new InvalidOperationException("Dataset contains zero bars.");
if (bars.Any(x => string.IsNullOrWhiteSpace(x.Instrument)
    || !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Phase 3 lifecycle decomposition requires MNQ-only data.");

var raw = new MorningMarketStateAdaptiveAnalyzer().Analyze(bars);
var potential = new MorningOpportunityPotentialAnalyzer().Analyze(bars, raw);
var entry = new MorningEntryEfficiencyAnalyzer().Analyze(bars, potential);
var weighted = new MorningStabilityWeightedPotentialAnalyzer().Analyze(potential);
var candidates = new MorningDailyOpportunitySequencer().BuildCandidates(entry, weighted)
    .Where(x => x.SessionDateCentral.Date > new DateTime(2026, 8, 10))
    .Where(x => x.EntryEfficiencyScore >= 70m)
    .Where(x => x.PotentialScore >= 80m)
    .Where(x => x.Entry.Features.BarsSinceLastReset >= 3)
    .OrderBy(x => x.EntryUtc)
    .ToList();

var authoritative = new MorningRiskControlDecompositionAnalyzer(150m, 0.50m)
    .Replay(bars, candidates, MorningRiskControlPolicy.FixedTwo, 2, 70m, 80m)
    .SelectedTrades
    .OrderBy(x => x.Candidate.EntryUtc)
    .ToList();

if (authoritative.Count != 6)
    throw new InvalidOperationException($"Expected 6 frozen selected trades, found {authoritative.Count}.");

var config = new MorningProtectedPositionConfig(enablePreExtensionAdaptiveBreakeven: false);
var observer = new MorningProtectedPositionIntelligenceAnalyzer(config);
var central = ResolveCentralTimeZone();
var orderedBars = bars.OrderBy(x => x.TimestampUtc).ToList();
var barRows = new List<LifecycleBarRow>();
var eventRows = new List<LifecycleEventRow>();
var tradeRows = new List<LifecycleTradeRow>();

for (var tradeIndex = 0; tradeIndex < authoritative.Count; tradeIndex++)
{
    var frozenTrade = authoritative[tradeIndex];
    var managed = frozenTrade.ManagedTrade;
    var candidate = frozenTrade.Candidate;
    var source = candidate.Entry.Source.Source;
    var entryCentral = ToCentral(source.EntryUtc, central);
    var tradeId = $"T{tradeIndex + 1:00}-{source.SessionDateCentral:yyyyMMdd}-{entryCentral:HHmm}";
    var sessionPath = orderedBars
        .Where(x => x.TimestampUtc >= source.EntryUtc && x.TimestampUtc <= managed.ExitUtc)
        .Where(x => ToCentral(x.TimestampUtc, central).Date == entryCentral.Date)
        .OrderBy(x => x.TimestampUtc)
        .ToList();

    if (sessionPath.Count == 0)
        throw new InvalidOperationException($"No lifecycle bars found for {tradeId}.");

    eventRows.Add(new LifecycleEventRow(
        tradeId, entryCentral, "StateEntered", "Scalp", 0m, 0m,
        ProtectionTicks(source.Direction, source.EntryPrice, source.StopPrice, config.TickSize),
        source.StopPrice, "InitialStructuralStop"));

    var previousMode = MorningProtectedPositionMode.Scalp;
    var previousProtectedTicks = ProtectionTicks(source.Direction, source.EntryPrice, source.StopPrice, config.TickSize);
    var previousStopReason = MorningProtectedPositionExitReason.StructuralStop;
    MorningProtectedManagedTrade? finalObserved = null;

    foreach (var bar in sessionPath)
    {
        var prefix = orderedBars.Where(x => x.TimestampUtc <= bar.TimestampUtc).ToList();
        var snapshot = observer.Manage(prefix, candidate)
            ?? throw new InvalidOperationException($"Observer returned null for {tradeId} at {bar.TimestampUtc:O}.");

        var actualExit = snapshot.ExitReason != MorningProtectedPositionExitReason.ResearchWindowEnd;
        var activeProtectedTicks = snapshot.ExtensionActivated
            ? snapshot.BestProtectedTicks
            : ProtectionTicks(source.Direction, source.EntryPrice, source.StopPrice, config.TickSize);
        var activeStopReason = snapshot.ExtensionActivated
            ? InferProtectionReason(snapshot.FinalMode)
            : MorningProtectedPositionExitReason.StructuralStop;
        var activeStopPrice = PriceAtTicks(source.Direction, source.EntryPrice, activeProtectedTicks, config.TickSize);

        if (snapshot.FinalMode != previousMode)
        {
            var detail = snapshot.FinalMode == MorningProtectedPositionMode.Core
                ? "Scalp target >=150 ticks touched while latest completed 5-minute VectorFlow was directionally aligned"
                : "Core active with MFE >=300 ticks and at least 2 consecutive aligned completed 5-minute states";
            eventRows.Add(new LifecycleEventRow(
                tradeId, ToCentral(bar.TimestampUtc, central), "StateEntered", snapshot.FinalMode.ToString(),
                snapshot.MaxFavorableTicks, snapshot.MaxAdverseTicks, activeProtectedTicks, activeStopPrice, detail));
            previousMode = snapshot.FinalMode;
        }

        if (activeProtectedTicks != previousProtectedTicks || activeStopReason != previousStopReason)
        {
            eventRows.Add(new LifecycleEventRow(
                tradeId, ToCentral(bar.TimestampUtc, central), "ProtectionFloorChanged", snapshot.FinalMode.ToString(),
                snapshot.MaxFavorableTicks, snapshot.MaxAdverseTicks, activeProtectedTicks, activeStopPrice,
                activeStopReason + ProtectionFormula(snapshot.FinalMode, snapshot.MaxFavorableTicks, activeProtectedTicks)));
            previousProtectedTicks = activeProtectedTicks;
            previousStopReason = activeStopReason;
        }

        barRows.Add(new LifecycleBarRow(
            tradeId,
            ToCentral(bar.TimestampUtc, central),
            bar.Open, bar.High, bar.Low, bar.Close,
            snapshot.FinalMode.ToString(), snapshot.ExtensionActivated,
            snapshot.MaxFavorableTicks, snapshot.MaxAdverseTicks,
            activeProtectedTicks, activeStopPrice, activeStopReason.ToString(),
            snapshot.MaximumAlignedFiveMinuteBars,
            actualExit ? snapshot.ExitReason.ToString() : string.Empty,
            actualExit ? snapshot.ExitPrice : (decimal?)null));

        if (actualExit)
        {
            finalObserved = snapshot;
            eventRows.Add(new LifecycleEventRow(
                tradeId, ToCentral(snapshot.ExitUtc, central), "Exit", snapshot.FinalMode.ToString(),
                snapshot.MaxFavorableTicks, snapshot.MaxAdverseTicks, activeProtectedTicks, snapshot.ExitPrice,
                ExitDetail(snapshot.ExitReason)));
            break;
        }
    }

    finalObserved ??= observer.Manage(orderedBars, candidate);
    if (finalObserved == null)
        throw new InvalidOperationException($"Final observer result is null for {tradeId}.");
    AssertMatchesAuthoritative(tradeId, managed, finalObserved);

    var coreGate = CoreGate(managed, config);
    var runnerGate = RunnerGate(managed, config);
    var giveback = managed.MaxFavorableTicks - frozenTrade.RealizedTicks;
    tradeRows.Add(new LifecycleTradeRow(
        tradeId, source.SessionDateCentral.Date, entryCentral, source.Direction.ToString(), source.EntryPrice,
        ToCentral(managed.ExitUtc, central), managed.ExitPrice, managed.FinalMode.ToString(), managed.ExitReason.ToString(),
        managed.ExtensionActivated, managed.FinalMode == MorningProtectedPositionMode.Runner,
        managed.MaxFavorableTicks, managed.MaxAdverseTicks, frozenTrade.RealizedTicks, frozenTrade.RealizedDollars,
        giveback, managed.MaxFavorableTicks > 0m ? frozenTrade.RealizedTicks / managed.MaxFavorableTicks : 0m,
        managed.BestProtectedTicks, managed.MaximumAlignedFiveMinuteBars,
        CoreActivationReason(managed), coreGate, RunnerActivationReason(managed), runnerGate,
        managed.FinalMode == MorningProtectedPositionMode.Scalp && managed.MaxFavorableTicks >= config.RunnerThresholdTicks));
}

WriteBarRows(Path.Combine(outputDirectory, "lifecycle-bar-by-bar.tsv"), barRows);
WriteEventRows(Path.Combine(outputDirectory, "lifecycle-events.tsv"), eventRows);
WriteTradeRows(Path.Combine(outputDirectory, "trade-lifecycle-summary.tsv"), tradeRows);
WriteJson(Path.Combine(outputDirectory, "summary.json"), datasetPath, tradeRows, eventRows);
WriteText(Path.Combine(outputDirectory, "summary.txt"), datasetPath, tradeRows);

Console.WriteLine("ISE Elite Phase 3 Protected Trade Lifecycle Decomposition");
Console.WriteLine("Diagnostic only. Frozen V7.8.7 and V7.3 management are unchanged.");
Console.WriteLine($"Dataset: {datasetPath}");
Console.WriteLine($"Output: {outputDirectory}");
Console.WriteLine($"Trades: {tradeRows.Count}");
Console.WriteLine($"Scalp/Core/Runner: {tradeRows.Count(x => x.FinalMode == "Scalp")}/{tradeRows.Count(x => x.FinalMode == "Core")}/{tradeRows.Count(x => x.FinalMode == "Runner")}");
Console.WriteLine($"Average peak-to-exit giveback: {Format.F(tradeRows.Average(x => x.PeakToExitGivebackTicks))} ticks");
foreach (var group in tradeRows.GroupBy(x => x.FinalMode).OrderBy(x => x.Key))
    Console.WriteLine($"Average capture ratio {group.Key}: {Format.F(group.Average(x => x.CaptureRatio))}");
Console.WriteLine($"MFE >=300 but never advanced beyond Scalp: {tradeRows.Count(x => x.MateriallyLargerMfeWithoutAdvance)}");
Console.WriteLine("All reconstructed outcomes matched the authoritative replay exactly.");

return 0;

static decimal ProtectionTicks(NewYorkResearchDirection direction, decimal entry, decimal stop, decimal tickSize) =>
    direction == NewYorkResearchDirection.Long ? (stop - entry) / tickSize : (entry - stop) / tickSize;

static decimal PriceAtTicks(NewYorkResearchDirection direction, decimal entry, decimal ticks, decimal tickSize) =>
    direction == NewYorkResearchDirection.Long ? entry + ticks * tickSize : entry - ticks * tickSize;

static MorningProtectedPositionExitReason InferProtectionReason(MorningProtectedPositionMode mode) =>
    mode == MorningProtectedPositionMode.Runner
        ? MorningProtectedPositionExitReason.RunnerTrail
        : MorningProtectedPositionExitReason.ExtensionFloor;

static string ProtectionFormula(MorningProtectedPositionMode mode, decimal mfe, decimal protectedTicks) =>
    mode == MorningProtectedPositionMode.Runner
        ? $"; max(core floor, MFE-250) at MFE={Format.F(mfe)} => {Format.F(protectedTicks)} ticks"
        : $"; max(100, floor(40% of MFE)) at MFE={Format.F(mfe)} => {Format.F(protectedTicks)} ticks";

static string CoreActivationReason(MorningProtectedManagedTrade trade) => trade.ExtensionActivated
    ? "Activated when the +150-tick scalp target was touched with completed five-minute VectorFlow aligned"
    : "Not activated";

static string CoreGate(MorningProtectedManagedTrade trade, MorningProtectedPositionConfig config)
{
    if (trade.ExtensionActivated) return "Passed: target touch and completed five-minute alignment coincided";
    return trade.ExitReason switch
    {
        MorningProtectedPositionExitReason.ScalpCapture => "Failed: +150 target touched, but latest completed five-minute VectorFlow was not aligned",
        MorningProtectedPositionExitReason.ScalpTimeout => $"Failed: +{config.ScalpTargetTicks} target was not touched before the {config.ScalpTimeoutMinutes}-minute deadline",
        MorningProtectedPositionExitReason.StructuralStop => $"Failed: structural stop touched before an aligned +{config.ScalpTargetTicks} target touch",
        _ => "Failed: trade exited before an aligned scalp-target touch"
    };
}

static string RunnerActivationReason(MorningProtectedManagedTrade trade) =>
    trade.FinalMode == MorningProtectedPositionMode.Runner
        ? "Activated with Core active, MFE >=300, and at least 2 consecutive aligned completed five-minute states"
        : "Not activated";

static string RunnerGate(MorningProtectedManagedTrade trade, MorningProtectedPositionConfig config)
{
    if (!trade.ExtensionActivated) return "Not evaluated: Core was never activated";
    if (trade.FinalMode == MorningProtectedPositionMode.Runner) return "Passed";
    if (trade.MaxFavorableTicks < config.RunnerThresholdTicks)
        return $"Failed: peak MFE {Format.F(trade.MaxFavorableTicks)} < {config.RunnerThresholdTicks} ticks";
    if (trade.MaximumAlignedFiveMinuteBars < config.RunnerAlignedBars)
        return $"Failed: maximum aligned completed five-minute sequence {trade.MaximumAlignedFiveMinuteBars} < {config.RunnerAlignedBars}";
    return "Failed: MFE and required consecutive alignment did not coexist while Core remained open";
}

static string ExitDetail(MorningProtectedPositionExitReason reason) => reason switch
{
    MorningProtectedPositionExitReason.StructuralStop => "Structural stop touched; stop-first same-bar precedence",
    MorningProtectedPositionExitReason.ScalpCapture => "+150 scalp target touched without completed five-minute alignment",
    MorningProtectedPositionExitReason.ScalpTimeout => "30-minute scalp deadline reached without target extension",
    MorningProtectedPositionExitReason.ExtensionFloor => "Active Core protection floor touched",
    MorningProtectedPositionExitReason.RunnerTrail => "Active Runner trailing floor touched",
    MorningProtectedPositionExitReason.VectorFlowBiasLoss => "New completed five-minute state lost directional alignment during extension",
    MorningProtectedPositionExitReason.ResearchWindowEnd => "11:00 CT research window ended",
    MorningProtectedPositionExitReason.AdaptiveBreakeven => "Adaptive breakeven touched",
    _ => reason.ToString()
};

static void AssertMatchesAuthoritative(string tradeId, MorningProtectedManagedTrade expected, MorningProtectedManagedTrade actual)
{
    if (expected.FinalMode != actual.FinalMode
        || expected.ExitReason != actual.ExitReason
        || expected.ExitUtc != actual.ExitUtc
        || expected.ExitPrice != actual.ExitPrice
        || expected.RealizedTicks != actual.RealizedTicks
        || expected.MaxFavorableTicks != actual.MaxFavorableTicks
        || expected.MaxAdverseTicks != actual.MaxAdverseTicks
        || expected.ExtensionActivated != actual.ExtensionActivated
        || expected.BestProtectedTicks != actual.BestProtectedTicks
        || expected.MaximumAlignedFiveMinuteBars != actual.MaximumAlignedFiveMinuteBars)
        throw new InvalidOperationException($"Lifecycle reconstruction diverged from authoritative result for {tradeId}.");
}

static void WriteBarRows(string path, IEnumerable<LifecycleBarRow> rows)
{
    using var writer = new StreamWriter(path, false);
    writer.WriteLine("tradeId\tbarCentral\topen\thigh\tlow\tclose\tmode\textensionActivated\tmfeTicks\tmaeTicks\tactiveProtectionTicks\tactiveProtectionPrice\tactiveProtectionReason\tmaximumAlignedFiveMinuteBars\texitReason\texitPrice");
    foreach (var x in rows)
        writer.WriteLine(string.Join("\t", new[] { x.TradeId, Format.Dt(x.BarCentral), Format.F(x.Open), Format.F(x.High), Format.F(x.Low), Format.F(x.Close), x.Mode, Format.Yn(x.ExtensionActivated), Format.F(x.MfeTicks), Format.F(x.MaeTicks), Format.F(x.ActiveProtectionTicks), Format.F(x.ActiveProtectionPrice), x.ActiveProtectionReason, x.MaximumAlignedFiveMinuteBars.ToString(CultureInfo.InvariantCulture), x.ExitReason, x.ExitPrice.HasValue ? Format.F(x.ExitPrice.Value) : string.Empty }));
}

static void WriteEventRows(string path, IEnumerable<LifecycleEventRow> rows)
{
    using var writer = new StreamWriter(path, false);
    writer.WriteLine("tradeId\teventCentral\teventType\tmode\tmfeTicks\tmaeTicks\tprotectionTicks\tprice\tdetail");
    foreach (var x in rows)
        writer.WriteLine(string.Join("\t", new[] { x.TradeId, Format.Dt(x.EventCentral), x.EventType, x.Mode, Format.F(x.MfeTicks), Format.F(x.MaeTicks), Format.F(x.ProtectionTicks), Format.F(x.Price), x.Detail }));
}

static void WriteTradeRows(string path, IEnumerable<LifecycleTradeRow> rows)
{
    using var writer = new StreamWriter(path, false);
    writer.WriteLine("tradeId\tsessionDateCentral\tentryCentral\tdirection\tentryPrice\texitCentral\texitPrice\tfinalMode\texitReason\textensionActivated\treachedRunner\tpeakMfeTicks\tmaeTicks\trealizedTicks\trealizedDollars\tpeakToExitGivebackTicks\tcaptureRatio\tbestProtectedTicks\tmaximumAlignedFiveMinuteBars\tcoreActivation\tcoreGate\trunnerActivation\trunnerGate\tmateriallyLargerMfeWithoutAdvance");
    foreach (var x in rows)
        writer.WriteLine(string.Join("\t", new[] { x.TradeId, x.SessionDateCentral.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Format.Dt(x.EntryCentral), x.Direction, Format.F(x.EntryPrice), Format.Dt(x.ExitCentral), Format.F(x.ExitPrice), x.FinalMode, x.ExitReason, Format.Yn(x.ExtensionActivated), Format.Yn(x.ReachedRunner), Format.F(x.PeakMfeTicks), Format.F(x.MaeTicks), Format.F(x.RealizedTicks), Format.F(x.RealizedDollars), Format.F(x.PeakToExitGivebackTicks), Format.F(x.CaptureRatio), Format.F(x.BestProtectedTicks), x.MaximumAlignedFiveMinuteBars.ToString(CultureInfo.InvariantCulture), x.CoreActivation, x.CoreGate, x.RunnerActivation, x.RunnerGate, Format.Yn(x.MateriallyLargerMfeWithoutAdvance) }));
}

static void WriteJson(string path, string datasetPath, IReadOnlyList<LifecycleTradeRow> trades, IReadOnlyList<LifecycleEventRow> events)
{
    var byMode = trades.GroupBy(x => x.FinalMode).ToDictionary(x => x.Key, x => new { count = x.Count(), averageCaptureRatio = x.Average(y => y.CaptureRatio) });
    var payload = new
    {
        schemaVersion = 1,
        datasetPath,
        governance = new { frozen = true, version = "V7.8.7", management = "V7.3", diagnosticOnly = true },
        counts = new { trades = trades.Count, scalp = trades.Count(x => x.FinalMode == "Scalp"), core = trades.Count(x => x.FinalMode == "Core"), runner = trades.Count(x => x.FinalMode == "Runner") },
        averagePeakToExitGivebackTicks = trades.Average(x => x.PeakToExitGivebackTicks),
        captureRatioByFinalMode = byMode,
        materiallyLargerMfeWithoutStateAdvance = trades.Count(x => x.MateriallyLargerMfeWithoutAdvance),
        trades,
        events
    };
    File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteText(string path, string datasetPath, IReadOnlyList<LifecycleTradeRow> trades)
{
    using var writer = new StreamWriter(path, false);
    writer.WriteLine("ISE Elite Phase 3 Protected Trade Lifecycle Decomposition");
    writer.WriteLine("Diagnostic only. Frozen V7.8.7 and V7.3 management are unchanged.");
    writer.WriteLine($"Dataset: {datasetPath}");
    writer.WriteLine($"Trades: {trades.Count}");
    writer.WriteLine($"Scalp/Core/Runner: {trades.Count(x => x.FinalMode == "Scalp")}/{trades.Count(x => x.FinalMode == "Core")}/{trades.Count(x => x.FinalMode == "Runner")}");
    writer.WriteLine($"Average peak-to-exit giveback: {Format.F(trades.Average(x => x.PeakToExitGivebackTicks))} ticks");
    foreach (var group in trades.GroupBy(x => x.FinalMode).OrderBy(x => x.Key))
        writer.WriteLine($"Average capture ratio {group.Key}: {Format.F(group.Average(x => x.CaptureRatio))}");
    writer.WriteLine($"MFE >=300 but never advanced beyond Scalp: {trades.Count(x => x.MateriallyLargerMfeWithoutAdvance)}");
    foreach (var x in trades)
        writer.WriteLine($"{x.TradeId}: Core={x.CoreGate}; Runner={x.RunnerGate}; Exit={x.ExitReason}; Giveback={Format.F(x.PeakToExitGivebackTicks)} ticks");
}

static DateTime ToCentral(DateTimeOffset utc, TimeZoneInfo central) => TimeZoneInfo.ConvertTime(utc, central).DateTime;

static TimeZoneInfo ResolveCentralTimeZone()
{
    try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
}

internal static class Format
{
    public static string F(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    public static string Dt(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    public static string Yn(bool value) => value ? "yes" : "no";
}

internal sealed record LifecycleBarRow(string TradeId, DateTime BarCentral, decimal Open, decimal High, decimal Low, decimal Close, string Mode, bool ExtensionActivated, decimal MfeTicks, decimal MaeTicks, decimal ActiveProtectionTicks, decimal ActiveProtectionPrice, string ActiveProtectionReason, int MaximumAlignedFiveMinuteBars, string ExitReason, decimal? ExitPrice);
internal sealed record LifecycleEventRow(string TradeId, DateTime EventCentral, string EventType, string Mode, decimal MfeTicks, decimal MaeTicks, decimal ProtectionTicks, decimal Price, string Detail);
internal sealed record LifecycleTradeRow(string TradeId, DateTime SessionDateCentral, DateTime EntryCentral, string Direction, decimal EntryPrice, DateTime ExitCentral, decimal ExitPrice, string FinalMode, string ExitReason, bool ExtensionActivated, bool ReachedRunner, decimal PeakMfeTicks, decimal MaeTicks, decimal RealizedTicks, decimal RealizedDollars, decimal PeakToExitGivebackTicks, decimal CaptureRatio, decimal BestProtectedTicks, int MaximumAlignedFiveMinuteBars, string CoreActivation, string CoreGate, string RunnerActivation, string RunnerGate, bool MateriallyLargerMfeWithoutAdvance);
