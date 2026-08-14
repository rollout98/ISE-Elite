using System.Globalization;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: <30-second-full-session-mnq-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"Dataset not found: {path}");
    return 3;
}

var central = ResolveCentral();
var bars30 = ReadBars(path, central).OrderBy(x => x.Utc).ToList();

if (bars30.Count == 0)
    throw new InvalidOperationException("30-second dataset contains zero bars.");

if (bars30.Any(x => x.IntervalSeconds != 30))
    throw new InvalidOperationException("V7.9.3 requires intervalSeconds=30 for every row.");

if (bars30.Any(x => !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("V7.9.3 requires MNQ-only data.");

var researchFrom = new DateTime(2026, 6, 1);
var researchTo = new DateTime(2026, 7, 31);

// Validate the underlying 30-second sessions BEFORE deriving 1-minute signal bars.
// The critical 06:00-11:00 CT window must contain all 600 expected half-minute bars.
// Legitimate shortened exchange sessions are allowed when the critical window is complete
// and there are no internal >30-second gaps in the observed session.
var validation = ValidateThirtySecondSessions(bars30, researchFrom, researchTo);
var validSessionDates = validation.Where(x => x.IsUsable).Select(x => x.Date).ToHashSet();

var bars1 = AggregateToOneMinute(bars30);
ComputeIndicators(bars1);

var sessions = bars1
    .Where(IsInsideResearchWindow)
    .GroupBy(SessionDate)
    .OrderBy(g => g.Key)
    .Where(g => g.Key >= researchFrom && g.Key <= researchTo)
    .Where(g => validSessionDates.Contains(g.Key))
    .Select(g => new Session(g.Key, g.OrderBy(x => x.Utc).ToList()))
    .ToList();

Console.WriteLine("ISE Elite V7.9.3R2 30-Second Execution-Resolution Study");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Signals: causal 1-minute bars, same V7.9 opportunity families.");
Console.WriteLine("Execution: underlying 30-second MNQ bars.");
Console.WriteLine("Session: 17:00 previous day -> 15:00 current day CT.");
Console.WriteLine("Quantity: 3 MNQ; tick value $0.50/contract.");
Console.WriteLine("Targets: 30/40/50 ticks; stop: 30 ticks.");
Console.WriteLine("Maximum holding horizon: 10 minutes.");
Console.WriteLine("One position at a time; one completed 1-minute cooldown; repeated-breakout suppression.");
Console.WriteLine("30-second same-bar target/stop conflicts remain explicitly ambiguous.");
Console.WriteLine("Worst-case assumes stop first; best-case assumes target first.");
Console.WriteLine("Friction: 2 ticks total slippage + $1.50 round-trip commissions/fees per contract.");
Console.WriteLine($"30s bars loaded: {bars30.Count}");
Console.WriteLine($"1m bars derived: {bars1.Count}");
Console.WriteLine($"Research sessions observed: {validation.Count}");
Console.WriteLine($"Usable research sessions: {sessions.Count}");
Console.WriteLine($"Excluded research sessions: {validation.Count(x => !x.IsUsable)}");
foreach (var x in validation.Where(x => !x.IsUsable))
    Console.WriteLine($"EXCLUDED SESSION {x.Date:yyyy-MM-dd} reason={x.Reason} bars={x.BarCount} criticalBars={x.CriticalBars} gapsGt30s={x.GapsGt30s}");
Console.WriteLine($"First session: {(sessions.Count == 0 ? "n/a" : sessions[0].Date.ToString("yyyy-MM-dd"))}");
Console.WriteLine($"Last session: {(sessions.Count == 0 ? "n/a" : sessions[^1].Date.ToString("yyyy-MM-dd"))}");
Console.WriteLine();

foreach (var targetTicks in new[] { 30, 40, 50 })
{
    var result = Run(sessions, bars30, targetTicks, 30);
    Print(result, targetTicks, 30);
}

return 0;

static StudyResult Run(
    IReadOnlyList<Session> sessions,
    IReadOnlyList<Bar30> bars30,
    int targetTicks,
    int stopTicks)
{
    var trades = new List<Trade>();
    var bySession30 = bars30
        .Where(IsInsideResearchWindow30)
        .GroupBy(SessionDate30)
        .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Utc).ToList());

    int repeatedSuppressed = 0;

    foreach (var session in sessions)
    {
        if (!bySession30.TryGetValue(session.Date, out var execBars))
            continue;

        var signalBars = session.Bars;
        int lastExitSignalIndex = -1000;

        for (int i = 6; i < signalBars.Count - 1; i++)
        {
            if (i <= lastExitSignalIndex + 1)
                continue;

            var signal = SignalAt(signalBars, i);
            if (signal.Direction == 0)
                continue;

            if (signal.IsRepeated)
            {
                repeatedSuppressed++;
                continue;
            }

            // NinjaTrader time-bar timestamps are bar-end timestamps.
            // A 1-minute signal bar ending at T becomes actionable immediately after T;
            // the first executable 30-second bar ends at T+30s and opens at T.
            var signalCloseTime = signalBars[i].Central;
            var expectedFirstExecEnd = signalCloseTime.AddSeconds(30);

            var firstExec = execBars.FirstOrDefault(x => x.Central == expectedFirstExecEnd);

            if (firstExec == null)
                continue;

            decimal entry = firstExec.Open;
            decimal target = signal.Direction > 0
                ? entry + targetTicks * 0.25m
                : entry - targetTicks * 0.25m;
            decimal stop = signal.Direction > 0
                ? entry - stopTicks * 0.25m
                : entry + stopTicks * 0.25m;

            var horizonEnd = signalCloseTime.AddMinutes(10);
            var path = execBars
                .Where(x => x.Central >= firstExec.Central && x.Central <= horizonEnd)
                .OrderBy(x => x.Utc)
                .ToList();

            if (path.Count == 0)
                continue;

            OutcomeKind outcome = OutcomeKind.Time;
            Bar30 exitBar = path[^1];
            decimal neutralExit = exitBar.Close;
            decimal mfePoints = 0m;
            decimal maePoints = 0m;
            bool first30SecondExit = false;

            foreach (var bar in path)
            {
                decimal favorable = signal.Direction > 0 ? bar.High - entry : entry - bar.Low;
                decimal adverse = signal.Direction > 0 ? entry - bar.Low : bar.High - entry;
                if (favorable > mfePoints) mfePoints = favorable;
                if (adverse > maePoints) maePoints = adverse;

                bool hitTarget = signal.Direction > 0 ? bar.High >= target : bar.Low <= target;
                bool hitStop = signal.Direction > 0 ? bar.Low <= stop : bar.High >= stop;

                if (hitTarget && hitStop)
                {
                    outcome = OutcomeKind.Ambiguous;
                    exitBar = bar;
                    first30SecondExit = bar.Utc == firstExec.Utc;
                    break;
                }

                if (hitStop)
                {
                    outcome = OutcomeKind.StopOnly;
                    exitBar = bar;
                    first30SecondExit = bar.Utc == firstExec.Utc;
                    break;
                }

                if (hitTarget)
                {
                    outcome = OutcomeKind.TargetOnly;
                    exitBar = bar;
                    first30SecondExit = bar.Utc == firstExec.Utc;
                    break;
                }
            }

            decimal worstExit = outcome switch
            {
                OutcomeKind.TargetOnly => target,
                OutcomeKind.StopOnly => stop,
                OutcomeKind.Ambiguous => stop,
                _ => neutralExit
            };

            decimal bestExit = outcome switch
            {
                OutcomeKind.TargetOnly => target,
                OutcomeKind.StopOnly => stop,
                OutcomeKind.Ambiguous => target,
                _ => neutralExit
            };

            decimal worstNet = NetDollars(signal.Direction, entry, worstExit);
            decimal bestNet = NetDollars(signal.Direction, entry, bestExit);

            trades.Add(new Trade(
                session.Date,
                firstExec.Central,
                exitBar.Central,
                signal.Family,
                ZoneAt(firstExec.Central),
                outcome,
                first30SecondExit,
                worstNet,
                bestNet,
                mfePoints / 0.25m,
                maePoints / 0.25m,
                Math.Max(0.5m, (decimal)(exitBar.Central - firstExec.Central).TotalSeconds / 60m + 0.5m)));

            // Convert execution exit time back to the latest completed/active one-minute index
            // so the research engine remains one-position-at-a-time.
            var exitMinute = new DateTime(
                exitBar.Central.Year,
                exitBar.Central.Month,
                exitBar.Central.Day,
                exitBar.Central.Hour,
                exitBar.Central.Minute,
                0,
                DateTimeKind.Unspecified);

            var idx = signalBars.FindLastIndex(x => x.Central <= exitMinute);
            lastExitSignalIndex = Math.Max(i, idx);
            i = lastExitSignalIndex;
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
    int first = t.Count(x => x.First30SecondExit);

    decimal worst = t.Sum(x => x.WorstNet);
    decimal best = t.Sum(x => x.BestNet);
    decimal range = best - worst;
    decimal ambPct = n == 0 ? 0 : 100m * amb / n;
    decimal firstPct = n == 0 ? 0 : 100m * first / n;

    var worstDaily = r.SessionCount == 0
        ? new List<decimal>()
        : t.GroupBy(x => x.SessionDate).Select(g => g.Sum(x => x.WorstNet)).ToList();

    Console.WriteLine(new string('=', 98));
    Console.WriteLine($"TARGET {targetTicks} / STOP {stopTicks} TICKS -- 30-SECOND EXECUTION");
    Console.WriteLine(new string('=', 98));
    Console.WriteLine($"sessions={r.SessionCount} trades={n} trades/day={(r.SessionCount == 0 ? 0 : (decimal)n / r.SessionCount):F2}");
    Console.WriteLine($"targetOnly={targetOnly} stopOnly={stopOnly} ambiguous30s={amb} time={time}");
    Console.WriteLine($"ambiguous30sPct={ambPct:F2}% first30SecondExitPct={firstPct:F1}%");
    Console.WriteLine($"worstCaseNet={worst:F2} worstCaseAvgDay={(r.SessionCount == 0 ? 0 : worst / r.SessionCount):F2} worstCaseAvgTrade={(n == 0 ? 0 : worst / n):F2}");
    Console.WriteLine($"bestCaseNet={best:F2} bestCaseAvgDay={(r.SessionCount == 0 ? 0 : best / r.SessionCount):F2} bestCaseAvgTrade={(n == 0 ? 0 : best / n):F2}");
    Console.WriteLine($"30sSequencingDollarRange={range:F2} rangePerDay={(r.SessionCount == 0 ? 0 : range / r.SessionCount):F2}");
    Console.WriteLine($"medianHoldMinutes={Median(t.Select(x => x.HoldMinutes)):F2} avgMFEticks={(n == 0 ? 0 : t.Average(x => x.MfeTicks)):F1} avgMAEticks={(n == 0 ? 0 : t.Average(x => x.MaeTicks)):F1}");
    Console.WriteLine($"positiveDaysWorstCase={(worstDaily.Count == 0 ? 0 : worstDaily.Count(x => x > 0))}/{r.SessionCount}");
    Console.WriteLine($"repeatedSignalsSuppressed={r.RepeatedSuppressed}");
    Console.WriteLine();

    Console.WriteLine("BY SESSION ZONE");
    foreach (var g in t.GroupBy(x => x.Zone).OrderBy(x => x.Key))
    {
        int ga = g.Count(x => x.Outcome == OutcomeKind.Ambiguous);
        decimal gp = 100m * ga / g.Count();
        decimal gw = g.Sum(x => x.WorstNet);
        decimal gb = g.Sum(x => x.BestNet);
        Console.WriteLine($"{g.Key}\ttrades={g.Count()}\tamb30s={ga}\tambPct={gp:F1}%\tworstNet={gw:F2}\tbestNet={gb:F2}\trange={(gb-gw):F2}");
    }

    Console.WriteLine("BY ENTRY HOUR CT");
    foreach (var g in t.GroupBy(x => x.EntryCentral.Hour).OrderBy(x => x.Key))
    {
        int ga = g.Count(x => x.Outcome == OutcomeKind.Ambiguous);
        decimal gp = 100m * ga / g.Count();
        Console.WriteLine($"{g.Key:00}:00\ttrades={g.Count()}\tamb30s={ga}\tambPct={gp:F1}%\tworstNet={g.Sum(x => x.WorstNet):F2}");
    }

    Console.WriteLine("BY SIGNAL FAMILY");
    foreach (var g in t.GroupBy(x => x.Family).OrderBy(x => x.Key))
    {
        int ga = g.Count(x => x.Outcome == OutcomeKind.Ambiguous);
        decimal gp = 100m * ga / g.Count();
        Console.WriteLine($"{g.Key}\ttrades={g.Count()}\tamb30s={ga}\tambPct={gp:F1}%\tworstNet={g.Sum(x => x.WorstNet):F2}\tbestNet={g.Sum(x => x.BestNet):F2}");
    }

    Console.WriteLine();
}

static List<Bar1> AggregateToOneMinute(IReadOnlyList<Bar30> bars30)
{
    // NinjaTrader timestamps historical time bars by BAR END.
    // 17:00:30 + 17:01:00 belong to the 1-minute bar ending 17:01:00.
    return bars30
        .GroupBy(x =>
        {
            var t = x.Central;
            if (t.Second == 0)
                return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Unspecified);

            var floored = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Unspecified);
            return floored.AddMinutes(1);
        })
        .OrderBy(g => g.Key)
        .Where(g => g.Count() == 2)
        .Select(g =>
        {
            var x = g.OrderBy(z => z.Utc).ToList();
            return new Bar1
            {
                Instrument = x[0].Instrument,
                Utc = x[^1].Utc,
                Central = g.Key,
                Open = x[0].Open,
                High = x.Max(z => z.High),
                Low = x.Min(z => z.Low),
                Close = x[^1].Close,
                Volume = x.Sum(z => z.Volume)
            };
        })
        .ToList();
}

static List<SessionValidation> ValidateThirtySecondSessions(
    IReadOnlyList<Bar30> bars,
    DateTime researchFrom,
    DateTime researchTo)
{
    const int expectedCriticalBars = 600;
    var result = new List<SessionValidation>();

    var grouped = bars
        .Where(IsInsideResearchWindow30)
        .GroupBy(SessionDate30)
        .Where(g => g.Key >= researchFrom && g.Key <= researchTo)
        .OrderBy(g => g.Key);

    foreach (var g in grouped)
    {
        var ordered = g.OrderBy(x => x.Utc).ToList();
        var critical = ordered
            .Where(x => x.Central.TimeOfDay >= new TimeSpan(6, 0, 0)
                     && x.Central.TimeOfDay < new TimeSpan(11, 0, 0))
            .ToList();

        int gaps = 0;
        for (int i = 1; i < ordered.Count; i++)
        {
            if ((ordered[i].Central - ordered[i - 1].Central).TotalSeconds > 45d)
                gaps++;
        }

        bool criticalComplete = critical.Count == expectedCriticalBars;
        bool usable = criticalComplete && gaps == 0;

        string reason = usable
            ? "OK"
            : !criticalComplete
                ? $"CriticalWindowIncomplete({critical.Count}/{expectedCriticalBars})"
                : $"InternalGaps({gaps})";

        result.Add(new SessionValidation(g.Key, ordered.Count, critical.Count, gaps, usable, reason));
    }

    return result;
}

static Signal SignalAt(IReadOnlyList<Bar1> b, int i)
{
    var x = b[i];
    var p = b[i - 1];

    if (!x.Atr14.HasValue || !x.Ema9.HasValue || !x.Ema21.HasValue ||
        !x.Ema27.HasValue || !x.Ema63.HasValue || !x.Ema45.HasValue || !x.Ema105.HasValue)
        return Signal.None;

    decimal atr = x.Atr14.Value;
    if (atr <= 0m) return Signal.None;

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

static bool IsInsideResearchWindow(Bar1 b)
{
    var t = b.Central.TimeOfDay;
    return t >= new TimeSpan(17, 0, 0) || t < new TimeSpan(15, 0, 0);
}

static bool IsInsideResearchWindow30(Bar30 b)
{
    var t = b.Central.TimeOfDay;
    return t >= new TimeSpan(17, 0, 0) || t < new TimeSpan(15, 0, 0);
}

static DateTime SessionDate(Bar1 b) =>
    b.Central.TimeOfDay >= new TimeSpan(17, 0, 0)
        ? b.Central.Date.AddDays(1)
        : b.Central.Date;

static DateTime SessionDate30(Bar30 b) =>
    b.Central.TimeOfDay >= new TimeSpan(17, 0, 0)
        ? b.Central.Date.AddDays(1)
        : b.Central.Date;

static void ComputeIndicators(List<Bar1> bars)
{
    decimal? ema9 = null, ema21 = null, ema27 = null, ema45 = null, ema63 = null, ema105 = null;
    var tr = new Queue<decimal>();
    Bar1? prev = null;

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
            : Math.Max(b.High - b.Low, Math.Max(Math.Abs(b.High - prev.Close), Math.Abs(b.Low - prev.Close)));

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

static List<Bar30> ReadBars(string path, TimeZoneInfo central)
{
    var lines = File.ReadLines(path).ToList();
    if (lines.Count < 2) return new List<Bar30>();

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
    int interval = I("intervalSeconds");
    int open = I("open");
    int high = I("high");
    int low = I("low");
    int close = I("close");
    int volume = I("volume");

    var result = new List<Bar30>(lines.Count - 1);

    foreach (var line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;

        var p = line.Split('\t');
        var u = DateTimeOffset.Parse(p[utc], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var c = TimeZoneInfo.ConvertTime(u, central).DateTime;

        result.Add(new Bar30
        {
            Instrument = p[instrument],
            Utc = u,
            Central = DateTime.SpecifyKind(c, DateTimeKind.Unspecified),
            IntervalSeconds = int.Parse(p[interval], CultureInfo.InvariantCulture),
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

sealed class Bar30
{
    public string Instrument { get; init; } = "";
    public DateTimeOffset Utc { get; init; }
    public DateTime Central { get; init; }
    public int IntervalSeconds { get; init; }
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public long Volume { get; init; }
}

sealed class Bar1
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

sealed record Session(DateTime Date, List<Bar1> Bars);

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
    OutcomeKind Outcome,
    bool First30SecondExit,
    decimal WorstNet,
    decimal BestNet,
    decimal MfeTicks,
    decimal MaeTicks,
    decimal HoldMinutes);

sealed record SessionValidation(
    DateTime Date,
    int BarCount,
    int CriticalBars,
    int GapsGt30s,
    bool IsUsable,
    string Reason);

sealed record StudyResult(
    List<Trade> Trades,
    int RepeatedSuppressed,
    int SessionCount);
