using System.Globalization;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: <validated-30-second-mnq-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path)) return 3;

var central = ResolveCentral();
var all = ReadBars(path, central).OrderBy(x => x.Utc).ToList();

var validations = ValidateSessions(all, new DateTime(2026,6,1), new DateTime(2026,7,31));
var validDates = validations.Where(x => x.IsUsable).Select(x => x.Date).ToHashSet();

var bySession = all
    .Where(IsInsideWindow)
    .Where(x => validDates.Contains(SessionDate(x)))
    .GroupBy(SessionDate)
    .OrderBy(g => g.Key)
    .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Utc).ToList());

var targets = new[] { 10,15,20,25,30,40,50,75,100 };
var stops = new[] { 10,15,20,25,30,40,50,75,100 };
const int horizonMinutes = 5;

Console.WriteLine("ISE Elite V7.9.6 Target/Stop Outcome Matrix");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Every five-minute anchor; five-minute forward execution path; validated 30-second MNQ.");
Console.WriteLine("For each target/stop pair, long and short are simulated independently.");
Console.WriteLine("Same-30-second-bar target/stop conflicts remain ambiguous and are reported explicitly.");
Console.WriteLine("Oracle metrics are hindsight ceilings only; they do NOT represent a tradable strategy.");
Console.WriteLine($"Research sessions observed: {validations.Count}");
Console.WriteLine($"Usable research sessions: {validDates.Count}");
Console.WriteLine($"Excluded sessions: {validations.Count(x=>!x.IsUsable)}");
foreach (var v in validations.Where(x=>!x.IsUsable))
    Console.WriteLine($"EXCLUDED SESSION {v.Date:yyyy-MM-dd} reason={v.Reason}");
Console.WriteLine();

foreach (var target in targets)
{
    Console.WriteLine(new string('=',120));
    Console.WriteLine($"TARGET {target} TICKS / 5-MINUTE HORIZON");
    Console.WriteLine(new string('=',120));

    foreach (var stop in stops)
    {
        var rows = new List<Row>();

        foreach (var kv in bySession)
        {
            var bars = kv.Value;

            foreach (var anchor in bars.Where(x => x.Central.Minute % 5 == 0 && x.Central.Second == 30))
            {
                var startTime = anchor.Central.AddSeconds(-30);
                var endTime = startTime.AddMinutes(horizonMinutes);
                var entry = anchor.Open;

                var future = bars
                    .Where(x => x.Central > startTime && x.Central <= endTime)
                    .ToList();

                if (future.Count < 9) continue;

                var lo = Simulate(future, entry, target, stop, true);
                var sh = Simulate(future, entry, target, stop, false);

                rows.Add(new Row(kv.Key,startTime,ZoneAt(startTime),lo,sh));
            }
        }

        PrintPair(target, stop, rows, validDates.Count);
    }
}

return 0;

static void PrintPair(int target, int stop, List<Row> rows, int sessions)
{
    int n = rows.Count;

    int lT = rows.Count(x => x.Long == Outcome.TargetFirst);
    int lS = rows.Count(x => x.Long == Outcome.StopFirst);
    int lA = rows.Count(x => x.Long == Outcome.Ambiguous);
    int lTime = rows.Count(x => x.Long == Outcome.Time);

    int sT = rows.Count(x => x.Short == Outcome.TargetFirst);
    int sS = rows.Count(x => x.Short == Outcome.StopFirst);
    int sA = rows.Count(x => x.Short == Outcome.Ambiguous);
    int sTime = rows.Count(x => x.Short == Outcome.Time);

    // Worst/best directional win probabilities count ambiguous as loss/win respectively.
    decimal lWorst = Pct(lT,n), lBest=Pct(lT+lA,n);
    decimal sWorst = Pct(sT,n), sBest=Pct(sT+sA,n);

    // Hindsight oracle: "could one of the two directions have won?"
    int oracleWorst = rows.Count(x => x.Long == Outcome.TargetFirst || x.Short == Outcome.TargetFirst);
    int oracleBest = rows.Count(x =>
        x.Long == Outcome.TargetFirst || x.Long == Outcome.Ambiguous ||
        x.Short == Outcome.TargetFirst || x.Short == Outcome.Ambiguous);

    int bothWorst = rows.Count(x => x.Long == Outcome.TargetFirst && x.Short == Outcome.TargetFirst);
    int neitherBest = rows.Count(x =>
        (x.Long == Outcome.StopFirst || x.Long == Outcome.Time) &&
        (x.Short == Outcome.StopFirst || x.Short == Outcome.Time));

    decimal rr = stop == 0 ? 0 : (decimal)target / stop;
    decimal breakEvenWinNoFriction = stop + target == 0 ? 0 : 100m * stop / (stop + target);

    Console.WriteLine();
    Console.WriteLine($"PAIR target={target} stop={stop} R:R={rr:F2}");
    Console.WriteLine($"anchors={n} anchors/day={(sessions==0?0:(decimal)n/sessions):F2}");
    Console.WriteLine($"long targetFirst={lT} stopFirst={lS} ambiguous={lA} time={lTime} winPctWorst={lWorst:F1}% winPctBest={lBest:F1}%");
    Console.WriteLine($"short targetFirst={sT} stopFirst={sS} ambiguous={sA} time={sTime} winPctWorst={sWorst:F1}% winPctBest={sBest:F1}%");
    Console.WriteLine($"oracleEitherSideWinPctWorst={Pct(oracleWorst,n):F1}% oracleEitherSideWinPctBest={Pct(oracleBest,n):F1}%");
    Console.WriteLine($"bothDirectionsTargetFirstPctWorst={Pct(bothWorst,n):F1}% neitherDirectionCanWinPctBest={Pct(neitherBest,n):F1}%");
    Console.WriteLine($"breakEvenWinPctIgnoringFriction={breakEvenWinNoFriction:F1}%");

    Console.WriteLine("BY SESSION ZONE");
    foreach (var g in rows.GroupBy(x=>x.Zone).OrderBy(x=>x.Key))
    {
        int zn=g.Count();
        int zlT=g.Count(x=>x.Long==Outcome.TargetFirst);
        int zlA=g.Count(x=>x.Long==Outcome.Ambiguous);
        int zsT=g.Count(x=>x.Short==Outcome.TargetFirst);
        int zsA=g.Count(x=>x.Short==Outcome.Ambiguous);
        int zoW=g.Count(x=>x.Long==Outcome.TargetFirst || x.Short==Outcome.TargetFirst);
        int zoB=g.Count(x=>x.Long==Outcome.TargetFirst || x.Long==Outcome.Ambiguous || x.Short==Outcome.TargetFirst || x.Short==Outcome.Ambiguous);
        int zBoth=g.Count(x=>x.Long==Outcome.TargetFirst && x.Short==Outcome.TargetFirst);

        Console.WriteLine(
            $"  {g.Key,-30} n={zn,5} " +
            $"Lwin={Pct(zlT,zn),5:F1}-{Pct(zlT+zlA,zn),5:F1}% " +
            $"Swin={Pct(zsT,zn),5:F1}-{Pct(zsT+zsA,zn),5:F1}% " +
            $"oracle={Pct(zoW,zn),5:F1}-{Pct(zoB,zn),5:F1}% " +
            $"bothWin={Pct(zBoth,zn),5:F1}%");
    }

    // Pure economic ceiling if an oracle always chooses a target-first side.
    foreach (var qty in new[] {3,4,5})
    {
        decimal targetGross = target * 0.50m * qty;
        decimal stopGross = stop * 0.50m * qty;
        decimal friction = (2m * 0.50m * qty) + (1.50m * qty);

        decimal winNet = targetGross - friction;
        decimal lossNet = -stopGross - friction;

        decimal beWithFriction = (winNet - lossNet) == 0 ? 0 : 100m * (-lossNet) / (winNet - lossNet);

        decimal oracleWinWorst = n==0?0:(decimal)oracleWorst/n;
        decimal oracleWinBest = n==0?0:(decimal)oracleBest/n;
        decimal dailyCount = sessions==0?0:(decimal)n/sessions;

        decimal oracleDailyWorst = dailyCount * (oracleWinWorst*winNet + (1m-oracleWinWorst)*lossNet);
        decimal oracleDailyBest = dailyCount * (oracleWinBest*winNet + (1m-oracleWinBest)*lossNet);

        Console.WriteLine($"  qty={qty} winNet=${winNet:F2} lossNet=${lossNet:F2} breakEvenWithFriction={beWithFriction:F1}% oracleDailyWorst=${oracleDailyWorst:F2} oracleDailyBest=${oracleDailyBest:F2}");
    }
}

static Outcome Simulate(List<Bar30> future, decimal entry, int targetTicks, int stopTicks, bool isLong)
{
    decimal target = isLong ? entry + targetTicks*0.25m : entry - targetTicks*0.25m;
    decimal stop = isLong ? entry - stopTicks*0.25m : entry + stopTicks*0.25m;

    foreach (var b in future)
    {
        bool targetHit = isLong ? b.High >= target : b.Low <= target;
        bool stopHit = isLong ? b.Low <= stop : b.High >= stop;

        if (targetHit && stopHit) return Outcome.Ambiguous;
        if (targetHit) return Outcome.TargetFirst;
        if (stopHit) return Outcome.StopFirst;
    }

    return Outcome.Time;
}

static decimal Pct(int n,int d)=>d==0?0:100m*n/d;

static List<SessionValidation> ValidateSessions(IReadOnlyList<Bar30> bars,DateTime from,DateTime to)
{
    const int expectedCritical=600;
    var result=new List<SessionValidation>();

    foreach(var g in bars.Where(IsInsideWindow).GroupBy(SessionDate).Where(g=>g.Key>=from&&g.Key<=to).OrderBy(g=>g.Key))
    {
        var x=g.OrderBy(b=>b.Utc).ToList();
        int critical=x.Count(b=>b.Central.TimeOfDay>=new TimeSpan(6,0,0)&&b.Central.TimeOfDay<new TimeSpan(11,0,0));
        int gaps=0;
        for(int i=1;i<x.Count;i++) if((x[i].Central-x[i-1].Central).TotalSeconds>45d) gaps++;

        bool usable=critical==expectedCritical&&gaps==0;
        string reason=usable?"OK":critical!=expectedCritical?$"CriticalWindowIncomplete({critical}/{expectedCritical})":$"InternalGaps({gaps})";
        result.Add(new SessionValidation(g.Key,usable,reason));
    }

    return result;
}

static bool IsInsideWindow(Bar30 b)
{
    var t=b.Central.TimeOfDay;
    return t>=new TimeSpan(17,0,0)||t<new TimeSpan(15,0,0);
}

static DateTime SessionDate(Bar30 b)=>b.Central.TimeOfDay>=new TimeSpan(17,0,0)?b.Central.Date.AddDays(1):b.Central.Date;

static string ZoneAt(DateTime c)
{
    var t=c.TimeOfDay;
    if(t>=new TimeSpan(17,0,0)&&t<new TimeSpan(19,0,0)) return "01_EveningReopen";
    if(t>=new TimeSpan(19,0,0)&&t<new TimeSpan(23,0,0)) return "02_AsiaDevelopment";
    if(t>=new TimeSpan(23,0,0)||t<new TimeSpan(3,0,0)) return "03_OvernightTransition";
    if(t>=new TimeSpan(3,0,0)&&t<new TimeSpan(6,30,0)) return "04_London_USPremarket";
    if(t>=new TimeSpan(6,30,0)&&t<new TimeSpan(8,45,0)) return "05_USPreopen_OpenDevelopment";
    if(t>=new TimeSpan(8,45,0)&&t<new TimeSpan(9,5,0)) return "06_NYTransition_0845_0905";
    if(t>=new TimeSpan(9,5,0)&&t<new TimeSpan(10,0,0)) return "07_NYSecondary_0905_1000";
    if(t>=new TimeSpan(10,0,0)&&t<new TimeSpan(12,0,0)) return "08_LateMorning";
    if(t>=new TimeSpan(12,0,0)&&t<new TimeSpan(15,0,0)) return "09_Midday_EarlyAfternoon";
    return "Outside";
}

static List<Bar30> ReadBars(string path,TimeZoneInfo central)
{
    var lines=File.ReadLines(path).ToList();
    var h=lines[0].Split('\t');
    var m=h.Select((name,i)=>new{name,i}).ToDictionary(x=>x.name,x=>x.i,StringComparer.OrdinalIgnoreCase);
    int I(string n)=>m.TryGetValue(n,out var i)?i:throw new InvalidOperationException($"Missing TSV column: {n}");

    int utc=I("timestampUtc"), interval=I("intervalSeconds"), open=I("open"), high=I("high"), low=I("low"), close=I("close");

    var r=new List<Bar30>(lines.Count-1);
    foreach(var line in lines.Skip(1))
    {
        if(string.IsNullOrWhiteSpace(line)) continue;
        var p=line.Split('\t');
        var u=DateTimeOffset.Parse(p[utc],CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
        var c=TimeZoneInfo.ConvertTime(u,central).DateTime;

        r.Add(new Bar30{
            Utc=u,Central=DateTime.SpecifyKind(c,DateTimeKind.Unspecified),
            IntervalSeconds=int.Parse(p[interval],CultureInfo.InvariantCulture),
            Open=decimal.Parse(p[open],CultureInfo.InvariantCulture),
            High=decimal.Parse(p[high],CultureInfo.InvariantCulture),
            Low=decimal.Parse(p[low],CultureInfo.InvariantCulture),
            Close=decimal.Parse(p[close],CultureInfo.InvariantCulture)
        });
    }
    return r;
}

static TimeZoneInfo ResolveCentral()
{
    try{return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");}
    catch{return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");}
}

enum Outcome { TargetFirst,StopFirst,Ambiguous,Time }

sealed class Bar30
{
    public DateTimeOffset Utc{get;init;}
    public DateTime Central{get;init;}
    public int IntervalSeconds{get;init;}
    public decimal Open{get;init;}
    public decimal High{get;init;}
    public decimal Low{get;init;}
    public decimal Close{get;init;}
}

sealed record SessionValidation(DateTime Date,bool IsUsable,string Reason);
sealed record Row(DateTime SessionDate,DateTime Start,string Zone,Outcome Long,Outcome Short);
