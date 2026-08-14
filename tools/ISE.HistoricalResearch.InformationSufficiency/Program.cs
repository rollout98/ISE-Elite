using System.Globalization;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: <validated-30-second-mnq-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path)) return 3;

var central = ResolveCentral();
var bars = ReadBars(path, central).OrderBy(x => x.Utc).ToList();

var validations = ValidateSessions(bars,new DateTime(2026,6,1),new DateTime(2026,7,31));
var validDates = validations.Where(x=>x.IsUsable).Select(x=>x.Date).ToHashSet();

var bySession = bars
    .Where(IsInsideWindow)
    .Where(x=>validDates.Contains(SessionDate(x)))
    .GroupBy(SessionDate)
    .ToDictionary(g=>g.Key,g=>g.OrderBy(x=>x.Utc).ToList());

Console.WriteLine("ISE Elite V7.9.8 Information Sufficiency & Direction Separability Diagnostic");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Question: does causal 30-second OHLCV contain enough information to identify the clean 50/30 winning side?");
Console.WriteLine("Training: June only. Frozen validation: July only.");
Console.WriteLine("Labels: exactly one side reaches 50 ticks before a 30-tick stop within 5 minutes; ambiguous labels excluded.");
Console.WriteLine("Models: majority baseline, linear ridge logistic, nonlinear engineered logistic, and zone-specific nonlinear logistic.");
Console.WriteLine("No model is allowed to use future information.");
Console.WriteLine($"Usable sessions: {validDates.Count}");
foreach(var v in validations.Where(x=>!x.IsUsable))
    Console.WriteLine($"EXCLUDED SESSION {v.Date:yyyy-MM-dd} reason={v.Reason}");
Console.WriteLine();

var samples = new List<Sample>();

foreach(var kv in bySession.OrderBy(x=>x.Key))
{
    var session=kv.Key;
    var s=kv.Value;

    foreach(var anchor in s.Where(x=>x.Central.Minute%5==0 && x.Central.Second==30))
    {
        var start=anchor.Central.AddSeconds(-30);
        int idx=s.FindIndex(x=>x.Utc==anchor.Utc);
        if(idx<40) continue;

        var future=s.Where(x=>x.Central>start && x.Central<=start.AddMinutes(5)).ToList();
        if(future.Count<9) continue;

        var lo=Simulate(future,anchor.Open,50,30,true);
        var sh=Simulate(future,anchor.Open,50,30,false);

        int? y=null;
        if(lo==Outcome.TargetFirst && sh!=Outcome.TargetFirst && sh!=Outcome.Ambiguous) y=1;
        else if(sh==Outcome.TargetFirst && lo!=Outcome.TargetFirst && lo!=Outcome.Ambiguous) y=0;
        if(!y.HasValue) continue;

        var prior=s.Take(idx).ToList();
        var linear=BuildLinearFeatures(prior,anchor.Open,start);
        var nonlinear=BuildNonlinearFeatures(linear,prior,anchor.Open,start);

        samples.Add(new Sample(session,start,ZoneAt(start),y.Value,linear,nonlinear));
    }
}

var june=samples.Where(x=>x.Session.Month==6).ToList();
var july=samples.Where(x=>x.Session.Month==7).ToList();

Console.WriteLine($"Clean labeled samples: total={samples.Count} June={june.Count} July={july.Count}");
Console.WriteLine($"June long={june.Count(x=>x.Y==1)} short={june.Count(x=>x.Y==0)}");
Console.WriteLine($"July long={july.Count(x=>x.Y==1)} short={july.Count(x=>x.Y==0)}");
Console.WriteLine();

var majority = june.Count(x=>x.Y==1) >= june.Count(x=>x.Y==0) ? 1 : 0;
Console.WriteLine($"MAJORITY BASELINE predicts={(majority==1?"LONG":"SHORT")}");
PrintBaseline("June",june,majority);
PrintBaseline("July",july,majority);
Console.WriteLine();

RunModel("LINEAR 30s OHLCV",june,july,x=>x.Linear,0.75);
RunModel("NONLINEAR 30s OHLCV",june,july,x=>x.Nonlinear,1.25);

Console.WriteLine();
Console.WriteLine("ZONE-SPECIFIC NONLINEAR MODELS");
foreach(var zone in samples.Select(x=>x.Zone).Distinct().OrderBy(x=>x))
{
    var jn=june.Where(x=>x.Zone==zone).ToList();
    var jl=july.Where(x=>x.Zone==zone).ToList();
    if(jn.Count<80 || jl.Count<40)
    {
        Console.WriteLine($"{zone}: insufficient samples June={jn.Count} July={jl.Count}");
        continue;
    }

    var scaler=Scaler.Fit(jn.Select(x=>x.Nonlinear).ToList());
    var trainX=jn.Select(x=>scaler.Transform(x.Nonlinear)).ToList();
    var trainY=jn.Select(x=>x.Y).ToList();
    var model=Logistic.Fit(trainX,trainY,lambda:1.0,iterations:3000,learningRate:0.025);

    var julX=jl.Select(x=>scaler.Transform(x.Nonlinear)).ToList();
    var ev=Evaluate(jl,julX,model);

    Console.WriteLine($"{zone,-30} June={jn.Count,4} July={jl.Count,4} acc={ev.Accuracy:P2} balAcc={ev.BalancedAccuracy:P2} auc={ev.Auc:F3}");
}

Console.WriteLine();
Console.WriteLine("CONFIDENCE LIFT -- JULY NONLINEAR GLOBAL MODEL");
{
    var scaler=Scaler.Fit(june.Select(x=>x.Nonlinear).ToList());
    var model=Logistic.Fit(
        june.Select(x=>scaler.Transform(x.Nonlinear)).ToList(),
        june.Select(x=>x.Y).ToList(),
        lambda:1.25,iterations:3500,learningRate:0.025);

    var scored=july.Select(x=>
    {
        var p=model.Predict(scaler.Transform(x.Nonlinear));
        int pred=p>=0.5?1:0;
        double conf=Math.Max(p,1-p);
        return new {S=x,P=p,Pred=pred,Conf=conf,Correct=pred==x.Y};
    }).ToList();

    foreach(var c in new[]{0.52,0.55,0.575,0.60,0.625,0.65,0.675,0.70})
    {
        var q=scored.Where(x=>x.Conf>=c).ToList();
        if(q.Count==0){Console.WriteLine($"conf>={c:P1}: n=0");continue;}
        Console.WriteLine($"conf>={c:P1}: n={q.Count,4} pctOfLabels={(double)q.Count/scored.Count:P1} accuracy={(double)q.Count(x=>x.Correct)/q.Count:P2}");
    }
}

Console.WriteLine();
Console.WriteLine("UNIVARIATE FEATURE LIFT -- JULY");
{
    var names=NonlinearFeatureNames();
    for(int f=0;f<names.Length;f++)
    {
        var tr=june.Select(x=>x.Nonlinear[f]).OrderBy(x=>x).ToList();
        if(tr.Count<20) continue;

        var cuts=new[]{0.2,0.4,0.6,0.8}.Select(q=>Percentile(tr,q)).ToArray();

        double weightedCorrect=0;
        int total=0;

        foreach(var s in july)
        {
            int bin=0;
            while(bin<cuts.Length && s.Nonlinear[f]>cuts[bin]) bin++;

            var trainBin=june.Where(x=>Bin(x.Nonlinear[f],cuts)==bin).ToList();
            if(trainBin.Count==0) continue;
            int pred=trainBin.Count(x=>x.Y==1)>=trainBin.Count(x=>x.Y==0)?1:0;
            if(pred==s.Y) weightedCorrect++;
            total++;
        }

        double acc=total==0?0:weightedCorrect/total;
        Console.WriteLine($"  {names[f],-34} binnedJulyAccuracy={acc:P2}");
    }
}

Console.WriteLine();
Console.WriteLine("DECISION RULE");
Console.WriteLine("If global and zone-specific frozen-July performance remains near the majority baseline,");
Console.WriteLine("30-second OHLCV is not demonstrating sufficient causal direction information for this 50/30 task.");
Console.WriteLine("The next justified data step would be tick/order-flow acquisition rather than threshold tuning.");

return 0;

static void RunModel(string name,List<Sample> june,List<Sample> july,Func<Sample,double[]> selector,double lambda)
{
    var scaler=Scaler.Fit(june.Select(selector).ToList());
    var trainX=june.Select(x=>scaler.Transform(selector(x))).ToList();
    var model=Logistic.Fit(trainX,june.Select(x=>x.Y).ToList(),lambda,3500,0.025);

    var jn=Evaluate(june,trainX,model);
    var julX=july.Select(x=>scaler.Transform(selector(x))).ToList();
    var jl=Evaluate(july,julX,model);

    Console.WriteLine(name);
    Console.WriteLine($"  June accuracy={jn.Accuracy:P2} balAcc={jn.BalancedAccuracy:P2} auc={jn.Auc:F3}");
    Console.WriteLine($"  July  accuracy={jl.Accuracy:P2} balAcc={jl.BalancedAccuracy:P2} auc={jl.Auc:F3}");
    Console.WriteLine();
}

static Eval Evaluate(List<Sample> samples,List<double[]> xs,Logistic model)
{
    int correct=0,tp=0,tn=0,fp=0,fn=0;
    var scored=new List<(double p,int y)>();

    for(int i=0;i<samples.Count;i++)
    {
        double p=model.Predict(xs[i]);
        int pred=p>=0.5?1:0;
        int y=samples[i].Y;
        scored.Add((p,y));

        if(pred==y)correct++;
        if(pred==1&&y==1)tp++;
        else if(pred==0&&y==0)tn++;
        else if(pred==1)fp++;
        else fn++;
    }

    double tpr=tp+fn==0?0:(double)tp/(tp+fn);
    double tnr=tn+fp==0?0:(double)tn/(tn+fp);

    return new Eval(
        samples.Count==0?0:(double)correct/samples.Count,
        (tpr+tnr)/2.0,
        Auc(scored));
}

static double Auc(List<(double p,int y)> scored)
{
    var pos=scored.Where(x=>x.y==1).ToList();
    var neg=scored.Where(x=>x.y==0).ToList();
    if(pos.Count==0||neg.Count==0)return 0.5;

    double wins=0;
    foreach(var p in pos)
    foreach(var n in neg)
    {
        if(p.p>n.p)wins+=1;
        else if(Math.Abs(p.p-n.p)<1e-12)wins+=0.5;
    }
    return wins/(pos.Count*neg.Count);
}

static void PrintBaseline(string name,List<Sample> x,int majority)
{
    int correct=x.Count(s=>s.Y==majority);
    int longs=x.Count(s=>s.Y==1), shorts=x.Count-longs;
    double tpr=majority==1?1:0;
    double tnr=majority==0?1:0;
    Console.WriteLine($"{name}: n={x.Count} accuracy={(double)correct/x.Count:P2} balancedAccuracy={(tpr+tnr)/2:P2} long={longs} short={shorts}");
}

static double[] BuildLinearFeatures(List<Bar30> prior,decimal entry,DateTime t)
{
    decimal tick=0.25m;
    var p=prior;

    double Ret(int n)
    {
        if(p.Count<n)return 0;
        return (double)((entry-p[^n].Open)/tick);
    }

    double Range(int n)
    {
        var x=p.TakeLast(Math.Min(n,p.Count)).ToList();
        return (double)((x.Max(b=>b.High)-x.Min(b=>b.Low))/tick);
    }

    double AvgRange(int n)
    {
        var x=p.TakeLast(Math.Min(n,p.Count)).ToList();
        return x.Average(b=>(double)((b.High-b.Low)/tick));
    }

    var last=p[^1];
    var last10=p.TakeLast(10).ToList();
    var last20=p.TakeLast(20).ToList();
    var last40=p.TakeLast(40).ToList();

    double signed10=last10.Sum(b=>Math.Sign(b.Close-b.Open)*(double)b.Volume);
    double signed20=last20.Sum(b=>Math.Sign(b.Close-b.Open)*(double)b.Volume);

    double hi20=(double)last20.Max(x=>x.High), lo20=(double)last20.Min(x=>x.Low);
    double hi40=(double)last40.Max(x=>x.High), lo40=(double)last40.Min(x=>x.Low);
    double price=(double)entry;

    double sessionHigh=(double)p.Max(x=>x.High);
    double sessionLow=(double)p.Min(x=>x.Low);

    var closes=p.Select(x=>(double)x.Close).ToList();
    double ema18=Ema(closes,18);  // 9 minutes on 30s
    double ema42=Ema(closes,42);  // 21 minutes on 30s
    double ema100=Ema(closes,100);// 50 minutes on 30s

    double minute=SessionMinute(t);

    return new[]
    {
        Ret(1),Ret(2),Ret(3),Ret(4),Ret(6),Ret(10),Ret(20),Ret(40),
        (double)((last.Close-last.Open)/tick),
        (double)((last.High-last.Low)/tick),
        (double)((last.High-Math.Max(last.Open,last.Close))/tick),
        (double)((Math.Min(last.Open,last.Close)-last.Low)/tick),
        Range(4),Range(10),Range(20),Range(40),
        AvgRange(4),AvgRange(10),AvgRange(40),
        (double)last.Volume,
        last10.Average(x=>(double)x.Volume),
        last40.Average(x=>(double)x.Volume),
        signed10,signed20,
        (price-ema18)/0.25,(price-ema42)/0.25,(price-ema100)/0.25,
        (ema18-ema42)/0.25,(ema42-ema100)/0.25,
        (sessionHigh-price)/0.25,(price-sessionLow)/0.25,
        (price-hi20)/0.25,(price-lo20)/0.25,
        (price-hi40)/0.25,(price-lo40)/0.25,
        Math.Sin(2*Math.PI*minute/1320.0),
        Math.Cos(2*Math.PI*minute/1320.0),
        ZoneNumber(t)
    };
}

static double[] BuildNonlinearFeatures(double[] b,List<Bar30> prior,decimal entry,DateTime t)
{
    var x=new List<double>(b);

    // Selected sign, magnitude and interaction terms to test nonlinear separability
    // without using future information.
    int[] signedIdx={0,1,3,5,6,7,8,10,11,22,23,25,26,27,28,30,31,32,33,34};
    foreach(int i in signedIdx)
    {
        x.Add(Math.Abs(b[i]));
        x.Add(Math.Sign(b[i]));
    }

    // Momentum x volatility / trend / location interactions.
    x.Add(b[5]*b[16]);
    x.Add(b[5]*b[17]);
    x.Add(b[7]*b[18]);
    x.Add(b[5]*b[27]);
    x.Add(b[5]*b[28]);
    x.Add(b[27]*b[28]);
    x.Add(b[22]*b[5]);
    x.Add(b[23]*b[5]);
    x.Add(b[30]*b[5]);
    x.Add(b[31]*b[5]);

    // Recent 30-second candle directional sequence.
    var last8=prior.TakeLast(8).ToList();
    foreach(var q in last8)
    {
        x.Add(Math.Sign((double)(q.Close-q.Open)));
        x.Add((double)((q.Close-q.Open)/0.25m));
        x.Add((double)((q.High-q.Low)/0.25m));
    }

    return x.ToArray();
}

static string[] NonlinearFeatureNames()
{
    var baseNames=new[]
    {
        "ret_30s","ret_60s","ret_90s","ret_2m","ret_3m","ret_5m","ret_10m","ret_20m",
        "last_body","last_range","upper_wick","lower_wick",
        "range_2m","range_5m","range_10m","range_20m",
        "avg_range_2m","avg_range_5m","avg_range_20m",
        "last_volume","avg_volume_5m","avg_volume_20m",
        "signed_volume_5m","signed_volume_10m",
        "price_ema9m","price_ema21m","price_ema50m",
        "ema9m_ema21m","ema21m_ema50m",
        "dist_session_high","dist_session_low",
        "dist_10m_high","dist_10m_low","dist_20m_high","dist_20m_low",
        "time_sin","time_cos","zone_number"
    };

    var names=new List<string>(baseNames);
    int[] signedIdx={0,1,3,5,6,7,8,10,11,22,23,25,26,27,28,30,31,32,33,34};
    foreach(int i in signedIdx){names.Add("abs_"+baseNames[i]);names.Add("sign_"+baseNames[i]);}
    names.AddRange(new[]{
        "ret5m_x_avgRange2m","ret5m_x_avgRange5m","ret20m_x_avgRange20m",
        "ret5m_x_ema9_21","ret5m_x_ema21_50","emaSpreadInteraction",
        "signedVol5m_x_ret5m","signedVol10m_x_ret5m",
        "distSessionLow_x_ret5m","dist10mHigh_x_ret5m"
    });

    for(int i=0;i<8;i++)
    {
        names.Add($"bar{i}_sign");
        names.Add($"bar{i}_body");
        names.Add($"bar{i}_range");
    }
    return names.ToArray();
}

static int Bin(double value,double[] cuts)
{
    int b=0;
    while(b<cuts.Length && value>cuts[b])b++;
    return b;
}

static double Percentile(List<double> sorted,double q)
{
    if(sorted.Count==0)return 0;
    int i=(int)Math.Floor(q*(sorted.Count-1));
    return sorted[Math.Clamp(i,0,sorted.Count-1)];
}

static double Ema(List<double> values,int period)
{
    if(values.Count==0)return 0;
    double k=2.0/(period+1.0),e=values[0];
    foreach(var v in values.Skip(1))e=v*k+e*(1-k);
    return e;
}

static Outcome Simulate(List<Bar30> future,decimal entry,int targetTicks,int stopTicks,bool isLong)
{
    decimal target=isLong?entry+targetTicks*0.25m:entry-targetTicks*0.25m;
    decimal stop=isLong?entry-stopTicks*0.25m:entry+stopTicks*0.25m;
    foreach(var b in future)
    {
        bool th=isLong?b.High>=target:b.Low<=target;
        bool sh=isLong?b.Low<=stop:b.High>=stop;
        if(th&&sh)return Outcome.Ambiguous;
        if(th)return Outcome.TargetFirst;
        if(sh)return Outcome.StopFirst;
    }
    return Outcome.Time;
}

static double SessionMinute(DateTime t)
{
    var tod=t.TimeOfDay;
    if(tod>=new TimeSpan(17,0,0))return (tod-new TimeSpan(17,0,0)).TotalMinutes;
    return 420+tod.TotalMinutes;
}

static double ZoneNumber(DateTime t)
{
    var z=ZoneAt(t);
    return z.StartsWith("01_")?1:z.StartsWith("02_")?2:z.StartsWith("03_")?3:z.StartsWith("04_")?4:
           z.StartsWith("05_")?5:z.StartsWith("06_")?6:z.StartsWith("07_")?7:z.StartsWith("08_")?8:9;
}

static List<SessionValidation> ValidateSessions(IReadOnlyList<Bar30> bars,DateTime from,DateTime to)
{
    const int expectedCritical=600;
    var r=new List<SessionValidation>();
    foreach(var g in bars.Where(IsInsideWindow).GroupBy(SessionDate).Where(g=>g.Key>=from&&g.Key<=to).OrderBy(g=>g.Key))
    {
        var x=g.OrderBy(b=>b.Utc).ToList();
        int critical=x.Count(b=>b.Central.TimeOfDay>=new TimeSpan(6,0,0)&&b.Central.TimeOfDay<new TimeSpan(11,0,0));
        int gaps=0;
        for(int i=1;i<x.Count;i++)if((x[i].Central-x[i-1].Central).TotalSeconds>45)gaps++;
        bool usable=critical==expectedCritical&&gaps==0;
        string reason=usable?"OK":critical!=expectedCritical?$"CriticalWindowIncomplete({critical}/{expectedCritical})":$"InternalGaps({gaps})";
        r.Add(new SessionValidation(g.Key,usable,reason));
    }
    return r;
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
sealed record SessionValidation(DateTime Date,bool IsUsable,string Reason);
sealed record Sample(DateTime Session,DateTime Start,string Zone,int Y,double[] Linear,double[] Nonlinear);
sealed record Eval(double Accuracy,double BalancedAccuracy,double Auc);

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

sealed class Scaler
{
    readonly double[] mean,sd;
    Scaler(double[] mean,double[] sd){this.mean=mean;this.sd=sd;}

    public static Scaler Fit(List<double[]> xs)
    {
        int p=xs[0].Length;
        var m=new double[p];
        var s=new double[p];
        for(int j=0;j<p;j++)
        {
            m[j]=xs.Average(x=>x[j]);
            s[j]=Math.Sqrt(xs.Average(x=>(x[j]-m[j])*(x[j]-m[j])));
            if(s[j]<1e-9)s[j]=1;
        }
        return new Scaler(m,s);
    }

    public double[] Transform(double[] x)
    {
        var z=new double[x.Length];
        for(int i=0;i<x.Length;i++)z[i]=(x[i]-mean[i])/sd[i];
        return z;
    }
}

sealed class Logistic
{
    readonly double[] w;
    Logistic(double[] w){this.w=w;}

    public static Logistic Fit(List<double[]> xs,List<int> ys,double lambda,int iterations,double learningRate)
    {
        int p=xs[0].Length,n=xs.Count;
        var w=new double[p+1];

        for(int it=0;it<iterations;it++)
        {
            var g=new double[p+1];

            for(int i=0;i<n;i++)
            {
                double z=w[0];
                for(int j=0;j<p;j++)z+=w[j+1]*xs[i][j];
                double pr=1/(1+Math.Exp(-Math.Clamp(z,-30,30)));
                double e=pr-ys[i];
                g[0]+=e;
                for(int j=0;j<p;j++)g[j+1]+=e*xs[i][j];
            }

            g[0]/=n;
            w[0]-=learningRate*g[0];

            for(int j=1;j<=p;j++)
            {
                g[j]=g[j]/n+lambda*w[j]/n;
                w[j]-=learningRate*g[j];
            }
        }

        return new Logistic(w);
    }

    public double Predict(double[] x)
    {
        double z=w[0];
        for(int j=0;j<x.Length;j++)z+=w[j+1]*x[j];
        return 1/(1+Math.Exp(-Math.Clamp(z,-30,30)));
    }
}
