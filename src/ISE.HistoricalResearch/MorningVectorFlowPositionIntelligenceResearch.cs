using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningPositionIntelligenceMode
    {
        Scalp = 0,
        Core = 1,
        Runner = 2
    }

    public enum MorningPositionIntelligenceExitReason
    {
        None = 0,
        StructuralStop = 1,
        ScalpCapture = 2,
        ScalpTimeout = 3,
        VectorFlowBiasLoss = 4,
        ResearchWindowEnd = 5
    }

    public sealed class MorningVectorFlowPositionIntelligenceConfig
    {
        public MorningVectorFlowPositionIntelligenceConfig(
            int vectorTimeframeMinutes = 5,
            int ftcLength = 100,
            int ftcAtrLength = 14,
            int ftcAtrHighestLookback = 100,
            int vidyaLength = 20,
            int vidyaMomentum = 20,
            int vidyaSmoothingLength = 15,
            int vidyaAtrLength = 200,
            decimal vidyaBandDistance = 2m,
            int scalpTargetTicks = 150,
            int scalpTimeoutMinutes = 30,
            int runnerThresholdTicks = 300,
            int runnerAlignedBars = 2,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2m,
            int contracts = 2)
        {
            if (vectorTimeframeMinutes < 1) throw new ArgumentOutOfRangeException(nameof(vectorTimeframeMinutes));
            if (ftcLength < 2 || ftcAtrLength < 2 || ftcAtrHighestLookback < 2) throw new ArgumentOutOfRangeException(nameof(ftcLength));
            if (vidyaLength < 2 || vidyaMomentum < 2 || vidyaSmoothingLength < 1 || vidyaAtrLength < 2 || vidyaBandDistance <= 0m)
                throw new ArgumentOutOfRangeException(nameof(vidyaLength));
            if (scalpTargetTicks < 1 || scalpTimeoutMinutes < 1 || runnerThresholdTicks <= scalpTargetTicks || runnerAlignedBars < 1)
                throw new ArgumentOutOfRangeException(nameof(scalpTargetTicks));
            if (tickSize <= 0m || pointValuePerContract <= 0m || contracts < 1)
                throw new ArgumentOutOfRangeException(nameof(tickSize));

            VectorTimeframeMinutes = vectorTimeframeMinutes;
            FtcLength = ftcLength;
            FtcAtrLength = ftcAtrLength;
            FtcAtrHighestLookback = ftcAtrHighestLookback;
            VidyaLength = vidyaLength;
            VidyaMomentum = vidyaMomentum;
            VidyaSmoothingLength = vidyaSmoothingLength;
            VidyaAtrLength = vidyaAtrLength;
            VidyaBandDistance = vidyaBandDistance;
            ScalpTargetTicks = scalpTargetTicks;
            ScalpTimeoutMinutes = scalpTimeoutMinutes;
            RunnerThresholdTicks = runnerThresholdTicks;
            RunnerAlignedBars = runnerAlignedBars;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
        }

        public int VectorTimeframeMinutes { get; }
        public int FtcLength { get; }
        public int FtcAtrLength { get; }
        public int FtcAtrHighestLookback { get; }
        public int VidyaLength { get; }
        public int VidyaMomentum { get; }
        public int VidyaSmoothingLength { get; }
        public int VidyaAtrLength { get; }
        public decimal VidyaBandDistance { get; }
        public int ScalpTargetTicks { get; }
        public int ScalpTimeoutMinutes { get; }
        public int RunnerThresholdTicks { get; }
        public int RunnerAlignedBars { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal DollarsPerTick => TickSize * PointValuePerContract * Contracts;
    }

    public sealed class MorningVectorFlowManagedTrade
    {
        public MorningVectorFlowManagedTrade(
            MorningDailySequencingCandidate candidate,
            MorningPositionIntelligenceMode finalMode,
            MorningPositionIntelligenceExitReason exitReason,
            DateTimeOffset exitUtc,
            decimal exitPrice,
            decimal realizedTicks,
            decimal realizedDollars,
            decimal maxFavorableTicks,
            decimal maxAdverseTicks,
            bool everAligned,
            int alignedFiveMinuteBars)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            FinalMode = finalMode;
            ExitReason = exitReason;
            ExitUtc = exitUtc;
            ExitPrice = exitPrice;
            RealizedTicks = realizedTicks;
            RealizedDollars = realizedDollars;
            MaxFavorableTicks = maxFavorableTicks;
            MaxAdverseTicks = maxAdverseTicks;
            EverAligned = everAligned;
            AlignedFiveMinuteBars = alignedFiveMinuteBars;
        }

        public MorningDailySequencingCandidate Candidate { get; }
        public MorningPositionIntelligenceMode FinalMode { get; }
        public MorningPositionIntelligenceExitReason ExitReason { get; }
        public DateTimeOffset ExitUtc { get; }
        public decimal ExitPrice { get; }
        public decimal RealizedTicks { get; }
        public decimal RealizedDollars { get; }
        public decimal MaxFavorableTicks { get; }
        public decimal MaxAdverseTicks { get; }
        public bool EverAligned { get; }
        public int AlignedFiveMinuteBars { get; }
    }

    /// <summary>
    /// V7 research-only post-entry manager.
    /// V6.1 StrictUpper80 owns selection. This class cannot create entries.
    /// Five-minute VectorFlow FTC+VIDYA state only decides whether an already-open trade
    /// remains scalp, graduates to core/runner, or exits on post-entry bias loss.
    /// </summary>
    public sealed class MorningVectorFlowPositionIntelligenceAnalyzer
    {
        private static readonly TimeSpan ResearchWindowEnd = new TimeSpan(11, 0, 0);
        private readonly MorningVectorFlowPositionIntelligenceConfig config;

        public MorningVectorFlowPositionIntelligenceAnalyzer(
            MorningVectorFlowPositionIntelligenceConfig? config = null)
        {
            this.config = config ?? new MorningVectorFlowPositionIntelligenceConfig();
        }

        public IReadOnlyList<MorningVectorFlowManagedTrade> Analyze(
            IReadOnlyList<HistoricalBar> oneMinuteBars,
            IReadOnlyList<MorningDailySequencingCandidate> selectedCandidates)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (selectedCandidates == null) throw new ArgumentNullException(nameof(selectedCandidates));

            var central = ResolveCentralTimeZone();
            var ordered = oneMinuteBars.OrderBy(x => x.TimestampUtc).ToList();
            var vectors = BuildVectorStates(Aggregate(ordered, config.VectorTimeframeMinutes, central));
            var result = new List<MorningVectorFlowManagedTrade>();

            foreach (var candidate in selectedCandidates.OrderBy(x => x.EntryUtc))
            {
                var source = candidate.Entry.Source.Source;
                var localEntry = TimeZoneInfo.ConvertTime(source.EntryUtc, central).DateTime;

                var path = ordered
                    .Where(x => x.TimestampUtc >= source.EntryUtc)
                    .TakeWhile(x =>
                    {
                        var local = TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime;
                        return local.Date == localEntry.Date && local.TimeOfDay < ResearchWindowEnd;
                    })
                    .ToList();

                if (path.Count == 0)
                    continue;

                result.Add(Manage(candidate, path, vectors));
            }

            return result;
        }

        private MorningVectorFlowManagedTrade Manage(
            MorningDailySequencingCandidate candidate,
            IReadOnlyList<HistoricalBar> path,
            IReadOnlyList<VectorStatePoint> vectors)
        {
            var source = candidate.Entry.Source.Source;
            var direction = source.Direction;
            var entryPrice = source.EntryPrice;
            var stopPrice = source.StopPrice;
            var target = direction == NewYorkResearchDirection.Long
                ? entryPrice + config.ScalpTargetTicks * config.TickSize
                : entryPrice - config.ScalpTargetTicks * config.TickSize;
            var deadline = source.EntryUtc.AddMinutes(config.ScalpTimeoutMinutes);

            var mode = MorningPositionIntelligenceMode.Scalp;
            var everAligned = false;
            var alignedBars = 0;
            DateTimeOffset? lastVectorEnd = null;
            decimal mfe = 0m;
            decimal mae = 0m;

            foreach (var bar in path)
            {
                var latest = LatestVectorStateBefore(vectors, bar.TimestampUtc);
                if (latest != null && (!lastVectorEnd.HasValue || latest.EndUtc > lastVectorEnd.Value))
                {
                    lastVectorEnd = latest.EndUtc;
                    var aligned = IsAligned(direction, latest.Bias);

                    if (aligned)
                    {
                        everAligned = true;
                        alignedBars++;
                        if (mode != MorningPositionIntelligenceMode.Runner)
                            mode = MorningPositionIntelligenceMode.Core;
                    }
                    else if (everAligned)
                    {
                        return Build(candidate, mode, MorningPositionIntelligenceExitReason.VectorFlowBiasLoss,
                            bar.TimestampUtc, bar.Open, mfe, mae, everAligned, alignedBars);
                    }
                }

                UpdateExcursions(bar, direction, entryPrice, ref mfe, ref mae);

                if (TouchesStop(bar, direction, stopPrice))
                {
                    return Build(candidate, mode, MorningPositionIntelligenceExitReason.StructuralStop,
                        bar.TimestampUtc, stopPrice, mfe, mae, everAligned, alignedBars);
                }

                if (everAligned && mfe >= config.RunnerThresholdTicks && alignedBars >= config.RunnerAlignedBars)
                    mode = MorningPositionIntelligenceMode.Runner;

                if (!everAligned)
                {
                    if (TouchesTarget(bar, direction, target))
                    {
                        return Build(candidate, MorningPositionIntelligenceMode.Scalp,
                            MorningPositionIntelligenceExitReason.ScalpCapture,
                            bar.TimestampUtc, target, mfe, mae, false, 0);
                    }

                    if (bar.TimestampUtc >= deadline)
                    {
                        return Build(candidate, MorningPositionIntelligenceMode.Scalp,
                            MorningPositionIntelligenceExitReason.ScalpTimeout,
                            bar.TimestampUtc, bar.Close, mfe, mae, false, 0);
                    }
                }
            }

            var last = path[path.Count - 1];
            return Build(candidate, mode, MorningPositionIntelligenceExitReason.ResearchWindowEnd,
                last.TimestampUtc, last.Close, mfe, mae, everAligned, alignedBars);
        }

        private MorningVectorFlowManagedTrade Build(
            MorningDailySequencingCandidate candidate,
            MorningPositionIntelligenceMode mode,
            MorningPositionIntelligenceExitReason reason,
            DateTimeOffset exitUtc,
            decimal exitPrice,
            decimal mfe,
            decimal mae,
            bool everAligned,
            int alignedBars)
        {
            var source = candidate.Entry.Source.Source;
            var ticks = source.Direction == NewYorkResearchDirection.Long
                ? (exitPrice - source.EntryPrice) / config.TickSize
                : (source.EntryPrice - exitPrice) / config.TickSize;

            return new MorningVectorFlowManagedTrade(
                candidate,
                mode,
                reason,
                exitUtc,
                exitPrice,
                ticks,
                ticks * config.DollarsPerTick,
                mfe,
                mae,
                everAligned,
                alignedBars);
        }

        private IReadOnlyList<VectorStatePoint> BuildVectorStates(IReadOnlyList<AggregatedBar> bars)
        {
            var result = new List<VectorStatePoint>();
            var smaClose = new RollingAverage(config.FtcLength);
            var atrFastState = new RmaAtrState(config.FtcAtrLength);
            var atrHighest = new RollingValues(config.FtcAtrHighestLookback);
            var atrSlowState = new RmaAtrState(config.VidyaAtrLength);
            var vidyaSmooth = new RollingAverage(config.VidyaSmoothingLength);
            var momentum = new RollingValues(config.VidyaMomentum);

            decimal? previousClose = null;
            decimal? previousFtcUpper = null;
            decimal? previousFtcLower = null;
            decimal? previousVidUpper = null;
            decimal? previousVidLower = null;
            decimal? vidya = null;

            var ftcTrend = false;
            var vidyaUp = false;
            var alpha = 2m / (config.VidyaLength + 1m);

            foreach (var bar in bars)
            {
                var sma = smaClose.Update(bar.Close);
                var atrFast = atrFastState.Update(bar.High, bar.Low, bar.Close);
                atrHighest.Add(atrFast);
                var atrFixed = atrHighest.Max;

                var ftcUpper = sma + atrFixed;
                var ftcLower = sma - atrFixed;

                if (previousClose.HasValue && previousFtcUpper.HasValue && previousFtcLower.HasValue)
                {
                    if (bar.Close > ftcUpper && previousClose.Value <= previousFtcUpper.Value)
                        ftcTrend = true;
                    if (bar.Close < ftcLower && previousClose.Value >= previousFtcLower.Value)
                        ftcTrend = false;
                }

                var change = previousClose.HasValue ? bar.Close - previousClose.Value : 0m;
                momentum.Add(change);
                var positive = momentum.Values.Where(x => x >= 0m).Sum();
                var negative = momentum.Values.Where(x => x < 0m).Sum(x => -x);
                var cmo = positive + negative > 0m
                    ? Math.Abs(100m * (positive - negative) / (positive + negative))
                    : 0m;

                vidya = !vidya.HasValue
                    ? bar.Close
                    : alpha * cmo / 100m * bar.Close
                        + (1m - alpha * cmo / 100m) * vidya.Value;

                var smoothedVidya = vidyaSmooth.Update(vidya.Value);
                var slowAtr = atrSlowState.Update(bar.High, bar.Low, bar.Close);
                var vidUpper = smoothedVidya + config.VidyaBandDistance * slowAtr;
                var vidLower = smoothedVidya - config.VidyaBandDistance * slowAtr;

                if (previousClose.HasValue && previousVidUpper.HasValue && previousVidLower.HasValue)
                {
                    if (bar.Close > vidUpper && previousClose.Value <= previousVidUpper.Value)
                        vidyaUp = true;
                    else if (bar.Close < vidLower && previousClose.Value >= previousVidLower.Value)
                        vidyaUp = false;
                }

                var bias = ftcTrend && vidyaUp
                    ? VectorFlowResearchBias.Bullish
                    : !ftcTrend && !vidyaUp
                        ? VectorFlowResearchBias.Bearish
                        : VectorFlowResearchBias.Neutral;

                result.Add(new VectorStatePoint(bar.EndUtc, bias));

                previousClose = bar.Close;
                previousFtcUpper = ftcUpper;
                previousFtcLower = ftcLower;
                previousVidUpper = vidUpper;
                previousVidLower = vidLower;
            }

            return result;
        }

        private static IReadOnlyList<AggregatedBar> Aggregate(
            IReadOnlyList<HistoricalBar> bars,
            int minutes,
            TimeZoneInfo central)
        {
            return bars
                .Select(x => new
                {
                    Bar = x,
                    Local = TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime
                })
                .GroupBy(x => new DateTime(
                    x.Local.Year, x.Local.Month, x.Local.Day,
                    x.Local.Hour, x.Local.Minute - x.Local.Minute % minutes, 0))
                .OrderBy(x => x.Key)
                .Select(group =>
                {
                    var ordered = group.OrderBy(x => x.Bar.TimestampUtc).ToList();
                    if (ordered.Count != minutes) return null;

                    return new AggregatedBar(
                        ordered[ordered.Count - 1].Bar.TimestampUtc,
                        ordered[0].Bar.Open,
                        ordered.Max(x => x.Bar.High),
                        ordered.Min(x => x.Bar.Low),
                        ordered[ordered.Count - 1].Bar.Close);
                })
                .Where(x => x != null)
                .Cast<AggregatedBar>()
                .ToList();
        }

        private static VectorStatePoint? LatestVectorStateBefore(
            IReadOnlyList<VectorStatePoint> states,
            DateTimeOffset utc)
        {
            VectorStatePoint? latest = null;
            for (var i = 0; i < states.Count; i++)
            {
                if (states[i].EndUtc >= utc) break;
                latest = states[i];
            }
            return latest;
        }

        private static bool IsAligned(
            NewYorkResearchDirection direction,
            VectorFlowResearchBias bias)
        {
            return direction == NewYorkResearchDirection.Long && bias == VectorFlowResearchBias.Bullish
                || direction == NewYorkResearchDirection.Short && bias == VectorFlowResearchBias.Bearish;
        }

        private void UpdateExcursions(
            HistoricalBar bar,
            NewYorkResearchDirection direction,
            decimal entryPrice,
            ref decimal mfe,
            ref decimal mae)
        {
            var favorable = direction == NewYorkResearchDirection.Long
                ? (bar.High - entryPrice) / config.TickSize
                : (entryPrice - bar.Low) / config.TickSize;

            var adverse = direction == NewYorkResearchDirection.Long
                ? (entryPrice - bar.Low) / config.TickSize
                : (bar.High - entryPrice) / config.TickSize;

            mfe = Math.Max(mfe, Math.Max(0m, favorable));
            mae = Math.Max(mae, Math.Max(0m, adverse));
        }

        private static bool TouchesStop(
            HistoricalBar bar,
            NewYorkResearchDirection direction,
            decimal stopPrice)
        {
            return direction == NewYorkResearchDirection.Long
                ? bar.Low <= stopPrice
                : bar.High >= stopPrice;
        }

        private static bool TouchesTarget(
            HistoricalBar bar,
            NewYorkResearchDirection direction,
            decimal targetPrice)
        {
            return direction == NewYorkResearchDirection.Long
                ? bar.High >= targetPrice
                : bar.Low <= targetPrice;
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
            }
        }

        private sealed class AggregatedBar
        {
            public AggregatedBar(DateTimeOffset endUtc, decimal open, decimal high, decimal low, decimal close)
            {
                EndUtc = endUtc;
                Open = open;
                High = high;
                Low = low;
                Close = close;
            }

            public DateTimeOffset EndUtc { get; }
            public decimal Open { get; }
            public decimal High { get; }
            public decimal Low { get; }
            public decimal Close { get; }
        }

        private sealed class VectorStatePoint
        {
            public VectorStatePoint(DateTimeOffset endUtc, VectorFlowResearchBias bias)
            {
                EndUtc = endUtc;
                Bias = bias;
            }

            public DateTimeOffset EndUtc { get; }
            public VectorFlowResearchBias Bias { get; }
        }

        private sealed class RollingAverage
        {
            private readonly int length;
            private readonly Queue<decimal> values = new Queue<decimal>();
            private decimal sum;

            public RollingAverage(int length)
            {
                this.length = length;
            }

            public decimal Update(decimal input)
            {
                values.Enqueue(input);
                sum += input;
                if (values.Count > length)
                    sum -= values.Dequeue();
                return sum / values.Count;
            }
        }

        private sealed class RollingValues
        {
            private readonly int length;
            private readonly Queue<decimal> values = new Queue<decimal>();

            public RollingValues(int length)
            {
                this.length = length;
            }

            public IEnumerable<decimal> Values => values;
            public decimal Max => values.Count == 0 ? 0m : values.Max();

            public void Add(decimal value)
            {
                values.Enqueue(value);
                if (values.Count > length)
                    values.Dequeue();
            }
        }

        private sealed class RmaAtrState
        {
            private readonly decimal alpha;
            private decimal? value;
            private decimal? previousClose;

            public RmaAtrState(int length)
            {
                alpha = 1m / length;
            }

            public decimal Update(decimal high, decimal low, decimal close)
            {
                var trueRange = previousClose.HasValue
                    ? Math.Max(high - low,
                        Math.Max(Math.Abs(high - previousClose.Value), Math.Abs(low - previousClose.Value)))
                    : high - low;

                value = value.HasValue
                    ? alpha * trueRange + (1m - alpha) * value.Value
                    : trueRange;

                previousClose = close;
                return value.Value;
            }
        }
    }
}
