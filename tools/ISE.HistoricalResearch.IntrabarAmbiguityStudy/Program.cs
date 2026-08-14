using System.Globalization;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: <full-day-mnq-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Dataset not found: {path}");
    return 3;
}

var central = ResolveCentral();
var bars = ReadBars(path, central).OrderBy(x => x.Utc).ToList();

if (bars.Count == 0)
    throw new InvalidOperationException("Dataset contains zero bars.");

ComputeIndicators(bars);

var sessions = bars
    .Where(IsInsideResearchWindow)
    .GroupBy(SessionDate)
    .OrderBy(g => g.Key)
    .Where(g => g.Count() >= 1100)
    .Select(g => new Session(g.Key, g.OrderBy(x => x.Utc).ToList()))
    .ToList();

Console.WriteLine("ISE Elite V7.9.2 Intrabar Ambiguity + Execution Resolution Diagnostic");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Purpose: determine whether 1-minute OHLC sequencing materially biases the scalp study.");
Console.WriteLine("Session: 17:00 previous day -> 15:00 current day CT.");
Console.WriteLine("Quantity: 3 MNQ. Tick value: $0.50/contract.");
Console.WriteLine("One position at a time; 1 completed-bar cooldown; repeated-breakout suppression.");
Console.WriteLine("Max hold: 10 one-minute bars.");
Console.WriteLine("Friction: 2 ticks total slippage + $1.50 round-trip commissions/fees per contract.");
Console.WriteLine("When target and stop occur inside the same 1-minute bar, OHLC data cannot reveal which occurred first.");
Console.WriteLine("Worst-case assumes stop first; best-case assumes target first.");
Console.WriteLine($"Bars loaded: {bars.Count}");
Console.WriteLine($"Complete sessions: {sessions.Count}");
Console.WriteLine($"First session: {(sessions.Count == 0 ? "n/a" : sessions[0].Date.ToString("yyyy-MM-dd"))}");
Console.WriteLine($"Last session: {(sessions.Count == 0 ? "n/a" : sessions[^1].Date.ToString("yyyy-MM-dd"))}");
Console.WriteLine();

foreach (var targetTicks in new[] { 30, 40, 50 })
{
    var result = Run(sessions, targetTicks, 30);
    Print(result, targetTicks, 30);
}

return 0;

static StudyResult Run(IReadOnlyList<Session> sessions, int targetTicks, int stopTicks)
{
    var trades = new List<Trade>();
    int repeatedSuppressed = 0;

    foreach (var session in sessions)
    {
        var b = session.Bars;
        int lastExitIndex = -1000;

        for (int i = 6; i < b.Count - 1; i++)
        {
            if (i <= lastExitIndex + 1)
                continue;

            var signal = SignalAt(b, i);
            if (signal.Direction == 0)
                continue;

            if (signal.IsRepeated)
            {
                repeatedSuppressed++;
                continue;
            }

            int entryIndex = i + 1;
            var entryBar = b[entryIndex];
            decimal entry = entryBar.Open;

            decimal targetPoints = targetTicks * 0.25m;
            decimal stopPoints = stopTicks * 0.25m;
            decimal target = signal.Direction > 0 ? entry + targetPoints : entry - targetPoints;
            decimal stop = signal.Direction > 0 ? entry - stopPoints : entry + stopPoints;

            int end = Math.Min(b.Count - 1, entryIndex + 10);

            int exitIndex = end;
            decimal neutralExit = b[end].Close;
            OutcomeKind outcome = OutcomeKind.Time;
            bool firstBarExit = false;
            bool firstBarAmbiguous = false;
            decimal maxFavorablePoints = 0m;
            decimal maxAdversePoints = 0m;

            for (int j = entryIndex; j <= end; j++)
            {
                var bar = b[j];

                decimal favorable = signal.Direction > 0 ? bar.High - entry : entry - bar.Low;
                decimal adverse = signal.Direction > 0 ? entry - bar.Low : bar.High - entry;
                if (favorable > maxFavorablePoints) maxFavorablePoints = favorable;
                if (adverse > maxAdversePoints) maxAdversePoints = adverse;

                bool hitTarget = signal.Direction > 0 ? bar.High >= target : bar.Low <= target;
                bool hitStop = signal.Direction > 0 ? bar.Low <= stop : bar.High >= stop;

                if (hitTarget && hitStop)
                {
                    outcome = OutcomeKind.Ambiguous;
                    exitIndex = j;
                    firstBarExit = j == entryIndex;
                    firstBarAmbiguous = firstBarExit;
                    break;
                }

                if (hitStop)
                {
                    outcome = OutcomeKind.StopOnly;
                    exitIndex = j;
                    firstBarExit = j == entryIndex;
                    break;
                }

                if (hitTarget)
                {
                    outcome = OutcomeKind.TargetOnly;
                    exitIndex = j;
                    firstBarExit = j == entryIndex;
                    break;
                }
            }

            decimal worstExit;
            decimal bestExit;

            switch (outcome)
            {
                case OutcomeKind.TargetOnly:
                    worstExit = target;
                    bestExit = target;
                    break;
                case OutcomeKind.StopOnly:
                    worstExit = stop;
                    bestExit = stop;
                    break;
                case OutcomeKind.Ambiguous:
                    worstExit = stop;
                    bestExit = target;
                    break;
                default:
                    worstExit = neutralExit;
                    bestExit = neutralExit;
                    break;
            }

            decimal worstNet = NetDollars(signal.Direction, entry, worstExit);
            decimal bestNet = NetDollars(signal.Direction, entry, bestExit);

            trades.Add(new Trade(
                session.Date,
                entryBar.Central,
                b[exitIndex].Central,
                signal.Family,
                ZoneAt(entryBar.Central),
                signal.Direction,
                outcome,
                firstBarExit,
                firstBarAmbiguous,
                worstNet,
                bestNet,
                maxFavorablePoints / 0.25m,
                maxAdversePoints / 0.25m,
                exitIndex - entryIndex + 1));

            lastExitIndex = exitIndex;
            i = exitIndex;
        }
    }

    return new StudyResult(trades, repeatedSuppressed, sessions.Count);
}

static decimal NetDollars(int direction, decimal entry, decimal exit)
{
    decimal grossPoints = direction > 0 ? exit - entry : entry - exit;
    decimal grossDollars = grossPoints / 0.25m * 0.50m * 3m;
    decimal friction = (2m * 0.50m * 3m) + (1.50m * 3m);
    return grossDollars - friction;
}

static void Print(StudyResult r, int targetTicks, int stopTicks)
{
    var t = r.Trades;
    int n = t.Count;
    int amb = t.Count(x => x.Outcome == OutcomeKind.Ambiguous);
    int targetOnly = t.Count(x => x.Outcome == OutcomeKind.TargetOnly);
    int stopOnly = t.Count(x => x.Outcome == OutcomeKind.StopOnly);
    int time = t.Count(x => x.Outcome == OutcomeKind.Time);
    int first = t.Count(x => x.FirstBarExit);
    int firstAmb = t.Count(x => x.FirstBarAmbiguous);

    decimal worst = t.Sum(x => x.WorstNet);
    decimal best = t.Sum(x => x.BestNet);
    decimal delta = best - worst;

    decimal ambiguityPct = n == 0 ? 0 : 100m * amb / n;
    decimal firstPct = n == 0 ? 0 : 100m * first / n;
    decimal firstAmbPct = n == 0 ? 0 : 100m * firstAmb / n;

    Console.WriteLine(new string('=', 96));
    Console.WriteLine($"TARGET {targetTicks} / STOP {stopTicks} TICKS");
    Console.WriteLine(new string('=', 96));
    Console.WriteLine($"sessions={r.SessionCount} trades={n} trades/day={(r.SessionCount == 0 ? 0 : (decimal)n / r.SessionCount):F2}");
    Console.WriteLine($"targetOnly={targetOnly} stopOnly={stopOnly} ambiguous={amb} time={time}");
    Console.WriteLine($"ambiguousPct={ambiguityPct:F1}% firstBarExitPct={firstPct:F1}% firstBarAmbiguousPct={firstAmbPct:F1}%");
    Console.WriteLine($"worstCaseNet={worst:F2} worstCaseAvgDay={(r.SessionCount == 0 ? 0 : worst / r.SessionCount):F2} worstCaseAvgTrade={(n == 0 ? 0 : worst / n):F2}");
    Console.WriteLine($"bestCaseNet={best:F2} bestCaseAvgDay={(r.SessionCount == 0 ? 0 : best / r.SessionCount):F2} bestCaseAvgTrade={(n == 0 ? 0 : best / n):F2}");
    Console.WriteLine($"intrabarSequencingDollarRange={delta:F2} rangePerDay={(r.SessionCount == 0 ? 0 : delta / r.SessionCount):F2}");
    Console.WriteLine($"medianHoldBars={Median(t.Select(x => (decimal)x.HoldBars)):F1} avgMFEticks={(n == 0 ? 0 : t.Average(x => x.MfeTicks)):F1} avgMAEticks={(n == 0 ? 0 : t.Average(x => x.MaeTicks)):F1}");
    Console.WriteLine($"repeatedSignalsSuppressed={r.RepeatedSuppressed}");
    Console.WriteLine();

    Console.WriteLine("AMBIGUITY BY SESSION ZONE");
    foreach (var g in t.GroupBy(x => x.Zone).OrderBy(x => x.Key))
    {
        int ga = g.Count(x => x.Outcome == OutcomeKind.Ambiguous);
        decimal gp = 100m * ga / g.Count();
        decimal gw = g.Sum(x => x.WorstNet);
        decimal gb = g.Sum(x => x.BestNet);
        Console.WriteLine($"{g.Key}\ttrades={g.Count()}\tambiguous={ga}\tambPct={gp:F1}%\tworstNet={gw:F2}\tbestNet={gb:F2}\tdelta={(gb-gw):F2}");
    }

    Console.WriteLine("AMBIGUITY BY ENTRY HOUR CT");
    foreach (var g in t.GroupBy(x => x.EntryCentral.Hour).OrderBy(x => x.Key))
    {
        int ga = g.Count(x => x.Outcome == OutcomeKind.Ambiguous);
        decimal gp = 100m * ga / g.Count();
        Console.WriteLine($"{g.Key:00}:00\ttrades={g.Count()}\tambiguous={ga}\tambPct={gp:F1}%");
    }

    Console.WriteLine("AMBIGUITY BY SIGNAL FAMILY");
    foreach (var g in t.GroupBy(x => x.Family).OrderBy(x => x.Key))
    {
        int ga = g.Count(x => x.Outcome == OutcomeKind.Ambiguous);
        decimal gp = 100m * ga / g.Count();
        decimal gw = g.Sum(x => x.WorstNet);
        decimal gb = g.Sum(x => x.BestNet);
        Console.WriteLine($"{g.Key}\ttrades={g.Count()}\tambiguous={ga}\tambPct={gp:F1}%\tworstNet={gw:F2}\tbestNet={gb:F2}\tdelta={(gb-gw):F2}");
    }

    Console.WriteLine();
}

static Signal SignalAt(IReadOnlyList<Bar> b, int i)
{
    var x = b[i];
    var p = b[i - 1];

    if (!x.Atr14.HasValue || !x.Ema9.HasValue || !x.Ema21.HasValue ||
        !x.Ema27.HasValue || !x.Ema63.HasValue || !x.Ema45.HasValue || !x.Ema105.HasValue)
        return Signal.None;

    decimal atr = x.Atr14.Value;
    if (atr <= 0m)
        return Signal.None;

    bool trend1Long = x.Ema9 > x.Ema21;
    bool trend1Short = x.Ema9 < x.Ema21;
    bool trend3Long = x.Ema27 > x.Ema63;
    bool trend3Short = x.Ema27 < x.Ema63;
    bool trend5Long = x.Ema45 > x.Ema105;
    bool trend5Short = x.Ema45 < x.Ema105;

    decimal body = Math.Abs(x.Close - x.Open);

    decimal prior5High = b.Skip(i - 5).Take(5).Max(z => z.High);
    decimal prior5Low = b.Skip(i - 5).Take(5).Min(z => z.Low);

    decimal beforePrior5High = b.Skip(i - 6).Take(5).Max(z => z.High);
    decimal beforePrior5Low = b.Skip(i - 6).Take(5).Min(z => z.Low);

    bool repeatedLong = p.Close > beforePrior5High;
    bool repeatedShort = p.Close < beforePrior5Low;

    if (trend1Long && trend3Long && trend5Long && x.Close > prior5High && body >= 0.35m * atr)
        return new Signal(+1, "TrendImpulse", repeatedLong);

    if (trend1Short && trend3Short && trend5Short && x.Close < prior5Low && body >= 0.35m * atr)
        return new Signal(-1, "TrendImpulse", repeatedShort);

    if (trend3Long && trend5Long && p.Low <= p.Ema9 && p.Close >= p.Ema21 && x.Close > x.Ema9 && x.Close > p.High)
        return new Signal(+1, "PullbackReset", false);

    if (trend3Short && trend5Short && p.High >= p.Ema9 && p.Close <= p.Ema21 && x.Close < x.Ema9 && x.Close < p.Low)
        return new Signal(-1, "PullbackReset", false);

    bool crossUp = p.Ema9 <= p.Ema21 && x.Ema9 > x.Ema21;
    bool crossDown = p.Ema9 >= p.Ema21 && x.Ema9 < x.Ema21;
    decimal fiveSep = Math.Abs(x.Ema45!.Value - x.Ema105!.Value);

    if (crossUp && x.Close > x.Ema21 && fiveSep <= 0.80m * atr)
        return new Signal(+1, "DirectionalTransition", false);

    if (crossDown && x.Close < x.Ema21 && fiveSep <= 0.80m * atr)
        return new Signal(-1, "DirectionalTransition", false);

    bool weak5 = fiveSep <= 0.35m * atr;

    if (weak5 && p.Close < p.Ema21 - 1.25m * atr && x.Close > p.High)
        return new Signal(+1, "MeanReversion", false);

    if (weak5 && p.Close > p.Ema21 + 1.25m * atr && x.Close < p.Low)
        return new Signal(-1, "MeanReversion", false);

    return Signal.None;
}

static string ZoneAt(DateTime central)
{
    var t = central.TimeOfDay;
    if (t >= new TimeSpan(17, 0, 0) && t < new TimeSpan(19, 0, 0)) return "01_EveningReopen";
    if (t >= new TimeSpan(19, 0, 0) && t < new TimeSpan(23, 0, 0)) return "02_AsiaDevelopment";
    if (t >= new TimeSpan(23, 0, 0) || t < new TimeSpan(3, 0, 0)) return "03_OvernightTransition";
    if (t >= new TimeSpan(3, 0, 0) && t < new TimeSpan(6, 30, 0)) return "04_London_USPremarket";
    if (t >= new TimeSpan(6, 30, 0) && t < new TimeSpan(8, 45, 0)) return "05_USPreopen_OpenDevelopment";
    if (t >= new TimeSpan(8, 45, 0) && t < new TimeSpan(9, 5, 0)) return "06_NYTransition_0845_0905";
    if (t >= new TimeSpan(9, 5, 0) && t < new TimeSpan(10, 0, 0)) return "07_NYSecondary_0905_1000";
    if (t >= new TimeSpan(10, 0, 0) && t < new TimeSpan(12, 0, 0)) return "08_LateMorning";
    if (t >= new TimeSpan(12, 0, 0) && t < new TimeSpan(15, 0, 0)) return "09_Midday_EarlyAfternoon";
    return "OutsideResearchWindow";
}

static bool IsInsideResearchWindow(Bar b)
{
    var t = b.Central.TimeOfDay;
    return t >= new TimeSpan(17, 0, 0) || t < new TimeSpan(15, 0, 0);
}

static DateTime SessionDate(Bar b) =>
    b.Central.TimeOfDay >= new TimeSpan(17, 0, 0)
        ? b.Central.Date.AddDays(1)
        : b.Central.Date;

static void ComputeIndicators(List<Bar> bars)
{
    decimal? ema9 = null, ema21 = null, ema27 = null, ema45 = null, ema63 = null, ema105 = null;
    var tr = new Queue<decimal>();
    Bar? prev = null;

    foreach (var b in bars)
    {
        ema9 = Ema(ema9, b.Close, 9);
        ema21 = Ema(ema21, b.Close, 21);
        ema27 = Ema(ema27, b.Close, 27);
        ema45 = Ema(ema45, b.Close, 45);
        ema63 = Ema(ema63, b.Close, 63);
        ema105 = Ema(ema105, b.Close, 105);

        b.Ema9 = ema9;
        b.Ema21 = ema21;
        b.Ema27 = ema27;
        b.Ema45 = ema45;
        b.Ema63 = ema63;
        b.Ema105 = ema105;

        decimal trueRange = prev == null
            ? b.High - b.Low
            : Math.Max(
                b.High - b.Low,
                Math.Max(Math.Abs(b.High - prev.Close), Math.Abs(b.Low - prev.Close)));

        tr.Enqueue(trueRange);
        if (tr.Count > 14) tr.Dequeue();
        if (tr.Count == 14) b.Atr14 = tr.Average();

        prev = b;
    }
}

static decimal Ema(decimal? prior, decimal value, int period)
{
    if (!prior.HasValue) return value;
    decimal alpha = 2m / (period + 1m);
    return alpha * value + (1m - alpha) * prior.Value;
}

static decimal Median(IEnumerable<decimal> values)
{
    var x = values.OrderBy(v => v).ToList();
    if (x.Count == 0) return 0m;
    int mid = x.Count / 2;
    return x.Count % 2 == 1 ? x[mid] : (x[mid - 1] + x[mid]) / 2m;
}

static List<Bar> ReadBars(string path, TimeZoneInfo central)
{
    var lines = File.ReadLines(path).ToList();
    if (lines.Count < 2) return new List<Bar>();

    var header = lines[0].Split('\t');
    var map = header
        .Select((name, index) => new { name, index })
        .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

    int I(string name) =>
        map.TryGetValue(name, out var idx)
            ? idx
            : throw new InvalidOperationException($"Missing TSV column: {name}");

    int instrument = I("instrument");
    int utc = I("timestampUtc");
    int open = I("open");
    int high = I("high");
    int low = I("low");
    int close = I("close");
    int volume = I("volume");

    var result = new List<Bar>(lines.Count - 1);

    foreach (var line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;

        var p = line.Split('\t');
        var u = DateTimeOffset.Parse(p[utc], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var c = TimeZoneInfo.ConvertTime(u, central).DateTime;

        result.Add(new Bar
        {
            Instrument = p[instrument],
            Utc = u,
            Central = DateTime.SpecifyKind(c, DateTimeKind.Unspecified),
            Open = decimal.Parse(p[open], CultureInfo.InvariantCulture),
            High = decimal.Parse(p[high], CultureInfo.InvariantCulture),
            Low = decimal.Parse(p[low], CultureInfo.InvariantCulture),
            Close = decimal.Parse(p[close], CultureInfo.InvariantCulture),
            Volume = long.Parse(p[volume], CultureInfo.InvariantCulture)
        });
    }

    return result;
}

static TimeZoneInfo ResolveCentral()
{
    try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
    catch { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
}

enum OutcomeKind
{
    TargetOnly,
    StopOnly,
    Ambiguous,
    Time
}

sealed class Bar
{
    public string Instrument { get; init; } = "";
    public DateTimeOffset Utc { get; init; }
    public DateTime Central { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }

    public decimal? Ema9 { get; set; }
    public decimal? Ema21 { get; set; }
    public decimal? Ema27 { get; set; }
    public decimal? Ema45 { get; set; }
    public decimal? Ema63 { get; set; }
    public decimal? Ema105 { get; set; }
    public decimal? Atr14 { get; set; }
}

sealed record Session(DateTime Date, List<Bar> Bars);

readonly record struct Signal(int Direction, string Family, bool IsRepeated)
{
    public static Signal None => new(0, "", false);
}

sealed record Trade(
    DateTime SessionDate,
    DateTime EntryCentral,
    DateTime ExitCentral,
    string Family,
    string Zone,
    int Direction,
    OutcomeKind Outcome,
    bool FirstBarExit,
    bool FirstBarAmbiguous,
    decimal WorstNet,
    decimal BestNet,
    decimal MfeTicks,
    decimal MaeTicks,
    int HoldBars);

sealed record StudyResult(
    List<Trade> Trades,
    int RepeatedSuppressed,
    int SessionCount);
