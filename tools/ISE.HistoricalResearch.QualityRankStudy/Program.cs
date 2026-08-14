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
    throw new InvalidOperationException("V7.9.1 requires MNQ-only data.");

ComputeIndicators(bars);

var sessions = bars
    .Where(IsInsideResearchWindow)
    .GroupBy(SessionDate)
    .OrderBy(g => g.Key)
    .Where(g => g.Count() >= 1100)
    .Select(g => new Session(g.Key, g.OrderBy(x => x.Utc).ToList()))
    .ToList();

var calibrationSessions = sessions.Where(s => s.Date < new DateTime(2026, 7, 1)).ToList();
var validationSessions = sessions.Where(s => s.Date >= new DateTime(2026, 7, 1)).ToList();

Console.WriteLine("ISE Elite V7.9.1 Opportunity Quality + Exit Surface Study");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Goal: find whether a causally scoreable high-quality subset exists before account-level caps are applied.");
Console.WriteLine("Calibration: June 2026. Frozen validation: July 2026.");
Console.WriteLine("Session: 17:00 previous day -> 15:00 current day CT; 1-minute execution; 3 MNQ.");
Console.WriteLine("One position at a time; 1 completed-bar cooldown; repeated-breakout suppression; no daily trade cap.");
Console.WriteLine("Friction: 2 ticks total slippage + $1.50 round-trip commissions/fees per contract.");
Console.WriteLine("Signal score is computed at signal-bar close from causal price/volume/EMA/ATR features only.");
Console.WriteLine($"Bars loaded: {bars.Count}");
Console.WriteLine($"Complete sessions: {sessions.Count} | calibration={calibrationSessions.Count} validation={validationSessions.Count}");
Console.WriteLine($"First session: {(sessions.Count == 0 ? "n/a" : sessions[0].Date.ToString("yyyy-MM-dd"))}");
Console.WriteLine($"Last session: {(sessions.Count == 0 ? "n/a" : sessions[^1].Date.ToString("yyyy-MM-dd"))}");
Console.WriteLine();

var profiles = new[]
{
    ExitProfile.Fixed("F40_S30", 40, 30, 10),
    ExitProfile.Fixed("F50_S30", 50, 30, 10),
    ExitProfile.Atr("ATR125_075", 1.25m, 0.75m, 30, 120, 20, 80, 12),
    ExitProfile.Atr("ATR150_100", 1.50m, 1.00m, 30, 140, 20, 100, 12),
    ExitProfile.Atr("ATR200_100", 2.00m, 1.00m, 40, 180, 20, 100, 15),
};

var thresholds = new[] { 50, 60, 70, 80, 90 };

PrintSignalScoreDiagnostics(calibrationSessions, validationSessions);

Console.WriteLine();
Console.WriteLine("CONFIGURATION SURFACE");
Console.WriteLine("profile\tthreshold\tperiod\tsessions\ttrades\ttrades/day\tnet\tavg/day\tavg/trade\twin%\tPF\tpositiveDays\tmaxDD\ttargetHit%\tmedianHold\tmax60m");

var rows = new List<ConfigRow>();
foreach (var profile in profiles)
{
    foreach (var threshold in thresholds)
    {
        foreach (var period in new[]
        {
            new PeriodSet("CAL", calibrationSessions),
            new PeriodSet("VAL", validationSessions),
            new PeriodSet("FULL", sessions)
        })
        {
            var result = Run(period.Sessions, threshold, profile);
            var row = BuildRow(profile, threshold, period.Name, result);
            rows.Add(row);
            PrintRow(row);
        }
    }
}

Console.WriteLine();
Console.WriteLine("CALIBRATION RANKING WITH FROZEN JULY READOUT");
Console.WriteLine("Ranked by June net P&L only. July is displayed but is NOT used to choose the rank.");
Console.WriteLine("rank\tprofile\tthreshold\tCAL trades/day\tCAL net\tCAL PF\tVAL trades/day\tVAL net\tVAL PF\tVAL avg/day");

var calibrationRank = rows
    .Where(r => r.Period == "CAL" && r.Trades >= Math.Max(20, calibrationSessions.Count))
    .OrderByDescending(r => r.Net)
    .ThenByDescending(r => r.ProfitFactor)
    .Take(10)
    .ToList();

for (int i = 0; i < calibrationRank.Count; i++)
{
    var cal = calibrationRank[i];
    var val = rows.Single(r => r.Period == "VAL" && r.Profile == cal.Profile && r.Threshold == cal.Threshold);
    Console.WriteLine($"{i + 1}\t{cal.Profile}\t{cal.Threshold}\t{cal.TradesPerDay:F2}\t{cal.Net:F2}\t{cal.ProfitFactor:F2}\t{val.TradesPerDay:F2}\t{val.Net:F2}\t{val.ProfitFactor:F2}\t{val.AvgDaily:F2}");
}

Console.WriteLine();
Console.WriteLine("QUALITY THRESHOLD STABILITY BY FAMILY - F50_S30");
Console.WriteLine("period\tfamily\tthreshold\ttrades\tnet\tavg\twin%\tPF");
foreach (var period in new[]
{
    new PeriodSet("CAL", calibrationSessions),
    new PeriodSet("VAL", validationSessions)
})
{
    foreach (var threshold in thresholds)
    {
        var result = Run(period.Sessions, threshold, profiles[1]);
        foreach (var g in result.Trades.GroupBy(t => t.Family).OrderBy(g => g.Key))
        {
            var pf = ProfitFactor(g);
            Console.WriteLine($"{period.Name}\t{g.Key}\t{threshold}\t{g.Count()}\t{g.Sum(x => x.NetDollars):F2}\t{g.Average(x => x.NetDollars):F2}\t{(100m * g.Count(x => x.NetDollars > 0) / g.Count()):F1}\t{pf:F2}");
        }
    }
}

Console.WriteLine();
Console.WriteLine("QUALITY THRESHOLD STABILITY BY ZONE - F50_S30");
Console.WriteLine("period\tzone\tthreshold\ttrades\tnet\tavg\twin%\tPF");
foreach (var period in new[]
{
    new PeriodSet("CAL", calibrationSessions),
    new PeriodSet("VAL", validationSessions)
})
{
    foreach (var threshold in new[] { 60, 70, 80, 90 })
    {
        var result = Run(period.Sessions, threshold, profiles[1]);
        foreach (var g in result.Trades.GroupBy(t => t.Zone).OrderBy(g => g.Key))
        {
            var pf = ProfitFactor(g);
            Console.WriteLine($"{period.Name}\t{g.Key}\t{threshold}\t{g.Count()}\t{g.Sum(x => x.NetDollars):F2}\t{g.Average(x => x.NetDollars):F2}\t{(100m * g.Count(x => x.NetDollars > 0) / g.Count()):F1}\t{pf:F2}");
        }
    }
}

return 0;

static void PrintSignalScoreDiagnostics(IReadOnlyList<Session> cal, IReadOnlyList<Session> val)
{
    Console.WriteLine("RAW CAUSAL SIGNAL SCORE DISTRIBUTION");
    Console.WriteLine("period\tsignals\tp10\tp25\tmedian\tp75\tp90\tmean");

    foreach (var p in new[] { new PeriodSet("CAL", cal), new PeriodSet("VAL", val) })
    {
        var scores = GatherSignalScores(p.Sessions).OrderBy(x => x).Select(x => (decimal)x).ToList();
        Console.WriteLine($"{p.Name}\t{scores.Count}\t{Percentile(scores, 0.10m):F1}\t{Percentile(scores, 0.25m):F1}\t{Median(scores):F1}\t{Percentile(scores, 0.75m):F1}\t{Percentile(scores, 0.90m):F1}\t{(scores.Count == 0 ? 0 : scores.Average()):F1}");
    }
}

static List<int> GatherSignalScores(IReadOnlyList<Session> sessions)
{
    var scores = new List<int>();
    foreach (var session in sessions)
    {
        var b = session.Bars;
        for (int i = 6; i < b.Count - 1; i++)
        {
            var s = SignalAt(b, i);
            if (s.Direction == 0 || s.IsRepeated) continue;
            scores.Add(s.Score);
        }
    }
    return scores;
}

static StudyResult Run(IReadOnlyList<Session> sessions, int minScore, ExitProfile profile)
{
    var trades = new List<Trade>();
    var sessionDates = sessions.Select(s => s.Date).ToList();

    foreach (var session in sessions)
    {
        var b = session.Bars;
        int lastExitIndex = -1000;

        for (int i = 6; i < b.Count - 1; i++)
        {
            if (i <= lastExitIndex + 1) continue;

            var signal = SignalAt(b, i);
            if (signal.Direction == 0 || signal.IsRepeated || signal.Score < minScore) continue;

            int entryIndex = i + 1;
            var entryBar = b[entryIndex];
            var entry = entryBar.Open;
            var atr = b[i].Atr14 ?? 0m;
            var (targetTicks, stopTicks, maxHoldBars) = ResolveExit(profile, atr);
            int maxHoldEnd = Math.Min(b.Count - 1, entryIndex + maxHoldBars);

            decimal targetPoints = targetTicks * 0.25m;
            decimal stopPoints = stopTicks * 0.25m;
            decimal target = signal.Direction > 0 ? entry + targetPoints : entry - targetPoints;
            decimal stop = signal.Direction > 0 ? entry - stopPoints : entry + stopPoints;

            decimal mfePoints = 0m, maePoints = 0m;
            int exitIndex = maxHoldEnd;
            decimal exit = b[maxHoldEnd].Close;
            string exitReason = "Time";

            for (int j = entryIndex; j <= maxHoldEnd; j++)
            {
                var bar = b[j];
                decimal favorable = signal.Direction > 0 ? bar.High - entry : entry - bar.Low;
                decimal adverse = signal.Direction > 0 ? entry - bar.Low : bar.High - entry;
                if (favorable > mfePoints) mfePoints = favorable;
                if (adverse > maePoints) maePoints = adverse;

                bool hitTarget = signal.Direction > 0 ? bar.High >= target : bar.Low <= target;
                bool hitStop = signal.Direction > 0 ? bar.Low <= stop : bar.High >= stop;

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

            trades.Add(new Trade(
                session.Date,
                entryBar.Central,
                b[exitIndex].Central,
                signal.Direction,
                signal.Family,
                ZoneAt(entryBar.Central),
                signal.Score,
                targetTicks,
                stopTicks,
                grossDollars,
                netDollars,
                mfePoints / 0.25m,
                maePoints / 0.25m,
                exitIndex - entryIndex + 1,
                exitReason));

            lastExitIndex = exitIndex;
            i = exitIndex;
        }
    }

    return new StudyResult(trades, sessions.Count, sessionDates);
}

static (int TargetTicks, int StopTicks, int MaxHoldBars) ResolveExit(ExitProfile profile, decimal atrPoints)
{
    if (!profile.UseAtr)
        return (profile.FixedTargetTicks, profile.FixedStopTicks, profile.MaxHoldBars);

    decimal atrTicks = atrPoints / 0.25m;
    int target = Clamp((int)Math.Round((double)(atrTicks * profile.TargetAtrMultiple), MidpointRounding.AwayFromZero), profile.MinTargetTicks, profile.MaxTargetTicks);
    int stop = Clamp((int)Math.Round((double)(atrTicks * profile.StopAtrMultiple), MidpointRounding.AwayFromZero), profile.MinStopTicks, profile.MaxStopTicks);
    return (target, stop, profile.MaxHoldBars);
}

static Signal SignalAt(IReadOnlyList<Bar> b, int i)
{
    var x = b[i];
    var p = b[i - 1];

    if (!x.Atr14.HasValue || x.Atr14 <= 0 ||
        !x.Ema9.HasValue || !x.Ema21.HasValue || !x.Ema27.HasValue || !x.Ema63.HasValue || !x.Ema45.HasValue || !x.Ema105.HasValue)
        return Signal.None;

    decimal atr = x.Atr14.Value;
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
        return new Signal(+1, "TrendImpulse", repeatedLong, ScoreTrendImpulse(x, +1, prior5High));

    if (trend1Short && trend3Short && trend5Short && x.Close < prior5Low && body >= 0.35m * atr)
        return new Signal(-1, "TrendImpulse", repeatedShort, ScoreTrendImpulse(x, -1, prior5Low));

    if (trend3Long && trend5Long && p.Low <= p.Ema9 && p.Close >= p.Ema21 && x.Close > x.Ema9 && x.Close > p.High)
        return new Signal(+1, "PullbackReset", false, ScorePullback(x, p, +1));

    if (trend3Short && trend5Short && p.High >= p.Ema9 && p.Close <= p.Ema21 && x.Close < x.Ema9 && x.Close < p.Low)
        return new Signal(-1, "PullbackReset", false, ScorePullback(x, p, -1));

    bool crossUp = p.Ema9 <= p.Ema21 && x.Ema9 > x.Ema21;
    bool crossDown = p.Ema9 >= p.Ema21 && x.Ema9 < x.Ema21;
    decimal fiveSep = Math.Abs(x.Ema45!.Value - x.Ema105!.Value);

    if (crossUp && x.Close > x.Ema21 && fiveSep <= 0.80m * atr)
        return new Signal(+1, "DirectionalTransition", false, ScoreTransition(x, p, +1));

    if (crossDown && x.Close < x.Ema21 && fiveSep <= 0.80m * atr)
        return new Signal(-1, "DirectionalTransition", false, ScoreTransition(x, p, -1));

    bool weak5 = fiveSep <= 0.35m * atr;
    if (weak5 && p.Close < p.Ema21 - 1.25m * atr && x.Close > p.High)
        return new Signal(+1, "MeanReversion", false, ScoreMeanReversion(x, p, +1));

    if (weak5 && p.Close > p.Ema21 + 1.25m * atr && x.Close < p.Low)
        return new Signal(-1, "MeanReversion", false, ScoreMeanReversion(x, p, -1));

    return Signal.None;
}

static int ScoreTrendImpulse(Bar x, int dir, decimal breakoutLevel)
{
    decimal atr = x.Atr14!.Value;
    decimal bodyRatio = Math.Abs(x.Close - x.Open) / atr;
    decimal breakout = dir > 0 ? x.Close - breakoutLevel : breakoutLevel - x.Close;
    decimal sep3 = Math.Abs(x.Ema27!.Value - x.Ema63!.Value) / atr;
    decimal sep5 = Math.Abs(x.Ema45!.Value - x.Ema105!.Value) / atr;

    decimal score = 20m;
    score += 15m * Sat(bodyRatio / 1.00m);
    score += 15m * Sat((breakout / atr) / 0.50m);
    score += 10m * DirectionalCloseLocation(x, dir);
    score += 10m * VolumeStrength(x);
    score += 15m * Sat(sep3 / 1.00m);
    score += 15m * Sat(sep5 / 1.50m);
    return ScoreInt(score);
}

static int ScorePullback(Bar x, Bar p, int dir)
{
    decimal atr = x.Atr14!.Value;
    decimal sep3 = Math.Abs(x.Ema27!.Value - x.Ema63!.Value) / atr;
    decimal sep5 = Math.Abs(x.Ema45!.Value - x.Ema105!.Value) / atr;
    decimal reclaim = dir > 0 ? x.Close - x.Ema9!.Value : x.Ema9!.Value - x.Close;
    decimal rejection = dir > 0 ? p.Close - p.Low : p.High - p.Close;

    decimal score = 20m;
    score += 15m * Sat(sep3 / 1.00m);
    score += 15m * Sat(sep5 / 1.50m);
    score += 15m * Sat((reclaim / atr) / 0.75m);
    score += 15m * Sat((rejection / atr) / 0.75m);
    score += 10m * DirectionalCloseLocation(x, dir);
    score += 10m * VolumeStrength(x);
    return ScoreInt(score);
}

static int ScoreTransition(Bar x, Bar p, int dir)
{
    decimal atr = x.Atr14!.Value;
    decimal priorSpread = p.Ema9!.Value - p.Ema21!.Value;
    decimal currentSpread = x.Ema9!.Value - x.Ema21!.Value;
    decimal spreadAcceleration = dir > 0 ? currentSpread - priorSpread : priorSpread - currentSpread;
    decimal closeDistance = dir > 0 ? x.Close - x.Ema21!.Value : x.Ema21!.Value - x.Close;
    decimal bodyRatio = Math.Abs(x.Close - x.Open) / atr;
    int higherAgree = 0;
    if (dir > 0 && x.Ema27 > x.Ema63 || dir < 0 && x.Ema27 < x.Ema63) higherAgree++;
    if (dir > 0 && x.Ema45 > x.Ema105 || dir < 0 && x.Ema45 < x.Ema105) higherAgree++;

    decimal score = 20m;
    score += 20m * Sat((spreadAcceleration / atr) / 0.30m);
    score += 15m * Sat((closeDistance / atr) / 0.50m);
    score += 15m * Sat(bodyRatio / 1.00m);
    score += 10m * DirectionalCloseLocation(x, dir);
    score += 10m * VolumeStrength(x);
    score += 10m * (higherAgree / 2m);
    return ScoreInt(score);
}

static int ScoreMeanReversion(Bar x, Bar p, int dir)
{
    decimal atr = x.Atr14!.Value;
    decimal overshoot = dir > 0 ? p.Ema21!.Value - p.Close : p.Close - p.Ema21!.Value;
    decimal overshootBeyondTrigger = Math.Max(0m, overshoot / atr - 1.25m);
    decimal bodyRatio = Math.Abs(x.Close - x.Open) / atr;
    decimal sep5 = Math.Abs(x.Ema45!.Value - x.Ema105!.Value) / atr;
    decimal weakness = 1m - Sat(sep5 / 0.35m);

    decimal score = 20m;
    score += 25m * Sat(overshootBeyondTrigger / 1.25m);
    score += 15m * Sat(bodyRatio / 1.00m);
    score += 15m * DirectionalCloseLocation(x, dir);
    score += 10m * VolumeStrength(x);
    score += 15m * weakness;
    return ScoreInt(score);
}

static decimal DirectionalCloseLocation(Bar x, int dir)
{
    decimal range = x.High - x.Low;
    if (range <= 0m) return 0.5m;
    return dir > 0 ? Sat((x.Close - x.Low) / range) : Sat((x.High - x.Close) / range);
}

static decimal VolumeStrength(Bar x)
{
    if (!x.PriorVolumeAvg20.HasValue || x.PriorVolumeAvg20 <= 0m) return 0.5m;
    decimal ratio = x.Volume / x.PriorVolumeAvg20.Value;
    return Sat((ratio - 0.75m) / 1.25m);
}

static decimal Sat(decimal x) => x < 0m ? 0m : x > 1m ? 1m : x;
static int ScoreInt(decimal x) => Clamp((int)Math.Round((double)x, MidpointRounding.AwayFromZero), 0, 100);
static int Clamp(int x, int lo, int hi) => x < lo ? lo : x > hi ? hi : x;

static ConfigRow BuildRow(ExitProfile profile, int threshold, string period, StudyResult r)
{
    var t = r.Trades;
    var daily = r.SessionDates.ToDictionary(d => d, _ => 0m);
    foreach (var g in t.GroupBy(x => x.SessionDate)) daily[g.Key] = g.Sum(x => x.NetDollars);

    decimal net = t.Sum(x => x.NetDollars);
    decimal avgDaily = r.SessionCount == 0 ? 0 : net / r.SessionCount;
    decimal avgTrade = t.Count == 0 ? 0 : t.Average(x => x.NetDollars);
    decimal win = t.Count == 0 ? 0 : 100m * t.Count(x => x.NetDollars > 0) / t.Count;
    decimal pf = ProfitFactor(t);
    int positiveDays = daily.Count(x => x.Value > 0);
    decimal maxDD = MaxDrawdown(daily.OrderBy(x => x.Key).Select(x => x.Value).ToList());
    decimal targetHit = t.Count == 0 ? 0 : 100m * t.Count(x => x.ExitReason == "Target") / t.Count;
    decimal medianHold = Median(t.Select(x => (decimal)x.HoldBars));
    int max60 = MaxEntriesRolling(t, 60);

    return new ConfigRow(
        profile.Name, threshold, period, r.SessionCount, t.Count,
        r.SessionCount == 0 ? 0 : (decimal)t.Count / r.SessionCount,
        net, avgDaily, avgTrade, win, pf, positiveDays, maxDD, targetHit, medianHold, max60);
}

static void PrintRow(ConfigRow r)
{
    Console.WriteLine($"{r.Profile}\t{r.Threshold}\t{r.Period}\t{r.Sessions}\t{r.Trades}\t{r.TradesPerDay:F2}\t{r.Net:F2}\t{r.AvgDaily:F2}\t{r.AvgTrade:F2}\t{r.WinRate:F1}\t{r.ProfitFactor:F2}\t{r.PositiveDays}\t{r.MaxDrawdown:F2}\t{r.TargetHit:F1}\t{r.MedianHold:F1}\t{r.MaxEntries60m}");
}

static decimal ProfitFactor(IEnumerable<Trade> trades)
{
    var x = trades.ToList();
    decimal wins = x.Where(t => t.NetDollars > 0).Sum(t => t.NetDollars);
    decimal losses = Math.Abs(x.Where(t => t.NetDollars < 0).Sum(t => t.NetDollars));
    if (losses == 0m) return wins > 0m ? 99m : 0m;
    return wins / losses;
}

static decimal MaxDrawdown(IReadOnlyList<decimal> daily)
{
    decimal equity = 0m, peak = 0m, max = 0m;
    foreach (var pnl in daily)
    {
        equity += pnl;
        if (equity > peak) peak = equity;
        max = Math.Max(max, peak - equity);
    }
    return max;
}

static int MaxEntriesRolling(IReadOnlyList<Trade> trades, int minutes)
{
    int max = 0;
    foreach (var sg in trades.GroupBy(t => t.SessionDate))
    {
        var ordered = sg.OrderBy(t => t.EntryCentral).ToList();
        int left = 0;
        for (int right = 0; right < ordered.Count; right++)
        {
            while (left < right && (ordered[right].EntryCentral - ordered[left].EntryCentral).TotalMinutes > minutes) left++;
            max = Math.Max(max, right - left + 1);
        }
    }
    return max;
}

static decimal Median(IEnumerable<decimal> values)
{
    var x = values.OrderBy(v => v).ToList();
    if (x.Count == 0) return 0m;
    int mid = x.Count / 2;
    return x.Count % 2 == 1 ? x[mid] : (x[mid - 1] + x[mid]) / 2m;
}

static decimal Percentile(IReadOnlyList<decimal> sorted, decimal p)
{
    if (sorted.Count == 0) return 0m;
    if (sorted.Count == 1) return sorted[0];
    decimal pos = p * (sorted.Count - 1);
    int lo = (int)Math.Floor(pos);
    int hi = (int)Math.Ceiling(pos);
    if (lo == hi) return sorted[lo];
    decimal f = pos - lo;
    return sorted[lo] + (sorted[hi] - sorted[lo]) * f;
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

static DateTime SessionDate(Bar b) => b.Central.TimeOfDay >= new TimeSpan(17, 0, 0) ? b.Central.Date.AddDays(1) : b.Central.Date;

static void ComputeIndicators(List<Bar> bars)
{
    decimal? ema9 = null, ema21 = null, ema27 = null, ema45 = null, ema63 = null, ema105 = null;
    var tr = new Queue<decimal>();
    var priorVolumes = new Queue<long>();
    long priorVolumeSum = 0;
    Bar? prev = null;

    foreach (var b in bars)
    {
        if (priorVolumes.Count >= 20)
            b.PriorVolumeAvg20 = (decimal)priorVolumeSum / priorVolumes.Count;

        ema9 = Ema(ema9, b.Close, 9);
        ema21 = Ema(ema21, b.Close, 21);
        ema27 = Ema(ema27, b.Close, 27);
        ema45 = Ema(ema45, b.Close, 45);
        ema63 = Ema(ema63, b.Close, 63);
        ema105 = Ema(ema105, b.Close, 105);
        b.Ema9 = ema9; b.Ema21 = ema21; b.Ema27 = ema27; b.Ema45 = ema45; b.Ema63 = ema63; b.Ema105 = ema105;

        decimal trueRange = prev == null ? b.High - b.Low : Math.Max(b.High - b.Low, Math.Max(Math.Abs(b.High - prev.Close), Math.Abs(b.Low - prev.Close)));
        tr.Enqueue(trueRange);
        if (tr.Count > 14) tr.Dequeue();
        if (tr.Count == 14) b.Atr14 = tr.Average();

        priorVolumes.Enqueue(b.Volume);
        priorVolumeSum += b.Volume;
        if (priorVolumes.Count > 20) priorVolumeSum -= priorVolumes.Dequeue();

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
    var map = header.Select((name, index) => new { name, index }).ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
    int I(string name) => map.TryGetValue(name, out var idx) ? idx : throw new InvalidOperationException($"Missing TSV column: {name}");

    int instrument = I("instrument"), utc = I("timestampUtc"), open = I("open"), high = I("high"), low = I("low"), close = I("close"), volume = I("volume");
    var result = new List<Bar>(lines.Count - 1);

    foreach (var line in lines.Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line)) continue;
        var p = line.Split('\t');
        var u = DateTimeOffset.Parse(p[utc], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var c = TimeZoneInfo.ConvertTime(u, central).DateTime;
        result.Add(new Bar
        {
            Instrument = p[instrument], Utc = u, Central = DateTime.SpecifyKind(c, DateTimeKind.Unspecified),
            Open = decimal.Parse(p[open], CultureInfo.InvariantCulture), High = decimal.Parse(p[high], CultureInfo.InvariantCulture),
            Low = decimal.Parse(p[low], CultureInfo.InvariantCulture), Close = decimal.Parse(p[close], CultureInfo.InvariantCulture),
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
    public decimal? PriorVolumeAvg20 { get; set; }
    public decimal? Ema9 { get; set; }
    public decimal? Ema21 { get; set; }
    public decimal? Ema27 { get; set; }
    public decimal? Ema45 { get; set; }
    public decimal? Ema63 { get; set; }
    public decimal? Ema105 { get; set; }
    public decimal? Atr14 { get; set; }
}

sealed record Session(DateTime Date, List<Bar> Bars);
readonly record struct Signal(int Direction, string Family, bool IsRepeated, int Score)
{
    public static Signal None => new(0, "", false, 0);
}

sealed record ExitProfile(
    string Name, bool UseAtr,
    int FixedTargetTicks, int FixedStopTicks,
    decimal TargetAtrMultiple, decimal StopAtrMultiple,
    int MinTargetTicks, int MaxTargetTicks,
    int MinStopTicks, int MaxStopTicks,
    int MaxHoldBars)
{
    public static ExitProfile Fixed(string name, int target, int stop, int maxHold) =>
        new(name, false, target, stop, 0m, 0m, target, target, stop, stop, maxHold);

    public static ExitProfile Atr(string name, decimal targetMult, decimal stopMult, int minTarget, int maxTarget, int minStop, int maxStop, int maxHold) =>
        new(name, true, 0, 0, targetMult, stopMult, minTarget, maxTarget, minStop, maxStop, maxHold);
}

sealed record Trade(
    DateTime SessionDate, DateTime EntryCentral, DateTime ExitCentral,
    int Direction, string Family, string Zone, int Score,
    int TargetTicks, int StopTicks,
    decimal GrossDollars, decimal NetDollars,
    decimal MfeTicks, decimal MaeTicks, int HoldBars, string ExitReason);

sealed record StudyResult(List<Trade> Trades, int SessionCount, List<DateTime> SessionDates);
sealed record PeriodSet(string Name, IReadOnlyList<Session> Sessions);
sealed record ConfigRow(
    string Profile, int Threshold, string Period, int Sessions, int Trades, decimal TradesPerDay,
    decimal Net, decimal AvgDaily, decimal AvgTrade, decimal WinRate, decimal ProfitFactor,
    int PositiveDays, decimal MaxDrawdown, decimal TargetHit, decimal MedianHold, int MaxEntries60m);

