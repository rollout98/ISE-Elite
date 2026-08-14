using System.Globalization;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: <tick-anchor-feature-tsv>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
if (!File.Exists(path)) return 3;

var rows = ReadRows(path);

Console.WriteLine("ISE Elite V7.9.11 Tick-Level Direction Separability & Exact-Path Classifier");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Source: V7.9.10 compact anchor dataset derived from true MNQ Last-tick sequencing.");
Console.WriteLine("Training: June 2026 only. Frozen validation: July 2026 only.");
Console.WriteLine("Models: majority baseline, ridge logistic, nonlinear ridge logistic, boosted decision stumps.");
Console.WriteLine("Labels require exactly one direction to reach target before stop; both-win and neither-win anchors are excluded from directional training.");
Console.WriteLine();

Console.WriteLine($"Rows total={rows.Count} June={rows.Count(x=>x.TradingDay.Month==6)} July={rows.Count(x=>x.TradingDay.Month==7)}");
Console.WriteLine($"Trading days total={rows.Select(x=>x.TradingDay).Distinct().Count()} June={rows.Where(x=>x.TradingDay.Month==6).Select(x=>x.TradingDay).Distinct().Count()} July={rows.Where(x=>x.TradingDay.Month==7).Select(x=>x.TradingDay).Distinct().Count()}");
Console.WriteLine();

var pairs = new[]
{
    new PairSpec("30/30", "Long30", "Short30", 30, 30),
    new PairSpec("50/30", "Long50", "Short50", 50, 30),
    new PairSpec("75/40", "Long75", "Short75", 75, 40),
    new PairSpec("100/50", "Long100", "Short100", 100, 50)
};

foreach (var pair in pairs)
{
    Console.WriteLine(new string('=', 118));
    Console.WriteLine($"PAIR {pair.Name}");
    Console.WriteLine(new string('=', 118));

    var labeled = rows
        .Select(r => ToDirectionalSample(r, pair))
        .Where(x => x != null)
        .Select(x => x!)
        .ToList();

    var june = labeled.Where(x=>x.Row.TradingDay.Month==6).ToList();
    var july = labeled.Where(x=>x.Row.TradingDay.Month==7).ToList();

    int totalBoth = rows.Count(r => IsTarget(r,pair.LongColumn) && IsTarget(r,pair.ShortColumn));
    int totalNeither = rows.Count(r => !IsTarget(r,pair.LongColumn) && !IsTarget(r,pair.ShortColumn));

    Console.WriteLine($"allAnchors={rows.Count} cleanDirectional={labeled.Count} bothTargetFirst={totalBoth} neitherTargetFirst={totalNeither}");
    Console.WriteLine($"June clean={june.Count} long={june.Count(x=>x.Y==1)} short={june.Count(x=>x.Y==0)}");
    Console.WriteLine($"July clean={july.Count} long={july.Count(x=>x.Y==1)} short={july.Count(x=>x.Y==0)}");
    Console.WriteLine();

    if (june.Count < 100 || july.Count < 100)
    {
        Console.WriteLine("Insufficient clean labels for frozen study.");
        continue;
    }

    int majority = june.Count(x=>x.Y==1) >= june.Count(x=>x.Y==0) ? 1 : 0;
    PrintBaseline("June", june, majority);
    PrintBaseline("July", july, majority);
    Console.WriteLine();

    RunLogistic("LINEAR TICK FEATURES", june, july, x=>x.Linear, 1.0);
    RunLogistic("NONLINEAR TICK FEATURES", june, july, x=>x.Nonlinear, 1.5);
    RunBoostedStumps(june, july);

    Console.WriteLine("ZONE-SPECIFIC NONLINEAR LOGISTIC -- JULY");
    foreach (var zone in labeled.Select(x=>x.Row.Zone).Distinct().OrderBy(x=>x))
    {
        var jn = june.Where(x=>x.Row.Zone==zone).ToList();
        var jl = july.Where(x=>x.Row.Zone==zone).ToList();

        if (jn.Count < 80 || jl.Count < 40)
        {
            Console.WriteLine($"  {zone,-30} insufficient June={jn.Count} July={jl.Count}");
            continue;
        }

        var scaler = Scaler.Fit(jn.Select(x=>x.Nonlinear).ToList());
        var model = Logistic.Fit(
            jn.Select(x=>scaler.Transform(x.Nonlinear)).ToList(),
            jn.Select(x=>x.Y).ToList(),
            lambda:1.0,iterations:3000,learningRate:0.025);

        var ev = Evaluate(
            jl,
            jl.Select(x=>scaler.Transform(x.Nonlinear)).ToList(),
            model);

        Console.WriteLine($"  {zone,-30} June={jn.Count,4} July={jl.Count,4} acc={ev.Accuracy:P2} balAcc={ev.BalancedAccuracy:P2} auc={ev.Auc:F3}");
    }

    Console.WriteLine();
    Console.WriteLine("CONFIDENCE LIFT -- JULY NONLINEAR GLOBAL");
    {
        var scaler = Scaler.Fit(june.Select(x=>x.Nonlinear).ToList());
        var model = Logistic.Fit(
            june.Select(x=>scaler.Transform(x.Nonlinear)).ToList(),
            june.Select(x=>x.Y).ToList(),
            lambda:1.5,iterations:3500,learningRate:0.025);

        var scored = july.Select(x =>
        {
            double p = model.Predict(scaler.Transform(x.Nonlinear));
            int pred = p >= 0.5 ? 1 : 0;
            double conf = Math.Max(p,1-p);
            return new { Sample=x, P=p, Pred=pred, Conf=conf, Correct=pred==x.Y };
        }).ToList();

        foreach (var c in new[]{0.52,0.55,0.575,0.60,0.625,0.65,0.675,0.70})
        {
            var q = scored.Where(x=>x.Conf>=c).ToList();
            if (q.Count == 0)
            {
                Console.WriteLine($"  conf>={c:P1}: n=0");
                continue;
            }

            Console.WriteLine($"  conf>={c:P1}: n={q.Count,4} pctLabels={(double)q.Count/scored.Count:P1} accuracy={(double)q.Count(x=>x.Correct)/q.Count:P2}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("JULY ECONOMIC DIAGNOSTIC -- NONLINEAR GLOBAL, CLEAN LABELS ONLY");
    {
        var scaler = Scaler.Fit(june.Select(x=>x.Nonlinear).ToList());
        var model = Logistic.Fit(
            june.Select(x=>scaler.Transform(x.Nonlinear)).ToList(),
            june.Select(x=>x.Y).ToList(),
            lambda:1.5,iterations:3500,learningRate:0.025);

        foreach (var c in new[]{0.55,0.60,0.65})
        {
            var trades = new List<(bool Win,DateTime Day)>();

            foreach (var s in july)
            {
                double p = model.Predict(scaler.Transform(s.Nonlinear));
                bool goLong = p >= 0.5;
                double conf = Math.Max(p,1-p);
                if (conf < c) continue;

                bool win = (goLong && s.Y==1) || (!goLong && s.Y==0);
                trades.Add((win,s.Row.TradingDay));
            }

            int sessions = july.Select(x=>x.Row.TradingDay).Distinct().Count();
            Console.WriteLine($"  conf>={c:P0} trades={trades.Count} trades/day={(sessions==0?0:(double)trades.Count/sessions):F2} winPct={(trades.Count==0?0:(double)trades.Count(x=>x.Win)/trades.Count):P2}");

            foreach (var qty in new[]{3,4,5})
            {
                decimal friction = 2.50m * qty;
                decimal winNet = pair.TargetTicks*0.50m*qty - friction;
                decimal lossNet = -pair.StopTicks*0.50m*qty - friction;
                decimal net = trades.Sum(t=>t.Win?winNet:lossNet);
                Console.WriteLine($"    qty={qty} net=${net:F2} avgDay=${(sessions==0?0:net/sessions):F2}");
            }
        }
    }

    Console.WriteLine();
}

Console.WriteLine(new string('=',118));
Console.WriteLine("DECISION RULE");
Console.WriteLine("If frozen-July tick-feature models materially exceed majority baseline and confidence produces monotonic lift,");
Console.WriteLine("continue into tick-feature refinement and out-of-sample August validation.");
Console.WriteLine("If performance remains near chance, Last-tick direction alone is insufficient and the next justified data layer is historical Bid/Ask/order-flow.");
Console.WriteLine(new string('=',118));

return 0;

static DirectionalSample? ToDirectionalSample(Row r, PairSpec p)
{
    bool l = IsTarget(r,p.LongColumn);
    bool s = IsTarget(r,p.ShortColumn);

    if (l == s) return null;

    int y = l ? 1 : 0;
    var linear = BuildLinear(r);
    var nonlinear = BuildNonlinear(linear,r);
    return new DirectionalSample(r,y,linear,nonlinear);
}

static bool IsTarget(Row r,string col) => col switch
{
    "Long30" => r.Long30=="TargetFirst",
    "Short30" => r.Short30=="TargetFirst",
    "Long50" => r.Long50=="TargetFirst",
    "Short50" => r.Short50=="TargetFirst",
    "Long75" => r.Long75=="TargetFirst",
    "Short75" => r.Short75=="TargetFirst",
    "Long100" => r.Long100=="TargetFirst",
    "Short100" => r.Short100=="TargetFirst",
    _ => false
};

static double[] BuildLinear(Row r)
{
    double priorTicks = Math.Max(1,r.PriorTicks);
    double priorVolume = Math.Max(1,r.PriorVolume);
    double directionalTicks = Math.Max(1,r.Upticks+r.Downticks);

    return new[]
    {
        (double)r.PriorRangeTicks,
        (double)r.PriorReturnTicks,
        r.PriorTicks,
        r.PriorVolume,
        (double)r.TickImbalance,
        (double)r.VolumeImbalance,
        r.Reversals,
        r.MaxRunUp,
        r.MaxRunDown,
        (double)(r.PriorClose-r.PriorOpen)/0.25,
        (double)(r.PriorHigh-r.PriorClose)/0.25,
        (double)(r.PriorClose-r.PriorLow)/0.25,
        r.Upticks/priorTicks,
        r.Downticks/priorTicks,
        r.ZeroTicks/priorTicks,
        r.UpVolume/priorVolume,
        r.DownVolume/priorVolume,
        r.Reversals/directionalTicks,
        r.MaxRunUp-r.MaxRunDown,
        Math.Log(1+r.PriorTicks),
        Math.Log(1+r.PriorVolume),
        ZoneNumber(r.Zone),
        Math.Sin(2*Math.PI*SessionMinute(r.Anchor)/1320.0),
        Math.Cos(2*Math.PI*SessionMinute(r.Anchor)/1320.0)
    };
}

static double[] BuildNonlinear(double[] b,Row r)
{
    var x = new List<double>(b);

    int[] signedIdx = {1,4,5,9,10,11,18};
    foreach(int i in signedIdx)
    {
        x.Add(Math.Abs(b[i]));
        x.Add(Math.Sign(b[i]));
        x.Add(b[i]*b[i]);
    }

    x.Add(b[1]*b[4]);
    x.Add(b[1]*b[5]);
    x.Add(b[4]*b[5]);
    x.Add(b[1]*b[6]);
    x.Add(b[4]*b[6]);
    x.Add(b[18]*b[4]);
    x.Add(b[18]*b[5]);
    x.Add(b[0]*b[4]);
    x.Add(b[0]*b[5]);
    x.Add(b[19]*b[4]);
    x.Add(b[20]*b[5]);

    return x.ToArray();
}

static void RunLogistic(
    string name,
    List<DirectionalSample> june,
    List<DirectionalSample> july,
    Func<DirectionalSample,double[]> selector,
    double lambda)
{
    var scaler = Scaler.Fit(june.Select(selector).ToList());
    var trainX = june.Select(x=>scaler.Transform(selector(x))).ToList();
    var model = Logistic.Fit(trainX,june.Select(x=>x.Y).ToList(),lambda,3500,0.025);

    var jn = Evaluate(june,trainX,model);
    var julX = july.Select(x=>scaler.Transform(selector(x))).ToList();
    var jl = Evaluate(july,julX,model);

    Console.WriteLine(name);
    Console.WriteLine($"  June accuracy={jn.Accuracy:P2} balAcc={jn.BalancedAccuracy:P2} auc={jn.Auc:F3}");
    Console.WriteLine($"  July  accuracy={jl.Accuracy:P2} balAcc={jl.BalancedAccuracy:P2} auc={jl.Auc:F3}");
    Console.WriteLine();
}

static void RunBoostedStumps(List<DirectionalSample> june,List<DirectionalSample> july)
{
    var scaler = Scaler.Fit(june.Select(x=>x.Nonlinear).ToList());
    var xTrain = june.Select(x=>scaler.Transform(x.Nonlinear)).ToList();
    var yTrain = june.Select(x=>x.Y).ToList();
    var model = BoostedStumps.Fit(xTrain,yTrain,rounds:80);

    var xJuly = july.Select(x=>scaler.Transform(x.Nonlinear)).ToList();

    int correct=0,tp=0,tn=0,fp=0,fn=0;
    var scored = new List<(double p,int y)>();

    for(int i=0;i<july.Count;i++)
    {
        double p=model.PredictProbability(xJuly[i]);
        int pred=p>=0.5?1:0;
        int y=july[i].Y;
        scored.Add((p,y));
        if(pred==y)correct++;
        if(pred==1&&y==1)tp++;
        else if(pred==0&&y==0)tn++;
        else if(pred==1)fp++;
        else fn++;
    }

    double tpr=tp+fn==0?0:(double)tp/(tp+fn);
    double tnr=tn+fp==0?0:(double)tn/(tn+fp);

    Console.WriteLine("BOOSTED DECISION STUMPS");
    Console.WriteLine($"  July accuracy={(double)correct/july.Count:P2} balAcc={(tpr+tnr)/2:P2} auc={Auc(scored):F3}");
    Console.WriteLine();
}

static Eval Evaluate(List<DirectionalSample> samples,List<double[]> xs,Logistic model)
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
        (tpr+tnr)/2,
        Auc(scored));
}

static double Auc(List<(double p,int y)> scored)
{
    var ordered=scored.OrderBy(x=>x.p).ToList();
    long pos=ordered.Count(x=>x.y==1), neg=ordered.Count-pos;
    if(pos==0||neg==0)return 0.5;

    double rankSum=0;
    int i=0;
    while(i<ordered.Count)
    {
        int j=i+1;
        while(j<ordered.Count && Math.Abs(ordered[j].p-ordered[i].p)<1e-12)j++;
        double avgRank=((i+1)+j)/2.0;
        for(int k=i;k<j;k++) if(ordered[k].y==1) rankSum+=avgRank;
        i=j;
    }

    return (rankSum-pos*(pos+1)/2.0)/(pos*neg);
}

static void PrintBaseline(string name,List<DirectionalSample> x,int majority)
{
    int correct=x.Count(s=>s.Y==majority);
    int longs=x.Count(s=>s.Y==1), shorts=x.Count-longs;
    Console.WriteLine($"MAJORITY {name}: predicts={(majority==1?"LONG":"SHORT")} n={x.Count} accuracy={(double)correct/x.Count:P2} long={longs} short={shorts}");
}

static double SessionMinute(DateTime t)
{
    var tod=t.TimeOfDay;
    if(tod>=new TimeSpan(17,0,0)) return (tod-new TimeSpan(17,0,0)).TotalMinutes;
    return 420+tod.TotalMinutes;
}

static double ZoneNumber(string z)
{
    if(z.StartsWith("01_"))return 1;
    if(z.StartsWith("02_"))return 2;
    if(z.StartsWith("03_"))return 3;
    if(z.StartsWith("04_"))return 4;
    if(z.StartsWith("05_"))return 5;
    if(z.StartsWith("06_"))return 6;
    if(z.StartsWith("07_"))return 7;
    if(z.StartsWith("08_"))return 8;
    return 9;
}

static List<Row> ReadRows(string path)
{
    var lines=File.ReadLines(path).ToList();
    var h=lines[0].Split('\t');
    var m=h.Select((name,i)=>new{name,i}).ToDictionary(x=>x.name,x=>x.i,StringComparer.OrdinalIgnoreCase);
    int I(string n)=>m.TryGetValue(n,out var i)?i:throw new InvalidOperationException($"Missing TSV column: {n}");

    int tradingDay=I("tradingDay"), contract=I("contract"), anchor=I("anchorLocal"), zone=I("zone"),
        entry=I("entry"), priorTicks=I("priorTicks"), priorVolume=I("priorVolume"), priorOpen=I("priorOpen"),
        priorHigh=I("priorHigh"), priorLow=I("priorLow"), priorClose=I("priorClose"),
        priorRangeTicks=I("priorRangeTicks"), priorReturnTicks=I("priorReturnTicks"),
        upticks=I("upticks"), downticks=I("downticks"), zeroTicks=I("zeroTicks"),
        upVolume=I("upVolume"), downVolume=I("downVolume"), tickImbalance=I("tickImbalance"),
        volumeImbalance=I("volumeImbalance"), reversals=I("reversals"), maxRunUp=I("maxRunUp"),
        maxRunDown=I("maxRunDown"), long30=I("long30"), short30=I("short30"),
        long50=I("long50"), short50=I("short50"), long75=I("long75"), short75=I("short75"),
        long100=I("long100"), short100=I("short100");

    var r=new List<Row>(lines.Count-1);

    foreach(var line in lines.Skip(1))
    {
        if(string.IsNullOrWhiteSpace(line))continue;
        var p=line.Split('\t');

        r.Add(new Row(
            DateTime.ParseExact(p[tradingDay],"yyyy-MM-dd",CultureInfo.InvariantCulture),
            p[contract],
            DateTime.ParseExact(p[anchor],"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture),
            p[zone],
            decimal.Parse(p[entry],CultureInfo.InvariantCulture),
            int.Parse(p[priorTicks],CultureInfo.InvariantCulture),
            long.Parse(p[priorVolume],CultureInfo.InvariantCulture),
            decimal.Parse(p[priorOpen],CultureInfo.InvariantCulture),
            decimal.Parse(p[priorHigh],CultureInfo.InvariantCulture),
            decimal.Parse(p[priorLow],CultureInfo.InvariantCulture),
            decimal.Parse(p[priorClose],CultureInfo.InvariantCulture),
            decimal.Parse(p[priorRangeTicks],CultureInfo.InvariantCulture),
            decimal.Parse(p[priorReturnTicks],CultureInfo.InvariantCulture),
            int.Parse(p[upticks],CultureInfo.InvariantCulture),
            int.Parse(p[downticks],CultureInfo.InvariantCulture),
            int.Parse(p[zeroTicks],CultureInfo.InvariantCulture),
            long.Parse(p[upVolume],CultureInfo.InvariantCulture),
            long.Parse(p[downVolume],CultureInfo.InvariantCulture),
            decimal.Parse(p[tickImbalance],CultureInfo.InvariantCulture),
            decimal.Parse(p[volumeImbalance],CultureInfo.InvariantCulture),
            int.Parse(p[reversals],CultureInfo.InvariantCulture),
            int.Parse(p[maxRunUp],CultureInfo.InvariantCulture),
            int.Parse(p[maxRunDown],CultureInfo.InvariantCulture),
            p[long30],p[short30],p[long50],p[short50],p[long75],p[short75],p[long100],p[short100]
        ));
    }

    return r;
}

sealed record PairSpec(string Name,string LongColumn,string ShortColumn,int TargetTicks,int StopTicks);
sealed record Eval(double Accuracy,double BalancedAccuracy,double Auc);

sealed record Row(
    DateTime TradingDay,string Contract,DateTime Anchor,string Zone,decimal Entry,
    int PriorTicks,long PriorVolume,decimal PriorOpen,decimal PriorHigh,decimal PriorLow,decimal PriorClose,
    decimal PriorRangeTicks,decimal PriorReturnTicks,
    int Upticks,int Downticks,int ZeroTicks,long UpVolume,long DownVolume,
    decimal TickImbalance,decimal VolumeImbalance,int Reversals,int MaxRunUp,int MaxRunDown,
    string Long30,string Short30,string Long50,string Short50,
    string Long75,string Short75,string Long100,string Short100);

sealed record DirectionalSample(Row Row,int Y,double[] Linear,double[] Nonlinear);

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

sealed class BoostedStumps
{
    readonly List<Stump> stumps;
    BoostedStumps(List<Stump> stumps){this.stumps=stumps;}

    public static BoostedStumps Fit(List<double[]> xs,List<int> ys,int rounds)
    {
        int n=xs.Count,p=xs[0].Length;
        var weights=Enumerable.Repeat(1.0/n,n).ToArray();
        var result=new List<Stump>();

        for(int round=0;round<rounds;round++)
        {
            Stump? best=null;
            double bestErr=double.MaxValue;

            for(int f=0;f<p;f++)
            {
                var vals=xs.Select(x=>x[f]).OrderBy(x=>x).ToList();
                var thresholds=new[]{0.1,0.25,0.4,0.5,0.6,0.75,0.9}
                    .Select(q=>vals[(int)Math.Floor(q*(vals.Count-1))])
                    .Distinct();

                foreach(var th in thresholds)
                foreach(var polarity in new[]{1,-1})
                {
                    double err=0;
                    for(int i=0;i<n;i++)
                    {
                        int pred=(xs[i][f]>=th?1:0);
                        if(polarity<0)pred=1-pred;
                        if(pred!=ys[i])err+=weights[i];
                    }

                    if(err<bestErr)
                    {
                        bestErr=err;
                        best=new Stump(f,th,polarity,0);
                    }
                }
            }

            if(best==null || bestErr>=0.499)break;
            bestErr=Math.Max(1e-9,bestErr);
            double alpha=0.5*Math.Log((1-bestErr)/bestErr);
            best=best with { Alpha=alpha };
            result.Add(best);

            double sum=0;
            for(int i=0;i<n;i++)
            {
                int pred=(xs[i][best.Feature]>=best.Threshold?1:0);
                if(best.Polarity<0)pred=1-pred;

                int y=ys[i]==1?1:-1;
                int h=pred==1?1:-1;
                weights[i]*=Math.Exp(-alpha*y*h);
                sum+=weights[i];
            }

            for(int i=0;i<n;i++)weights[i]/=sum;
        }

        return new BoostedStumps(result);
    }

    public double PredictProbability(double[] x)
    {
        double score=0;
        foreach(var s in stumps)
        {
            int pred=(x[s.Feature]>=s.Threshold?1:0);
            if(s.Polarity<0)pred=1-pred;
            score+=s.Alpha*(pred==1?1:-1);
        }
        return 1/(1+Math.Exp(-2*Math.Clamp(score,-20,20)));
    }

    sealed record Stump(int Feature,double Threshold,int Polarity,double Alpha);
}
