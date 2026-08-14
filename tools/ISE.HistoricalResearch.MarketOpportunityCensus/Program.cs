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
    throw new InvalidOperationException("V7.9.4 requires 30-second source bars.");

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

Console.WriteLine("ISE Elite V7.9.4 Market Opportunity Census");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Purpose: test how often MNQ actually offers 30/50/75/100-tick directional excursions");
Console.WriteLine("from 5-minute decision anchors across the 17:00->15:00 CT research session.");
Console.WriteLine("This is an opportunity-availability study, NOT a tradable strategy.");
Console.WriteLine("Direction is selected with future information only to measure the market opportunity ceiling.");
Console.WriteLine("Execution source: validated 30-second MNQ bars.");
Console.WriteLine($"Research sessions observed: {validations.Count}");
Console.WriteLine($"Usable research sessions: {validDates.Count}");
Console.WriteLine($"Excluded sessions: {validations.Count(x => !x.IsUsable)}");
foreach (var v in validations.Where(x => !x.IsUsable))
    Console.WriteLine($"EXCLUDED SESSION {v.Date:yyyy-MM-dd} reason={v.Reason}");
Console.WriteLine();

var horizons = new[] { 5, 10, 15 };
var thresholds = new[] { 10, 15, 20, 25, 30, 40, 50, 75, 100 };

foreach (var horizon in horizons)
{
    var observations = new List<Obs>();

    foreach (var kv in bySession)
    {
        var date = kv.Key;
        var bars = kv.Value;

        // One anchor every five minutes, using the first 30s bar ending at HH:MM:30.
        foreach (var anchor in bars.Where(x => x.Central.Minute % 5 == 0 && x.Central.Second == 30))
        {
            var startPrice = anchor.Open;
            var startTime = anchor.Central.AddSeconds(-30);
            var endTime = startTime.AddMinutes(horizon);

            var future = bars
                .Where(x => x.Central > startTime && x.Central <= endTime)
                .OrderBy(x => x.Utc)
                .ToList();

            if (future.Count < Math.Max(1, horizon * 2 - 1))
                continue;

            var maxHigh = future.Max(x => x.High);
            var minLow = future.Min(x => x.Low);
            var upTicks = (maxHigh - startPrice) / 0.25m;
            var downTicks = (startPrice - minLow) / 0.25m;
            var bestTicks = Math.Max(upTicks, downTicks);
            var bestDirection = upTicks >= downTicks ? "Long" : "Short";

            observations.Add(new Obs(
                date, startTime, ZoneAt(startTime), startPrice,
                upTicks, downTicks, bestTicks, bestDirection));
        }
    }

    Console.WriteLine(new string('=', 106));
    Console.WriteLine($"FORWARD HORIZON {horizon} MINUTES");
    Console.WriteLine(new string('=', 106));
    Console.WriteLine($"anchors={observations.Count} anchors/day={(validDates.Count == 0 ? 0 : (decimal)observations.Count / validDates.Count):F2}");
    Console.WriteLine($"medianBestExcursionTicks={Median(observations.Select(x => x.BestTicks)):F1} p75={Percentile(observations.Select(x => x.BestTicks), 0.75m):F1} p90={Percentile(observations.Select(x => x.BestTicks), 0.90m):F1}");
    Console.WriteLine();

    foreach (var threshold in thresholds)
    {
        var qualified = observations.Where(x => x.BestTicks >= threshold).ToList();
        var pct = observations.Count == 0 ? 0m : 100m * qualified.Count / observations.Count;
        var perDay = validDates.Count == 0 ? 0m : (decimal)qualified.Count / validDates.Count;

        Console.WriteLine($"THRESHOLD >= {threshold} TICKS");
        Console.WriteLine($"qualified={qualified.Count}/{observations.Count} ({pct:F1}%) opportunities/day={perDay:F2}");

        foreach (var qty in new[] { 3, 4, 5 })
        {
            // Perfect-capture ceiling only: threshold ticks captured, one round-trip per qualified anchor.
            // Same conservative friction model as V7.9: 2 total slippage ticks + $1.50 RT fees per contract.
            decimal grossPer = threshold * 0.50m * qty;
            decimal frictionPer = (2m * 0.50m * qty) + (1.50m * qty);
            decimal netPer = grossPer - frictionPer;
            decimal theoreticalDaily = netPer * perDay;

            Console.WriteLine($"  qty={qty} thresholdCaptureNetPerOpportunity=${netPer:F2} perfectCaptureDailyCeiling=${theoreticalDaily:F2}");
        }

        Console.WriteLine("  BY ZONE");
        foreach (var g in observations.GroupBy(x => x.Zone).OrderBy(x => x.Key))
        {
            int q = g.Count(x => x.BestTicks >= threshold);
            decimal gp = g.Count() == 0 ? 0 : 100m * q / g.Count();
            decimal qpd = validDates.Count == 0 ? 0 : (decimal)q / validDates.Count;
            Console.WriteLine($"    {g.Key,-30} anchors={g.Count(),5} qualify={q,5} pct={gp,5:F1}% perDay={qpd,6:F2}");
        }

        Console.WriteLine("  BY ENTRY HOUR CT");
        foreach (var g in observations.GroupBy(x => x.Start.Hour).OrderBy(x => x.Key))
        {
            int q = g.Count(x => x.BestTicks >= threshold);
            decimal gp = g.Count() == 0 ? 0 : 100m * q / g.Count();
            Console.WriteLine($"    {g.Key:00}:00 anchors={g.Count(),4} qualify={q,4} pct={gp,5:F1}%");
        }
        Console.WriteLine();
    }
}

Console.WriteLine(new string('=', 106));
Console.WriteLine("INDEPENDENT OPPORTUNITY CLUSTER ESTIMATE");
Console.WriteLine(new string('=', 106));
Console.WriteLine("Greedy non-overlap rule: for each threshold, scan 30-second bars; when either direction reaches");
Console.WriteLine("the threshold within the next 5 minutes, count one opportunity and advance to the first hit bar.");
Console.WriteLine("This remains a hindsight ceiling, but reduces the double-counting inherent in overlapping 5-minute anchors.");
Console.WriteLine();

foreach (var clusterThreshold in thresholds)
{
    var clusters = new List<Cluster>();

    foreach (var kv in bySession)
    {
        var bars = kv.Value;
        int i = 0;

        while (i < bars.Count - 2)
        {
            var start = bars[i];
            var startTime = start.Central.AddSeconds(-30);
            var endTime = startTime.AddMinutes(5);
            var entry = start.Open;

            int hitIndex = -1;
            string direction = "";

            for (int j = i; j < bars.Count && bars[j].Central <= endTime; j++)
            {
                bool up = (bars[j].High - entry) / 0.25m >= clusterThreshold;
                bool down = (entry - bars[j].Low) / 0.25m >= clusterThreshold;

                if (up || down)
                {
                    hitIndex = j;
                    direction = up && down ? "Both" : up ? "Long" : "Short";
                    break;
                }
            }

            if (hitIndex >= 0)
            {
                clusters.Add(new Cluster(kv.Key, startTime, ZoneAt(startTime), direction));
                i = hitIndex + 1;
            }
            else
            {
                i++;
            }
        }
    }

    Console.WriteLine($"THRESHOLD {clusterThreshold} TICKS");
    Console.WriteLine($"independentClusters={clusters.Count}");
    Console.WriteLine($"clusters/day={(validDates.Count == 0 ? 0 : (decimal)clusters.Count / validDates.Count):F2}");

    Console.WriteLine("BY ZONE");
    foreach (var g in clusters.GroupBy(x => x.Zone).OrderBy(x => x.Key))
        Console.WriteLine($"  {g.Key,-30} count={g.Count(),5} perDay={(validDates.Count == 0 ? 0 : (decimal)g.Count() / validDates.Count),6:F2}");

    Console.WriteLine("BY HOUR");
    foreach (var g in clusters.GroupBy(x => x.Start.Hour).OrderBy(x => x.Key))
        Console.WriteLine($"  {g.Key:00}:00 count={g.Count(),5} perDay={(validDates.Count == 0 ? 0 : (decimal)g.Count() / validDates.Count),6:F2}");

    foreach (var qty in new[] { 3, 4, 5 })
    {
        decimal netPer = (clusterThreshold * 0.50m * qty) - ((2m * 0.50m * qty) + (1.50m * qty));
        decimal daily = netPer * (validDates.Count == 0 ? 0 : (decimal)clusters.Count / validDates.Count);
        Console.WriteLine($"qty={qty} perfectIndependentCaptureDailyCeiling=${daily:F2}");
    }

    Console.WriteLine();
}

return 0;

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
            Instrument=p[instrument], Utc=u, Central=DateTime.SpecifyKind(c,DateTimeKind.Unspecified),
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

sealed record SessionValidation(DateTime Date,int BarCount,int CriticalBars,int Gaps,bool IsUsable,string Reason);
sealed record Obs(DateTime SessionDate,DateTime Start,string Zone,decimal StartPrice,decimal UpTicks,decimal DownTicks,decimal BestTicks,string BestDirection);
sealed record Cluster(DateTime SessionDate,DateTime Start,string Zone,string Direction);
