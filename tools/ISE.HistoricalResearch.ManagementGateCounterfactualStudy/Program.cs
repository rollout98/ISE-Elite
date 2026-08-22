using System.Collections;
using System.Globalization;
using System.Reflection;
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
var bars = new HistoricalDataFileStore().ReadContractAware(datasetPath).OrderBy(x => x.TimestampUtc).ToList();
if (bars.Count == 0) throw new InvalidOperationException("Dataset contains zero bars.");
if (bars.Any(x => string.IsNullOrWhiteSpace(x.Instrument) || !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("Phase 4 requires MNQ-only data.");

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

var frozenTrades = new MorningRiskControlDecompositionAnalyzer(150m, 0.50m)
    .Replay(bars, candidates, MorningRiskControlPolicy.FixedTwo, 2, 70m, 80m)
    .SelectedTrades.OrderBy(x => x.Candidate.EntryUtc).ToList();
if (frozenTrades.Count != 6)
    throw new InvalidOperationException($"Expected 6 authoritative trades, found {frozenTrades.Count}.");

var config = new MorningProtectedPositionConfig(enablePreExtensionAdaptiveBreakeven: false);
var central = ResolveCentralTimeZone();
var vectors = FrozenVectorReader.Read(bars, config, central);
var pathRows = new List<ShadowPathRow>();
var bandRows = new List<PeakBandRow>();
var tradeRows = new List<TradeComparisonRow>();

for (var index = 0; index < frozenTrades.Count; index++)
{
    var frozen = frozenTrades[index];
    var source = frozen.Candidate.Entry.Source.Source;
    var entryCentral = ToCentral(source.EntryUtc, central);
    var tradeId = $"T{index + 1:00}-{source.SessionDateCentral:yyyyMMdd}-{entryCentral:HHmm}";
    var sessionPath = bars.Where(x => x.TimestampUtc >= source.EntryUtc)
        .TakeWhile(x =>
        {
            var local = ToCentral(x.TimestampUtc, central);
            return local.Date == entryCentral.Date && local.TimeOfDay < new TimeSpan(11, 0, 0);
        }).ToList();

    var authoritativeShadow = ShadowManager.Run(frozen.Candidate, sessionPath, vectors, config, requireAlignmentForCore: true);
    AssertAuthoritative(tradeId, frozen.ManagedTrade, authoritativeShadow);
    var unlocked = ShadowManager.Run(frozen.Candidate, sessionPath, vectors, config, requireAlignmentForCore: false);

    pathRows.Add(ToPathRow(tradeId, "AuthoritativeV7.8.7", frozen.ManagedTrade.FinalMode.ToString(), frozen.ManagedTrade.ExitReason.ToString(),
        frozen.ManagedTrade.ExtensionActivated, frozen.ManagedTrade.FinalMode == MorningProtectedPositionMode.Runner,
        frozen.ManagedTrade.MaxFavorableTicks, frozen.ManagedTrade.MaxAdverseTicks, frozen.RealizedTicks,
        frozen.ManagedTrade.MaxFavorableTicks - frozen.RealizedTicks,
        frozen.ManagedTrade.ExitReason == MorningProtectedPositionExitReason.StructuralStop,
        false, "Frozen authoritative result"));

    pathRows.Add(ToPathRow(tradeId, "ShadowCoreUnlocked", unlocked.FinalMode.ToString(), unlocked.ExitReason.ToString(),
        unlocked.CoreEntered, unlocked.RunnerEntered, unlocked.MfeTicks, unlocked.MaeTicks, unlocked.RealizedTicks,
        unlocked.MfeTicks - unlocked.RealizedTicks, unlocked.StructuralStopBeforeProtection,
        false, "Core permitted at +150 target touch without requiring completed five-minute alignment; all other gates unchanged"));

    var reached300 = frozen.ManagedTrade.MaxFavorableTicks >= config.RunnerThresholdTicks;
    pathRows.Add(ToPathRow(tradeId, "ShadowRunnerEligibilityObservation", unlocked.FinalMode.ToString(), unlocked.ExitReason.ToString(),
        unlocked.CoreEntered, unlocked.RunnerEntered, unlocked.MfeTicks, unlocked.MaeTicks, unlocked.RealizedTicks,
        unlocked.MfeTicks - unlocked.RealizedTicks, unlocked.StructuralStopBeforeProtection,
        false, reached300
            ? (unlocked.RunnerEntered ? "Reached Runner after Core eligibility was unlocked" : RunnerFailure(unlocked, config))
            : "Authoritative path did not reach +300 MFE; Runner eligibility observation not triggered"));

    foreach (var fraction in new[] { 0.25m, 0.40m, 0.50m, 0.60m })
    {
        var peak = frozen.ManagedTrade.MaxFavorableTicks;
        var retained = Math.Floor(peak * fraction);
        var ordering = DescribeBandOrdering(sessionPath, source.Direction, source.EntryPrice, source.StopPrice, peak, config.TickSize);
        bandRows.Add(new PeakBandRow(tradeId, $"PeakProtection{fraction * 100m:0}", fraction, peak,
            frozen.ManagedTrade.MaxAdverseTicks, retained, peak - retained,
            ordering.StructuralStopBeforePeak, true,
            "Descriptive full-peak retention band; depends on knowing the achieved peak and is not an executable rule"));
    }

    tradeRows.Add(new TradeComparisonRow(tradeId, entryCentral, source.Direction.ToString(),
        frozen.ManagedTrade.FinalMode.ToString(), frozen.ManagedTrade.ExitReason.ToString(), frozen.RealizedTicks,
        frozen.ManagedTrade.MaxFavorableTicks, frozen.ManagedTrade.MaxAdverseTicks,
        frozen.ManagedTrade.MaxFavorableTicks - frozen.RealizedTicks,
        unlocked.CoreEntered, unlocked.RunnerEntered, unlocked.FinalMode.ToString(), unlocked.ExitReason.ToString(),
        unlocked.RealizedTicks, unlocked.MfeTicks, unlocked.MaeTicks, unlocked.MfeTicks - unlocked.RealizedTicks,
        unlocked.RealizedTicks - frozen.RealizedTicks,
        reached300, reached300 && unlocked.RunnerEntered,
        CoreGateAttribution(frozen.ManagedTrade), RunnerGateAttribution(frozen.ManagedTrade, unlocked, config)));
}

WritePaths(Path.Combine(outputDirectory, "shadow-management-paths.tsv"), pathRows);
WriteBands(Path.Combine(outputDirectory, "peak-protection-observations.tsv"), bandRows);
WriteTrades(Path.Combine(outputDirectory, "trade-by-trade-summary.tsv"), tradeRows);
WriteJson(Path.Combine(outputDirectory, "summary.json"), datasetPath, tradeRows, bandRows);
WriteText(Path.Combine(outputDirectory, "summary.txt"), datasetPath, tradeRows, bandRows);

Console.WriteLine("ISE Elite Phase 4 Management Gate Counterfactual Study");
Console.WriteLine("Diagnostic only. Frozen V7.8.7 remains authoritative and unchanged.");
Console.WriteLine($"Dataset: {datasetPath}");
Console.WriteLine($"Output: {outputDirectory}");
Console.WriteLine($"Trades: {tradeRows.Count}");
Console.WriteLine($"Authoritative realized ticks: {F(tradeRows.Sum(x => x.AuthoritativeRealizedTicks))}");
Console.WriteLine($"Core-unlocked shadow realized ticks: {F(tradeRows.Sum(x => x.UnlockedRealizedTicks))}");
Console.WriteLine($"Core-unlocked diagnostic delta: {F(tradeRows.Sum(x => x.UnlockedDeltaTicks))}");
Console.WriteLine($"Authoritative +300-MFE trades: {tradeRows.Count(x => x.AuthoritativeReached300)}");
Console.WriteLine($"Runner reachable after Core unlock: {tradeRows.Count(x => x.RunnerReachableAfterUnlock)}");
Console.WriteLine("Authoritative shadow reproduction matched all six frozen outcomes exactly.");
return 0;

static ShadowPathRow ToPathRow(string tradeId, string path, string finalMode, string exitReason, bool core, bool runner,
    decimal mfe, decimal mae, decimal retained, decimal giveback, bool structuralBefore, bool hindsight, string note) =>
    new(tradeId, path, core, runner, finalMode, exitReason, mfe, mae, retained, giveback, structuralBefore, hindsight, note);

static string RunnerFailure(ShadowOutcome x, MorningProtectedPositionConfig config)
{
    if (!x.CoreEntered) return "Core still was not entered on the unlocked path";
    if (x.MfeTicks < config.RunnerThresholdTicks) return $"Unlocked path exited before reaching +{config.RunnerThresholdTicks} MFE";
    if (x.MaximumAlignedBars < config.RunnerAlignedBars) return $"Reached +{config.RunnerThresholdTicks}, but consecutive aligned completed five-minute states remained below {config.RunnerAlignedBars}";
    return "Runner conditions did not coexist before the unlocked path exited";
}

static string CoreGateAttribution(MorningProtectedManagedTrade x)
{
    if (x.ExtensionActivated) return "Core alignment gate passed";
    return x.ExitReason switch
    {
        MorningProtectedPositionExitReason.ScalpCapture => "Core alignment gate blocked extension at the +150 target touch",
        MorningProtectedPositionExitReason.ScalpTimeout => "Core target gate blocked extension: +150 was not reached before timeout",
        MorningProtectedPositionExitReason.StructuralStop => "Structural stop occurred before Core target/alignment eligibility",
        _ => "Exited before Core eligibility"
    };
}

static string RunnerGateAttribution(MorningProtectedManagedTrade authoritative, ShadowOutcome unlocked, MorningProtectedPositionConfig config)
{
    if (authoritative.FinalMode == MorningProtectedPositionMode.Runner) return "Runner gates passed";
    if (authoritative.ExtensionActivated && authoritative.MaxFavorableTicks < config.RunnerThresholdTicks)
        return $"Runner MFE gate: authoritative peak {F(authoritative.MaxFavorableTicks)} < {config.RunnerThresholdTicks}";
    if (!authoritative.ExtensionActivated && authoritative.MaxFavorableTicks >= config.RunnerThresholdTicks)
        return unlocked.RunnerEntered
            ? "Core alignment was the upstream blocker; Runner became reachable after unlock"
            : "Core alignment was upstream; unlocked path still did not satisfy all Runner gates before exit";
    return "Runner was downstream of an unearned Core state";
}

static BandOrdering DescribeBandOrdering(IReadOnlyList<HistoricalBar> path, NewYorkResearchDirection direction,
    decimal entry, decimal stop, decimal peakTicks, decimal tickSize)
{
    DateTimeOffset? peakUtc = null;
    DateTimeOffset? stopUtc = null;
    decimal mfe = 0m;
    foreach (var bar in path)
    {
        if (!stopUtc.HasValue && (direction == NewYorkResearchDirection.Long ? bar.Low <= stop : bar.High >= stop)) stopUtc = bar.TimestampUtc;
        var favorable = direction == NewYorkResearchDirection.Long ? (bar.High - entry) / tickSize : (entry - bar.Low) / tickSize;
        mfe = Math.Max(mfe, Math.Max(0m, favorable));
        if (!peakUtc.HasValue && mfe >= peakTicks) peakUtc = bar.TimestampUtc;
        if (peakUtc.HasValue && stopUtc.HasValue) break;
    }
    return new BandOrdering(stopUtc.HasValue && (!peakUtc.HasValue || stopUtc.Value <= peakUtc.Value));
}

static void AssertAuthoritative(string id, MorningProtectedManagedTrade expected, ShadowOutcome actual)
{
    if (expected.FinalMode != actual.FinalMode || expected.ExitReason != actual.ExitReason || expected.ExitUtc != actual.ExitUtc
        || expected.ExitPrice != actual.ExitPrice || expected.RealizedTicks != actual.RealizedTicks
        || expected.MaxFavorableTicks != actual.MfeTicks || expected.MaxAdverseTicks != actual.MaeTicks
        || expected.ExtensionActivated != actual.CoreEntered || expected.BestProtectedTicks != actual.BestProtectedTicks
        || expected.MaximumAlignedFiveMinuteBars != actual.MaximumAlignedBars)
        throw new InvalidOperationException($"Authoritative shadow diverged for {id}.");
}

static void WritePaths(string path, IEnumerable<ShadowPathRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("tradeId\tpath\tcoreEntered\trunnerReachable\tfinalMode\texitReason\tpeakMfeTicks\tmaeTicks\tretainedTicksAtExit\tpeakToExitGivebackTicks\tstructuralStopBeforeProtectionValid\thindsightDependent\tnote");
    foreach (var x in rows) w.WriteLine(string.Join("\t", new[] { x.TradeId, x.Path, Yn(x.CoreEntered), Yn(x.RunnerReachable), x.FinalMode, x.ExitReason, F(x.PeakMfeTicks), F(x.MaeTicks), F(x.RetainedTicksAtExit), F(x.PeakToExitGivebackTicks), Yn(x.StructuralStopBeforeProtectionValid), Yn(x.HindsightDependent), x.Note }));
}

static void WriteBands(string path, IEnumerable<PeakBandRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("tradeId\tobservation\tpeakFraction\tpeakMfeTicks\tmaeTicks\ttheoreticalRetainedTicks\tpeakToRetainedGivebackTicks\tstructuralStopBeforePeakKnown\thindsightDependent\tnote");
    foreach (var x in rows) w.WriteLine(string.Join("\t", new[] { x.TradeId, x.Observation, F(x.PeakFraction), F(x.PeakMfeTicks), F(x.MaeTicks), F(x.TheoreticalRetainedTicks), F(x.PeakToRetainedGivebackTicks), Yn(x.StructuralStopBeforePeakKnown), Yn(x.HindsightDependent), x.Note }));
}

static void WriteTrades(string path, IEnumerable<TradeComparisonRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("tradeId\tentryCentral\tdirection\tauthoritativeMode\tauthoritativeExit\tauthoritativeRealizedTicks\tauthoritativeMfeTicks\tauthoritativeMaeTicks\tauthoritativeGivebackTicks\tunlockedCoreEntered\tunlockedRunnerEntered\tunlockedFinalMode\tunlockedExit\tunlockedRealizedTicks\tunlockedMfeTicks\tunlockedMaeTicks\tunlockedGivebackTicks\tunlockedDeltaTicks\tauthoritativeReached300\trunnerReachableAfterUnlock\tcoreGateAttribution\trunnerGateAttribution");
    foreach (var x in rows) w.WriteLine(string.Join("\t", new[] { x.TradeId, Dt(x.EntryCentral), x.Direction, x.AuthoritativeMode, x.AuthoritativeExit, F(x.AuthoritativeRealizedTicks), F(x.AuthoritativeMfeTicks), F(x.AuthoritativeMaeTicks), F(x.AuthoritativeGivebackTicks), Yn(x.UnlockedCoreEntered), Yn(x.UnlockedRunnerEntered), x.UnlockedFinalMode, x.UnlockedExit, F(x.UnlockedRealizedTicks), F(x.UnlockedMfeTicks), F(x.UnlockedMaeTicks), F(x.UnlockedGivebackTicks), F(x.UnlockedDeltaTicks), Yn(x.AuthoritativeReached300), Yn(x.RunnerReachableAfterUnlock), x.CoreGateAttribution, x.RunnerGateAttribution }));
}

static void WriteJson(string path, string dataset, IReadOnlyList<TradeComparisonRow> trades, IReadOnlyList<PeakBandRow> bands)
{
    var payload = new
    {
        schemaVersion = 1, datasetPath = dataset,
        governance = new { frozen = true, version = "V7.8.7", diagnosticOnly = true, entriesUnchanged = true, sizingUnchanged = true, initialStopsUnchanged = true },
        authoritative = new { trades = trades.Count, realizedTicks = trades.Sum(x => x.AuthoritativeRealizedTicks), averageGivebackTicks = trades.Average(x => x.AuthoritativeGivebackTicks) },
        coreUnlocked = new { realizedTicks = trades.Sum(x => x.UnlockedRealizedTicks), diagnosticDeltaTicks = trades.Sum(x => x.UnlockedDeltaTicks), coreEntered = trades.Count(x => x.UnlockedCoreEntered), runnerEntered = trades.Count(x => x.UnlockedRunnerEntered), averageGivebackTicks = trades.Average(x => x.UnlockedGivebackTicks) },
        runnerEligibility = new { authoritativeReached300 = trades.Count(x => x.AuthoritativeReached300), runnerReachableAfterCoreUnlock = trades.Count(x => x.RunnerReachableAfterUnlock) },
        peakProtectionBands = bands.GroupBy(x => x.PeakFraction).ToDictionary(x => x.Key.ToString("0.00", CultureInfo.InvariantCulture), x => new { averageRetainedTicks = x.Average(y => y.TheoreticalRetainedTicks), averageGivebackTicks = x.Average(y => y.PeakToRetainedGivebackTicks), hindsightDependent = true }),
        trades
    };
    File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteText(string path, string dataset, IReadOnlyList<TradeComparisonRow> trades, IReadOnlyList<PeakBandRow> bands)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("ISE Elite Phase 4 Management Gate Counterfactual Study");
    w.WriteLine("Diagnostic only. Frozen V7.8.7 remains authoritative and unchanged.");
    w.WriteLine($"Dataset: {dataset}");
    w.WriteLine($"Authoritative realized ticks: {F(trades.Sum(x => x.AuthoritativeRealizedTicks))}");
    w.WriteLine($"Core-unlocked shadow realized ticks: {F(trades.Sum(x => x.UnlockedRealizedTicks))}");
    w.WriteLine($"Core-unlocked diagnostic delta: {F(trades.Sum(x => x.UnlockedDeltaTicks))}");
    w.WriteLine($"Authoritative average giveback: {F(trades.Average(x => x.AuthoritativeGivebackTicks))}");
    w.WriteLine($"Core-unlocked average giveback: {F(trades.Average(x => x.UnlockedGivebackTicks))}");
    w.WriteLine($"Authoritative +300-MFE trades: {trades.Count(x => x.AuthoritativeReached300)}");
    w.WriteLine($"Runner reachable after Core unlock: {trades.Count(x => x.RunnerReachableAfterUnlock)}");
    foreach (var g in bands.GroupBy(x => x.PeakFraction).OrderBy(x => x.Key))
        w.WriteLine($"Peak band {g.Key:P0}: average theoretical retained={F(g.Average(x => x.TheoreticalRetainedTicks))}, average giveback={F(g.Average(x => x.PeakToRetainedGivebackTicks))}; hindsight-dependent observation only");
    foreach (var x in trades) w.WriteLine($"{x.TradeId}: Core={x.CoreGateAttribution}; Runner={x.RunnerGateAttribution}; unlockedDelta={F(x.UnlockedDeltaTicks)} ticks");
}

static DateTime ToCentral(DateTimeOffset utc, TimeZoneInfo central) => TimeZoneInfo.ConvertTime(utc, central).DateTime;
static string F(decimal x) => x.ToString("0.00", CultureInfo.InvariantCulture);
static string Yn(bool x) => x ? "yes" : "no";
static string Dt(DateTime x) => x.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
static TimeZoneInfo ResolveCentralTimeZone()
{
    try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
}

internal static class FrozenVectorReader
{
    public static IReadOnlyList<ShadowVector> Read(IReadOnlyList<HistoricalBar> bars, MorningProtectedPositionConfig config, TimeZoneInfo central)
    {
        var type = typeof(MorningProtectedPositionIntelligenceAnalyzer);
        var aggregate = type.GetMethod("Aggregate", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new MissingMethodException(type.FullName, "Aggregate");
        var build = type.GetMethod("BuildVectorStates", BindingFlags.NonPublic | BindingFlags.Instance) ?? throw new MissingMethodException(type.FullName, "BuildVectorStates");
        var manager = new MorningProtectedPositionIntelligenceAnalyzer(config);
        var aggregated = aggregate.Invoke(null, new object[] { bars, config.VectorTimeframeMinutes, central }) ?? throw new InvalidOperationException("Frozen aggregate returned null.");
        var states = (IEnumerable)(build.Invoke(manager, new[] { aggregated }) ?? throw new InvalidOperationException("Frozen vector builder returned null."));
        var result = new List<ShadowVector>();
        foreach (var state in states)
        {
            var stateType = state.GetType();
            var end = (DateTimeOffset)(stateType.GetProperty("EndUtc")?.GetValue(state) ?? throw new InvalidOperationException("Vector EndUtc missing."));
            var bias = stateType.GetProperty("Bias")?.GetValue(state)?.ToString() ?? throw new InvalidOperationException("Vector Bias missing.");
            result.Add(new ShadowVector(end, bias));
        }
        return result;
    }
}

internal static class ShadowManager
{
    public static ShadowOutcome Run(MorningDailySequencingCandidate candidate, IReadOnlyList<HistoricalBar> path,
        IReadOnlyList<ShadowVector> vectors, MorningProtectedPositionConfig config, bool requireAlignmentForCore)
    {
        var source = candidate.Entry.Source.Source;
        var direction = source.Direction;
        var entry = source.EntryPrice;
        var target = Price(direction, entry, config.ScalpTargetTicks, config.TickSize);
        var deadline = source.EntryUtc.AddMinutes(config.ScalpTimeoutMinutes);
        var stop = source.StopPrice;
        var stopReason = MorningProtectedPositionExitReason.StructuralStop;
        var mode = MorningProtectedPositionMode.Scalp;
        var core = false;
        var runner = false;
        var alignedBars = 0;
        var maximumAligned = 0;
        DateTimeOffset? lastVector = null;
        decimal mfe = 0m, mae = 0m, bestProtected = 0m;

        foreach (var bar in path)
        {
            var latest = vectors.LastOrDefault(x => x.EndUtc < bar.TimestampUtc);
            var aligned = latest != null && (direction == NewYorkResearchDirection.Long ? latest.Bias == "Bullish" : latest.Bias == "Bearish");
            if (latest != null && (!lastVector.HasValue || latest.EndUtc > lastVector.Value))
            {
                lastVector = latest.EndUtc;
                if (aligned) { alignedBars++; maximumAligned = Math.Max(maximumAligned, alignedBars); }
                else
                {
                    alignedBars = 0;
                    if (core) return Build(mode, MorningProtectedPositionExitReason.VectorFlowBiasLoss, bar.TimestampUtc, bar.Open, mfe, mae, core, runner, bestProtected, maximumAligned, false, direction, entry, config);
                }
            }

            if (TouchesStop(bar, direction, stop))
                return Build(mode, stopReason, bar.TimestampUtc, stop, mfe, mae, core, runner, bestProtected, maximumAligned, !core, direction, entry, config);

            Update(bar, direction, entry, config.TickSize, ref mfe, ref mae);
            if (!core)
            {
                if (TouchesTarget(bar, direction, target))
                {
                    if (requireAlignmentForCore && !aligned)
                        return Build(MorningProtectedPositionMode.Scalp, MorningProtectedPositionExitReason.ScalpCapture, bar.TimestampUtc, target, mfe, mae, false, false, bestProtected, maximumAligned, false, direction, entry, config);
                    core = true;
                    mode = MorningProtectedPositionMode.Core;
                    bestProtected = Math.Max(bestProtected, config.ExtensionProfitFloorTicks);
                    Tighten(direction, Price(direction, entry, bestProtected, config.TickSize), MorningProtectedPositionExitReason.ExtensionFloor, ref stop, ref stopReason);
                }
                else if (bar.TimestampUtc >= deadline)
                    return Build(MorningProtectedPositionMode.Scalp, MorningProtectedPositionExitReason.ScalpTimeout, bar.TimestampUtc, bar.Close, mfe, mae, false, false, bestProtected, maximumAligned, false, direction, entry, config);
            }

            if (core)
            {
                if (mfe >= config.RunnerThresholdTicks && alignedBars >= config.RunnerAlignedBars) { mode = MorningProtectedPositionMode.Runner; runner = true; }
                var coreFloor = Math.Max((decimal)config.ExtensionProfitFloorTicks, Math.Floor(mfe * config.CoreRetentionFraction));
                var protectedTicks = runner ? Math.Max(coreFloor, mfe - config.RunnerTrailTicks) : coreFloor;
                var reason = runner ? MorningProtectedPositionExitReason.RunnerTrail : MorningProtectedPositionExitReason.ExtensionFloor;
                if (protectedTicks > bestProtected)
                {
                    bestProtected = protectedTicks;
                    Tighten(direction, Price(direction, entry, protectedTicks, config.TickSize), reason, ref stop, ref stopReason);
                }
            }
        }
        var last = path[^1];
        return Build(mode, MorningProtectedPositionExitReason.ResearchWindowEnd, last.TimestampUtc, last.Close, mfe, mae, core, runner, bestProtected, maximumAligned, false, direction, entry, config);
    }

    private static ShadowOutcome Build(MorningProtectedPositionMode mode, MorningProtectedPositionExitReason reason,
        DateTimeOffset utc, decimal exit, decimal mfe, decimal mae, bool core, bool runner, decimal best, int aligned,
        bool structuralBefore, NewYorkResearchDirection direction, decimal entry, MorningProtectedPositionConfig config)
    {
        var ticks = direction == NewYorkResearchDirection.Long ? (exit - entry) / config.TickSize : (entry - exit) / config.TickSize;
        return new ShadowOutcome(mode, reason, utc, exit, ticks, mfe, mae, core, runner, best, aligned, structuralBefore);
    }

    private static void Update(HistoricalBar bar, NewYorkResearchDirection d, decimal entry, decimal tick, ref decimal mfe, ref decimal mae)
    {
        var favorable = d == NewYorkResearchDirection.Long ? (bar.High - entry) / tick : (entry - bar.Low) / tick;
        var adverse = d == NewYorkResearchDirection.Long ? (entry - bar.Low) / tick : (bar.High - entry) / tick;
        mfe = Math.Max(mfe, Math.Max(0m, favorable)); mae = Math.Max(mae, Math.Max(0m, adverse));
    }
    private static decimal Price(NewYorkResearchDirection d, decimal entry, decimal ticks, decimal tick) => d == NewYorkResearchDirection.Long ? entry + ticks * tick : entry - ticks * tick;
    private static bool TouchesStop(HistoricalBar b, NewYorkResearchDirection d, decimal p) => d == NewYorkResearchDirection.Long ? b.Low <= p : b.High >= p;
    private static bool TouchesTarget(HistoricalBar b, NewYorkResearchDirection d, decimal p) => d == NewYorkResearchDirection.Long ? b.High >= p : b.Low <= p;
    private static void Tighten(NewYorkResearchDirection d, decimal proposed, MorningProtectedPositionExitReason reason, ref decimal stop, ref MorningProtectedPositionExitReason active)
    {
        if (d == NewYorkResearchDirection.Long ? proposed > stop : proposed < stop) { stop = proposed; active = reason; }
    }
}

internal sealed record ShadowVector(DateTimeOffset EndUtc, string Bias);
internal sealed record ShadowOutcome(MorningProtectedPositionMode FinalMode, MorningProtectedPositionExitReason ExitReason, DateTimeOffset ExitUtc, decimal ExitPrice, decimal RealizedTicks, decimal MfeTicks, decimal MaeTicks, bool CoreEntered, bool RunnerEntered, decimal BestProtectedTicks, int MaximumAlignedBars, bool StructuralStopBeforeProtection);
internal sealed record BandOrdering(bool StructuralStopBeforePeak);
internal sealed record ShadowPathRow(string TradeId, string Path, bool CoreEntered, bool RunnerReachable, string FinalMode, string ExitReason, decimal PeakMfeTicks, decimal MaeTicks, decimal RetainedTicksAtExit, decimal PeakToExitGivebackTicks, bool StructuralStopBeforeProtectionValid, bool HindsightDependent, string Note);
internal sealed record PeakBandRow(string TradeId, string Observation, decimal PeakFraction, decimal PeakMfeTicks, decimal MaeTicks, decimal TheoreticalRetainedTicks, decimal PeakToRetainedGivebackTicks, bool StructuralStopBeforePeakKnown, bool HindsightDependent, string Note);
internal sealed record TradeComparisonRow(string TradeId, DateTime EntryCentral, string Direction, string AuthoritativeMode, string AuthoritativeExit, decimal AuthoritativeRealizedTicks, decimal AuthoritativeMfeTicks, decimal AuthoritativeMaeTicks, decimal AuthoritativeGivebackTicks, bool UnlockedCoreEntered, bool UnlockedRunnerEntered, string UnlockedFinalMode, string UnlockedExit, decimal UnlockedRealizedTicks, decimal UnlockedMfeTicks, decimal UnlockedMaeTicks, decimal UnlockedGivebackTicks, decimal UnlockedDeltaTicks, bool AuthoritativeReached300, bool RunnerReachableAfterUnlock, string CoreGateAttribution, string RunnerGateAttribution);
