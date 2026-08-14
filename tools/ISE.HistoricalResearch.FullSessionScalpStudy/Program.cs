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
if (bars.Count == 0) throw new InvalidOperationException("Dataset contains zero bars.");
if (bars.Any(x => !x.Instrument.StartsWith("MNQ", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException("V7.9 requires MNQ-only data.");

ComputeIndicators(bars);

var sessionBars = bars
    .Where(IsInsideResearchWindow)
    .GroupBy(SessionDate)
    .OrderBy(g => g.Key)
    .Where(g => g.Count() >= 1100)
    .Select(g => new Session(g.Key, g.OrderBy(x => x.Utc).ToList()))
    .ToList();

Console.WriteLine("ISE Elite V7.9 Full-Session Scalp Engine - Opportunity Atlas + Compliance Governor");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Session: 17:00 previous day -> 15:00 current day CT");
Console.WriteLine("Primary execution: 1 minute.");
Console.WriteLine("Multi-horizon context: causal 1m EMA equivalents 9/21, 27/63, 45/105.");
Console.WriteLine("Quantity: 3 MNQ. Tick value: $0.50/contract.");
Console.WriteLine("Targets: 30/40/50 ticks. Fixed initial stop: 30 ticks.");
Console.WriteLine("Max hold: 10 one-minute bars.");
Console.WriteLine("Discovery pass: no daily trade cap; one position; 1 completed-bar cooldown; repeated-breakout suppression.");
Console.WriteLine("Governed pass: max 15 trades/session; max 2 per zone; 2 completed-bar cooldown; rolling entry-rate limits.");
Console.WriteLine("Governed rate limits: <=2 entries/5m, <=4/15m, <=6/60m.");
Console.WriteLine("Default friction: 2 ticks total slippage + $1.50 round-trip commissions/fees per contract.");
Console.WriteLine("V7.9 correction: session scan begins at bar 6, not bar 110, because indicators are precomputed causally across continuous history.");
Console.WriteLine("V7.9 correction: repeated-breakout detection compares the prior bar against the five bars BEFORE it.");
Console.WriteLine($"Bars loaded: {bars.Count}");
Console.WriteLine($"Complete-enough sessions: {sessionBars.Count}");
Console.WriteLine($"First session: {(sessionBars.Count == 0 ? "n/a" : sessionBars[0].Date.ToString("yyyy-MM-dd"))}");
Console.WriteLine($"Last session: {(sessionBars.Count == 0 ? "n/a" : sessionBars[^1].Date.ToString("yyyy-MM-dd"))}");
Console.WriteLine();

foreach (var targetTicks in new[] { 30, 40, 50 })
{
    Console.WriteLine(new string('=', 92));
    Console.WriteLine($"TARGET {targetTicks} TICKS");
    Console.WriteLine(new string('=', 92));

    var discovery = Run(sessionBars, targetTicks, 30, governed: false);
    PrintSummary(discovery, targetTicks, "DISCOVERY / UNCAPPPED OPPORTUNITY ATLAS");

    var governed = Run(sessionBars, targetTicks, 30, governed: true);
    PrintSummary(governed, targetTicks, "PROP-COMPLIANCE GOVERNED");
}

return 0;

static StudyResult Run(IReadOnlyList<Session> sessions, int targetTicks, int stopTicks, bool governed)
{
    var trades = new List<Trade>();
    var suppression = new SuppressionCounts();

    foreach (var session in sessions)
    {
        var sb = session.Bars;
        var lastExitIndex = -1000;
        var sessionTradeCount = 0;
        var zoneCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var acceptedEntryTimes = new List<DateTime>();

        // i=6 is sufficient for the signal's two five-bar lookbacks.
        // EMA/ATR values were already computed causally on the continuous input stream.
        for (int i = 6; i < sb.Count - 1; i++)
        {
            var cooldownBars = governed ? 2 : 1;
            if (i <= lastExitIndex + cooldownBars)
            {
                suppression.Cooldown++;
                continue;
            }

            var signal = SignalAt(sb, i);
            if (signal.Direction == 0) continue;

            suppression.RawSignals++;

            if (signal.IsRepeated)
            {
                suppression.RepeatedSignal++;
                continue;
            }

            var entryIndex = i + 1;
            var entryBar = sb[entryIndex];
            var zone = ZoneAt(entryBar.Central);

            if (governed)
            {
                if (sessionTradeCount >= 15)
                {
                    suppression.SessionCap++;
                    continue;
                }

                var zoneCount = zoneCounts.TryGetValue(zone, out var zc) ? zc : 0;
                if (zoneCount >= 2)
                {
                    suppression.ZoneCap++;
                    continue;
                }

                if (CountWithin(acceptedEntryTimes, entryBar.Central, 5) >= 2)
                {
                    suppression.Rolling5m++;
                    continue;
                }

                if (CountWithin(acceptedEntryTimes, entryBar.Central, 15) >= 4)
                {
                    suppression.Rolling15m++;
                    continue;
                }

                if (CountWithin(acceptedEntryTimes, entryBar.Central, 60) >= 6)
                {
                    suppression.Rolling60m++;
                    continue;
                }
            }

            var entry = entryBar.Open;
            var maxHoldEnd = Math.Min(sb.Count - 1, entryIndex + 10);

            var targetPoints = targetTicks * 0.25m;
            var stopPoints = stopTicks * 0.25m;
            decimal target = signal.Direction > 0 ? entry + targetPoints : entry - targetPoints;
            decimal stop = signal.Direction > 0 ? entry - stopPoints : entry + stopPoints;

            decimal mfePoints = 0m, maePoints = 0m;
            int exitIndex = maxHoldEnd;
            decimal exit = sb[maxHoldEnd].Close;
            string exitReason = "Time";

            for (int j = entryIndex; j <= maxHoldEnd; j++)
            {
                var bar = sb[j];
                decimal favorable = signal.Direction > 0 ? bar.High - entry : entry - bar.Low;
                decimal adverse = signal.Direction > 0 ? entry - bar.Low : bar.High - entry;
                if (favorable > mfePoints) mfePoints = favorable;
                if (adverse > maePoints) maePoints = adverse;

                bool hitTarget = signal.Direction > 0 ? bar.High >= target : bar.Low <= target;
                bool hitStop = signal.Direction > 0 ? bar.Low <= stop : bar.High >= stop;

                // Conservative same-bar ambiguity handling: if both were touched, assume the stop occurred first.
                if (hitTarget && hitStop)
                {
                    exitIndex = j;
                    exit = stop;
                    exitReason = "StopAmbiguous";
                    break;
                }

                if (hitStop)
                {
                    exitIndex = j;
                    exit = stop;
                    exitReason = "Stop";
                    break;
                }

                if (hitTarget)
                {
                    exitIndex = j;
                    exit = target;
                    exitReason = "Target";
                    break;
                }
            }

            decimal grossPoints = signal.Direction > 0 ? exit - entry : entry - exit;
            decimal grossDollars = grossPoints / 0.25m * 0.50m * 3m;
            decimal friction = (2m * 0.50m * 3m) + (1.50m * 3m);
            decimal netDollars = grossDollars - friction;

            decimal postExitMfePoints = 0m;
            int postEnd = Math.Min(sb.Count - 1, exitIndex + 10);
            for (int j = exitIndex + 1; j <= postEnd; j++)
            {
                var bar = sb[j];
                var favorable = signal.Direction > 0 ? bar.High - exit : exit - bar.Low;
                if (favorable > postExitMfePoints) postExitMfePoints = favorable;
            }

            trades.Add(new Trade(
                session.Date,
                entryBar.Central,
                sb[exitIndex].Central,
                signal.Direction,
                signal.Family,
                zone,
                entry,
                exit,
                grossDollars,
                netDollars,
                mfePoints / 0.25m,
                maePoints / 0.25m,
                postExitMfePoints / 0.25m,
                exitIndex - entryIndex + 1,
                exitReason));

            suppression.Accepted++;
            sessionTradeCount++;
            zoneCounts[zone] = zoneCounts.TryGetValue(zone, out var old) ? old + 1 : 1;
            acceptedEntryTimes.Add(entryBar.Central);

            lastExitIndex = exitIndex;
            i = exitIndex;
        }
    }

    return new StudyResult(
        trades,
        suppression,
        sessions.Count,
        sessions.Select(x => x.Date).ToList(),
        governed);
}

static Signal SignalAt(IReadOnlyList<Bar> b, int i)
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

    // Breakout range for the current bar: i-5 .. i-1.
    decimal prior5High = b.Skip(i - 5).Take(5).Max(z => z.High);
    decimal prior5Low = b.Skip(i - 5).Take(5).Min(z => z.Low);

    // Repeated-breakout range for the PRIOR bar: i-6 .. i-2.
    // The original baseline accidentally included p itself, which made
    // p.Close > high (or p.Close < low) effectively impossible.
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

static int CountWithin(IReadOnlyList<DateTime> entries, DateTime now, int minutes) =>
    entries.Count(x => now >= x && (now - x).TotalMinutes <= minutes);

static void PrintSummary(StudyResult r, int targetTicks, string label)
{
    var t = r.Trades;
    var sessionsWithTrades = t.Select(x => x.SessionDate).Distinct().OrderBy(x => x).ToList();
    var daily = r.SessionDates.ToDictionary(x => x, _ => 0m);

    foreach (var g in t.GroupBy(x => x.SessionDate))
        daily[g.Key] = g.Sum(x => x.NetDollars);

    decimal totalNet = t.Sum(x => x.NetDollars);
    decimal totalGross = t.Sum(x => x.GrossDollars);
    decimal avgTrade = t.Count == 0 ? 0 : t.Average(x => x.NetDollars);
    decimal winRate = t.Count == 0 ? 0 : 100m * t.Count(x => x.NetDollars > 0) / t.Count;
    decimal avgDaily = r.SessionCount == 0 ? 0 : totalNet / r.SessionCount;
    decimal medianDaily = Median(daily.Values);
    decimal avgWin = t.Where(x => x.NetDollars > 0).Select(x => x.NetDollars).DefaultIfEmpty(0).Average();
    decimal avgLoss = t.Where(x => x.NetDollars <= 0).Select(x => x.NetDollars).DefaultIfEmpty(0).Average();
    decimal grossProfit = t.Where(x => x.NetDollars > 0).Sum(x => x.NetDollars);
    decimal grossLossAbs = Math.Abs(t.Where(x => x.NetDollars < 0).Sum(x => x.NetDollars));
    decimal profitFactor = grossLossAbs == 0 ? 0 : grossProfit / grossLossAbs;
    decimal maxDD = MaxDrawdown(daily.OrderBy(x => x.Key).Select(x => x.Value).ToList());
    int maxConsecLosses = MaxConsecutiveLosses(t);
    int positiveDays = daily.Count(x => x.Value > 0);
    decimal positiveDayPct = r.SessionCount == 0 ? 0 : 100m * positiveDays / r.SessionCount;
    decimal targetHitPct = t.Count == 0 ? 0 : 100m * t.Count(x => x.ExitReason == "Target") / t.Count;
    decimal stopPct = t.Count == 0 ? 0 : 100m * t.Count(x => x.ExitReason == "Stop" || x.ExitReason == "StopAmbiguous") / t.Count;
    decimal timePct = t.Count == 0 ? 0 : 100m * t.Count(x => x.ExitReason == "Time") / t.Count;
    decimal avgMfe = t.Count == 0 ? 0 : t.Average(x => x.MfeTicks);
    decimal avgMae = t.Count == 0 ? 0 : t.Average(x => x.MaeTicks);
    decimal avgPostExit = t.Count == 0 ? 0 : t.Average(x => x.PostExitMfeTicks);

    var perSessionCounts = r.SessionDates
        .Select(d => (decimal)t.Count(x => x.SessionDate == d))
        .OrderBy(x => x)
        .ToList();

    Console.WriteLine();
    Console.WriteLine($"--- {label} ---");
    Console.WriteLine($"targetTicks={targetTicks} sessions={r.SessionCount} sessionsWithTrades={sessionsWithTrades.Count}");
    Console.WriteLine($"trades={t.Count} tradesPerSession={(r.SessionCount == 0 ? 0 : (decimal)t.Count / r.SessionCount):F2}");
    Console.WriteLine($"tradeCountP25={Percentile(perSessionCounts, 0.25m):F1} median={Median(perSessionCounts):F1} p75={Percentile(perSessionCounts, 0.75m):F1} max={(perSessionCounts.Count == 0 ? 0 : perSessionCounts.Max()):F0}");
    Console.WriteLine($"grossPnL={totalGross:F2} netPnL={totalNet:F2} avgDailyNet={avgDaily:F2} medianDailyNet={medianDaily:F2} avgTradeNet={avgTrade:F2}");
    Console.WriteLine($"winRate={winRate:F1}% netProfitFactor={profitFactor:F2} avgWin={avgWin:F2} avgLoss={avgLoss:F2}");
    Console.WriteLine($"positiveDays={positiveDays}/{r.SessionCount} ({positiveDayPct:F1}%) bestDay={(daily.Count == 0 ? 0 : daily.Max(x => x.Value)):F2} worstDay={(daily.Count == 0 ? 0 : daily.Min(x => x.Value)):F2}");
    Console.WriteLine($"maxDailyCloseDD={maxDD:F2} maxConsecutiveLosses={maxConsecLosses}");
    Console.WriteLine($"days>=300={daily.Count(x => x.Value >= 300m)} days>=500={daily.Count(x => x.Value >= 500m)} days>=1000={daily.Count(x => x.Value >= 1000m)}");
    Console.WriteLine($"targetHit={targetHitPct:F1}% stopExit={stopPct:F1}% timeExit={timePct:F1}%");
    Console.WriteLine($"avgMFEticks={avgMfe:F1} avgMAEticks={avgMae:F1} avgPostExitMFEticks={avgPostExit:F1}");
    Console.WriteLine($"medianHoldBars={Median(t.Select(x => (decimal)x.HoldBars)):F1} medianEntryGapMinutes={MedianEntryGap(t):F1}");
    Console.WriteLine($"maxEntriesRolling5m={MaxEntriesRolling(t, 5)} maxEntriesRolling15m={MaxEntriesRolling(t, 15)} maxEntriesRolling60m={MaxEntriesRolling(t, 60)}");

    var s = r.Suppression;
    Console.WriteLine($"signalsRaw={s.RawSignals} accepted={s.Accepted} repeatedSuppressed={s.RepeatedSignal} cooldownBarsSkipped={s.Cooldown}");
    if (r.Governed)
    {
        Console.WriteLine($"governorSuppressed: sessionCap={s.SessionCap} zoneCap={s.ZoneCap} rolling5m={s.Rolling5m} rolling15m={s.Rolling15m} rolling60m={s.Rolling60m}");
    }

    Console.WriteLine("BY FAMILY");
    foreach (var g in t.GroupBy(x => x.Family).OrderBy(x => x.Key))
    {
        Console.WriteLine(
            $"{g.Key}\ttrades={g.Count()}\tnet={g.Sum(x => x.NetDollars):F2}\tavg={g.Average(x => x.NetDollars):F2}" +
            $"\twin={(100m * g.Count(x => x.NetDollars > 0) / g.Count()):F1}%\tPF={ProfitFactor(g):F2}" +
            $"\tMFE={g.Average(x => x.MfeTicks):F1}\tMAE={g.Average(x => x.MaeTicks):F1}" +
            $"\tpostExitMFE={g.Average(x => x.PostExitMfeTicks):F1}");
    }

    Console.WriteLine("BY ENTRY HOUR CT");
    foreach (var g in t.GroupBy(x => x.EntryCentral.Hour).OrderBy(x => x.Key))
    {
        Console.WriteLine(
            $"{g.Key:00}:00\ttrades={g.Count()}\tnet={g.Sum(x => x.NetDollars):F2}\tavg={g.Average(x => x.NetDollars):F2}" +
            $"\twin={(100m * g.Count(x => x.NetDollars > 0) / g.Count()):F1}%\tPF={ProfitFactor(g):F2}");
    }

    Console.WriteLine("BY SESSION ZONE CT");
    foreach (var g in t.GroupBy(x => x.Zone).OrderBy(x => x.Key))
    {
        Console.WriteLine(
            $"{g.Key}\ttrades={g.Count()}\tnet={g.Sum(x => x.NetDollars):F2}\tavg={g.Average(x => x.NetDollars):F2}" +
            $"\twin={(100m * g.Count(x => x.NetDollars > 0) / g.Count()):F1}%\tPF={ProfitFactor(g):F2}" +
            $"\tMFE={g.Average(x => x.MfeTicks):F1}\tMAE={g.Average(x => x.MaeTicks):F1}" +
            $"\ttargetHit={(100m * g.Count(x => x.ExitReason == "Target") / g.Count()):F1}%");
    }

    Console.WriteLine("ZONE COVERAGE");
    foreach (var zone in AllZones())
    {
        var count = t.Count(x => x.Zone == zone);
        var sessionsRepresented = t.Where(x => x.Zone == zone).Select(x => x.SessionDate).Distinct().Count();
        Console.WriteLine($"{zone}\ttrades={count}\tsessionsRepresented={sessionsRepresented}/{r.SessionCount}");
    }

    Console.WriteLine();
}

static decimal ProfitFactor(IEnumerable<Trade> trades)
{
    var x = trades.ToList();
    decimal wins = x.Where(t => t.NetDollars > 0).Sum(t => t.NetDollars);
    decimal losses = Math.Abs(x.Where(t => t.NetDollars < 0).Sum(t => t.NetDollars));
    return losses == 0 ? 0 : wins / losses;
}

static int MaxConsecutiveLosses(IReadOnlyList<Trade> trades)
{
    int max = 0, cur = 0;
    foreach (var t in trades.OrderBy(x => x.EntryCentral))
    {
        if (t.NetDollars <= 0)
        {
            cur++;
            if (cur > max) max = cur;
        }
        else
        {
            cur = 0;
        }
    }
    return max;
}

static decimal MaxDrawdown(IReadOnlyList<decimal> daily)
{
    decimal equity = 0, peak = 0, max = 0;
    foreach (var p in daily)
    {
        equity += p;
        if (equity > peak) peak = equity;
        var dd = peak - equity;
        if (dd > max) max = dd;
    }
    return max;
}

static decimal Median(IEnumerable<decimal> values)
{
    var x = values.OrderBy(v => v).ToList();
    if (x.Count == 0) return 0;
    int mid = x.Count / 2;
    return x.Count % 2 == 1 ? x[mid] : (x[mid - 1] + x[mid]) / 2m;
}

static decimal Percentile(IReadOnlyList<decimal> sortedValues, decimal p)
{
    if (sortedValues.Count == 0) return 0;
    if (sortedValues.Count == 1) return sortedValues[0];

    decimal pos = p * (sortedValues.Count - 1);
    int lo = (int)Math.Floor(pos);
    int hi = (int)Math.Ceiling(pos);
    if (lo == hi) return sortedValues[lo];

    decimal frac = pos - lo;
    return sortedValues[lo] + (sortedValues[hi] - sortedValues[lo]) * frac;
}

static decimal MedianEntryGap(IReadOnlyList<Trade> trades)
{
    var gaps = new List<decimal>();
    foreach (var g in trades.GroupBy(x => x.SessionDate))
    {
        var x = g.OrderBy(z => z.EntryCentral).ToList();
        for (int i = 1; i < x.Count; i++)
            gaps.Add((decimal)(x[i].EntryCentral - x[i - 1].EntryCentral).TotalMinutes);
    }
    return Median(gaps);
}

static int MaxEntriesRolling(IReadOnlyList<Trade> trades, int minutes)
{
    int max = 0;

    foreach (var session in trades.GroupBy(x => x.SessionDate))
    {
        var ordered = session.OrderBy(x => x.EntryCentral).ToList();
        int left = 0;

        for (int right = 0; right < ordered.Count; right++)
        {
            while (left < right &&
                   (ordered[right].EntryCentral - ordered[left].EntryCentral).TotalMinutes > minutes)
            {
                left++;
            }

            max = Math.Max(max, right - left + 1);
        }
    }

    return max;
}

static IReadOnlyList<string> AllZones() => new[]
{
    "01_EveningReopen",
    "02_AsiaDevelopment",
    "03_OvernightTransition",
    "04_London_USPremarket",
    "05_USPreopen_OpenDevelopment",
    "06_NYTransition_0845_0905",
    "07_NYSecondary_0905_1000",
    "08_LateMorning",
    "09_Midday_EarlyAfternoon"
};

static string ZoneAt(DateTime central)
{
    var t = central.TimeOfDay;

    if (t >= new TimeSpan(17, 0, 0) && t < new TimeSpan(19, 0, 0))
        return "01_EveningReopen";

    if (t >= new TimeSpan(19, 0, 0) && t < new TimeSpan(23, 0, 0))
        return "02_AsiaDevelopment";

    if (t >= new TimeSpan(23, 0, 0) || t < new TimeSpan(3, 0, 0))
        return "03_OvernightTransition";

    if (t >= new TimeSpan(3, 0, 0) && t < new TimeSpan(6, 30, 0))
        return "04_London_USPremarket";

    if (t >= new TimeSpan(6, 30, 0) && t < new TimeSpan(8, 45, 0))
        return "05_USPreopen_OpenDevelopment";

    if (t >= new TimeSpan(8, 45, 0) && t < new TimeSpan(9, 5, 0))
        return "06_NYTransition_0845_0905";

    if (t >= new TimeSpan(9, 5, 0) && t < new TimeSpan(10, 0, 0))
        return "07_NYSecondary_0905_1000";

    if (t >= new TimeSpan(10, 0, 0) && t < new TimeSpan(12, 0, 0))
        return "08_LateMorning";

    if (t >= new TimeSpan(12, 0, 0) && t < new TimeSpan(15, 0, 0))
        return "09_Midday_EarlyAfternoon";

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
                Math.Max(
                    Math.Abs(b.High - prev.Close),
                    Math.Abs(b.Low - prev.Close)));

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
        var u = DateTimeOffset.Parse(
            p[utc],
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

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
    try
    {
        return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
    }
    catch
    {
        return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
    }
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

sealed class SuppressionCounts
{
    public int RawSignals { get; set; }
    public int Accepted { get; set; }
    public int RepeatedSignal { get; set; }
    public int Cooldown { get; set; }
    public int SessionCap { get; set; }
    public int ZoneCap { get; set; }
    public int Rolling5m { get; set; }
    public int Rolling15m { get; set; }
    public int Rolling60m { get; set; }
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
    int Direction,
    string Family,
    string Zone,
    decimal Entry,
    decimal Exit,
    decimal GrossDollars,
    decimal NetDollars,
    decimal MfeTicks,
    decimal MaeTicks,
    decimal PostExitMfeTicks,
    int HoldBars,
    string ExitReason);

sealed record StudyResult(
    List<Trade> Trades,
    SuppressionCounts Suppression,
    int SessionCount,
    List<DateTime> SessionDates,
    bool Governed);
