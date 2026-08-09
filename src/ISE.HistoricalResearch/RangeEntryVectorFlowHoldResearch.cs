using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum RangeFilterResearchDirection
    {
        None = 0,
        Long = 1,
        Short = -1
    }

    public enum VectorFlowResearchBias
    {
        Neutral = 0,
        Bullish = 1,
        Bearish = -1
    }

    public enum RangeVectorManagementMode
    {
        Scalp = 0,
        Core = 1,
        Runner = 2
    }

    public enum RangeVectorExitReason
    {
        None = 0,
        StructuralStop = 1,
        ScalpCapture = 2,
        ScalpTimeout = 3,
        VectorFlowBiasLoss = 4,
        ResearchWindowEnd = 5
    }

    public sealed class RangeEntryVectorFlowHoldConfig
    {
        public RangeEntryVectorFlowHoldConfig(
            int rangeTimeframeMinutes = 3,
            int vectorTimeframeMinutes = 5,
            int rangeSamplingPeriod = 100,
            decimal rangeMultiplier = 3.0m,
            int ftcLength = 100,
            int ftcAtrLength = 14,
            int ftcAtrHighestLookback = 100,
            int vidyaLength = 20,
            int vidyaMomentum = 20,
            int vidyaSmoothingLength = 15,
            int vidyaAtrLength = 200,
            decimal vidyaBandDistance = 2m,
            int structureLookbackRangeBars = 5,
            int scalpTargetTicks = 150,
            int scalpTimeoutMinutes = 30,
            int runnerThresholdTicks = 300,
            int runnerAlignedBars = 2,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2m,
            int contracts = 2)
        {
            if (rangeTimeframeMinutes < 1 || vectorTimeframeMinutes < 1) throw new ArgumentOutOfRangeException(nameof(rangeTimeframeMinutes));
            if (rangeSamplingPeriod < 2 || rangeMultiplier <= 0m) throw new ArgumentOutOfRangeException(nameof(rangeSamplingPeriod));
            if (ftcLength < 2 || ftcAtrLength < 2 || ftcAtrHighestLookback < 2) throw new ArgumentOutOfRangeException(nameof(ftcLength));
            if (vidyaLength < 2 || vidyaMomentum < 2 || vidyaSmoothingLength < 1 || vidyaAtrLength < 2 || vidyaBandDistance <= 0m)
                throw new ArgumentOutOfRangeException(nameof(vidyaLength));
            if (structureLookbackRangeBars < 2) throw new ArgumentOutOfRangeException(nameof(structureLookbackRangeBars));
            if (scalpTargetTicks < 1 || scalpTimeoutMinutes < 1 || runnerThresholdTicks <= scalpTargetTicks || runnerAlignedBars < 1)
                throw new ArgumentOutOfRangeException(nameof(scalpTargetTicks));
            if (tickSize <= 0m || pointValuePerContract <= 0m || contracts < 1) throw new ArgumentOutOfRangeException(nameof(tickSize));

            RangeTimeframeMinutes = rangeTimeframeMinutes;
            VectorTimeframeMinutes = vectorTimeframeMinutes;
            RangeSamplingPeriod = rangeSamplingPeriod;
            RangeMultiplier = rangeMultiplier;
            FtcLength = ftcLength;
            FtcAtrLength = ftcAtrLength;
            FtcAtrHighestLookback = ftcAtrHighestLookback;
            VidyaLength = vidyaLength;
            VidyaMomentum = vidyaMomentum;
            VidyaSmoothingLength = vidyaSmoothingLength;
            VidyaAtrLength = vidyaAtrLength;
            VidyaBandDistance = vidyaBandDistance;
            StructureLookbackRangeBars = structureLookbackRangeBars;
            ScalpTargetTicks = scalpTargetTicks;
            ScalpTimeoutMinutes = scalpTimeoutMinutes;
            RunnerThresholdTicks = runnerThresholdTicks;
            RunnerAlignedBars = runnerAlignedBars;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
        }

        public int RangeTimeframeMinutes { get; }
        public int VectorTimeframeMinutes { get; }
        public int RangeSamplingPeriod { get; }
        public decimal RangeMultiplier { get; }
        public int FtcLength { get; }
        public int FtcAtrLength { get; }
        public int FtcAtrHighestLookback { get; }
        public int VidyaLength { get; }
        public int VidyaMomentum { get; }
        public int VidyaSmoothingLength { get; }
        public int VidyaAtrLength { get; }
        public decimal VidyaBandDistance { get; }
        public int StructureLookbackRangeBars { get; }
        public int ScalpTargetTicks { get; }
        public int ScalpTimeoutMinutes { get; }
        public int RunnerThresholdTicks { get; }
        public int RunnerAlignedBars { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal DollarsPerTick => TickSize * PointValuePerContract * Contracts;
    }

    public sealed class RangeVectorManagedOutcome
    {
        public RangeVectorManagedOutcome(RangeVectorManagementMode finalMode, RangeVectorExitReason exitReason,
            DateTimeOffset exitUtc, decimal exitPrice, decimal realizedTicks, decimal realizedDollars,
            decimal maxFavorableTicks, decimal maxAdverseTicks)
        {
            FinalMode = finalMode;
            ExitReason = exitReason;
            ExitUtc = exitUtc;
            ExitPrice = exitPrice;
            RealizedTicks = realizedTicks;
            RealizedDollars = realizedDollars;
            MaxFavorableTicks = maxFavorableTicks;
            MaxAdverseTicks = maxAdverseTicks;
        }

        public RangeVectorManagementMode FinalMode { get; }
        public RangeVectorExitReason ExitReason { get; }
        public DateTimeOffset ExitUtc { get; }
        public decimal ExitPrice { get; }
        public decimal RealizedTicks { get; }
        public decimal RealizedDollars { get; }
        public decimal MaxFavorableTicks { get; }
        public decimal MaxAdverseTicks { get; }
    }

    public sealed class RangeEntryVectorFlowComparison
    {
        public RangeEntryVectorFlowComparison(DateTime sessionDateCentral, RangeFilterResearchDirection direction,
            DateTimeOffset signalUtc, DateTimeOffset entryUtc, decimal entryPrice, decimal stopPrice,
            decimal initialRiskTicks, VectorFlowResearchBias vectorBiasAtEntry, bool alignedAtEntry,
            bool alignedBeforeScalpExit, RangeVectorManagedOutcome rangeOnlyControl, RangeVectorManagedOutcome vectorFlowHold)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Direction = direction;
            SignalUtc = signalUtc;
            EntryUtc = entryUtc;
            EntryPrice = entryPrice;
            StopPrice = stopPrice;
            InitialRiskTicks = initialRiskTicks;
            VectorBiasAtEntry = vectorBiasAtEntry;
            AlignedAtEntry = alignedAtEntry;
            AlignedBeforeScalpExit = alignedBeforeScalpExit;
            RangeOnlyControl = rangeOnlyControl ?? throw new ArgumentNullException(nameof(rangeOnlyControl));
            VectorFlowHold = vectorFlowHold ?? throw new ArgumentNullException(nameof(vectorFlowHold));
        }

        public DateTime SessionDateCentral { get; }
        public RangeFilterResearchDirection Direction { get; }
        public DateTimeOffset SignalUtc { get; }
        public DateTimeOffset EntryUtc { get; }
        public decimal EntryPrice { get; }
        public decimal StopPrice { get; }
        public decimal InitialRiskTicks { get; }
        public VectorFlowResearchBias VectorBiasAtEntry { get; }
        public bool AlignedAtEntry { get; }
        public bool AlignedBeforeScalpExit { get; }
        public RangeVectorManagedOutcome RangeOnlyControl { get; }
        public RangeVectorManagedOutcome VectorFlowHold { get; }
        public decimal VectorFlowImprovementDollars => VectorFlowHold.RealizedDollars - RangeOnlyControl.RealizedDollars;
    }

    /// <summary>
    /// Research-only implementation of the user's established workflow:
    /// 1) confirmed 3-minute Range Filter direction flips create entries,
    /// 2) fills are modeled at the next one-minute bar open,
    /// 3) 5-minute VectorFlow FTC+VIDYA state is used only after entry to decide whether a scalp may be held longer.
    /// Range Filter never receives authority to hold a position and VectorFlow never receives authority to open one.
    /// The control uses the same entry and structural stop but exits as a fixed scalp; the comparison isolates the
    /// incremental effect of VectorFlow hold authority. Parameters are research seeds, not production settings.
    /// </summary>
    public sealed class RangeEntryVectorFlowHoldAnalyzer
    {
        private static readonly TimeSpan EntryWindowStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan EntryWindowEnd = new TimeSpan(10, 30, 0);
        private static readonly TimeSpan ResearchWindowEnd = new TimeSpan(11, 0, 0);
        private readonly RangeEntryVectorFlowHoldConfig config;

        public RangeEntryVectorFlowHoldAnalyzer(RangeEntryVectorFlowHoldConfig? config = null)
        {
            this.config = config ?? new RangeEntryVectorFlowHoldConfig();
        }

        public IReadOnlyList<RangeEntryVectorFlowComparison> Analyze(IReadOnlyList<HistoricalBar> oneMinuteBars)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (oneMinuteBars.Count == 0) return Array.Empty<RangeEntryVectorFlowComparison>();

            var central = ResolveCentralTimeZone();
            var minuteBars = oneMinuteBars.OrderBy(x => x.TimestampUtc).ToList();
            var rangeBars = Aggregate(minuteBars, config.RangeTimeframeMinutes, central);
            var vectorBars = Aggregate(minuteBars, config.VectorTimeframeMinutes, central);
            var rangeStates = BuildRangeStates(rangeBars);
            var vectorStates = BuildVectorStates(vectorBars);
            var results = new List<RangeEntryVectorFlowComparison>();

            for (var i = 0; i < rangeStates.Count; i++)
            {
                var signal = rangeStates[i];
                if (signal.Signal == RangeFilterResearchDirection.None) continue;
                if (signal.Bar.LocalStart.TimeOfDay < EntryWindowStart || signal.Bar.LocalStart.TimeOfDay >= EntryWindowEnd) continue;

                var entryIndex = minuteBars.FindIndex(x => x.TimestampUtc > signal.Bar.EndUtc);
                if (entryIndex < 0) continue;
                var entryBar = minuteBars[entryIndex];
                var entryLocal = TimeZoneInfo.ConvertTime(entryBar.TimestampUtc, central).DateTime;
                if (entryLocal.Date != signal.Bar.LocalStart.Date || entryLocal.TimeOfDay >= ResearchWindowEnd) continue;

                var structureStart = Math.Max(0, i - config.StructureLookbackRangeBars + 1);
                var structure = rangeStates.Skip(structureStart).Take(i - structureStart + 1).Select(x => x.Bar).ToList();
                var stop = signal.Signal == RangeFilterResearchDirection.Long
                    ? structure.Min(x => x.Low) - config.TickSize
                    : structure.Max(x => x.High) + config.TickSize;
                var riskTicks = Math.Abs(entryBar.Open - stop) / config.TickSize;
                var biasAtEntry = LatestBiasBefore(vectorStates, entryBar.TimestampUtc);
                var alignedAtEntry = IsAligned(signal.Signal, biasAtEntry);

                var path = minuteBars.Skip(entryIndex)
                    .TakeWhile(x => TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime.Date == entryLocal.Date
                        && TimeZoneInfo.ConvertTime(x.TimestampUtc, central).TimeOfDay < ResearchWindowEnd)
                    .ToList();
                if (path.Count == 0) continue;

                var control = ManageControl(path, signal.Signal, entryBar.Open, stop);
                bool alignedBeforeScalpExit;
                var vector = ManageWithVectorFlow(path, signal.Signal, entryBar.Open, stop, vectorStates, control.ExitUtc, out alignedBeforeScalpExit);
                results.Add(new RangeEntryVectorFlowComparison(entryLocal.Date, signal.Signal, signal.Bar.EndUtc,
                    entryBar.TimestampUtc, entryBar.Open, stop, riskTicks, biasAtEntry, alignedAtEntry,
                    alignedBeforeScalpExit, control, vector));
            }

            return results;
        }

        private RangeVectorManagedOutcome ManageControl(IReadOnlyList<HistoricalBar> path, RangeFilterResearchDirection direction,
            decimal entryPrice, decimal stopPrice)
        {
            var target = direction == RangeFilterResearchDirection.Long
                ? entryPrice + config.ScalpTargetTicks * config.TickSize
                : entryPrice - config.ScalpTargetTicks * config.TickSize;
            var deadline = path[0].TimestampUtc.AddMinutes(config.ScalpTimeoutMinutes);
            decimal mfe = 0m, mae = 0m;

            foreach (var bar in path)
            {
                UpdateExcursions(bar, direction, entryPrice, ref mfe, ref mae);
                if (TouchesStop(bar, direction, stopPrice))
                    return BuildOutcome(RangeVectorManagementMode.Scalp, RangeVectorExitReason.StructuralStop, bar.TimestampUtc,
                        stopPrice, direction, entryPrice, mfe, mae);
                if (TouchesTarget(bar, direction, target))
                    return BuildOutcome(RangeVectorManagementMode.Scalp, RangeVectorExitReason.ScalpCapture, bar.TimestampUtc,
                        target, direction, entryPrice, mfe, mae);
                if (bar.TimestampUtc >= deadline)
                    return BuildOutcome(RangeVectorManagementMode.Scalp, RangeVectorExitReason.ScalpTimeout, bar.TimestampUtc,
                        bar.Close, direction, entryPrice, mfe, mae);
            }

            var last = path[path.Count - 1];
            return BuildOutcome(RangeVectorManagementMode.Scalp, RangeVectorExitReason.ResearchWindowEnd, last.TimestampUtc,
                last.Close, direction, entryPrice, mfe, mae);
        }

        private RangeVectorManagedOutcome ManageWithVectorFlow(IReadOnlyList<HistoricalBar> path, RangeFilterResearchDirection direction,
            decimal entryPrice, decimal stopPrice, IReadOnlyList<VectorStatePoint> vectorStates, DateTimeOffset controlExitUtc,
            out bool alignedBeforeScalpExit)
        {
            var target = direction == RangeFilterResearchDirection.Long
                ? entryPrice + config.ScalpTargetTicks * config.TickSize
                : entryPrice - config.ScalpTargetTicks * config.TickSize;
            var deadline = path[0].TimestampUtc.AddMinutes(config.ScalpTimeoutMinutes);
            var mode = RangeVectorManagementMode.Scalp;
            var everAligned = false;
            var alignedBars = 0;
            DateTimeOffset? lastVectorEnd = null;
            decimal mfe = 0m, mae = 0m;
            alignedBeforeScalpExit = false;

            foreach (var bar in path)
            {
                var latest = LatestVectorStateBefore(vectorStates, bar.TimestampUtc);
                if (latest != null && (!lastVectorEnd.HasValue || latest.Bar.EndUtc > lastVectorEnd.Value))
                {
                    lastVectorEnd = latest.Bar.EndUtc;
                    var aligned = IsAligned(direction, latest.Bias);
                    if (aligned)
                    {
                        everAligned = true;
                        alignedBars++;
                        mode = mode == RangeVectorManagementMode.Runner ? mode : RangeVectorManagementMode.Core;
                    }
                    else if (everAligned)
                    {
                        return BuildOutcome(mode, RangeVectorExitReason.VectorFlowBiasLoss, bar.TimestampUtc,
                            bar.Open, direction, entryPrice, mfe, mae);
                    }
                }

                if (everAligned && bar.TimestampUtc <= controlExitUtc)
                    alignedBeforeScalpExit = true;

                UpdateExcursions(bar, direction, entryPrice, ref mfe, ref mae);
                if (TouchesStop(bar, direction, stopPrice))
                    return BuildOutcome(mode, RangeVectorExitReason.StructuralStop, bar.TimestampUtc,
                        stopPrice, direction, entryPrice, mfe, mae);

                if (everAligned && mfe >= config.RunnerThresholdTicks && alignedBars >= config.RunnerAlignedBars)
                    mode = RangeVectorManagementMode.Runner;

                if (!everAligned)
                {
                    if (TouchesTarget(bar, direction, target))
                        return BuildOutcome(RangeVectorManagementMode.Scalp, RangeVectorExitReason.ScalpCapture, bar.TimestampUtc,
                            target, direction, entryPrice, mfe, mae);
                    if (bar.TimestampUtc >= deadline)
                        return BuildOutcome(RangeVectorManagementMode.Scalp, RangeVectorExitReason.ScalpTimeout, bar.TimestampUtc,
                            bar.Close, direction, entryPrice, mfe, mae);
                }
            }

            var last = path[path.Count - 1];
            return BuildOutcome(mode, RangeVectorExitReason.ResearchWindowEnd, last.TimestampUtc,
                last.Close, direction, entryPrice, mfe, mae);
        }

        private RangeVectorManagedOutcome BuildOutcome(RangeVectorManagementMode mode, RangeVectorExitReason reason,
            DateTimeOffset exitUtc, decimal exitPrice, RangeFilterResearchDirection direction, decimal entryPrice,
            decimal mfe, decimal mae)
        {
            var ticks = direction == RangeFilterResearchDirection.Long
                ? (exitPrice - entryPrice) / config.TickSize
                : (entryPrice - exitPrice) / config.TickSize;
            return new RangeVectorManagedOutcome(mode, reason, exitUtc, exitPrice, ticks,
                ticks * config.DollarsPerTick, mfe, mae);
        }

        private IReadOnlyList<RangeStatePoint> BuildRangeStates(IReadOnlyList<AggregatedBar> bars)
        {
            var result = new List<RangeStatePoint>();
            var firstEma = new EmaState(config.RangeSamplingPeriod);
            var secondEma = new EmaState(config.RangeSamplingPeriod * 2 - 1);
            decimal? previousSource = null;
            decimal? previousFilter = null;
            var upward = 0;
            var downward = 0;
            var conditionState = 0;

            foreach (var bar in bars)
            {
                var source = bar.Close;
                var change = previousSource.HasValue ? Math.Abs(source - previousSource.Value) : 0m;
                var averageRange = firstEma.Update(change);
                var smoothRange = secondEma.Update(averageRange) * config.RangeMultiplier;
                decimal filter;
                if (!previousFilter.HasValue) filter = source;
                else if (source > previousFilter.Value)
                    filter = source - smoothRange < previousFilter.Value ? previousFilter.Value : source - smoothRange;
                else
                    filter = source + smoothRange > previousFilter.Value ? previousFilter.Value : source + smoothRange;

                if (previousFilter.HasValue)
                {
                    if (filter > previousFilter.Value) { upward++; downward = 0; }
                    else if (filter < previousFilter.Value) { downward++; upward = 0; }
                }

                var longCondition = source > filter && upward >= 1;
                var shortCondition = source < filter && downward >= 1;
                var previousConditionState = conditionState;
                conditionState = longCondition ? 1 : shortCondition ? -1 : conditionState;
                var signal = longCondition && previousConditionState == -1 ? RangeFilterResearchDirection.Long
                    : shortCondition && previousConditionState == 1 ? RangeFilterResearchDirection.Short
                    : RangeFilterResearchDirection.None;
                result.Add(new RangeStatePoint(bar, filter, upward, downward, signal));
                previousSource = source;
                previousFilter = filter;
            }
            return result;
        }

        private IReadOnlyList<VectorStatePoint> BuildVectorStates(IReadOnlyList<AggregatedBar> bars)
        {
            var result = new List<VectorStatePoint>();
            var smaClose = new RollingAverage(config.FtcLength);
            var atr14 = new RmaAtrState(config.FtcAtrLength);
            var atrHighest = new RollingValues(config.FtcAtrHighestLookback);
            var atr200 = new RmaAtrState(config.VidyaAtrLength);
            var vidyaSmooth = new RollingAverage(config.VidyaSmoothingLength);
            var momentum = new RollingValues(config.VidyaMomentum);
            decimal? previousClose = null, previousFtcUpper = null, previousFtcLower = null;
            decimal? previousVidUpper = null, previousVidLower = null;
            decimal? vidya = null;
            var ftcTrend = false;
            var isVidUp = false;
            var alpha = 2m / (config.VidyaLength + 1m);

            foreach (var bar in bars)
            {
                var sma = smaClose.Update(bar.Close);
                var atrFast = atr14.Update(bar.High, bar.Low, bar.Close);
                atrHighest.Add(atrFast);
                var atrFixed = atrHighest.Max;
                var ftcUpper = sma + atrFixed;
                var ftcLower = sma - atrFixed;
                if (previousClose.HasValue && previousFtcUpper.HasValue && previousFtcLower.HasValue)
                {
                    if (bar.Close > ftcUpper && previousClose.Value <= previousFtcUpper.Value) ftcTrend = true;
                    if (bar.Close < ftcLower && previousClose.Value >= previousFtcLower.Value) ftcTrend = false;
                }

                var change = previousClose.HasValue ? bar.Close - previousClose.Value : 0m;
                momentum.Add(change);
                var positive = momentum.Values.Where(x => x >= 0m).Sum();
                var negative = momentum.Values.Where(x => x < 0m).Sum(x => -x);
                var cmo = positive + negative > 0m ? Math.Abs(100m * (positive - negative) / (positive + negative)) : 0m;
                vidya = !vidya.HasValue ? bar.Close
                    : alpha * cmo / 100m * bar.Close + (1m - alpha * cmo / 100m) * vidya.Value;
                var smoothedVidya = vidyaSmooth.Update(vidya.Value);
                var slowAtr = atr200.Update(bar.High, bar.Low, bar.Close);
                var vidUpper = smoothedVidya + config.VidyaBandDistance * slowAtr;
                var vidLower = smoothedVidya - config.VidyaBandDistance * slowAtr;
                if (previousClose.HasValue && previousVidUpper.HasValue && previousVidLower.HasValue)
                {
                    if (bar.Close > vidUpper && previousClose.Value <= previousVidUpper.Value) isVidUp = true;
                    else if (bar.Close < vidLower && previousClose.Value >= previousVidLower.Value) isVidUp = false;
                }

                var bias = ftcTrend && isVidUp ? VectorFlowResearchBias.Bullish
                    : !ftcTrend && !isVidUp ? VectorFlowResearchBias.Bearish
                    : VectorFlowResearchBias.Neutral;
                result.Add(new VectorStatePoint(bar, bias, ftcTrend, isVidUp));
                previousClose = bar.Close;
                previousFtcUpper = ftcUpper;
                previousFtcLower = ftcLower;
                previousVidUpper = vidUpper;
                previousVidLower = vidLower;
            }
            return result;
        }

        private static IReadOnlyList<AggregatedBar> Aggregate(IReadOnlyList<HistoricalBar> bars, int minutes, TimeZoneInfo central)
        {
            var grouped = bars.Select(x => new { Bar = x, Local = TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime })
                .GroupBy(x => new DateTime(x.Local.Year, x.Local.Month, x.Local.Day, x.Local.Hour, x.Local.Minute - x.Local.Minute % minutes, 0))
                .OrderBy(x => x.Key);
            var result = new List<AggregatedBar>();
            foreach (var group in grouped)
            {
                var ordered = group.OrderBy(x => x.Bar.TimestampUtc).ToList();
                if (ordered.Count != minutes) continue;
                result.Add(new AggregatedBar(group.Key, ordered[0].Bar.TimestampUtc, ordered[ordered.Count - 1].Bar.TimestampUtc,
                    ordered[0].Bar.Open, ordered.Max(x => x.Bar.High), ordered.Min(x => x.Bar.Low), ordered[ordered.Count - 1].Bar.Close));
            }
            return result;
        }

        private static VectorFlowResearchBias LatestBiasBefore(IReadOnlyList<VectorStatePoint> states, DateTimeOffset utc)
        {
            var point = LatestVectorStateBefore(states, utc);
            return point == null ? VectorFlowResearchBias.Neutral : point.Bias;
        }

        private static VectorStatePoint? LatestVectorStateBefore(IReadOnlyList<VectorStatePoint> states, DateTimeOffset utc)
        {
            VectorStatePoint? latest = null;
            for (var i = 0; i < states.Count; i++)
            {
                if (states[i].Bar.EndUtc >= utc) break;
                latest = states[i];
            }
            return latest;
        }

        private static bool IsAligned(RangeFilterResearchDirection direction, VectorFlowResearchBias bias)
        {
            return direction == RangeFilterResearchDirection.Long && bias == VectorFlowResearchBias.Bullish
                || direction == RangeFilterResearchDirection.Short && bias == VectorFlowResearchBias.Bearish;
        }

        private void UpdateExcursions(HistoricalBar bar, RangeFilterResearchDirection direction, decimal entryPrice, ref decimal mfe, ref decimal mae)
        {
            var favorable = direction == RangeFilterResearchDirection.Long ? (bar.High - entryPrice) / config.TickSize : (entryPrice - bar.Low) / config.TickSize;
            var adverse = direction == RangeFilterResearchDirection.Long ? (entryPrice - bar.Low) / config.TickSize : (bar.High - entryPrice) / config.TickSize;
            mfe = Math.Max(mfe, Math.Max(0m, favorable));
            mae = Math.Max(mae, Math.Max(0m, adverse));
        }

        private static bool TouchesStop(HistoricalBar bar, RangeFilterResearchDirection direction, decimal stopPrice)
        {
            return direction == RangeFilterResearchDirection.Long ? bar.Low <= stopPrice : bar.High >= stopPrice;
        }

        private static bool TouchesTarget(HistoricalBar bar, RangeFilterResearchDirection direction, decimal targetPrice)
        {
            return direction == RangeFilterResearchDirection.Long ? bar.High >= targetPrice : bar.Low <= targetPrice;
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }

        private sealed class AggregatedBar
        {
            public AggregatedBar(DateTime localStart, DateTimeOffset startUtc, DateTimeOffset endUtc, decimal open, decimal high, decimal low, decimal close)
            {
                LocalStart = localStart;
                StartUtc = startUtc;
                EndUtc = endUtc;
                Open = open;
                High = high;
                Low = low;
                Close = close;
            }
            public DateTime LocalStart { get; }
            public DateTimeOffset StartUtc { get; }
            public DateTimeOffset EndUtc { get; }
            public decimal Open { get; }
            public decimal High { get; }
            public decimal Low { get; }
            public decimal Close { get; }
        }

        private sealed class RangeStatePoint
        {
            public RangeStatePoint(AggregatedBar bar, decimal filter, int upward, int downward, RangeFilterResearchDirection signal)
            { Bar = bar; Filter = filter; Upward = upward; Downward = downward; Signal = signal; }
            public AggregatedBar Bar { get; }
            public decimal Filter { get; }
            public int Upward { get; }
            public int Downward { get; }
            public RangeFilterResearchDirection Signal { get; }
        }

        private sealed class VectorStatePoint
        {
            public VectorStatePoint(AggregatedBar bar, VectorFlowResearchBias bias, bool ftcTrend, bool vidyaUp)
            { Bar = bar; Bias = bias; FtcTrend = ftcTrend; VidyaUp = vidyaUp; }
            public AggregatedBar Bar { get; }
            public VectorFlowResearchBias Bias { get; }
            public bool FtcTrend { get; }
            public bool VidyaUp { get; }
        }

        private sealed class EmaState
        {
            private readonly decimal alpha;
            private decimal? value;
            public EmaState(int length) { alpha = 2m / (length + 1m); }
            public decimal Update(decimal input)
            {
                value = value.HasValue ? alpha * input + (1m - alpha) * value.Value : input;
                return value.Value;
            }
        }

        private sealed class RollingAverage
        {
            private readonly int length;
            private readonly Queue<decimal> values = new Queue<decimal>();
            private decimal sum;
            public RollingAverage(int length) { this.length = length; }
            public decimal Update(decimal input)
            {
                values.Enqueue(input); sum += input;
                if (values.Count > length) sum -= values.Dequeue();
                return sum / values.Count;
            }
        }

        private sealed class RollingValues
        {
            private readonly int length;
            private readonly Queue<decimal> values = new Queue<decimal>();
            public RollingValues(int length) { this.length = length; }
            public IEnumerable<decimal> Values => values;
            public decimal Max => values.Count == 0 ? 0m : values.Max();
            public void Add(decimal value)
            {
                values.Enqueue(value);
                if (values.Count > length) values.Dequeue();
            }
        }

        private sealed class RmaAtrState
        {
            private readonly decimal alpha;
            private decimal? value;
            private decimal? previousClose;
            public RmaAtrState(int length) { alpha = 1m / length; }
            public decimal Update(decimal high, decimal low, decimal close)
            {
                var tr = previousClose.HasValue
                    ? Math.Max(high - low, Math.Max(Math.Abs(high - previousClose.Value), Math.Abs(low - previousClose.Value)))
                    : high - low;
                value = value.HasValue ? alpha * tr + (1m - alpha) * value.Value : tr;
                previousClose = close;
                return value.Value;
            }
        }
    }
}
