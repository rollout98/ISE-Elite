using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum ProtectedRangeVectorExitReason
    {
        None = 0,
        StructuralStop = 1,
        ScalpCapture = 2,
        ScalpTimeout = 3,
        BreakevenProtection = 4,
        PullbackProtection = 5,
        RunnerTrailProtection = 6,
        VectorFlowBiasLoss = 7,
        ResearchWindowEnd = 8
    }

    public sealed class ProtectedRangeVectorConfig
    {
        public ProtectedRangeVectorConfig(
            RangeEntryVectorFlowHoldConfig? indicatorConfig = null,
            int breakevenTriggerTicks = 100,
            decimal maxPullbackPercent = 75m,
            int minimumPeakTicksForPullbackProtection = 20,
            int runnerTrailTicks = 250,
            decimal combineMaximumStructuralRiskTicks = 325m,
            decimal fundedMaximumStructuralRiskTicks = 250m)
        {
            IndicatorConfig = indicatorConfig ?? new RangeEntryVectorFlowHoldConfig();
            if (breakevenTriggerTicks < 1 || breakevenTriggerTicks >= IndicatorConfig.ScalpTargetTicks)
                throw new ArgumentOutOfRangeException(nameof(breakevenTriggerTicks));
            if (maxPullbackPercent <= 0m || maxPullbackPercent >= 100m)
                throw new ArgumentOutOfRangeException(nameof(maxPullbackPercent));
            if (minimumPeakTicksForPullbackProtection < 1)
                throw new ArgumentOutOfRangeException(nameof(minimumPeakTicksForPullbackProtection));
            if (runnerTrailTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(runnerTrailTicks));
            if (combineMaximumStructuralRiskTicks <= 0m || fundedMaximumStructuralRiskTicks <= 0m
                || fundedMaximumStructuralRiskTicks > combineMaximumStructuralRiskTicks)
                throw new ArgumentOutOfRangeException(nameof(combineMaximumStructuralRiskTicks));

            BreakevenTriggerTicks = breakevenTriggerTicks;
            MaxPullbackPercent = maxPullbackPercent;
            MinimumPeakTicksForPullbackProtection = minimumPeakTicksForPullbackProtection;
            RunnerTrailTicks = runnerTrailTicks;
            CombineMaximumStructuralRiskTicks = combineMaximumStructuralRiskTicks;
            FundedMaximumStructuralRiskTicks = fundedMaximumStructuralRiskTicks;
        }

        public RangeEntryVectorFlowHoldConfig IndicatorConfig { get; }
        public int BreakevenTriggerTicks { get; }
        public decimal MaxPullbackPercent { get; }
        public int MinimumPeakTicksForPullbackProtection { get; }
        public int RunnerTrailTicks { get; }
        public decimal CombineMaximumStructuralRiskTicks { get; }
        public decimal FundedMaximumStructuralRiskTicks { get; }
    }

    public sealed class ProtectedRangeVectorManagedOutcome
    {
        public ProtectedRangeVectorManagedOutcome(RangeVectorManagementMode finalMode, ProtectedRangeVectorExitReason exitReason,
            DateTimeOffset exitUtc, decimal exitPrice, decimal realizedTicks, decimal realizedDollars,
            decimal maxFavorableTicks, decimal maxAdverseTicks, bool extensionActivated,
            bool breakevenActivated, decimal bestProtectedTicks)
        {
            FinalMode = finalMode;
            ExitReason = exitReason;
            ExitUtc = exitUtc;
            ExitPrice = exitPrice;
            RealizedTicks = realizedTicks;
            RealizedDollars = realizedDollars;
            MaxFavorableTicks = maxFavorableTicks;
            MaxAdverseTicks = maxAdverseTicks;
            ExtensionActivated = extensionActivated;
            BreakevenActivated = breakevenActivated;
            BestProtectedTicks = bestProtectedTicks;
        }

        public RangeVectorManagementMode FinalMode { get; }
        public ProtectedRangeVectorExitReason ExitReason { get; }
        public DateTimeOffset ExitUtc { get; }
        public decimal ExitPrice { get; }
        public decimal RealizedTicks { get; }
        public decimal RealizedDollars { get; }
        public decimal MaxFavorableTicks { get; }
        public decimal MaxAdverseTicks { get; }
        public bool ExtensionActivated { get; }
        public bool BreakevenActivated { get; }
        public decimal BestProtectedTicks { get; }
    }

    public sealed class ProtectedRangeVectorComparison
    {
        public ProtectedRangeVectorComparison(RangeEntryVectorFlowComparison source,
            ProtectedRangeVectorManagedOutcome protectedHold, bool combineRiskQualified, bool fundedRiskQualified)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ProtectedHold = protectedHold ?? throw new ArgumentNullException(nameof(protectedHold));
            CombineRiskQualified = combineRiskQualified;
            FundedRiskQualified = fundedRiskQualified;
        }

        public RangeEntryVectorFlowComparison Source { get; }
        public ProtectedRangeVectorManagedOutcome ProtectedHold { get; }
        public bool CombineRiskQualified { get; }
        public bool FundedRiskQualified { get; }
        public decimal ImprovementVsControlDollars => ProtectedHold.RealizedDollars - Source.RangeOnlyControl.RealizedDollars;
        public decimal ImprovementVsV1Dollars => ProtectedHold.RealizedDollars - Source.VectorFlowHold.RealizedDollars;
    }

    /// <summary>
    /// Research-only v2 position manager. Entry authority remains the confirmed 3-minute Range Filter flip implemented
    /// by RangeEntryVectorFlowHoldAnalyzer. Five-minute VectorFlow receives hold authority only after the normal scalp
    /// target has actually been reached while the completed 5-minute bias is aligned. Before that point, ordinary scalp
    /// target/timeout behavior remains authoritative. A 100-tick breakeven trigger, peak-retention floor, and runner trail
    /// prevent an extended winner from being surrendered all the way back to the original structural stop. Protection
    /// calculated from a bar's excursion becomes active on the following bar so the research does not use same-bar
    /// hindsight. Combine/Funded risk flags are eligibility diagnostics only; they do not create entries or change exits.
    /// </summary>
    public sealed class ProtectedRangeVectorAnalyzer
    {
        private static readonly TimeSpan ResearchWindowEnd = new TimeSpan(11, 0, 0);
        private readonly ProtectedRangeVectorConfig config;

        public ProtectedRangeVectorAnalyzer(ProtectedRangeVectorConfig? config = null)
        {
            this.config = config ?? new ProtectedRangeVectorConfig();
        }

        public IReadOnlyList<ProtectedRangeVectorComparison> Analyze(IReadOnlyList<HistoricalBar> oneMinuteBars)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (oneMinuteBars.Count == 0) return Array.Empty<ProtectedRangeVectorComparison>();

            var ordered = oneMinuteBars.OrderBy(x => x.TimestampUtc).ToList();
            var entries = new RangeEntryVectorFlowHoldAnalyzer(config.IndicatorConfig).Analyze(ordered);
            var central = ResolveCentralTimeZone();
            var vectorStates = BuildVectorStates(Aggregate(ordered, config.IndicatorConfig.VectorTimeframeMinutes, central));
            var result = new List<ProtectedRangeVectorComparison>();

            foreach (var source in entries)
            {
                var entryLocal = TimeZoneInfo.ConvertTime(source.EntryUtc, central).DateTime;
                var path = ordered.Where(x => x.TimestampUtc >= source.EntryUtc)
                    .TakeWhile(x => TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime.Date == entryLocal.Date
                        && TimeZoneInfo.ConvertTime(x.TimestampUtc, central).TimeOfDay < ResearchWindowEnd)
                    .ToList();
                if (path.Count == 0) continue;

                var managed = Manage(path, source.Direction, source.EntryPrice, source.StopPrice, vectorStates);
                result.Add(new ProtectedRangeVectorComparison(source, managed,
                    source.InitialRiskTicks <= config.CombineMaximumStructuralRiskTicks,
                    source.InitialRiskTicks <= config.FundedMaximumStructuralRiskTicks));
            }

            return result;
        }

        private ProtectedRangeVectorManagedOutcome Manage(IReadOnlyList<HistoricalBar> path,
            RangeFilterResearchDirection direction, decimal entryPrice, decimal structuralStop,
            IReadOnlyList<VectorStatePoint> vectorStates)
        {
            var indicator = config.IndicatorConfig;
            var targetPrice = direction == RangeFilterResearchDirection.Long
                ? entryPrice + indicator.ScalpTargetTicks * indicator.TickSize
                : entryPrice - indicator.ScalpTargetTicks * indicator.TickSize;
            var deadline = path[0].TimestampUtc.AddMinutes(indicator.ScalpTimeoutMinutes);
            var activeStop = structuralStop;
            var activeStopReason = ProtectedRangeVectorExitReason.StructuralStop;
            var mode = RangeVectorManagementMode.Scalp;
            var extensionActivated = false;
            var breakevenActivated = false;
            var alignedVectorBars = 0;
            DateTimeOffset? lastVectorEnd = null;
            decimal mfe = 0m;
            decimal mae = 0m;
            decimal bestProtectedTicks = 0m;

            foreach (var bar in path)
            {
                var latest = LatestVectorStateBefore(vectorStates, bar.TimestampUtc);
                var alignedNow = latest != null && IsAligned(direction, latest.Bias);
                if (latest != null && (!lastVectorEnd.HasValue || latest.Bar.EndUtc > lastVectorEnd.Value))
                {
                    lastVectorEnd = latest.Bar.EndUtc;
                    alignedVectorBars = alignedNow ? alignedVectorBars + 1 : 0;
                }

                // A completed 5-minute loss of bias is known at this minute's open. Once extension has been earned,
                // the position exits rather than waiting for the original structural stop.
                if (extensionActivated && !alignedNow)
                    return Build(mode, ProtectedRangeVectorExitReason.VectorFlowBiasLoss, bar.TimestampUtc,
                        bar.Open, direction, entryPrice, mfe, mae, extensionActivated, breakevenActivated, bestProtectedTicks);

                // Protection derived from a prior bar is authoritative before this bar's high/low can improve it.
                if (TouchesStop(bar, direction, activeStop))
                    return Build(mode, activeStopReason, bar.TimestampUtc, activeStop, direction, entryPrice,
                        mfe, mae, extensionActivated, breakevenActivated, bestProtectedTicks);

                UpdateExcursions(bar, direction, entryPrice, ref mfe, ref mae);

                if (!breakevenActivated && mfe >= config.BreakevenTriggerTicks)
                {
                    breakevenActivated = true;
                    RatchetStop(direction, entryPrice, ref activeStop, ref activeStopReason,
                        ProtectedRangeVectorExitReason.BreakevenProtection);
                }

                if (!extensionActivated)
                {
                    if (TouchesTarget(bar, direction, targetPrice))
                    {
                        // VectorFlow can extend only a scalp that has already earned its normal objective and only from
                        // bias that was known before this one-minute bar began.
                        if (!alignedNow)
                            return Build(RangeVectorManagementMode.Scalp, ProtectedRangeVectorExitReason.ScalpCapture,
                                bar.TimestampUtc, targetPrice, direction, entryPrice, mfe, mae, false,
                                breakevenActivated, bestProtectedTicks);

                        extensionActivated = true;
                        mode = RangeVectorManagementMode.Core;
                    }
                    else if (bar.TimestampUtc >= deadline)
                    {
                        return Build(RangeVectorManagementMode.Scalp, ProtectedRangeVectorExitReason.ScalpTimeout,
                            bar.TimestampUtc, bar.Close, direction, entryPrice, mfe, mae, false,
                            breakevenActivated, bestProtectedTicks);
                    }
                }

                if (extensionActivated)
                {
                    if (mfe >= indicator.RunnerThresholdTicks && alignedVectorBars >= indicator.RunnerAlignedBars)
                        mode = RangeVectorManagementMode.Runner;

                    if (mfe >= config.MinimumPeakTicksForPullbackProtection)
                    {
                        var retainedFraction = 1m - config.MaxPullbackPercent / 100m;
                        var retainedTicks = mfe * retainedFraction;
                        var pullbackStop = direction == RangeFilterResearchDirection.Long
                            ? entryPrice + retainedTicks * indicator.TickSize
                            : entryPrice - retainedTicks * indicator.TickSize;
                        if (RatchetStop(direction, pullbackStop, ref activeStop, ref activeStopReason,
                            ProtectedRangeVectorExitReason.PullbackProtection))
                            bestProtectedTicks = Math.Max(bestProtectedTicks, retainedTicks);
                    }

                    if (mode == RangeVectorManagementMode.Runner && mfe > config.RunnerTrailTicks)
                    {
                        var runnerProtectedTicks = mfe - config.RunnerTrailTicks;
                        var runnerStop = direction == RangeFilterResearchDirection.Long
                            ? entryPrice + runnerProtectedTicks * indicator.TickSize
                            : entryPrice - runnerProtectedTicks * indicator.TickSize;
                        if (RatchetStop(direction, runnerStop, ref activeStop, ref activeStopReason,
                            ProtectedRangeVectorExitReason.RunnerTrailProtection))
                            bestProtectedTicks = Math.Max(bestProtectedTicks, runnerProtectedTicks);
                    }
                }
            }

            var last = path[path.Count - 1];
            return Build(mode, ProtectedRangeVectorExitReason.ResearchWindowEnd, last.TimestampUtc, last.Close,
                direction, entryPrice, mfe, mae, extensionActivated, breakevenActivated, bestProtectedTicks);
        }

        private ProtectedRangeVectorManagedOutcome Build(RangeVectorManagementMode mode, ProtectedRangeVectorExitReason reason,
            DateTimeOffset exitUtc, decimal exitPrice, RangeFilterResearchDirection direction, decimal entryPrice,
            decimal mfe, decimal mae, bool extensionActivated, bool breakevenActivated, decimal bestProtectedTicks)
        {
            var indicator = config.IndicatorConfig;
            var ticks = direction == RangeFilterResearchDirection.Long
                ? (exitPrice - entryPrice) / indicator.TickSize
                : (entryPrice - exitPrice) / indicator.TickSize;
            return new ProtectedRangeVectorManagedOutcome(mode, reason, exitUtc, exitPrice, ticks,
                ticks * indicator.DollarsPerTick, mfe, mae, extensionActivated, breakevenActivated, bestProtectedTicks);
        }

        private static bool RatchetStop(RangeFilterResearchDirection direction, decimal candidate,
            ref decimal activeStop, ref ProtectedRangeVectorExitReason activeReason, ProtectedRangeVectorExitReason candidateReason)
        {
            var improves = direction == RangeFilterResearchDirection.Long ? candidate > activeStop : candidate < activeStop;
            if (!improves) return false;
            activeStop = candidate;
            activeReason = candidateReason;
            return true;
        }

        private void UpdateExcursions(HistoricalBar bar, RangeFilterResearchDirection direction, decimal entryPrice,
            ref decimal mfe, ref decimal mae)
        {
            var tick = config.IndicatorConfig.TickSize;
            var favorable = direction == RangeFilterResearchDirection.Long
                ? (bar.High - entryPrice) / tick : (entryPrice - bar.Low) / tick;
            var adverse = direction == RangeFilterResearchDirection.Long
                ? (entryPrice - bar.Low) / tick : (bar.High - entryPrice) / tick;
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

        private IReadOnlyList<VectorStatePoint> BuildVectorStates(IReadOnlyList<AggregatedBar> bars)
        {
            var indicator = config.IndicatorConfig;
            var result = new List<VectorStatePoint>();
            var smaClose = new RollingAverage(indicator.FtcLength);
            var atrFast = new RmaAtrState(indicator.FtcAtrLength);
            var atrHighest = new RollingValues(indicator.FtcAtrHighestLookback);
            var atrSlow = new RmaAtrState(indicator.VidyaAtrLength);
            var vidyaSmooth = new RollingAverage(indicator.VidyaSmoothingLength);
            var momentum = new RollingValues(indicator.VidyaMomentum);
            decimal? previousClose = null;
            decimal? previousFtcUpper = null;
            decimal? previousFtcLower = null;
            decimal? previousVidUpper = null;
            decimal? previousVidLower = null;
            decimal? vidya = null;
            var ftcTrend = false;
            var isVidUp = false;
            var alpha = 2m / (indicator.VidyaLength + 1m);

            foreach (var bar in bars)
            {
                var sma = smaClose.Update(bar.Close);
                var fastAtr = atrFast.Update(bar.High, bar.Low, bar.Close);
                atrHighest.Add(fastAtr);
                var ftcUpper = sma + atrHighest.Max;
                var ftcLower = sma - atrHighest.Max;
                if (previousClose.HasValue && previousFtcUpper.HasValue && previousFtcLower.HasValue)
                {
                    if (bar.Close > ftcUpper && previousClose.Value <= previousFtcUpper.Value) ftcTrend = true;
                    if (bar.Close < ftcLower && previousClose.Value >= previousFtcLower.Value) ftcTrend = false;
                }

                var change = previousClose.HasValue ? bar.Close - previousClose.Value : 0m;
                momentum.Add(change);
                var positive = momentum.Values.Where(x => x >= 0m).Sum();
                var negative = momentum.Values.Where(x => x < 0m).Sum(x => -x);
                var cmo = positive + negative > 0m
                    ? Math.Abs(100m * (positive - negative) / (positive + negative)) : 0m;
                vidya = !vidya.HasValue ? bar.Close
                    : alpha * cmo / 100m * bar.Close + (1m - alpha * cmo / 100m) * vidya.Value;
                var smoothedVidya = vidyaSmooth.Update(vidya.Value);
                var slowAtr = atrSlow.Update(bar.High, bar.Low, bar.Close);
                var vidUpper = smoothedVidya + indicator.VidyaBandDistance * slowAtr;
                var vidLower = smoothedVidya - indicator.VidyaBandDistance * slowAtr;
                if (previousClose.HasValue && previousVidUpper.HasValue && previousVidLower.HasValue)
                {
                    if (bar.Close > vidUpper && previousClose.Value <= previousVidUpper.Value) isVidUp = true;
                    else if (bar.Close < vidLower && previousClose.Value >= previousVidLower.Value) isVidUp = false;
                }

                var bias = ftcTrend && isVidUp ? VectorFlowResearchBias.Bullish
                    : !ftcTrend && !isVidUp ? VectorFlowResearchBias.Bearish
                    : VectorFlowResearchBias.Neutral;
                result.Add(new VectorStatePoint(bar, bias));
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
                .GroupBy(x => new DateTime(x.Local.Year, x.Local.Month, x.Local.Day, x.Local.Hour,
                    x.Local.Minute - x.Local.Minute % minutes, 0)).OrderBy(x => x.Key);
            var result = new List<AggregatedBar>();
            foreach (var group in grouped)
            {
                var ordered = group.OrderBy(x => x.Bar.TimestampUtc).ToList();
                if (ordered.Count != minutes) continue;
                result.Add(new AggregatedBar(group.Key, ordered[ordered.Count - 1].Bar.TimestampUtc,
                    ordered.Max(x => x.Bar.High), ordered.Min(x => x.Bar.Low), ordered[ordered.Count - 1].Bar.Close));
            }
            return result;
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

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }

        private sealed class AggregatedBar
        {
            public AggregatedBar(DateTime localStart, DateTimeOffset endUtc, decimal high, decimal low, decimal close)
            { LocalStart = localStart; EndUtc = endUtc; High = high; Low = low; Close = close; }
            public DateTime LocalStart { get; }
            public DateTimeOffset EndUtc { get; }
            public decimal High { get; }
            public decimal Low { get; }
            public decimal Close { get; }
        }

        private sealed class VectorStatePoint
        {
            public VectorStatePoint(AggregatedBar bar, VectorFlowResearchBias bias) { Bar = bar; Bias = bias; }
            public AggregatedBar Bar { get; }
            public VectorFlowResearchBias Bias { get; }
        }

        private sealed class RollingAverage
        {
            private readonly int length;
            private readonly Queue<decimal> values = new Queue<decimal>();
            private decimal sum;
            public RollingAverage(int length) { this.length = length; }
            public decimal Update(decimal input)
            {
                values.Enqueue(input);
                sum += input;
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
