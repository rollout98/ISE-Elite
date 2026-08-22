using System.Globalization;
using System.Text.Json;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: <phase-4-trade-by-trade-summary.tsv> <output-directory>");
    return 2;
}

var inputPath = Path.GetFullPath(args[0]);
var outputDirectory = Path.GetFullPath(args[1]);
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"Phase 4 trade summary not found: {inputPath}");
    return 3;
}

Directory.CreateDirectory(outputDirectory);
var rows = ReadRows(inputPath);
if (rows.Count != 6)
    throw new InvalidOperationException($"Expected 6 Phase 4 trades, found {rows.Count}.");

var authoritativeTotal = rows.Sum(x => x.AuthoritativeTicks);
var unlockedTotal = rows.Sum(x => x.UnlockedTicks);
var totalDelta = rows.Sum(x => x.DeltaTicks);
if (authoritativeTotal != 537m || totalDelta != 836m)
    throw new InvalidOperationException($"Validated Phase 4 anchors changed: authoritative={authoritativeTotal}, delta={totalDelta}.");

var largest = rows.OrderByDescending(x => x.DeltaTicks).First();
var withoutLargest = rows.Where(x => x.TradeId != largest.TradeId).ToList();
var tradeContributions = rows.Select(x => new ContributionRow(
    x.TradeId, x.EntryCentral.Date, x.AuthoritativeMode, x.AuthoritativeExit,
    x.AuthoritativeTicks, x.UnlockedTicks, x.DeltaTicks,
    totalDelta == 0m ? 0m : x.DeltaTicks / totalDelta,
    Classify(x.DeltaTicks))).ToList();

var leaveOneTradeOut = rows.Select(excluded => BuildExclusion(
    "Trade", excluded.TradeId, rows.Where(x => x.TradeId != excluded.TradeId).ToList())).ToList();

var leaveOneSessionOut = rows.GroupBy(x => x.EntryCentral.Date).OrderBy(x => x.Key)
    .Select(group => BuildExclusion("Session", group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        rows.Where(x => x.EntryCentral.Date != group.Key).ToList())).ToList();

var byMode = BuildGroups(rows, x => x.AuthoritativeMode);
var byExit = BuildGroups(rows, x => x.AuthoritativeExit);
var medianDelta = Median(rows.Select(x => x.DeltaTicks));
var conclusionWithoutLargest = withoutLargest.Sum(x => x.DeltaTicks) > 0m
    ? "Core alignment remains a positive lost-capture contributor after removing the largest trade."
    : "No. After removing the largest contributor, Core unlock changes the remaining sample by "
        + F(withoutLargest.Sum(x => x.DeltaTicks)) + " ticks; the dominant-gate conclusion does not remain true.";

WriteContributions(Path.Combine(outputDirectory, "trade-contributions.tsv"), tradeContributions);
WriteExclusions(Path.Combine(outputDirectory, "leave-one-trade-out.tsv"), leaveOneTradeOut);
WriteExclusions(Path.Combine(outputDirectory, "leave-one-session-out.tsv"), leaveOneSessionOut);
WriteGroups(Path.Combine(outputDirectory, "improvement-by-authoritative-mode.tsv"), byMode);
WriteGroups(Path.Combine(outputDirectory, "improvement-by-exit-reason.tsv"), byExit);
WriteJson(Path.Combine(outputDirectory, "summary.json"), inputPath, rows, tradeContributions,
    leaveOneTradeOut, leaveOneSessionOut, byMode, byExit, largest, medianDelta, conclusionWithoutLargest);
WriteText(Path.Combine(outputDirectory, "summary.txt"), inputPath, rows, largest, medianDelta,
    byMode, byExit, conclusionWithoutLargest);

Console.WriteLine("ISE Elite Phase 5 Frozen Management Robustness Diagnostics");
Console.WriteLine("Diagnostic only. Frozen V7.8.7 remains authoritative and unchanged.");
Console.WriteLine($"Authoritative/Core-unlocked/Delta: {F(authoritativeTotal)}/{F(unlockedTotal)}/{F(totalDelta)} ticks");
Console.WriteLine($"Median trade improvement: {F(medianDelta)} ticks");
Console.WriteLine($"Improved/Unchanged/Worsened: {rows.Count(x => x.DeltaTicks > 0m)}/{rows.Count(x => x.DeltaTicks == 0m)}/{rows.Count(x => x.DeltaTicks < 0m)}");
Console.WriteLine($"Largest contribution: {largest.TradeId} {F(largest.DeltaTicks)} ticks ({F(largest.DeltaTicks / totalDelta * 100m)}%)");
Console.WriteLine($"Without largest contributor: {F(withoutLargest.Sum(x => x.DeltaTicks))} ticks");
Console.WriteLine(conclusionWithoutLargest);
return 0;

static List<TradeRow> ReadRows(string path)
{
    var lines = File.ReadAllLines(path);
    if (lines.Length < 2) return new List<TradeRow>();
    var header = lines[0].Split('\t');
    int Col(string name)
    {
        var index = Array.IndexOf(header, name);
        if (index < 0) throw new InvalidOperationException($"Required Phase 4 column missing: {name}");
        return index;
    }
    var id = Col("tradeId"); var entry = Col("entryCentral"); var mode = Col("authoritativeMode");
    var exit = Col("authoritativeExit"); var authority = Col("authoritativeRealizedTicks");
    var unlocked = Col("unlockedRealizedTicks"); var delta = Col("unlockedDeltaTicks");
    return lines.Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)).Select(line =>
    {
        var f = line.Split('\t');
        return new TradeRow(f[id], DateTime.ParseExact(f[entry], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            f[mode], f[exit], D(f[authority]), D(f[unlocked]), D(f[delta]));
    }).ToList();
}

static ExclusionRow BuildExclusion(string type, string excluded, IReadOnlyList<TradeRow> included)
{
    var a = included.Sum(x => x.AuthoritativeTicks); var u = included.Sum(x => x.UnlockedTicks);
    var d = included.Sum(x => x.DeltaTicks);
    return new ExclusionRow(type, excluded, included.Count, a, u, d, Median(included.Select(x => x.DeltaTicks)),
        included.Count(x => x.DeltaTicks > 0m), included.Count(x => x.DeltaTicks == 0m), included.Count(x => x.DeltaTicks < 0m));
}

static List<GroupRow> BuildGroups(IReadOnlyList<TradeRow> rows, Func<TradeRow, string> key) => rows
    .GroupBy(key).OrderBy(x => x.Key).Select(g => new GroupRow(g.Key, g.Count(),
        g.Sum(x => x.AuthoritativeTicks), g.Sum(x => x.UnlockedTicks), g.Sum(x => x.DeltaTicks),
        Median(g.Select(x => x.DeltaTicks)), g.Count(x => x.DeltaTicks > 0m),
        g.Count(x => x.DeltaTicks == 0m), g.Count(x => x.DeltaTicks < 0m))).ToList();

static string Classify(decimal value) => value > 0m ? "Improved" : value < 0m ? "Worsened" : "Unchanged";
static decimal Median(IEnumerable<decimal> values)
{
    var x = values.OrderBy(v => v).ToList();
    if (x.Count == 0) return 0m;
    return x.Count % 2 == 1 ? x[x.Count / 2] : (x[x.Count / 2 - 1] + x[x.Count / 2]) / 2m;
}

static void WriteContributions(string path, IEnumerable<ContributionRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("tradeId\tsessionDate\tauthoritativeMode\tauthoritativeExit\tauthoritativeTicks\tcoreUnlockedTicks\tdeltaTicks\tshareOfTotalDelta\tclassification");
    foreach (var x in rows) w.WriteLine(string.Join("\t", new[] { x.TradeId, x.SessionDate.ToString("yyyy-MM-dd"), x.Mode, x.ExitReason, F(x.AuthoritativeTicks), F(x.UnlockedTicks), F(x.DeltaTicks), F(x.Share), x.Classification }));
}

static void WriteExclusions(string path, IEnumerable<ExclusionRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("exclusionType\texcluded\tincludedTrades\tauthoritativeTicks\tcoreUnlockedTicks\tdeltaTicks\tmedianTradeDelta\timproved\tunchanged\tworsened");
    foreach (var x in rows) w.WriteLine(string.Join("\t", new[] { x.ExclusionType, x.Excluded, x.IncludedTrades.ToString(), F(x.AuthoritativeTicks), F(x.UnlockedTicks), F(x.DeltaTicks), F(x.MedianDelta), x.Improved.ToString(), x.Unchanged.ToString(), x.Worsened.ToString() }));
}

static void WriteGroups(string path, IEnumerable<GroupRow> rows)
{
    using var w = new StreamWriter(path, false);
    w.WriteLine("group\ttrades\tauthoritativeTicks\tcoreUnlockedTicks\tdeltaTicks\tmedianTradeDelta\timproved\tunchanged\tworsened");
    foreach (var x in rows) w.WriteLine(string.Join("\t", new[] { x.Group, x.Trades.ToString(), F(x.AuthoritativeTicks), F(x.UnlockedTicks), F(x.DeltaTicks), F(x.MedianDelta), x.Improved.ToString(), x.Unchanged.ToString(), x.Worsened.ToString() }));
}

static void WriteJson(string path, string input, IReadOnlyList<TradeRow> rows, object contributions,
    object loto, object loso, object byMode, object byExit, TradeRow largest, decimal median, string conclusion)
{
    var without = rows.Where(x => x.TradeId != largest.TradeId).ToList();
    var payload = new { schemaVersion = 1, phase4Input = input,
        governance = new { frozen = true, version = "V7.8.7", diagnosticOnly = true },
        fullSample = new { trades = rows.Count, authoritativeTicks = rows.Sum(x => x.AuthoritativeTicks), coreUnlockedTicks = rows.Sum(x => x.UnlockedTicks), deltaTicks = rows.Sum(x => x.DeltaTicks), medianTradeDeltaTicks = median, improved = rows.Count(x => x.DeltaTicks > 0m), unchanged = rows.Count(x => x.DeltaTicks == 0m), worsened = rows.Count(x => x.DeltaTicks < 0m) },
        concentration = new { largestTrade = largest.TradeId, largestDeltaTicks = largest.DeltaTicks, largestShareOfTotal = largest.DeltaTicks / rows.Sum(x => x.DeltaTicks), deltaWithoutLargest = without.Sum(x => x.DeltaTicks), conclusionWithoutLargest = conclusion },
        contributions, leaveOneTradeOut = loto, leaveOneSessionOut = loso, improvementByAuthoritativeMode = byMode, improvementByExitReason = byExit };
    File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteText(string path, string input, IReadOnlyList<TradeRow> rows, TradeRow largest, decimal median,
    IReadOnlyList<GroupRow> modes, IReadOnlyList<GroupRow> exits, string conclusion)
{
    using var w = new StreamWriter(path, false);
    var total = rows.Sum(x => x.DeltaTicks);
    w.WriteLine("ISE Elite Phase 5 Frozen Management Robustness Diagnostics");
    w.WriteLine("Diagnostic only. Frozen V7.8.7 remains authoritative and unchanged.");
    w.WriteLine($"Phase 4 input: {input}");
    w.WriteLine($"Authoritative/Core-unlocked/Delta: {F(rows.Sum(x => x.AuthoritativeTicks))}/{F(rows.Sum(x => x.UnlockedTicks))}/{F(total)} ticks");
    w.WriteLine($"Median trade improvement: {F(median)} ticks");
    w.WriteLine($"Improved/Unchanged/Worsened: {rows.Count(x => x.DeltaTicks > 0m)}/{rows.Count(x => x.DeltaTicks == 0m)}/{rows.Count(x => x.DeltaTicks < 0m)}");
    w.WriteLine($"Largest contributor: {largest.TradeId}, {F(largest.DeltaTicks)} ticks, {F(largest.DeltaTicks / total * 100m)}% of net improvement");
    w.WriteLine($"Delta without largest contributor: {F(total - largest.DeltaTicks)} ticks");
    w.WriteLine(conclusion);
    foreach (var x in modes) w.WriteLine($"Mode {x.Group}: trades={x.Trades}, delta={F(x.DeltaTicks)}, median={F(x.MedianDelta)}");
    foreach (var x in exits) w.WriteLine($"Exit {x.Group}: trades={x.Trades}, delta={F(x.DeltaTicks)}, median={F(x.MedianDelta)}");
}

static decimal D(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
static string F(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

internal sealed record TradeRow(string TradeId, DateTime EntryCentral, string AuthoritativeMode, string AuthoritativeExit, decimal AuthoritativeTicks, decimal UnlockedTicks, decimal DeltaTicks);
internal sealed record ContributionRow(string TradeId, DateTime SessionDate, string Mode, string ExitReason, decimal AuthoritativeTicks, decimal UnlockedTicks, decimal DeltaTicks, decimal Share, string Classification);
internal sealed record ExclusionRow(string ExclusionType, string Excluded, int IncludedTrades, decimal AuthoritativeTicks, decimal UnlockedTicks, decimal DeltaTicks, decimal MedianDelta, int Improved, int Unchanged, int Worsened);
internal sealed record GroupRow(string Group, int Trades, decimal AuthoritativeTicks, decimal UnlockedTicks, decimal DeltaTicks, decimal MedianDelta, int Improved, int Unchanged, int Worsened);
