using System.Globalization;

if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: <first-orderflow.tsv> <resume-orderflow.tsv> <historical-labels.tsv> <august-holdout.tsv>");
    return 2;
}

var q1 = ReadQuoteRows(args[0]);
var q2 = ReadQuoteRows(args[1]);
var histLabels = ReadHistoricalLabels(args[2]);
var august = ReadAugust(args[3]);

var quotes = q1.Concat(q2)
    .GroupBy(x => (x.TradingDay,x.Anchor))
    .Select(g => g.Last())
    .ToList();

var labelMap = histLabels.ToDictionary(x => (x.TradingDay,x.Anchor));

var historical = quotes
    .Where(q => labelMap.ContainsKey((q.TradingDay,q.Anchor)))
    .Select(q => new Joined(q,labelMap[(q.TradingDay,q.Anchor)]))
    .ToList();

var june = historical.Where(x => x.Quote.TradingDay.Month == 6).ToList();

Console.WriteLine("ISE Elite V7.9.17 Frozen August Validation");
Console.WriteLine("Research only. No production promotion.");
Console.WriteLine("Training data: June 2026 ONLY.");
Console.WriteLine("July was prior validation and is not used to fit or tune this run.");
Console.WriteLine("August 3-14 is untouched holdout scoring only.");
Console.WriteLine();

Console.WriteLine($"Historical quote rows deduped={quotes.Count}");
Console.WriteLine($"June joined rows={june.Count}");
Console.WriteLine($"August holdout rows={august.Count}");
Console.WriteLine($"August sessions={august.Select(x=>x.TradingDay).Distinct().Count()}");
Console.WriteLine();

var pairs = new[]
{
    new PairSpec("30/30", l=>l.Long30, l=>l.Short30, a=>a.Long30, a=>a.Short30),
    new PairSpec("50/30", l=>l.Long50, l=>l.Short50, a=>a.Long50, a=>a.Short50),
    new PairSpec("75/40", l=>l.Long75, l=>l.Short75, a=>a.Long75, a=>a.Short75),
    new PairSpec("100/50", l=>l.Long100, l=>l.Short100, a=>a.Long100, a=>a.Short100)
};

foreach (var pair in pairs)
{
    Console.WriteLine(new string('=',120));
    Console.WriteLine($"PAIR {pair.Name}");
    Console.WriteLine(new string('=',120));

    var train = june
        .Select(x => MakeTrainingSample(x,pair))
        .Where(x=>x!=null)
        .Select(x=>x!)
        .ToList();

    var test = august
        .Select(x => MakeAugustSample(x,pair))
        .Where(x=>x!=null)
        .Select(x=>x!)
        .ToList();

    int majority = train.Count(x=>x.Y==1) >= train.Count(x=>x.Y==0) ? 1 : 0;

    Console.WriteLine($"June clean={train.Count} long={train.Count(x=>x.Y==1)} short={train.Count(x=>x.Y==0)}");
    Console.WriteLine($"August clean={test.Count} long={test.Count(x=>x.Y==1)} short={test.Count(x=>x.Y==0)}");
    Console.WriteLine($"August majority baseline ({(majority==1?"LONG":"SHORT")})={(test.Count==0?0:(double)test.Count(x=>x.Y==majority)/test.Count):P2}");
    Console.WriteLine();

    if(train.Count < 100 || test.Count < 20)
    {
        Console.WriteLine("Insufficient sample.");
        continue;
    }

    RunFrozen("GLOBAL QUOTE LINEAR",train,test,x=>x.QuoteLinear,1.0);
    RunFrozen("GLOBAL QUOTE NONLINEAR",train,test,x=>x.QuoteNonlinear,1.5);
    RunFrozen("GLOBAL QUOTE + LAST",train,test,x=>x.Combined,2.0);

    Console.WriteLine("ZONE-SPECIFIC QUOTE+LAST -- FROZEN AUGUST");

    foreach(var zone in new[]
    {
        "05_USPreopen_OpenDevelopment",
        "06_NYTransition_0845_0905",
        "07_NYSecondary_0905_1000"
    })
    {
        var tr = train.Where(x=>x.Zone==zone).ToList();
        var te = test.Where(x=>x.Zone==zone).ToList();

        if(tr.Count < 80 || te.Count < 20)
        {
            Console.WriteLine($"  {zone,-32} insufficient June={tr.Count} August={te.Count}");
            continue;
        }

        var ev = EvaluateFrozen(tr,te,x=>x.Combined,2.0);
        Console.WriteLine($"  {zone,-32} June={tr.Count,4} August={te.Count,4} acc={ev.Accuracy:P2} balAcc={ev.BalancedAccuracy:P2} auc={ev.Auc:F3}");

        if(pair.Name=="75/40" && zone=="05_USPreopen_OpenDevelopment")
        {
            Console.WriteLine();
            Console.WriteLine("PRIMARY HOLDOUT: 75/40 US PREOPEN/OPEN DEVELOPMENT");
            Console.WriteLine($"  August accuracy={ev.Accuracy:P2}");
            Console.WriteLine($"  August balancedAccuracy={ev.BalancedAccuracy:P2}");
            Console.WriteLine($"  August AUC={ev.Auc:F3}");

            PrintConfidence(tr,te);
        }
    }

    Console.WriteLine();
}

Console.WriteLine(new string('=',120));
Console.WriteLine("PRIMARY DECISION RULE");
Console.WriteLine("75/40 US preopen survives only if August remains materially above chance/baseline,");
Console.WriteLine("AUC remains meaningfully above 0.55, and confidence lift remains directionally coherent on useful sample sizes.");
Console.WriteLine("If August collapses toward chance, treat July as a non-robust localized result and do not promote.");
Console.WriteLine("If August confirms the edge, next step is non-overlapping execution/lifecycle simulation with realistic friction and account governors.");
Console.WriteLine(new string('=',120));

return 0;

static TrainSample? MakeTrainingSample(Joined j,PairSpec p)
{
    bool l=p.HistLong(j.Label)=="TargetFirst";
    bool s=p.HistShort(j.Label)=="TargetFirst";
    if(l==s)return null;

    var q=QuoteFeatures(j.Quote);
    var qn=Nonlinear(q);
    var last=LastFeatures(j.Label);

    return new TrainSample(
        l?1:0,
        j.Quote.Zone,
        q,
        qn,
        qn.Concat(last).ToArray());
}

static TrainSample? MakeAugustSample(AugustRow a,PairSpec p)
{
    bool l=p.AugLong(a)=="TargetFirst";
    bool s=p.AugShort(a)=="TargetFirst";
    if(l==s)return null;

    var q=QuoteFeatures(a);
    var qn=Nonlinear(q);
    var last=LastFeatures(a);

    return new TrainSample(
        l?1:0,
        a.Zone,
        q,
        qn,
        qn.Concat(last).ToArray());
}

static void RunFrozen(string name,List<TrainSample> train,List<TrainSample> test,Func<TrainSample,double[]> f,double lambda)
{
    var ev=EvaluateFrozen(train,test,f,lambda);
    Console.WriteLine($"{name}: August acc={ev.Accuracy:P2} balAcc={ev.BalancedAccuracy:P2} auc={ev.Auc:F3}");
}

static Eval EvaluateFrozen(List<TrainSample> train,List<TrainSample> test,Func<TrainSample,double[]> f,double lambda)
{
    var scaler=Scaler.Fit(train.Select(f).ToList());
    var model=Logistic.Fit(
        train.Select(x=>scaler.Transform(f(x))).ToList(),
        train.Select(x=>x.Y).ToList(),
        lambda,4000,0.02);

    return Evaluate(test,scaler,model,f);
}

static Eval Evaluate(List<TrainSample> test,Scaler scaler,Logistic model,Func<TrainSample,double[]> f)
{
    int correct=0,tp=0,tn=0,fp=0,fn=0;
    var scores=new List<(double p,int y)>();

    foreach(var x in test)
    {
        double p=model.Predict(scaler.Transform(f(x)));
        int pred=p>=0.5?1:0;
        scores.Add((p,x.Y));

        if(pred==x.Y)correct++;
        if(pred==1&&x.Y==1)tp++;
        else if(pred==0&&x.Y==0)tn++;
        else if(pred==1)fp++;
        else fn++;
    }

    double tpr=tp+fn==0?0:(double)tp/(tp+fn);
    double tnr=tn+fp==0?0:(double)tn/(tn+fp);

    return new Eval(
        test.Count==0?0:(double)correct/test.Count,
        (tpr+tnr)/2,
        Auc(scores));
}

static void PrintConfidence(List<TrainSample> train,List<TrainSample> test)
{
    var scaler=Scaler.Fit(train.Select(x=>x.Combined).ToList());
    var model=Logistic.Fit(
        train.Select(x=>scaler.Transform(x.Combined)).ToList(),
        train.Select(x=>x.Y).ToList(),
        2.0,4000,0.02);

    var scored=test.Select(x =>
    {
        double p=model.Predict(scaler.Transform(x.Combined));
        int pred=p>=.5?1:0;
        return new {Conf=Math.Max(p,1-p),Correct=pred==x.Y};
    }).ToList();

    Console.WriteLine("  Confidence lift:");
    foreach(var c in new[]{0.55,0.575,0.60,0.625,0.65,0.675,0.70})
    {
        var s=scored.Where(x=>x.Conf>=c).ToList();
        Console.WriteLine(s.Count==0
            ? $"    conf>={c:P1}: n=0"
            : $"    conf>={c:P1}: n={s.Count,3} pct={((double)s.Count/scored.Count):P1} accuracy={((double)s.Count(x=>x.Correct)/s.Count):P2}");
    }
}

static double[] QuoteFeatures(IQuoteLike q)
{
    double totalUpdates=Math.Max(1,q.BidUpdates+q.AskUpdates);
    double totalMoves=Math.Max(1,q.BidUpMoves+q.BidDownMoves+q.AskUpMoves+q.AskDownMoves);
    double spreadEvents=Math.Max(1,q.SpreadWideningEvents+q.SpreadNarrowingEvents);

    return new[]
    {
        (double)q.BidUpdates,(double)q.AskUpdates,q.QuoteUpdateImbalance,
        (double)q.BidUpMoves,(double)q.BidDownMoves,(double)q.AskUpMoves,(double)q.AskDownMoves,
        q.BidMoveImbalance,q.AskMoveImbalance,q.CombinedDirectionalPressure,
        q.BidReturnTicks,q.AskReturnTicks,q.BidRangeTicks,q.AskRangeTicks,
        (double)q.BidReversals,(double)q.AskReversals,
        (double)q.BidMaxUpRun,(double)q.BidMaxDownRun,(double)q.AskMaxUpRun,(double)q.AskMaxDownRun,
        (double)q.MergedQuoteStates,q.AvgSpreadTicks,q.MedianSpreadTicks,q.MinSpreadTicks,q.MaxSpreadTicks,
        (double)q.LockedStates,(double)q.CrossedStates,(double)q.SpreadWideningEvents,(double)q.SpreadNarrowingEvents,
        q.LastSpreadTicks,
        q.BidUpdates/totalUpdates,
        q.AskUpdates/totalUpdates,
        (q.BidUpMoves+q.AskUpMoves)/totalMoves,
        (q.BidDownMoves+q.AskDownMoves)/totalMoves,
        q.SpreadWideningEvents/spreadEvents,
        q.SpreadNarrowingEvents/spreadEvents,
        Math.Log(1+q.BidUpdates+q.AskUpdates),
        ZoneNumber(q.Zone)
    };
}

static double[] Nonlinear(double[] b)
{
    var x=new List<double>(b);
    int[] signed={2,7,8,9,10,11,29};

    foreach(int i in signed)
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

static double[] LastFeatures(ILastLike r)
{
    double ticks=Math.Max(1,r.PriorTicks);
    double vol=Math.Max(1,r.PriorVolume);
    double directional=Math.Max(1,r.Upticks+r.Downticks);

    return new[]
    {
        r.PriorRangeTicks,
        r.PriorReturnTicks,
        Math.Log(1+r.PriorTicks),
        Math.Log(1+r.PriorVolume),
        r.TickImbalance,
        r.VolumeImbalance,
        r.Reversals/directional,
        (double)(r.MaxRunUp-r.MaxRunDown),
        r.Upticks/ticks,
        r.Downticks/ticks,
        r.UpVolume/vol,
        r.DownVolume/vol
    };
}

static double Auc(List<(double p,int y)> scored)
{
    var o=scored.OrderBy(x=>x.p).ToList();
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
    int I(string n)=>m[n];

    double D(string[] p,string n)=>double.Parse(p[I(n)],CultureInfo.InvariantCulture);
    int N(string[] p,string n)=>int.Parse(p[I(n)],CultureInfo.InvariantCulture);

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
            N(p,"bidReversals"),N(p,"askReversals"),N(p,"bidMaxUpRun"),N(p,"bidMaxDownRun"),
            N(p,"askMaxUpRun"),N(p,"askMaxDownRun"),N(p,"mergedQuoteStates"),
            D(p,"avgSpreadTicks"),D(p,"medianSpreadTicks"),D(p,"minSpreadTicks"),D(p,"maxSpreadTicks"),
            N(p,"lockedStates"),N(p,"crossedStates"),N(p,"spreadWideningEvents"),N(p,"spreadNarrowingEvents"),
            D(p,"lastSpreadTicks")
        ));
    }

    return rows;
}

static List<HistoricalLabel> ReadHistoricalLabels(string path)
{
    var lines=File.ReadLines(path).ToList();
    var h=lines[0].Split('\t');
    var m=h.Select((n,i)=>new{n,i}).ToDictionary(x=>x.n,x=>x.i,StringComparer.OrdinalIgnoreCase);
    int I(string n)=>m[n];

    var rows=new List<HistoricalLabel>();

    foreach(var line in lines.Skip(1))
    {
        if(string.IsNullOrWhiteSpace(line))continue;
        var p=line.Split('\t');

        rows.Add(new HistoricalLabel(
            DateTime.ParseExact(p[I("tradingDay")],"yyyy-MM-dd",CultureInfo.InvariantCulture),
            DateTime.ParseExact(p[I("anchorLocal")],"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture),
            int.Parse(p[I("priorTicks")],CultureInfo.InvariantCulture),
            long.Parse(p[I("priorVolume")],CultureInfo.InvariantCulture),
            double.Parse(p[I("priorRangeTicks")],CultureInfo.InvariantCulture),
            double.Parse(p[I("priorReturnTicks")],CultureInfo.InvariantCulture),
            int.Parse(p[I("upticks")],CultureInfo.InvariantCulture),
            int.Parse(p[I("downticks")],CultureInfo.InvariantCulture),
            long.Parse(p[I("upVolume")],CultureInfo.InvariantCulture),
            long.Parse(p[I("downVolume")],CultureInfo.InvariantCulture),
            double.Parse(p[I("tickImbalance")],CultureInfo.InvariantCulture),
            double.Parse(p[I("volumeImbalance")],CultureInfo.InvariantCulture),
            int.Parse(p[I("reversals")],CultureInfo.InvariantCulture),
            int.Parse(p[I("maxRunUp")],CultureInfo.InvariantCulture),
            int.Parse(p[I("maxRunDown")],CultureInfo.InvariantCulture),
            p[I("long30")],p[I("short30")],p[I("long50")],p[I("short50")],
            p[I("long75")],p[I("short75")],p[I("long100")],p[I("short100")]
        ));
    }

    return rows;
}

static List<AugustRow> ReadAugust(string path)
{
    var lines=File.ReadLines(path).ToList();
    var h=lines[0].Split('\t');
    var m=h.Select((n,i)=>new{n,i}).ToDictionary(x=>x.n,x=>x.i,StringComparer.OrdinalIgnoreCase);
    int I(string n)=>m[n];

    double D(string[] p,string n)=>double.Parse(p[I(n)],CultureInfo.InvariantCulture);
    int N(string[] p,string n)=>int.Parse(p[I(n)],CultureInfo.InvariantCulture);
    long L(string[] p,string n)=>long.Parse(p[I(n)],CultureInfo.InvariantCulture);

    var rows=new List<AugustRow>();

    foreach(var line in lines.Skip(1))
    {
        if(string.IsNullOrWhiteSpace(line))continue;
        var p=line.Split('\t');

        rows.Add(new AugustRow(
            DateTime.ParseExact(p[I("tradingDay")],"yyyy-MM-dd",CultureInfo.InvariantCulture),
            DateTime.ParseExact(p[I("anchorLocal")],"yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture),
            p[I("zone")],
            N(p,"priorTicks"),L(p,"priorVolume"),D(p,"priorRangeTicks"),D(p,"priorReturnTicks"),
            N(p,"upticks"),N(p,"downticks"),L(p,"upVolume"),L(p,"downVolume"),
            D(p,"tickImbalance"),D(p,"volumeImbalance"),N(p,"reversals"),N(p,"maxRunUp"),N(p,"maxRunDown"),
            N(p,"bidUpdates"),N(p,"askUpdates"),D(p,"quoteUpdateImbalance"),
            N(p,"bidUpMoves"),N(p,"bidDownMoves"),N(p,"askUpMoves"),N(p,"askDownMoves"),
            D(p,"bidMoveImbalance"),D(p,"askMoveImbalance"),D(p,"combinedDirectionalPressure"),
            D(p,"bidReturnTicks"),D(p,"askReturnTicks"),D(p,"bidRangeTicks"),D(p,"askRangeTicks"),
            N(p,"bidReversals"),N(p,"askReversals"),N(p,"bidMaxUpRun"),N(p,"bidMaxDownRun"),
            N(p,"askMaxUpRun"),N(p,"askMaxDownRun"),N(p,"mergedQuoteStates"),
            D(p,"avgSpreadTicks"),D(p,"medianSpreadTicks"),D(p,"minSpreadTicks"),D(p,"maxSpreadTicks"),
            N(p,"lockedStates"),N(p,"crossedStates"),N(p,"spreadWideningEvents"),N(p,"spreadNarrowingEvents"),
            D(p,"lastSpreadTicks"),
            p[I("long30")],p[I("short30")],p[I("long50")],p[I("short50")],
            p[I("long75")],p[I("short75")],p[I("long100")],p[I("short100")]
        ));
    }

    return rows;
}

interface IQuoteLike
{
    string Zone {get;}
    int BidUpdates {get;} int AskUpdates {get;} double QuoteUpdateImbalance {get;}
    int BidUpMoves {get;} int BidDownMoves {get;} int AskUpMoves {get;} int AskDownMoves {get;}
    double BidMoveImbalance {get;} double AskMoveImbalance {get;} double CombinedDirectionalPressure {get;}
    double BidReturnTicks {get;} double AskReturnTicks {get;} double BidRangeTicks {get;} double AskRangeTicks {get;}
    int BidReversals {get;} int AskReversals {get;} int BidMaxUpRun {get;} int BidMaxDownRun {get;}
    int AskMaxUpRun {get;} int AskMaxDownRun {get;} int MergedQuoteStates {get;}
    double AvgSpreadTicks {get;} double MedianSpreadTicks {get;} double MinSpreadTicks {get;} double MaxSpreadTicks {get;}
    int LockedStates {get;} int CrossedStates {get;} int SpreadWideningEvents {get;} int SpreadNarrowingEvents {get;}
    double LastSpreadTicks {get;}
}

interface ILastLike
{
    int PriorTicks {get;} long PriorVolume {get;}
    double PriorRangeTicks {get;} double PriorReturnTicks {get;}
    int Upticks {get;} int Downticks {get;} long UpVolume {get;} long DownVolume {get;}
    double TickImbalance {get;} double VolumeImbalance {get;}
    int Reversals {get;} int MaxRunUp {get;} int MaxRunDown {get;}
}

sealed record QuoteRow(
    DateTime TradingDay,DateTime Anchor,string Zone,
    int BidUpdates,int AskUpdates,double QuoteUpdateImbalance,
    int BidUpMoves,int BidDownMoves,int AskUpMoves,int AskDownMoves,
    double BidMoveImbalance,double AskMoveImbalance,double CombinedDirectionalPressure,
    double BidReturnTicks,double AskReturnTicks,double BidRangeTicks,double AskRangeTicks,
    int BidReversals,int AskReversals,int BidMaxUpRun,int BidMaxDownRun,int AskMaxUpRun,int AskMaxDownRun,
    int MergedQuoteStates,double AvgSpreadTicks,double MedianSpreadTicks,double MinSpreadTicks,double MaxSpreadTicks,
    int LockedStates,int CrossedStates,int SpreadWideningEvents,int SpreadNarrowingEvents,double LastSpreadTicks
) : IQuoteLike;

sealed record HistoricalLabel(
    DateTime TradingDay,DateTime Anchor,int PriorTicks,long PriorVolume,
    double PriorRangeTicks,double PriorReturnTicks,int Upticks,int Downticks,long UpVolume,long DownVolume,
    double TickImbalance,double VolumeImbalance,int Reversals,int MaxRunUp,int MaxRunDown,
    string Long30,string Short30,string Long50,string Short50,string Long75,string Short75,string Long100,string Short100
) : ILastLike;

sealed record AugustRow(
    DateTime TradingDay,DateTime Anchor,string Zone,
    int PriorTicks,long PriorVolume,double PriorRangeTicks,double PriorReturnTicks,
    int Upticks,int Downticks,long UpVolume,long DownVolume,double TickImbalance,double VolumeImbalance,
    int Reversals,int MaxRunUp,int MaxRunDown,
    int BidUpdates,int AskUpdates,double QuoteUpdateImbalance,
    int BidUpMoves,int BidDownMoves,int AskUpMoves,int AskDownMoves,
    double BidMoveImbalance,double AskMoveImbalance,double CombinedDirectionalPressure,
    double BidReturnTicks,double AskReturnTicks,double BidRangeTicks,double AskRangeTicks,
    int BidReversals,int AskReversals,int BidMaxUpRun,int BidMaxDownRun,int AskMaxUpRun,int AskMaxDownRun,
    int MergedQuoteStates,double AvgSpreadTicks,double MedianSpreadTicks,double MinSpreadTicks,double MaxSpreadTicks,
    int LockedStates,int CrossedStates,int SpreadWideningEvents,int SpreadNarrowingEvents,double LastSpreadTicks,
    string Long30,string Short30,string Long50,string Short50,string Long75,string Short75,string Long100,string Short100
) : IQuoteLike, ILastLike;

sealed record Joined(QuoteRow Quote,HistoricalLabel Label);
sealed record TrainSample(int Y,string Zone,double[] QuoteLinear,double[] QuoteNonlinear,double[] Combined);
sealed record Eval(double Accuracy,double BalancedAccuracy,double Auc);

sealed record PairSpec(
    string Name,
    Func<HistoricalLabel,string> HistLong,
    Func<HistoricalLabel,string> HistShort,
    Func<AugustRow,string> AugLong,
    Func<AugustRow,string> AugShort);

sealed class Scaler
{
    readonly double[] mean,sd;
    Scaler(double[] mean,double[] sd){this.mean=mean;this.sd=sd;}

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
        for(int i=0;i<x.Length;i++)z[i]=(x[i]-mean[i])/sd[i];
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
            for(int j=1;j<=p;j++)
                w[j]-=lr*(g[j]/n+lambda*w[j]/n);
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
