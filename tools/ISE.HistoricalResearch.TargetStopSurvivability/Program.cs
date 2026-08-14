using System.Globalization;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: <validated-30-second-mnq-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Dataset not found: {path}");
    return 3;
}

var central = ResolveCentral();
var all = ReadBars(path, central).OrderBy(x => x.Utc).ToList();

if (all.Any(x => x.IntervalSeconds != 30))
    throw new InvalidOperationException("V7.9.5 requires 30-second source bars.");

var researchFrom = new DateTime(2026, 6, 1);
var researchTo = new DateTime(2026, 7, 31);
var validations = ValidateSessions(all, researchFrom, researchTo);
var validDates = validations.Where(x => x.IsUsable).Select(x => x.Date).ToHashSet();

var researchBars = all
    .Where(IsInsideWindow)
    .Where(x => validDates.Contains(SessionDate(x)))
    .OrderBy(x => x.Utc)
    .ToList();

var bySession = researchBars
    .GroupBy(SessionDate)
    .OrderBy(g => g.Key)
    .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Utc).ToList());

var thresholds = new[] { 10, 15, 20, 25, 30, 40, 50, 75, 100 };
var horizons = new[] { 5, 10, 15 };
var stopGrid = new[] { 10, 15, 20, 25, 30, 40, 50, 75, 100 };

Console.WriteLine("ISE Elite V7.9.5 Target / Stop Survivability & Direction Study");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Purpose: determine whether each forward opportunity is long-only, short-only, both, or neither,");
Console.WriteLine("and quantify adverse excursion / stop survivability before target.");
Console.WriteLine("Decision anchors: every 5 minutes across the 17:00 previous day -> 15:00 current day CT session.");
Console.WriteLine("Execution source: validated 30-second MNQ bars.");
Console.WriteLine("IMPORTANT: This remains a hindsight labeling study. It does not assume ISE knows direction in advance.");
Console.WriteLine($"Research sessions observed: {validations.Count}");
Console.WriteLine($"Usable research sessions: {validDates.Count}");
Console.WriteLine($"Excluded sessions: {validations.Count(x => !x.IsUsable)}");
foreach (var v in validations.Where(x => !x.IsUsable))
    Console.WriteLine($"EXCLUDED SESSION {v.Date:yyyy-MM-dd} reason={v.Reason}");
Console.WriteLine();

foreach (var horizon in horizons)
{
    Console.WriteLine(new string('=', 118));
    Console.WriteLine($"FORWARD HORIZON {horizon} MINUTES");
    Console.WriteLine(new string('=', 118));

    foreach (var threshold in thresholds)
    {
        var labels = new List<Label>();

        foreach (var kv in bySession)
        {
            var bars = kv.Value;

            foreach (var anchor in bars.Where(x => x.Central.Minute % 5 == 0 && x.Central.Second == 30))
            {
                var startTime = anchor.Central.AddSeconds(-30);
                var endTime = startTime.AddMinutes(horizon);
                var entry = anchor.Open;

                var future = bars
                    .Select((b, idx) => new IndexedBar(b, idx))
                    .Where(x => x.Bar.Central > startTime && x.Bar.Central <= endTime)
                    .ToList();

                if (future.Count < Math.Max(1, horizon * 2 - 1))
                    continue;

                var longInfo = AnalyzeDirection(future, entry, threshold, true);
                var shortInfo = AnalyzeDirection(future, entry, threshold, false);

                string cls;
                string first;
                if (longInfo.Hit && shortInfo.Hit)
                {
                    cls = "Both";
                    if (longInfo.HitIndex < shortInfo.HitIndex) first = "Long";
                    else if (shortInfo.HitIndex < longInfo.HitIndex) first = "Short";
                    else first = "Same30sBar";
                }
                else if (longInfo.Hit) { cls = "LongOnly"; first = "Long"; }
                else if (shortInfo.Hit) { cls = "ShortOnly"; first = "Short"; }
                else { cls = "Neither"; first = "None"; }

                labels.Add(new Label(
                    kv.Key, startTime, ZoneAt(startTime), threshold,
                    cls, first,
                    longInfo.Hit, longInfo.MaeTicksBeforeTarget, longInfo.TimeToTargetMinutes,
                    shortInfo.Hit, shortInfo.MaeTicksBeforeTarget, shortInfo.TimeToTargetMinutes));
            }
        }

        Console.WriteLine();
        Console.WriteLine($"TARGET {threshold} TICKS");
        Console.WriteLine($"anchors={labels.Count} anchors/day={(validDates.Count == 0 ? 0 : (decimal)labels.Count / validDates.Count):F2}");
        PrintClassSummary(labels);

        var hittable = labels.Where(x => x.Classification != "Neither").ToList();
        Console.WriteLine($"directionalOpportunityPct={(labels.Count == 0 ? 0 : 100m * hittable.Count / labels.Count):F1}%");

        var longHits = labels.Where(x => x.LongHit).ToList();
        var shortHits = labels.Where(x => x.ShortHit).ToList();

        Console.WriteLine($"longHitPct={(labels.Count == 0 ? 0 : 100m * longHits.Count / labels.Count):F1}% shortHitPct={(labels.Count == 0 ? 0 : 100m * shortHits.Count / labels.Count):F1}%");
        Console.WriteLine($"longMedianMAE={Median(longHits.Select(x => x.LongMae)):F1} longP75MAE={Percentile(longHits.Select(x => x.LongMae),0.75m):F1} longP90MAE={Percentile(longHits.Select(x => x.LongMae),0.90m):F1}");
        Console.WriteLine($"shortMedianMAE={Median(shortHits.Select(x => x.ShortMae)):F1} shortP75MAE={Percentile(shortHits.Select(x => x.ShortMae),0.75m):F1} shortP90MAE={Percentile(shortHits.Select(x => x.ShortMae),0.90m):F1}");
        Console.WriteLine($"longMedianMinutesToTarget={Median(longHits.Select(x => x.LongMinutes)):F2} shortMedianMinutesToTarget={Median(shortHits.Select(x => x.ShortMinutes)):F2}");

        Console.WriteLine("STOP SURVIVABILITY BEFORE TARGET");
        foreach (var stop in stopGrid)
        {
            decimal longSurvive = longHits.Count == 0 ? 0 : 100m * longHits.Count(x => x.LongMae < stop) / longHits.Count;
            decimal shortSurvive = shortHits.Count == 0 ? 0 : 100m * shortHits.Count(x => x.ShortMae < stop) / shortHits.Count;
            Console.WriteLine($"  stop={stop,3} ticks longSurvival={longSurvive,6:F1}% shortSurvival={shortSurvive,6:F1}%");
        }

        Console.WriteLine("BY SESSION ZONE");
        foreach (var g in labels.GroupBy(x => x.Zone).OrderBy(x => x.Key))
        {
            int longOnly = g.Count(x => x.Classification == "LongOnly");
            int shortOnly = g.Count(x => x.Classification == "ShortOnly");
            int both = g.Count(x => x.Classification == "Both");
            int neither = g.Count(x => x.Classification == "Neither");
            int same = g.Count(x => x.FirstTarget == "Same30sBar");

            var zLongHits = g.Where(x => x.LongHit).ToList();
            var zShortHits = g.Where(x => x.ShortHit).ToList();

            Console.WriteLine(
                $"  {g.Key,-30} n={g.Count(),5} longOnly={Pct(longOnly,g.Count()),5:F1}% shortOnly={Pct(shortOnly,g.Count()),5:F1}% both={Pct(both,g.Count()),5:F1}% neither={Pct(neither,g.Count()),5:F1}% same30s={Pct(same,g.Count()),5:F1}% " +
                $"LmedMAE={Median(zLongHits.Select(x=>x.LongMae)),6:F1} SmedMAE={Median(zShortHits.Select(x=>x.ShortMae)),6:F1}");
        }

        Console.WriteLine("ZONE STOP SURVIVABILITY -- KEY STOPS 15/20/25/30/40/50");
        foreach (var g in labels.GroupBy(x => x.Zone).OrderBy(x => x.Key))
        {
            var zLongHits = g.Where(x => x.LongHit).ToList();
            var zShortHits = g.Where(x => x.ShortHit).ToList();

            Console.WriteLine($"  {g.Key}");
            foreach (var stop in new[] { 15, 20, 25, 30, 40, 50 })
            {
                decimal longSurvive = zLongHits.Count == 0 ? 0 : 100m * zLongHits.Count(x => x.LongMae < stop) / zLongHits.Count;
                decimal shortSurvive = zShortHits.Count == 0 ? 0 : 100m * zShortHits.Count(x => x.ShortMae < stop) / zShortHits.Count;
                Console.WriteLine($"    stop={stop,2} L={longSurvive,6:F1}% S={shortSurvive,6:F1}%");
            }
        }

        Console.WriteLine("BY ENTRY HOUR CT");
        foreach (var g in labels.GroupBy(x => x.Start.Hour).OrderBy(x => x.Key))
        {
            int longOnly = g.Count(x => x.Classification == "LongOnly");
            int shortOnly = g.Count(x => x.Classification == "ShortOnly");
            int both = g.Count(x => x.Classification == "Both");
            int neither = g.Count(x => x.Classification == "Neither");
            Console.WriteLine($"  {g.Key:00}:00 n={g.Count(),4} longOnly={Pct(longOnly,g.Count()),5:F1}% shortOnly={Pct(shortOnly,g.Count()),5:F1}% both={Pct(both,g.Count()),5:F1}% neither={Pct(neither,g.Count()),5:F1}%");
        }
    }
}

Console.WriteLine();
Console.WriteLine(new string('=', 118));
Console.WriteLine("INTERPRETATION GUIDE");
Console.WriteLine(new string('=', 118));
Console.WriteLine("LongOnly / ShortOnly: direction matters materially at that anchor.");
Console.WriteLine("Both: both directional targets occur inside the horizon; timing/path and stop placement may matter more than directional prediction.");
Console.WriteLine("Same30sBar: both directional targets first appear in the same 30-second candle; tick data is required for exact first-hit ordering.");
Console.WriteLine("Stop survival is measured only among paths that eventually hit the requested target.");
Console.WriteLine("A path 'survives' a stop only when adverse excursion before target is strictly less than the stop threshold.");

return 0;

static DirectionInfo AnalyzeDirection(List<IndexedBar> future, decimal entry, int targetTicks, bool isLong)
{
    decimal targetPrice = isLong ? entry + targetTicks * 0.25m : entry - targetTicks * 0.25m;
    decimal worstAdverseTicks = 0m;

    foreach (var x in future)
    {
        var b = x.Bar;

        decimal adverse = isLong
            ? Math.Max(0m, (entry - b.Low) / 0.25m)
            : Math.Max(0m, (b.High - entry) / 0.25m);

        bool hit = isLong ? b.High >= targetPrice : b.Low <= targetPrice;

        if (hit)
        {
            // Since OHLC cannot reveal ordering inside the target bar, MAE before target excludes
            // any new adverse extreme from the same 30-second candle as the target hit.
            var minutes = (decimal)(b.Central - future[0].Bar.Central.AddSeconds(-30)).TotalMinutes;
            return new DirectionInfo(true, x.Index, worstAdverseTicks, minutes);
        }

        if (adverse > worstAdverseTicks)
            worstAdverseTicks = adverse;
    }

    return new DirectionInfo(false, int.MaxValue, worstAdverseTicks, 0m);
}

static void PrintClassSummary(List<Label> labels)
{
    foreach (var cls in new[] { "LongOnly", "ShortOnly", "Both", "Neither" })
    {
        int n = labels.Count(x => x.Classification == cls);
        Console.WriteLine($"{cls}={n} ({Pct(n,labels.Count):F1}%)");
    }

    foreach (var first in new[] { "Long", "Short", "Same30sBar", "None" })
    {
        int n = labels.Count(x => x.FirstTarget == first);
        Console.WriteLine($"firstTarget_{first}={n} ({Pct(n,labels.Count):F1}%)");
    }
}

static decimal Pct(int n, int d) => d == 0 ? 0m : 100m * n / d;

static List<SessionValidation> ValidateSessions(
    IReadOnlyList<Bar30> bars,
    DateTime researchFrom,
    DateTime researchTo)
{
    const int expectedCritical = 600;
    var result = new List<SessionValidation>();

    var groups = bars
        .Where(IsInsideWindow)
        .GroupBy(SessionDate)
        .Where(g => g.Key >= researchFrom && g.Key <= researchTo)
        .OrderBy(g => g.Key);

    foreach (var g in groups)
    {
        var x = g.OrderBy(b => b.Utc).ToList();
        var critical = x.Count(b => b.Central.TimeOfDay >= new TimeSpan(6,0,0) &&
                                    b.Central.TimeOfDay < new TimeSpan(11,0,0));

        int gaps = 0;
        for (int i = 1; i < x.Count; i++)
            if ((x[i].Central - x[i-1].Central).TotalSeconds > 45d) gaps++;

        bool usable = critical == expectedCritical && gaps == 0;
        string reason = usable ? "OK" :
            critical != expectedCritical ? $"CriticalWindowIncomplete({critical}/{expectedCritical})" :
            $"InternalGaps({gaps})";

        result.Add(new SessionValidation(g.Key, x.Count, critical, gaps, usable, reason));
    }

    return result;
}

static bool IsInsideWindow(Bar30 b)
{
    var t = b.Central.TimeOfDay;
    return t >= new TimeSpan(17,0,0) || t < new TimeSpan(15,0,0);
}

static DateTime SessionDate(Bar30 b) =>
    b.Central.TimeOfDay >= new TimeSpan(17,0,0) ? b.Central.Date.AddDays(1) : b.Central.Date;

static string ZoneAt(DateTime central)
{
    var t = central.TimeOfDay;
    if (t >= new TimeSpan(17,0,0) && t < new TimeSpan(19,0,0)) return "01_EveningReopen";
    if (t >= new TimeSpan(19,0,0) && t < new TimeSpan(23,0,0)) return "02_AsiaDevelopment";
    if (t >= new TimeSpan(23,0,0) || t < new TimeSpan(3,0,0)) return "03_OvernightTransition";
    if (t >= new TimeSpan(3,0,0) && t < new TimeSpan(6,30,0)) return "04_London_USPremarket";
    if (t >= new TimeSpan(6,30,0) && t < new TimeSpan(8,45,0)) return "05_USPreopen_OpenDevelopment";
    if (t >= new TimeSpan(8,45,0) && t < new TimeSpan(9,5,0)) return "06_NYTransition_0845_0905";
    if (t >= new TimeSpan(9,5,0) && t < new TimeSpan(10,0,0)) return "07_NYSecondary_0905_1000";
    if (t >= new TimeSpan(10,0,0) && t < new TimeSpan(12,0,0)) return "08_LateMorning";
    if (t >= new TimeSpan(12,0,0) && t < new TimeSpan(15,0,0)) return "09_Midday_EarlyAfternoon";
    return "Outside";
}

static decimal Median(IEnumerable<decimal> values)
{
    var x = values.OrderBy(v => v).ToList();
    if (x.Count == 0) return 0;
    int m = x.Count / 2;
    return x.Count % 2 == 1 ? x[m] : (x[m-1] + x[m]) / 2m;
}

static decimal Percentile(IEnumerable<decimal> values, decimal p)
{
    var x = values.OrderBy(v => v).ToList();
    if (x.Count == 0) return 0;
    var idx = (int)Math.Floor((double)(p * (x.Count - 1)));
    return x[Math.Clamp(idx, 0, x.Count - 1)];
}

static List<Bar30> ReadBars(string path, TimeZoneInfo central)
{
    var lines = File.ReadLines(path).ToList();
    var h = lines[0].Split('\t');
    var m = h.Select((name, i) => new {name, i})
             .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);

    int I(string n) => m.TryGetValue(n, out var i) ? i : throw new InvalidOperationException($"Missing TSV column: {n}");

    int instrument=I("instrument"), utc=I("timestampUtc"), interval=I("intervalSeconds"),
        open=I("open"), high=I("high"), low=I("low"), close=I("close"), volume=I("volume");

    var r = new List<Bar30>(lines.Count-1);

    foreach (var line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        var p = line.Split('\t');
        var u = DateTimeOffset.Parse(p[utc], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var c = TimeZoneInfo.ConvertTime(u, central).DateTime;

        r.Add(new Bar30 {
            Instrument=p[instrument],
            Utc=u,
            Central=DateTime.SpecifyKind(c,DateTimeKind.Unspecified),
            IntervalSeconds=int.Parse(p[interval],CultureInfo.InvariantCulture),
            Open=decimal.Parse(p[open],CultureInfo.InvariantCulture),
            High=decimal.Parse(p[high],CultureInfo.InvariantCulture),
            Low=decimal.Parse(p[low],CultureInfo.InvariantCulture),
            Close=decimal.Parse(p[close],CultureInfo.InvariantCulture),
            Volume=long.Parse(p[volume],CultureInfo.InvariantCulture)
        });
    }

    return r;
}

static TimeZoneInfo ResolveCentral()
{
    try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    catch { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
}

sealed class Bar30
{
    public string Instrument { get; init; }="";
    public DateTimeOffset Utc { get; init; }
    public DateTime Central { get; init; }
    public int IntervalSeconds { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
}

sealed record IndexedBar(Bar30 Bar, int Index);
sealed record SessionValidation(DateTime Date,int BarCount,int CriticalBars,int Gaps,bool IsUsable,string Reason);
sealed record DirectionInfo(bool Hit,int HitIndex,decimal MaeTicksBeforeTarget,decimal TimeToTargetMinutes);
sealed record Label(
    DateTime SessionDate,
    DateTime Start,
    string Zone,
    int TargetTicks,
    string Classification,
    string FirstTarget,
    bool LongHit,
    decimal LongMae,
    decimal LongMinutes,
    bool ShortHit,
    decimal ShortMae,
    decimal ShortMinutes);
