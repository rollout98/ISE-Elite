using System.Globalization;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: <first-orderflow.tsv> <resume-orderflow.tsv> <tick-labels.tsv>");
    return 2;
}

var first = ReadQuoteRows(args[0]);
var resume = ReadQuoteRows(args[1]);
var labels = ReadLabelRows(args[2]);

var quotes = first.Concat(resume)
    .GroupBy(x => (x.TradingDay, x.Anchor))
    .Select(g => g.Last())
    .OrderBy(x => x.Anchor)
    .ToList();

var labelMap = labels.ToDictionary(x => (x.TradingDay, x.Anchor));
var joined = quotes
    .Where(q => labelMap.ContainsKey((q.TradingDay, q.Anchor)))
    .Select(q => new Joined(q, labelMap[(q.TradingDay, q.Anchor)]))
    .ToList();

Console.WriteLine("ISE Elite V7.9.15 U.S. Order-Flow Direction Separability");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Order-flow window 06:30-10:00 CT; five-minute causal lookback.");
Console.WriteLine("Exact target/stop labels from V7.9.10 Last-tick sequencing.");
Console.WriteLine("Training June 2026; frozen validation July 2026.");
Console.WriteLine();

Console.WriteLine($"First-segment rows={first.Count}");
Console.WriteLine($"Resume rows={resume.Count}");
Console.WriteLine($"Deduplicated quote rows={quotes.Count}");
Console.WriteLine($"Joined quote+label rows={joined.Count}");
Console.WriteLine($"Quote sessions={quotes.Select(x=>x.TradingDay).Distinct().Count()} June={quotes.Where(x=>x.TradingDay.Month==6).Select(x=>x.TradingDay).Distinct().Count()} July={quotes.Where(x=>x.TradingDay.Month==7).Select(x=>x.TradingDay).Distinct().Count()}");
Console.WriteLine();

foreach (var g in quotes.GroupBy(x=>x.TradingDay).OrderBy(x=>x.Key))
    Console.WriteLine($"COVERAGE {g.Key:yyyy-MM-dd} rows={g.Count()} first={g.Min(x=>x.Anchor):HH:mm} last={g.Max(x=>x.Anchor):HH:mm}");

Console.WriteLine();

var pairs = new[]
{
    new Pair("30/30", l=>l.Long30,  l=>l.Short30),
    new Pair("50/30", l=>l.Long50,  l=>l.Short50),
    new Pair("75/40", l=>l.Long75,  l=>l.Short75),
    new Pair("100/50",l=>l.Long100, l=>l.Short100)
};

foreach (var pair in pairs)
{
    Console.WriteLine(new string('=',110));
    Console.WriteLine($"PAIR {pair.Name}");
    Console.WriteLine(new string('=',110));

    var all = joined.Select(x => new
    {
        J = x,
        L = pair.Long(x.Label) == "TargetFirst",
        S = pair.Short(x.Label) == "TargetFirst"
    }).ToList();

    var samples = all.Where(x=>x.L!=x.S)
        .Select(x=>MakeSample(x.J,x.L?1:0))
        .ToList();

    var june = samples.Where(x=>x.J.Quote.TradingDay.Month==6).ToList();
    var july = samples.Where(x=>x.J.Quote.TradingDay.Month==7).ToList();

    Console.WriteLine($"allJoined={all.Count} cleanDirectional={samples.Count} bothTarget={all.Count(x=>x.L&&x.S)} neitherTarget={all.Count(x=>!x.L&&!x.S)}");
    Console.WriteLine($"June clean={june.Count} long={june.Count(x=>x.Y==1)} short={june.Count(x=>x.Y==0)}");
    Console.WriteLine($"July clean={july.Count} long={july.Count(x=>x.Y==1)} short={july.Count(x=>x.Y==0)}");

    if (june.Count < 100 || july.Count < 100)
    {
        Console.WriteLine("Insufficient sample.");
        continue;
    }

    int majority = june.Count(x=>x.Y==1) >= june.Count(x=>x.Y==0) ? 1 : 0;
    Console.WriteLine($"MAJORITY June predicts={(majority==1?"LONG":"SHORT")} accuracy={(double)june.Count(x=>x.Y==majority)/june.Count:P2}");
    Console.WriteLine($"MAJORITY July predicts={(majority==1?"LONG":"SHORT")} accuracy={(double)july.Count(x=>x.Y==majority)/july.Count:P2}");
    Console.WriteLine();

    Run("QUOTE LINEAR", june, july, x=>x.QuoteLinear, 1.0);
    Run("QUOTE NONLINEAR", june, july, x=>x.QuoteNonlinear, 1.5);
    Run("QUOTE + LAST TICK", june, july, x=>x.Combined, 2.0);

    Console.WriteLine("ZONE-SPECIFIC QUOTE+LAST -- JULY");
    foreach (var zone in samples.Select(x=>x.J.Quote.Zone).Distinct().OrderBy(x=>x))
    {
        var tr=june.Where(x=>x.J.Quote.Zone==zone).ToList();
        var te=july.Where(x=>x.J.Quote.Zone==zone).ToList();
        if(tr.Count<80||te.Count<40)
        {
            Console.WriteLine($"  {zone,-32} insufficient June={tr.Count} July={te.Count}");
            continue;
        }

        var ev=FitEval(tr,te,x=>x.Combined,2.0);
        Console.WriteLine($"  {zone,-32} June={tr.Count,4} July={te.Count,4} acc={ev.Acc:P2} balAcc={ev.Bal:P2} auc={ev.Auc:F3}");
    }

    Console.WriteLine();
    Console.WriteLine("CONFIDENCE LIFT -- JULY QUOTE+LAST");
    {
        var sc=Scaler.Fit(june.Select(x=>x.Combined).ToList());
        var model=Logistic.Fit(june.Select(x=>sc.Transform(x.Combined)).ToList(),june.Select(x=>x.Y).ToList(),2.0,3500,.02);
        var scored=july.Select(x=>{
            double p=model.Predict(sc.Transform(x.Combined));
            int pred=p>=.5?1:0;
            return new { Conf=Math.Max(p,1-p), Correct=pred==x.Y };
        }).ToList();

        foreach(var c in new[]{.52,.55,.575,.60,.625,.65,.675,.70})
        {
            var s=scored.Where(x=>x.Conf>=c).ToList();
            Console.WriteLine(s.Count==0
                ? $"  conf>={c:P1}: n=0"
                : $"  conf>={c:P1}: n={s.Count,4} pctLabels={(double)s.Count/scored.Count:P1} accuracy={(double)s.Count(x=>x.Correct)/s.Count:P2}");
        }
    }

    Console.WriteLine();
}

Console.WriteLine(new string('=',110));
Console.WriteLine("DECISION RULE");
Console.WriteLine("Promising: frozen-July AUC materially >0.55, balanced accuracy above chance/baseline, and confidence lift on useful sample sizes.");
Console.WriteLine("If quote+Last clearly improves over V7.9.11 Last-only, expand Bid/Ask research beyond 06:30-10:00.");
Console.WriteLine("If it remains near chance, top-of-book price updates without size are insufficient; next layer is true depth/size or a different opportunity formulation.");
Console.WriteLine(new string('=',110));

return 0;

static Sample MakeSample(Joined j,int y)
{
    var q=QuoteFeatures(j.Quote);
    var qn=Nonlinear(q);
    var last=LastFeatures(j.Label);
    return new Sample(j,y,q,qn,qn.Concat(last).ToArray());
}

static double[] QuoteFeatures(QuoteRow q)
{
    double updates=Math.Max(1,q.BidUpdates+q.AskUpdates);
    double moves=Math.Max(1,q.BidUpMoves+q.BidDownMoves+q.AskUpMoves+q.AskDownMoves);
    double spreadMoves=Math.Max(1,q.SpreadWideningEvents+q.SpreadNarrowingEvents);

    return new[]
    {
        q.BidUpdates,q.AskUpdates,q.QuoteUpdateImbalance,
        q.BidUpMoves,q.BidDownMoves,q.AskUpMoves,q.AskDownMoves,
        q.BidMoveImbalance,q.AskMoveImbalance,q.CombinedDirectionalPressure,
        q.BidReturnTicks,q.AskReturnTicks,q.BidRangeTicks,q.AskRangeTicks,
        q.BidReversals,q.AskReversals,q.BidMaxUpRun,q.BidMaxDownRun,q.AskMaxUpRun,q.AskMaxDownRun,
        q.MergedQuoteStates,q.AvgSpreadTicks,q.MedianSpreadTicks,q.MinSpreadTicks,q.MaxSpreadTicks,
        q.LockedStates,q.CrossedStates,q.SpreadWideningEvents,q.SpreadNarrowingEvents,q.LastSpreadTicks,
        q.BidUpdates/updates,q.AskUpdates/updates,
        (q.BidUpMoves+q.AskUpMoves)/moves,
        (q.BidDownMoves+q.AskDownMoves)/moves,
        q.SpreadWideningEvents/spreadMoves,
        q.SpreadNarrowingEvents/spreadMoves,
        Math.Log(1+q.BidUpdates+q.AskUpdates),
        ZoneNumber(q.Zone)
    };
}

static double[] Nonlinear(double[] b)
{
    var x=new List<double>(b);
    foreach(int i in new[]{2,7,8,9,10,11,29})
    {
        x.Add(Math.Abs(b[i]));
        x.Add(b[i]*b[i]);
        x.Add(Math.Sign(b[i]));
    }
    x.Add(b[2]*b[9]);
    x.Add(b[7]*b[8]);
    x.Add(b[10]*b[11]);
    x.Add(b[9]*b[21]);
    x.Add(b[9]*b[24]);
    x.Add(b[34]-b[35]);
    return x.ToArray();
}

static double[] LastFeatures(LabelRow r)
{
    double ticks=Math.Max(1,r.PriorTicks);
    double volume=Math.Max(1,r.PriorVolume);
    double directional=Math.Max(1,r.Upticks+r.Downticks);

    return new[]
    {
        (double)r.PriorRangeTicks,(double)r.PriorReturnTicks,
        Math.Log(1+r.PriorTicks),Math.Log(1+r.PriorVolume),
        (double)r.TickImbalance,(double)r.VolumeImbalance,
        r.Reversals/directional,r.MaxRunUp-r.MaxRunDown,
        r.Upticks/ticks,r.Downticks/ticks,r.UpVolume/volume,r.DownVolume/volume
    };
}

static void Run(string name,List<Sample> tr,List<Sample> te,Func<Sample,double[]> f,double lambda)
{
    var e=FitEval(tr,te,f,lambda);
    Console.WriteLine($"{name}: July acc={e.Acc:P2} balAcc={e.Bal:P2} auc={e.Auc:F3}");
}

static Eval FitEval(List<Sample> tr,List<Sample> te,Func<Sample,double[]> f,double lambda)
{
    var sc=Scaler.Fit(tr.Select(f).ToList());
    var model=Logistic.Fit(tr.Select(x=>sc.Transform(f(x))).ToList(),tr.Select(x=>x.Y).ToList(),lambda,3500,.02);

    int correct=0,tp=0,tn=0,fp=0,fn=0;
    var scored=new List<(double,int)>();

    foreach(var s in te)
    {
        double p=model.Predict(sc.Transform(f(s)));
        int pred=p>=.5?1:0;
        scored.Add((p,s.Y));
        if(pred==s.Y)correct++;
        if(pred==1&&s.Y==1)tp++;
        else if(pred==0&&s.Y==0)tn++;
        else if(pred==1)fp++;
        else fn++;
    }

    double tpr=tp+fn==0?0:(double)tp/(tp+fn);
    double tnr=tn+fp==0?0:(double)tn/(tn+fp);
    return new Eval((double)correct/te.Count,(tpr+tnr)/2,Auc(scored));
}

static double Auc(List<(double p,int y)> s)
{
    var o=s.OrderBy(x=>x.p).ToList();
    long pos=o.Count(x=>x.y==1),neg=o.Count-pos;
    if(pos==0||neg==0)return .5;
    double rankSum=0;
    int i=0;
    while(i<o.Count)
    {
        int j=i+1;
        while(j<o.Count&&Math.Abs(o[j].p-o[i].p)<1e-12)j++;
        double avg=((i+1)+j)/2.0;
        for(int k=i;k<j;k++)if(o[k].y==1)rankSum+=avg;
        i=j;
    }
    return (rankSum-pos*(pos+1)/2.0)/(pos*neg);
}

static double ZoneNumber(string z)
{
    if(z.StartsWith("05_"))return 5;
    if(z.StartsWith("06_"))return 6;
    if(z.StartsWith("07_"))return 7;
    return 0;
}

static List<QuoteRow> ReadQuoteRows(string path)
{
    var lines=File.ReadLines(path).ToList();
    var h=lines[0].Split('\t');
    var m=h.Select((n,i)=>new{n,i}).ToDictionary(x=>x.n,x=>x.i,StringComparer.OrdinalIgnoreCase);
    int I(string n)=>m.TryGetValue(n,out var i)?i:throw new Exception($"Missing quote column {n}");
    int N(string[] p,string n)=>int.Parse(p[I(n)],CultureInfo.InvariantCulture);
    double D(string[] p,string n)=>double.Parse(p[I(n)],CultureInfo.InvariantCulture);

    var rows=new List<QuoteRow>();
    foreach(var line in lines.Skip(1))
    {
        if(string.IsNullOrWhiteSpace(line))continue;
        var p=line.Split('\t');
        rows.Add(new QuoteRow(
            DateTime.ParseExact(p[I("tradingDay")],"yyyy-MM-dd",CultureInfo.InvariantCulture),
            DateTime.ParseExact(p[I("anchorLocal")],"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture),
            p[I("zone")],
            N(p,"bidUpdates"),N(p,"askUpdates"),D(p,"quoteUpdateImbalance"),
            N(p,"bidUpMoves"),N(p,"bidDownMoves"),N(p,"askUpMoves"),N(p,"askDownMoves"),
            D(p,"bidMoveImbalance"),D(p,"askMoveImbalance"),D(p,"combinedDirectionalPressure"),
            D(p,"bidReturnTicks"),D(p,"askReturnTicks"),D(p,"bidRangeTicks"),D(p,"askRangeTicks"),
            N(p,"bidReversals"),N(p,"askReversals"),N(p,"bidMaxUpRun"),N(p,"bidMaxDownRun"),N(p,"askMaxUpRun"),N(p,"askMaxDownRun"),
            N(p,"mergedQuoteStates"),D(p,"avgSpreadTicks"),D(p,"medianSpreadTicks"),D(p,"minSpreadTicks"),D(p,"maxSpreadTicks"),
            N(p,"lockedStates"),N(p,"crossedStates"),N(p,"spreadWideningEvents"),N(p,"spreadNarrowingEvents"),D(p,"lastSpreadTicks")));
    }
    return rows;
}

static List<LabelRow> ReadLabelRows(string path)
{
    var lines=File.ReadLines(path).ToList();
    var h=lines[0].Split('\t');
    var m=h.Select((n,i)=>new{n,i}).ToDictionary(x=>x.n,x=>x.i,StringComparer.OrdinalIgnoreCase);
    int I(string n)=>m.TryGetValue(n,out var i)?i:throw new Exception($"Missing label column {n}");

    var rows=new List<LabelRow>();
    foreach(var line in lines.Skip(1))
    {
        if(string.IsNullOrWhiteSpace(line))continue;
        var p=line.Split('\t');
        rows.Add(new LabelRow(
            DateTime.ParseExact(p[I("tradingDay")],"yyyy-MM-dd",CultureInfo.InvariantCulture),
            DateTime.ParseExact(p[I("anchorLocal")],"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture),
            int.Parse(p[I("priorTicks")],CultureInfo.InvariantCulture),
            long.Parse(p[I("priorVolume")],CultureInfo.InvariantCulture),
            decimal.Parse(p[I("priorRangeTicks")],CultureInfo.InvariantCulture),
            decimal.Parse(p[I("priorReturnTicks")],CultureInfo.InvariantCulture),
            int.Parse(p[I("upticks")],CultureInfo.InvariantCulture),
            int.Parse(p[I("downticks")],CultureInfo.InvariantCulture),
            long.Parse(p[I("upVolume")],CultureInfo.InvariantCulture),
            long.Parse(p[I("downVolume")],CultureInfo.InvariantCulture),
            decimal.Parse(p[I("tickImbalance")],CultureInfo.InvariantCulture),
            decimal.Parse(p[I("volumeImbalance")],CultureInfo.InvariantCulture),
            int.Parse(p[I("reversals")],CultureInfo.InvariantCulture),
            int.Parse(p[I("maxRunUp")],CultureInfo.InvariantCulture),
            int.Parse(p[I("maxRunDown")],CultureInfo.InvariantCulture),
            p[I("long30")],p[I("short30")],p[I("long50")],p[I("short50")],
            p[I("long75")],p[I("short75")],p[I("long100")],p[I("short100")]));
    }
    return rows;
}

sealed record Pair(string Name,Func<LabelRow,string> Long,Func<LabelRow,string> Short);
sealed record Joined(QuoteRow Quote,LabelRow Label);
sealed record Sample(Joined J,int Y,double[] QuoteLinear,double[] QuoteNonlinear,double[] Combined);
sealed record Eval(double Acc,double Bal,double Auc);

sealed record QuoteRow(
    DateTime TradingDay,DateTime Anchor,string Zone,
    int BidUpdates,int AskUpdates,double QuoteUpdateImbalance,
    int BidUpMoves,int BidDownMoves,int AskUpMoves,int AskDownMoves,
    double BidMoveImbalance,double AskMoveImbalance,double CombinedDirectionalPressure,
    double BidReturnTicks,double AskReturnTicks,double BidRangeTicks,double AskRangeTicks,
    int BidReversals,int AskReversals,int BidMaxUpRun,int BidMaxDownRun,int AskMaxUpRun,int AskMaxDownRun,
    int MergedQuoteStates,double AvgSpreadTicks,double MedianSpreadTicks,double MinSpreadTicks,double MaxSpreadTicks,
    int LockedStates,int CrossedStates,int SpreadWideningEvents,int SpreadNarrowingEvents,double LastSpreadTicks);

sealed record LabelRow(
    DateTime TradingDay,DateTime Anchor,int PriorTicks,long PriorVolume,
    decimal PriorRangeTicks,decimal PriorReturnTicks,int Upticks,int Downticks,long UpVolume,long DownVolume,
    decimal TickImbalance,decimal VolumeImbalance,int Reversals,int MaxRunUp,int MaxRunDown,
    string Long30,string Short30,string Long50,string Short50,string Long75,string Short75,string Long100,string Short100);

sealed class Scaler
{
    readonly double[] m,s;
    Scaler(double[] m,double[] s){this.m=m;this.s=s;}
    public static Scaler Fit(List<double[]> xs)
    {
        int p=xs[0].Length;
        var m=new double[p]; var s=new double[p];
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
        for(int i=0;i<x.Length;i++)z[i]=(x[i]-m[i])/s[i];
        return z;
    }
}

sealed class Logistic
{
    readonly double[] w;
    Logistic(double[] w){this.w=w;}
    public static Logistic Fit(List<double[]> xs,List<int> ys,double lambda,int iterations,double lr)
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
            w[0]-=lr*g[0]/n;
            for(int j=1;j<=p;j++)w[j]-=lr*(g[j]/n+lambda*w[j]/n);
        }
        return new Logistic(w);
    }
    public double Predict(double[] x)
    {
        double z=w[0];
        for(int i=0;i<x.Length;i++)z+=w[i+1]*x[i];
        return 1/(1+Math.Exp(-Math.Clamp(z,-30,30)));
    }
}
