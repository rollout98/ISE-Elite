using System.Globalization;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: <validated-30-second-mnq-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path)) return 3;

var central = ResolveCentral();
var bars30 = ReadBars(path, central).OrderBy(x => x.Utc).ToList();

var validations = ValidateSessions(bars30, new DateTime(2026,6,1), new DateTime(2026,7,31));
var validDates = validations.Where(x => x.IsUsable).Select(x => x.Date).ToHashSet();

var research30 = bars30
    .Where(IsInsideWindow)
    .Where(x => validDates.Contains(SessionDate(x)))
    .OrderBy(x => x.Utc)
    .ToList();

var bars1 = AggregateToMinute(research30);
var bars3 = AggregateMinutes(bars1, 3);
var bars5 = AggregateMinutes(bars1, 5);

var bySession30 = research30.GroupBy(SessionDate).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Utc).ToList());
var bySession1  = bars1.GroupBy(SessionDate1).ToDictionary(g => g.Key, g => g.OrderBy(x => x.End).ToList());
var bySession3  = bars3.GroupBy(SessionDate1).ToDictionary(g => g.Key, g => g.OrderBy(x => x.End).ToList());
var bySession5  = bars5.GroupBy(SessionDate1).ToDictionary(g => g.Key, g => g.OrderBy(x => x.End).ToList());

Console.WriteLine("ISE Elite V7.9.7 Causal Direction & Opportunity Classifier Diagnostic");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Training: June 2026 only. Frozen validation: July 2026 only.");
Console.WriteLine("Features use only data available before each five-minute decision anchor.");
Console.WriteLine("True aggregated 1m / 3m / 5m OHLC is used; no EMA-equivalent pseudo-timeframes.");
Console.WriteLine("Direction model: binary ridge logistic regression on clean 50-target / 30-stop labels.");
Console.WriteLine("Opportunity model: 4-class softmax on maximum clean target-first class using the predicted direction.");
Console.WriteLine("Classes: Micro=10-15, Scalp=20-30, Expansion=40-50, Large=75-100.");
Console.WriteLine("No-edge anchors are retained for deployment simulation through confidence thresholds.");
Console.WriteLine("Same-30-second target/stop conflicts are excluded from model labels.");
Console.WriteLine($"Research sessions observed: {validations.Count}");
Console.WriteLine($"Usable research sessions: {validDates.Count}");
Console.WriteLine($"Excluded sessions: {validations.Count(x=>!x.IsUsable)}");
foreach (var v in validations.Where(x=>!x.IsUsable))
    Console.WriteLine($"EXCLUDED SESSION {v.Date:yyyy-MM-dd} reason={v.Reason}");
Console.WriteLine();

var rows = new List<Sample>();

foreach (var kv in bySession30.OrderBy(x => x.Key))
{
    var sessionDate = kv.Key;
    var s30 = kv.Value;
    if (!bySession1.TryGetValue(sessionDate, out var s1)) continue;
    bySession3.TryGetValue(sessionDate, out var s3);
    bySession5.TryGetValue(sessionDate, out var s5);
    s3 ??= new List<BarN>();
    s5 ??= new List<BarN>();

    foreach (var anchor in s30.Where(x => x.Central.Minute % 5 == 0 && x.Central.Second == 30))
    {
        var anchorStart = anchor.Central.AddSeconds(-30);

        var prior1 = s1.Where(x => x.End <= anchorStart).OrderBy(x => x.End).ToList();
        if (prior1.Count < 30) continue;

        var prior3 = s3.Where(x => x.End <= anchorStart).OrderBy(x => x.End).ToList();
        var prior5 = s5.Where(x => x.End <= anchorStart).OrderBy(x => x.End).ToList();

        var features = BuildFeatures(anchorStart, anchor.Open, prior1, prior3, prior5);
        if (features is null) continue;

        var future = s30.Where(x => x.Central > anchorStart && x.Central <= anchorStart.AddMinutes(5)).ToList();
        if (future.Count < 9) continue;

        var long50 = Simulate(future, anchor.Open, 50, 30, true);
        var short50 = Simulate(future, anchor.Open, 50, 30, false);

        int? directionLabel = null;
        if (long50 == Outcome.TargetFirst && short50 != Outcome.TargetFirst && short50 != Outcome.Ambiguous)
            directionLabel = 1;
        else if (short50 == Outcome.TargetFirst && long50 != Outcome.TargetFirst && long50 != Outcome.Ambiguous)
            directionLabel = 0;

        int? longClass = MaxOpportunityClass(future, anchor.Open, true);
        int? shortClass = MaxOpportunityClass(future, anchor.Open, false);

        rows.Add(new Sample(
            sessionDate,
            anchorStart,
            ZoneAt(anchorStart),
            features,
            directionLabel,
            longClass,
            shortClass,
            long50,
            short50) { EntryPrice = anchor.Open });
    }
}

var june = rows.Where(x => x.SessionDate.Month == 6).ToList();
var july = rows.Where(x => x.SessionDate.Month == 7).ToList();

Console.WriteLine($"Samples total={rows.Count} June={june.Count} July={july.Count}");
Console.WriteLine($"June direction-labeled={june.Count(x=>x.DirectionLabel.HasValue)} July direction-labeled={july.Count(x=>x.DirectionLabel.HasValue)}");
Console.WriteLine();

var featureNames = FeatureNames();
var scaler = FitScaler(june.Select(x => x.Features).ToList());
var juneX = june.Select(x => scaler.Transform(x.Features)).ToList();
var julyX = july.Select(x => scaler.Transform(x.Features)).ToList();

var juneDirIdx = june.Select((s,i)=>(s,i)).Where(x=>x.s.DirectionLabel.HasValue).ToList();
var julyDirIdx = july.Select((s,i)=>(s,i)).Where(x=>x.s.DirectionLabel.HasValue).ToList();

var xTrainDir = juneDirIdx.Select(x=>juneX[x.i]).ToList();
var yTrainDir = juneDirIdx.Select(x=>x.s.DirectionLabel!.Value).ToList();

var directionModel = LogisticRegression.Fit(xTrainDir, yTrainDir, lambda: 1.0, iterations: 2500, learningRate: 0.03);

Console.WriteLine("DIRECTION MODEL -- 50 TARGET / 30 STOP CLEAN LABELS");
EvaluateDirection("June calibration", juneDirIdx, juneX, directionModel);
EvaluateDirection("July frozen", julyDirIdx, julyX, directionModel);
Console.WriteLine();

Console.WriteLine("TOP DIRECTION COEFFICIENTS");
foreach (var item in featureNames.Select((n,i)=>(Name:n,Weight:directionModel.Weights[i+1])).OrderByDescending(x=>Math.Abs(x.Weight)).Take(15))
    Console.WriteLine($"  {item.Name,-30} weight={item.Weight,9:F4}");
Console.WriteLine();

var juneOpp = new List<(double[] X,int Y)>();
for (int i=0;i<june.Count;i++)
{
    var s=june[i];
    if (!s.DirectionLabel.HasValue) continue;
    var cls = s.DirectionLabel.Value == 1 ? s.LongClass : s.ShortClass;
    if (cls.HasValue) juneOpp.Add((juneX[i],cls.Value));
}

var oppModel = SoftmaxRegression.Fit(
    juneOpp.Select(x=>x.X).ToList(),
    juneOpp.Select(x=>x.Y).ToList(),
    classes: 4,
    lambda: 1.0,
    iterations: 3000,
    learningRate: 0.02);

Console.WriteLine("OPPORTUNITY CLASS MODEL -- CLEAN DIRECTION LABELS");
EvaluateOpportunity("June calibration", june, juneX, directionModel, oppModel);
EvaluateOpportunity("July frozen", july, julyX, directionModel, oppModel);
Console.WriteLine();

Console.WriteLine("JULY DEPLOYMENT SIMULATION");
Console.WriteLine("Decision rule: trade only when direction confidence >= threshold.");
Console.WriteLine("Predicted opportunity class selects target/stop pair:");
Console.WriteLine("  Micro -> 15/15");
Console.WriteLine("  Scalp -> 30/30");
Console.WriteLine("  Expansion -> 50/30");
Console.WriteLine("  Large -> 100/50");
Console.WriteLine("Outcome uses 30-second path; ambiguous is counted worst-case as loss and best-case as win.");
Console.WriteLine();

foreach (var conf in new[] {0.55,0.60,0.65,0.70,0.75})
{
    var sims = new List<DeployResult>();

    for (int i=0;i<july.Count;i++)
    {
        var s = july[i];
        var x = julyX[i];

        double pLong = directionModel.PredictProbability(x);
        bool? goLong = null;
        double confidence;

        if (pLong >= 0.5)
        {
            goLong = true;
            confidence = pLong;
        }
        else
        {
            goLong = false;
            confidence = 1.0 - pLong;
        }

        if (confidence < conf) continue;

        var probs = oppModel.PredictProbabilities(x);
        int cls = ArgMax(probs);

        var (target,stop) = cls switch
        {
            0 => (15,15),
            1 => (30,30),
            2 => (50,30),
            _ => (100,50)
        };

        var s30 = bySession30[s.SessionDate];
        var future = s30.Where(b => b.Central > s.Start && b.Central <= s.Start.AddMinutes(5)).ToList();
        if (future.Count < 9) continue;

        var outcome = Simulate(future, s.EntryPrice, target, stop, goLong.Value);

        sims.Add(new DeployResult(s.SessionDate,s.Start,s.Zone,target,stop,goLong.Value,outcome,confidence,cls));
    }

    Console.WriteLine($"CONFIDENCE >= {conf:P0}");
    PrintDeployment(sims, july.Select(x=>x.SessionDate).Distinct().Count());
    Console.WriteLine();
}

return 0;

static double[]? BuildFeatures(DateTime t, decimal entry, List<BarN> p1, List<BarN> p3, List<BarN> p5)
{
    if (p1.Count < 30) return null;

    var last1 = p1[^1];
    var last5 = p1.TakeLast(5).ToList();
    var last10 = p1.TakeLast(10).ToList();
    var last20 = p1.TakeLast(20).ToList();
    var last30 = p1.TakeLast(30).ToList();

    double Ret(int n)
    {
        if (p1.Count < n+1) return 0;
        return (double)((p1[^1].Close - p1[^(n+1)].Close) / 0.25m);
    }

    double RangeTicks(IEnumerable<BarN> x)
    {
        var a=x.ToList();
        if(a.Count==0) return 0;
        return (double)((a.Max(b=>b.High)-a.Min(b=>b.Low))/0.25m);
    }

    double AvgRange(IEnumerable<BarN> x)
    {
        var a=x.ToList();
        if(a.Count==0) return 0;
        return a.Average(b=>(double)((b.High-b.Low)/0.25m));
    }

    double BodyTicks(BarN b)=>(double)((b.Close-b.Open)/0.25m);

    double ema9 = Ema(p1.Select(x=>(double)x.Close).ToList(),9);
    double ema21 = Ema(p1.Select(x=>(double)x.Close).ToList(),21);
    double ema50 = Ema(p1.Select(x=>(double)x.Close).ToList(),50);

    double vwap = Vwap(p1);
    double atr5=AvgRange(last5), atr20=AvgRange(last20);
    double sessionHigh=(double)p1.Max(x=>x.High);
    double sessionLow=(double)p1.Min(x=>x.Low);
    double price=(double)entry;

    var last3 = p3.Count>0?p3[^1]:last1;
    var last5bar = p5.Count>0?p5[^1]:last1;

    int up5=last5.Count(x=>x.Close>x.Open);
    int down5=last5.Count(x=>x.Close<x.Open);
    int up10=last10.Count(x=>x.Close>x.Open);
    int down10=last10.Count(x=>x.Close<x.Open);

    double minuteOfSession = SessionMinute(t);
    double sinTod=Math.Sin(2*Math.PI*minuteOfSession/1320.0);
    double cosTod=Math.Cos(2*Math.PI*minuteOfSession/1320.0);

    return new[]
    {
        Ret(1),Ret(3),Ret(5),Ret(10),Ret(20),Ret(30),
        BodyTicks(last1),
        RangeTicks(last5),RangeTicks(last10),RangeTicks(last20),RangeTicks(last30),
        atr5,atr20, atr20==0?0:atr5/atr20,
        (price-ema9)/0.25,(price-ema21)/0.25,(price-ema50)/0.25,
        (ema9-ema21)/0.25,(ema21-ema50)/0.25,
        (price-vwap)/0.25,
        (sessionHigh-price)/0.25,(price-sessionLow)/0.25,
        BodyTicks(last3), (double)((last3.High-last3.Low)/0.25m),
        BodyTicks(last5bar), (double)((last5bar.High-last5bar.Low)/0.25m),
        up5-down5,up10-down10,
        (double)last1.Volume,
        last5.Average(x=>(double)x.Volume),
        last20.Average(x=>(double)x.Volume),
        sinTod,cosTod,
        ZoneNumber(t)
    };
}

static string[] FeatureNames()=>new[]
{
    "ret1_ticks","ret3_ticks","ret5_ticks","ret10_ticks","ret20_ticks","ret30_ticks",
    "last1_body_ticks",
    "range5_ticks","range10_ticks","range20_ticks","range30_ticks",
    "atr5","atr20","atr5_atr20_ratio",
    "price_minus_ema9","price_minus_ema21","price_minus_ema50",
    "ema9_minus_ema21","ema21_minus_ema50",
    "price_minus_vwap",
    "dist_session_high","dist_session_low",
    "last3_body_ticks","last3_range_ticks",
    "last5_body_ticks","last5_range_ticks",
    "upminusdown5","upminusdown10",
    "last_volume","avg_volume5","avg_volume20",
    "time_sin","time_cos","zone_number"
};

static int? MaxOpportunityClass(List<Bar30> future, decimal entry, bool isLong)
{
    var candidates = new (int cls,int target,int stop)[]
    {
        (3,100,50),
        (3,75,40),
        (2,50,30),
        (2,40,30),
        (1,30,30),
        (1,25,25),
        (1,20,20),
        (0,15,15),
        (0,10,10)
    };

    foreach (var c in candidates)
        if (Simulate(future,entry,c.target,c.stop,isLong)==Outcome.TargetFirst)
            return c.cls;

    return null;
}

static void EvaluateDirection(string name,List<(Sample s,int i)> idx,List<double[]> x,LogisticRegression model)
{
    if(idx.Count==0){Console.WriteLine($"{name}: no labeled samples");return;}

    int correct=0;
    int tp=0,tn=0,fp=0,fn=0;

    foreach(var item in idx)
    {
        var p=model.PredictProbability(x[item.i]);
        int pred=p>=0.5?1:0;
        int y=item.s.DirectionLabel!.Value;

        if(pred==y) correct++;
        if(pred==1&&y==1) tp++;
        else if(pred==0&&y==0) tn++;
        else if(pred==1&&y==0) fp++;
        else fn++;
    }

    Console.WriteLine($"{name}: n={idx.Count} accuracy={(double)correct/idx.Count:P2} longRecall={(tp+fn==0?0:(double)tp/(tp+fn)):P2} shortRecall={(tn+fp==0?0:(double)tn/(tn+fp)):P2}");
}

static void EvaluateOpportunity(string name,List<Sample> samples,List<double[]> x,LogisticRegression dir,SoftmaxRegression opp)
{
    int eligible=0,correct=0;
    var matrix=new int[4,4];

    for(int i=0;i<samples.Count;i++)
    {
        var s=samples[i];
        if(!s.DirectionLabel.HasValue) continue;
        int? actual=s.DirectionLabel.Value==1?s.LongClass:s.ShortClass;
        if(!actual.HasValue) continue;

        eligible++;
        int pred=ArgMax(opp.PredictProbabilities(x[i]));
        if(pred==actual.Value) correct++;
        matrix[actual.Value,pred]++;
    }

    Console.WriteLine($"{name}: n={eligible} exactClassAccuracy={(eligible==0?0:(double)correct/eligible):P2}");
    for(int a=0;a<4;a++)
        Console.WriteLine($"  actualClass={a} -> [{matrix[a,0]},{matrix[a,1]},{matrix[a,2]},{matrix[a,3]}]");
}

static void PrintDeployment(List<DeployResult> sims,int sessions)
{
    if(sims.Count==0){Console.WriteLine("  no trades");return;}

    int wins=sims.Count(x=>x.Outcome==Outcome.TargetFirst);
    int losses=sims.Count(x=>x.Outcome==Outcome.StopFirst||x.Outcome==Outcome.Time);
    int amb=sims.Count(x=>x.Outcome==Outcome.Ambiguous);

    Console.WriteLine($"  trades={sims.Count} trades/day={(sessions==0?0:(double)sims.Count/sessions):F2} wins={wins} lossesOrTime={losses} ambiguous={amb}");
    Console.WriteLine($"  winPctWorst={(double)wins/sims.Count:P2} winPctBest={(double)(wins+amb)/sims.Count:P2}");

    foreach(var qty in new[]{3,4,5})
    {
        decimal worst=0,best=0;
        foreach(var s in sims)
        {
            decimal friction=(2m*0.50m*qty)+(1.50m*qty);
            decimal win=s.Target*0.50m*qty-friction;
            decimal loss=-s.Stop*0.50m*qty-friction;

            if(s.Outcome==Outcome.TargetFirst){worst+=win;best+=win;}
            else if(s.Outcome==Outcome.Ambiguous){worst+=loss;best+=win;}
            else {worst+=loss;best+=loss;}
        }

        Console.WriteLine($"  qty={qty} worstNet=${worst:F2} worstAvgDay=${(sessions==0?0:worst/sessions):F2} bestNet=${best:F2} bestAvgDay=${(sessions==0?0:best/sessions):F2}");
    }

    Console.WriteLine("  BY ZONE");
    foreach(var g in sims.GroupBy(x=>x.Zone).OrderBy(x=>x.Key))
    {
        int gw=g.Count(x=>x.Outcome==Outcome.TargetFirst);
        int ga=g.Count(x=>x.Outcome==Outcome.Ambiguous);
        Console.WriteLine($"    {g.Key,-30} n={g.Count(),4} winWorst={(double)gw/g.Count():P1} winBest={(double)(gw+ga)/g.Count():P1}");
    }

    Console.WriteLine("  BY PREDICTED CLASS");
    foreach(var g in sims.GroupBy(x=>x.Class).OrderBy(x=>x.Key))
    {
        int gw=g.Count(x=>x.Outcome==Outcome.TargetFirst);
        int ga=g.Count(x=>x.Outcome==Outcome.Ambiguous);
        Console.WriteLine($"    class={g.Key} n={g.Count(),4} winWorst={(double)gw/g.Count():P1} winBest={(double)(gw+ga)/g.Count():P1}");
    }
}

static Outcome Simulate(List<Bar30> future,decimal entry,int targetTicks,int stopTicks,bool isLong)
{
    decimal target=isLong?entry+targetTicks*0.25m:entry-targetTicks*0.25m;
    decimal stop=isLong?entry-stopTicks*0.25m:entry+stopTicks*0.25m;

    foreach(var b in future)
    {
        bool t=isLong?b.High>=target:b.Low<=target;
        bool s=isLong?b.Low<=stop:b.High>=stop;
        if(t&&s)return Outcome.Ambiguous;
        if(t)return Outcome.TargetFirst;
        if(s)return Outcome.StopFirst;
    }
    return Outcome.Time;
}

static List<BarN> AggregateToMinute(List<Bar30> bars)
{
    DateTime Bucket(DateTime end)
    {
        if(end.Second==0)return end;
        return new DateTime(end.Year,end.Month,end.Day,end.Hour,end.Minute,0).AddMinutes(1);
    }

    var result=new List<BarN>();

    foreach(var g in bars.GroupBy(x=>new{Session=SessionDate(x),End=Bucket(x.Central)}).OrderBy(x=>x.Key.End))
    {
        var a=g.OrderBy(x=>x.Utc).ToList();
        if(a.Count!=2)continue;
        result.Add(new BarN(g.Key.End,a[0].Open,a.Max(x=>x.High),a.Min(x=>x.Low),a[^1].Close,a.Sum(x=>x.Volume)));
    }
    return result;
}

static List<BarN> AggregateMinutes(List<BarN> source,int n)
{
    var result=new List<BarN>();
    foreach(var sg in source.GroupBy(SessionDate1))
    {
        var list=sg.OrderBy(x=>x.End).ToList();
        for(int i=n-1;i<list.Count;i++)
        {
            var end=list[i].End;
            if(SessionMinute(end)%n!=0)continue;
            var a=list.Skip(i-n+1).Take(n).ToList();
            if(a.Count!=n)continue;
            bool contiguous=true;
            for(int k=1;k<a.Count;k++) if((a[k].End-a[k-1].End).TotalMinutes!=1)contiguous=false;
            if(!contiguous)continue;
            result.Add(new BarN(end,a[0].Open,a.Max(x=>x.High),a.Min(x=>x.Low),a[^1].Close,a.Sum(x=>x.Volume)));
        }
    }
    return result;
}

static double Vwap(List<BarN> bars)
{
    double pv=0,v=0;
    foreach(var b in bars)
    {
        double typical=(double)((b.High+b.Low+b.Close)/3m);
        double vol=Math.Max(1,b.Volume);
        pv+=typical*vol;
        v+=vol;
    }
    return v==0?(double)bars[^1].Close:pv/v;
}

static double Ema(List<double> values,int period)
{
    if(values.Count==0)return 0;
    double k=2.0/(period+1.0);
    double e=values[0];
    foreach(var v in values.Skip(1))e=v*k+e*(1-k);
    return e;
}

static double SessionMinute(DateTime t)
{
    var tod=t.TimeOfDay;
    if(tod>=new TimeSpan(17,0,0))return (tod-new TimeSpan(17,0,0)).TotalMinutes;
    return 420+(tod.TotalMinutes); // 17:00->24:00 is 420 min.
}

static double ZoneNumber(DateTime t)
{
    var z=ZoneAt(t);
    return z.StartsWith("01_")?1:z.StartsWith("02_")?2:z.StartsWith("03_")?3:z.StartsWith("04_")?4:
           z.StartsWith("05_")?5:z.StartsWith("06_")?6:z.StartsWith("07_")?7:z.StartsWith("08_")?8:9;
}

static int ArgMax(double[] x)
{
    int best=0;
    for(int i=1;i<x.Length;i++)if(x[i]>x[best])best=i;
    return best;
}

static Scaler FitScaler(List<double[]> xs)
{
    int p=xs[0].Length;
    var mean=new double[p];
    var sd=new double[p];

    for(int j=0;j<p;j++)
    {
        mean[j]=xs.Average(x=>x[j]);
        sd[j]=Math.Sqrt(xs.Average(x=>(x[j]-mean[j])*(x[j]-mean[j])));
        if(sd[j]<1e-9)sd[j]=1;
    }
    return new Scaler(mean,sd);
}

static List<SessionValidation> ValidateSessions(IReadOnlyList<Bar30> bars,DateTime from,DateTime to)
{
    const int expectedCritical=600;
    var result=new List<SessionValidation>();

    foreach(var g in bars.Where(IsInsideWindow).GroupBy(SessionDate).Where(g=>g.Key>=from&&g.Key<=to).OrderBy(g=>g.Key))
    {
        var x=g.OrderBy(b=>b.Utc).ToList();
        int critical=x.Count(b=>b.Central.TimeOfDay>=new TimeSpan(6,0,0)&&b.Central.TimeOfDay<new TimeSpan(11,0,0));
        int gaps=0;
        for(int i=1;i<x.Count;i++)if((x[i].Central-x[i-1].Central).TotalSeconds>45)gaps++;

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
static DateTime SessionDate1(BarN b)=>b.End.TimeOfDay>=new TimeSpan(17,0,0)?b.End.Date.AddDays(1):b.End.Date;

static string ZoneAt(DateTime c)
{
    var t=c.TimeOfDay;
    if(t>=new TimeSpan(17,0,0)&&t<new TimeSpan(19,0,0))return "01_EveningReopen";
    if(t>=new TimeSpan(19,0,0)&&t<new TimeSpan(23,0,0))return "02_AsiaDevelopment";
    if(t>=new TimeSpan(23,0,0)||t<new TimeSpan(3,0,0))return "03_OvernightTransition";
    if(t>=new TimeSpan(3,0,0)&&t<new TimeSpan(6,30,0))return "04_London_USPremarket";
    if(t>=new TimeSpan(6,30,0)&&t<new TimeSpan(8,45,0))return "05_USPreopen_OpenDevelopment";
    if(t>=new TimeSpan(8,45,0)&&t<new TimeSpan(9,5,0))return "06_NYTransition_0845_0905";
    if(t>=new TimeSpan(9,5,0)&&t<new TimeSpan(10,0,0))return "07_NYSecondary_0905_1000";
    if(t>=new TimeSpan(10,0,0)&&t<new TimeSpan(12,0,0))return "08_LateMorning";
    if(t>=new TimeSpan(12,0,0)&&t<new TimeSpan(15,0,0))return "09_Midday_EarlyAfternoon";
    return "Outside";
}

static List<Bar30> ReadBars(string path,TimeZoneInfo central)
{
    var lines=File.ReadLines(path).ToList();
    var h=lines[0].Split('\t');
    var m=h.Select((name,i)=>new{name,i}).ToDictionary(x=>x.name,x=>x.i,StringComparer.OrdinalIgnoreCase);
    int I(string n)=>m.TryGetValue(n,out var i)?i:throw new InvalidOperationException($"Missing TSV column: {n}");

    int utc=I("timestampUtc"),interval=I("intervalSeconds"),open=I("open"),high=I("high"),low=I("low"),close=I("close"),volume=I("volume");

    var r=new List<Bar30>(lines.Count-1);
    foreach(var line in lines.Skip(1))
    {
        if(string.IsNullOrWhiteSpace(line))continue;
        var p=line.Split('\t');
        var u=DateTimeOffset.Parse(p[utc],CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind);
        var c=TimeZoneInfo.ConvertTime(u,central).DateTime;

        r.Add(new Bar30{
            Utc=u,Central=DateTime.SpecifyKind(c,DateTimeKind.Unspecified),
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
    try{return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");}
    catch{return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");}
}

enum Outcome{TargetFirst,StopFirst,Ambiguous,Time}

sealed class Bar30
{
    public DateTimeOffset Utc{get;init;}
    public DateTime Central{get;init;}
    public int IntervalSeconds{get;init;}
    public decimal Open{get;init;}
    public decimal High{get;init;}
    public decimal Low{get;init;}
    public decimal Close{get;init;}
    public long Volume{get;init;}
}

sealed record BarN(DateTime End,decimal Open,decimal High,decimal Low,decimal Close,long Volume);
sealed record SessionValidation(DateTime Date,bool IsUsable,string Reason);

sealed record Sample(
    DateTime SessionDate,
    DateTime Start,
    string Zone,
    double[] Features,
    int? DirectionLabel,
    int? LongClass,
    int? ShortClass,
    Outcome Long50,
    Outcome Short50)
{
    public decimal EntryPrice { get; init; }
}

sealed record DeployResult(DateTime SessionDate,DateTime Start,string Zone,int Target,int Stop,bool Long,Outcome Outcome,double Confidence,int Class);

sealed class Scaler
{
    readonly double[] mean,sd;
    public Scaler(double[] mean,double[] sd){this.mean=mean;this.sd=sd;}
    public double[] Transform(double[] x)
    {
        var z=new double[x.Length];
        for(int i=0;i<x.Length;i++)z[i]=(x[i]-mean[i])/sd[i];
        return z;
    }
}

sealed class LogisticRegression
{
    public double[] Weights{get;}
    LogisticRegression(double[] w){Weights=w;}

    public static LogisticRegression Fit(List<double[]> xs,List<int> ys,double lambda,int iterations,double learningRate)
    {
        int p=xs[0].Length;
        var w=new double[p+1];
        int n=xs.Count;

        for(int it=0;it<iterations;it++)
        {
            var g=new double[p+1];

            for(int i=0;i<n;i++)
            {
                double z=w[0];
                for(int j=0;j<p;j++)z+=w[j+1]*xs[i][j];
                double pr=1.0/(1.0+Math.Exp(-Math.Clamp(z,-30,30)));
                double e=pr-ys[i];
                g[0]+=e;
                for(int j=0;j<p;j++)g[j+1]+=e*xs[i][j];
            }

            g[0]/=n;
            for(int j=1;j<g.Length;j++)g[j]=g[j]/n+lambda*w[j]/n;

            for(int j=0;j<w.Length;j++)w[j]-=learningRate*g[j];
        }

        return new LogisticRegression(w);
    }

    public double PredictProbability(double[] x)
    {
        double z=Weights[0];
        for(int j=0;j<x.Length;j++)z+=Weights[j+1]*x[j];
        return 1.0/(1.0+Math.Exp(-Math.Clamp(z,-30,30)));
    }
}

sealed class SoftmaxRegression
{
    readonly double[][] w;
    SoftmaxRegression(double[][] w){this.w=w;}

    public static SoftmaxRegression Fit(List<double[]> xs,List<int> ys,int classes,double lambda,int iterations,double learningRate)
    {
        int p=xs[0].Length;
        int n=xs.Count;
        var w=Enumerable.Range(0,classes).Select(_=>new double[p+1]).ToArray();

        for(int it=0;it<iterations;it++)
        {
            var g=Enumerable.Range(0,classes).Select(_=>new double[p+1]).ToArray();

            for(int i=0;i<n;i++)
            {
                var logits=new double[classes];
                for(int c=0;c<classes;c++)
                {
                    double z=w[c][0];
                    for(int j=0;j<p;j++)z+=w[c][j+1]*xs[i][j];
                    logits[c]=z;
                }

                double max=logits.Max();
                var ex=logits.Select(z=>Math.Exp(Math.Clamp(z-max,-30,30))).ToArray();
                double sum=ex.Sum();

                for(int c=0;c<classes;c++)
                {
                    double pr=ex[c]/sum;
                    double e=pr-(ys[i]==c?1.0:0.0);
                    g[c][0]+=e;
                    for(int j=0;j<p;j++)g[c][j+1]+=e*xs[i][j];
                }
            }

            for(int c=0;c<classes;c++)
            {
                g[c][0]/=n;
                w[c][0]-=learningRate*g[c][0];
                for(int j=1;j<=p;j++)
                {
                    g[c][j]=g[c][j]/n+lambda*w[c][j]/n;
                    w[c][j]-=learningRate*g[c][j];
                }
            }
        }

        return new SoftmaxRegression(w);
    }

    public double[] PredictProbabilities(double[] x)
    {
        var logits=new double[w.Length];
        for(int c=0;c<w.Length;c++)
        {
            double z=w[c][0];
            for(int j=0;j<x.Length;j++)z+=w[c][j+1]*x[j];
            logits[c]=z;
        }
        double max=logits.Max();
        var ex=logits.Select(z=>Math.Exp(Math.Clamp(z-max,-30,30))).ToArray();
        double sum=ex.Sum();
        return ex.Select(v=>v/sum).ToArray();
    }
}

