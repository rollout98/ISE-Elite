using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum EfficientAdaptiveEntryDisposition
    {
        Immediate = 0,
        Deferred = 1,
        RejectedStructure = 2,
        RejectedNoEfficientEntry = 3
    }

    public enum EfficientAdaptiveExitReason
    {
        None = 0,
        StructuralStop = 1,
        ScalpCapture = 2,
        ScalpTimeout = 3,
        AdaptiveBreakeven = 4,
        ExtensionFloor = 5,
        RunnerTrail = 6,
        VectorFlowBiasLoss = 7,
        ResearchWindowEnd = 8
    }

    public sealed class EfficientAdaptiveRangeVectorConfig
    {
        public EfficientAdaptiveRangeVectorConfig(
            MorningResearchAccountStage stage,
            RangeEntryVectorFlowHoldConfig? indicatorConfig = null,
            decimal maximumStructuralRiskTicks = 0m,
            int maximumDeferralMinutes = 20,
            int nonAlignedBreakevenTriggerTicks = 100,
            int extensionProfitFloorTicks = 100,
            decimal coreRetentionFraction = 0.40m,
            int runnerThresholdTicks = 300,
            int runnerAlignedBars = 2,
            int runnerTrailTicks = 250)
        {
            Stage = stage;
            IndicatorConfig = indicatorConfig ?? new RangeEntryVectorFlowHoldConfig();
            MaximumStructuralRiskTicks = maximumStructuralRiskTicks > 0m
                ? maximumStructuralRiskTicks
                : stage == MorningResearchAccountStage.Combine ? 325m : 250m;
            if (MaximumStructuralRiskTicks <= 0m) throw new ArgumentOutOfRangeException(nameof(maximumStructuralRiskTicks));
            if (maximumDeferralMinutes < 1) throw new ArgumentOutOfRangeException(nameof(maximumDeferralMinutes));
            if (nonAlignedBreakevenTriggerTicks < 1 || nonAlignedBreakevenTriggerTicks >= IndicatorConfig.ScalpTargetTicks)
                throw new ArgumentOutOfRangeException(nameof(nonAlignedBreakevenTriggerTicks));
            if (extensionProfitFloorTicks < 0 || extensionProfitFloorTicks > IndicatorConfig.ScalpTargetTicks)
                throw new ArgumentOutOfRangeException(nameof(extensionProfitFloorTicks));
            if (coreRetentionFraction <= 0m || coreRetentionFraction >= 1m)
                throw new ArgumentOutOfRangeException(nameof(coreRetentionFraction));
            if (runnerThresholdTicks <= IndicatorConfig.ScalpTargetTicks || runnerAlignedBars < 1 || runnerTrailTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(runnerThresholdTicks));

            MaximumDeferralMinutes = maximumDeferralMinutes;
            NonAlignedBreakevenTriggerTicks = nonAlignedBreakevenTriggerTicks;
            ExtensionProfitFloorTicks = extensionProfitFloorTicks;
            CoreRetentionFraction = coreRetentionFraction;
            RunnerThresholdTicks = runnerThresholdTicks;
            RunnerAlignedBars = runnerAlignedBars;
            RunnerTrailTicks = runnerTrailTicks;
        }

        public static EfficientAdaptiveRangeVectorConfig CombineDefault =>
            new EfficientAdaptiveRangeVectorConfig(MorningResearchAccountStage.Combine);

        public static EfficientAdaptiveRangeVectorConfig FundedDefault =>
            new EfficientAdaptiveRangeVectorConfig(MorningResearchAccountStage.Funded);

        public MorningResearchAccountStage Stage { get; }
        public RangeEntryVectorFlowHoldConfig IndicatorConfig { get; }
        public decimal MaximumStructuralRiskTicks { get; }
        public int MaximumDeferralMinutes { get; }
        public int NonAlignedBreakevenTriggerTicks { get; }
        public int ExtensionProfitFloorTicks { get; }
        public decimal CoreRetentionFraction { get; }
        public int RunnerThresholdTicks { get; }
        public int RunnerAlignedBars { get; }
        public int RunnerTrailTicks { get; }
    }

    public sealed class EfficientAdaptiveManagedOutcome
    {
        public EfficientAdaptiveManagedOutcome(RangeVectorManagementMode finalMode, EfficientAdaptiveExitReason exitReason,
            DateTimeOffset exitUtc, decimal exitPrice, decimal realizedTicks, decimal realizedDollars,
            decimal maxFavorableTicks, decimal maxAdverseTicks, bool extensionActivated,
            bool adaptiveBreakevenActivated, decimal bestProtectedTicks)
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
            AdaptiveBreakevenActivated = adaptiveBreakevenActivated;
            BestProtectedTicks = bestProtectedTicks;
        }

        public RangeVectorManagementMode FinalMode { get; }
        public EfficientAdaptiveExitReason ExitReason { get; }
        public DateTimeOffset ExitUtc { get; }
        public decimal ExitPrice { get; }
        public decimal RealizedTicks { get; }
        public decimal RealizedDollars { get; }
        public decimal MaxFavorableTicks { get; }
        public decimal MaxAdverseTicks { get; }
        public bool ExtensionActivated { get; }
        public bool AdaptiveBreakevenActivated { get; }
        public decimal BestProtectedTicks { get; }
    }

    public sealed class EfficientAdaptiveRangeVectorOutcome
    {
        public EfficientAdaptiveRangeVectorOutcome(RangeEntryVectorFlowComparison source,
            MorningResearchAccountStage stage, EfficientAdaptiveEntryDisposition disposition,
            string reason, DateTimeOffset? entryUtc, decimal? entryPrice, decimal? initialRiskTicks,
            int deferralMinutes, VectorFlowResearchBias vectorBiasAtEntry,
            EfficientAdaptiveManagedOutcome? managedOutcome)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Stage = stage;
            Disposition = disposition;
            Reason = reason ?? string.Empty;
            EntryUtc = entryUtc;
            EntryPrice = entryPrice;
            InitialRiskTicks = initialRiskTicks;
            DeferralMinutes = deferralMinutes;
            VectorBiasAtEntry = vectorBiasAtEntry;
            ManagedOutcome = managedOutcome;
        }

        public RangeEntryVectorFlowComparison Source { get; }
        public MorningResearchAccountStage Stage { get; }
        public EfficientAdaptiveEntryDisposition Disposition { get; }
        public string Reason { get; }
        public DateTimeOffset? EntryUtc { get; }
        public decimal? EntryPrice { get; }
        public decimal? InitialRiskTicks { get; }
        public int DeferralMinutes { get; }
        public VectorFlowResearchBias VectorBiasAtEntry { get; }
        public EfficientAdaptiveManagedOutcome? ManagedOutcome { get; }
        public bool Selected => ManagedOutcome != null;
        public bool Deferred => Disposition == EfficientAdaptiveEntryDisposition.Deferred;
        public bool ConvertedFromOverRisk => Selected && Source.InitialRiskTicks > (Stage == MorningResearchAccountStage.Combine ? 325m : 250m);
    }

    /// <summary>
    /// Research-only v3 layer that preserves the user's indicator separation of duties while adding ISE intelligence
    /// around them. The confirmed 3-minute Range Filter flip creates an opportunity, not an unconditional fill. If the
    /// next-bar entry is too far from the signal's structural stop, the analyzer may wait briefly for a causal pullback
    /// toward that same stop while the Range Filter opportunity remains valid. It never widens or relocates the stop.
    /// Five-minute VectorFlow still has hold authority only. When VectorFlow is not aligned, +100 ticks may arm a
    /// next-bar breakeven stop; when it is aligned, that blanket breakeven is deferred so a legitimate continuation is
    /// not prematurely killed. Extension is earned only after the normal scalp target is reached with completed 5-minute
    /// alignment. Core/runner protection then tightens from prior-bar information only. Parameters are research seeds,
    /// not production settings, and future MFE/P&L never participates in entry selection.
    /// </summary>
    public sealed class EfficientAdaptiveRangeVectorAnalyzer
    {
        private static readonly TimeSpan ResearchWindowEnd = new TimeSpan(11, 0, 0);
        private static readonly TimeSpan LatestEntryTime = new TimeSpan(10, 30, 0);
        private readonly EfficientAdaptiveRangeVectorConfig config;

        public EfficientAdaptiveRangeVectorAnalyzer(EfficientAdaptiveRangeVectorConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IReadOnlyList<EfficientAdaptiveRangeVectorOutcome> Analyze(IReadOnlyList<HistoricalBar> oneMinuteBars)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (oneMinuteBars.Count == 0) return Array.Empty<EfficientAdaptiveRangeVectorOutcome>();

            var central = ResolveCentralTimeZone();
            var ordered = oneMinuteBars.OrderBy(x => x.TimestampUtc).ToList();
            var raw = new RangeEntryVectorFlowHoldAnalyzer(config.IndicatorConfig).Analyze(ordered).ToList();
            var vectorStates = BuildVectorStates(Aggregate(ordered, config.IndicatorConfig.VectorTimeframeMinutes, central));
            var result = new List<EfficientAdaptiveRangeVectorOutcome>();

            for (var i = 0; i < raw.Count; i++)
            {
                var source = raw[i];
                DateTimeOffset? nextSignalUtc = null;
                if (i + 1 < raw.Count && raw[i + 1].SessionDateCentral == source.SessionDateCentral)
                    nextSignalUtc = raw[i + 1].SignalUtc;

                DateTimeOffset entryUtc;
                decimal entryPrice;
                decimal riskTicks;
                int deferralMinutes;
                EfficientAdaptiveEntryDisposition disposition;
                string reason;
                if (!TryFindEfficientEntry(ordered, source, nextSignalUtc, central,
                    out entryUtc, out entryPrice, out riskTicks, out deferralMinutes, out disposition, out reason))
                {
                    result.Add(new EfficientAdaptiveRangeVectorOutcome(source, config.Stage, disposition, reason,
                        null, null, null, deferralMinutes, VectorFlowResearchBias.Neutral, null));
                    continue;
                }

                var local = TimeZoneInfo.ConvertTime(entryUtc, central).DateTime;
                var path = ordered.Where(x => x.TimestampUtc >= entryUtc)
                    .TakeWhile(x => TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime.Date == local.Date
                        && TimeZoneInfo.ConvertTime(x.TimestampUtc, central).TimeOfDay < ResearchWindowEnd)
                    .ToList();
                if (path.Count == 0)
                {
                    result.Add(new EfficientAdaptiveRangeVectorOutcome(source, config.Stage,
                        EfficientAdaptiveEntryDisposition.RejectedNoEfficientEntry, "NoPath", null, null, null,
                        deferralMinutes, VectorFlowResearchBias.Neutral, null));
                    continue;
                }

                var biasAtEntry = LatestBiasBefore(vectorStates, entryUtc);
                var managed = Manage(path, source.Direction, entryPrice, source.StopPrice, vectorStates);
                result.Add(new EfficientAdaptiveRangeVectorOutcome(source, config.Stage, disposition, reason,
                    entryUtc, entryPrice, riskTicks, deferralMinutes, biasAtEntry, managed));
            }

            return result;
        }

        private bool TryFindEfficientEntry(IReadOnlyList<HistoricalBar> bars, RangeEntryVectorFlowComparison source,
            DateTimeOffset? nextSignalUtc, TimeZoneInfo central, out DateTimeOffset entryUtc, out decimal entryPrice,
            out decimal riskTicks, out int deferralMinutes, out EfficientAdaptiveEntryDisposition disposition, out string reason)
        {
            entryUtc = default(DateTimeOffset);
            entryPrice = 0m;
            riskTicks = 0m;
            deferralMinutes = 0;
            disposition = EfficientAdaptiveEntryDisposition.RejectedNoEfficientEntry;
            reason = "NoEfficientEntry";

            var deadline = source.EntryUtc.AddMinutes(config.MaximumDeferralMinutes);
            if (nextSignalUtc.HasValue && nextSignalUtc.Value < deadline) deadline = nextSignalUtc.Value;

            foreach (var bar in bars.Where(x => x.TimestampUtc >= source.EntryUtc && x.TimestampUtc < deadline))
            {
                var local = TimeZoneInfo.ConvertTime(bar.TimestampUtc, central).DateTime;
                if (local.Date != source.SessionDateCentral || local.TimeOfDay >= LatestEntryTime) break;

                var directionalRisk = source.Direction == RangeFilterResearchDirection.Long
                    ? (bar.Open - source.StopPrice) / config.IndicatorConfig.TickSize
                    : (source.StopPrice - bar.Open) / config.IndicatorConfig.TickSize;
                if (directionalRisk <= 0m)
                {
                    disposition = EfficientAdaptiveEntryDisposition.RejectedStructure;
                    reason = "OpenBeyondStructure";
                    return false;
                }

                if (directionalRisk <= config.MaximumStructuralRiskTicks)
                {
                    entryUtc = bar.TimestampUtc;
                    entryPrice = bar.Open;
                    riskTicks = directionalRisk;
                    deferralMinutes = Math.Max(0, (int)Math.Round((entryUtc - source.EntryUtc).TotalMinutes));
                    disposition = deferralMinutes == 0
                        ? EfficientAdaptiveEntryDisposition.Immediate
                        : EfficientAdaptiveEntryDisposition.Deferred;
                    reason = deferralMinutes == 0 ? "ImmediateRiskQualified" : "DeferredPullbackRiskQualified";
                    return true;
                }

                if (TouchesStop(bar, source.Direction, source.StopPrice))
                {
                    disposition = EfficientAdaptiveEntryDisposition.RejectedStructure;
                    reason = "StructureInvalidatedBeforeEfficientEntry";
                    deferralMinutes = Math.Max(0, (int)Math.Round((bar.TimestampUtc - source.EntryUtc).TotalMinutes));
                    return false;
                }
            }

            deferralMinutes = config.MaximumDeferralMinutes;
            return false;
        }

        private EfficientAdaptiveManagedOutcome Manage(IReadOnlyList<HistoricalBar> path,
            RangeFilterResearchDirection direction, decimal entryPrice, decimal structuralStop,
            IReadOnlyList<VectorStatePoint> vectorStates)
        {
            var indicator = config.IndicatorConfig;
            var targetPrice = direction == RangeFilterResearchDirection.Long
                ? entryPrice + indicator.ScalpTargetTicks * indicator.TickSize
                : entryPrice - indicator.ScalpTargetTicks * indicator.TickSize;
            var deadline = path[0].TimestampUtc.AddMinutes(indicator.ScalpTimeoutMinutes);
            var activeStop = structuralStop;
            var activeStopReason = EfficientAdaptiveExitReason.StructuralStop;
            var mode = RangeVectorManagementMode.Scalp;
            var extension = false;
            var adaptiveBreakeven = false;
            var alignedBars = 0;
            DateTimeOffset? lastVectorEnd = null;
            decimal mfe = 0m;
            decimal mae = 0m;
            decimal bestProtectedTicks = 0m;

            foreach (var bar in path)
            {
                var latest = LatestVectorStateBefore(vectorStates, bar.TimestampUtc);
                var aligned = latest != null && IsAligned(direction, latest.Bias);
                if (latest != null && (!lastVectorEnd.HasValue || latest.Bar.EndUtc > lastVectorEnd.Value))
                {
                    lastVectorEnd = latest.Bar.EndUtc;
                    if (aligned) alignedBars++;
                    else
                    {
                        alignedBars = 0;
                        if (extension)
                            return BuildOutcome(mode, EfficientAdaptiveExitReason.VectorFlowBiasLoss, bar.TimestampUtc,
                                bar.Open, direction, entryPrice, mfe, mae, extension, adaptiveBreakeven, bestProtectedTicks);
                    }
                }

                if (TouchesStop(bar, direction, activeStop))
                    return BuildOutcome(mode, activeStopReason, bar.TimestampUtc, activeStop, direction, entryPrice,
                        mfe, mae, extension, adaptiveBreakeven, bestProtectedTicks);

                UpdateExcursions(bar, direction, entryPrice, ref mfe, ref mae);

                if (!extension)
                {
                    if (!aligned && mfe >= config.NonAlignedBreakevenTriggerTicks)
                    {
                        adaptiveBreakeven = true;
                        TightenStop(direction, entryPrice, EfficientAdaptiveExitReason.AdaptiveBreakeven,
                            ref activeStop, ref activeStopReason);
                    }

                    if (TouchesTarget(bar, direction, targetPrice))
                    {
                        if (!aligned)
                            return BuildOutcome(RangeVectorManagementMode.Scalp, EfficientAdaptiveExitReason.ScalpCapture,
                                bar.TimestampUtc, targetPrice, direction, entryPrice, mfe, mae, false,
                                adaptiveBreakeven, bestProtectedTicks);

                        extension = true;
                        mode = RangeVectorManagementMode.Core;
                        var floorTicks = config.ExtensionProfitFloorTicks;
                        bestProtectedTicks = Math.Max(bestProtectedTicks, floorTicks);
                        TightenStop(direction, PriceAtTicks(direction, entryPrice, floorTicks),
                            EfficientAdaptiveExitReason.ExtensionFloor, ref activeStop, ref activeStopReason);
                    }
                    else if (bar.TimestampUtc >= deadline)
                    {
                        return BuildOutcome(RangeVectorManagementMode.Scalp, EfficientAdaptiveExitReason.ScalpTimeout,
                            bar.TimestampUtc, bar.Close, direction, entryPrice, mfe, mae, false,
                            adaptiveBreakeven, bestProtectedTicks);
                    }
                }

                if (extension)
                {
                    if (mfe >= config.RunnerThresholdTicks && alignedBars >= config.RunnerAlignedBars)
                        mode = RangeVectorManagementMode.Runner;

                    var coreFloorTicks = Math.Max((decimal)config.ExtensionProfitFloorTicks,
                        Math.Floor(mfe * config.CoreRetentionFraction));
                    var protectedTicks = coreFloorTicks;
                    var reason = EfficientAdaptiveExitReason.ExtensionFloor;
                    if (mode == RangeVectorManagementMode.Runner)
                    {
                        protectedTicks = Math.Max(coreFloorTicks, mfe - config.RunnerTrailTicks);
                        reason = EfficientAdaptiveExitReason.RunnerTrail;
                    }
                    if (protectedTicks > bestProtectedTicks)
                    {
                        bestProtectedTicks = protectedTicks;
                        TightenStop(direction, PriceAtTicks(direction, entryPrice, protectedTicks), reason,
                            ref activeStop, ref activeStopReason);
                    }
                }
            }

            var last = path[path.Count - 1];
            return BuildOutcome(mode, EfficientAdaptiveExitReason.ResearchWindowEnd, last.TimestampUtc, last.Close,
                direction, entryPrice, mfe, mae, extension, adaptiveBreakeven, bestProtectedTicks);
        }

        private EfficientAdaptiveManagedOutcome BuildOutcome(RangeVectorManagementMode mode, EfficientAdaptiveExitReason reason,
            DateTimeOffset exitUtc, decimal exitPrice, RangeFilterResearchDirection direction, decimal entryPrice,
            decimal mfe, decimal mae, bool extension, bool adaptiveBreakeven, decimal bestProtectedTicks)
        {
            var ticks = direction == RangeFilterResearchDirection.Long
                ? (exitPrice - entryPrice) / config.IndicatorConfig.TickSize
                : (entryPrice - exitPrice) / config.IndicatorConfig.TickSize;
            return new EfficientAdaptiveManagedOutcome(mode, reason, exitUtc, exitPrice, ticks,
                ticks * config.IndicatorConfig.DollarsPerTick, mfe, mae, extension, adaptiveBreakeven, bestProtectedTicks);
        }

        private decimal PriceAtTicks(RangeFilterResearchDirection direction, decimal entryPrice, decimal ticks)
        {
            return direction == RangeFilterResearchDirection.Long
                ? entryPrice + ticks * config.IndicatorConfig.TickSize
                : entryPrice - ticks * config.IndicatorConfig.TickSize;
        }

        private static void TightenStop(RangeFilterResearchDirection direction, decimal proposedStop,
            EfficientAdaptiveExitReason proposedReason, ref decimal activeStop, ref EfficientAdaptiveExitReason activeReason)
        {
            if (direction == RangeFilterResearchDirection.Long)
            {
                if (proposedStop > activeStop) { activeStop = proposedStop; activeReason = proposedReason; }
            }
            else if (proposedStop < activeStop)
            {
                activeStop = proposedStop;
                activeReason = proposedReason;
            }
        }

        private IReadOnlyList<VectorStatePoint> BuildVectorStates(IReadOnlyList<AggregatedBar> bars)
        {
            var result = new List<VectorStatePoint>();
            var indicator = config.IndicatorConfig;
            var smaClose = new RollingAverage(indicator.FtcLength);
            var atr14 = new RmaAtrState(indicator.FtcAtrLength);
            var atrHighest = new RollingValues(indicator.FtcAtrHighestLookback);
            var atr200 = new RmaAtrState(indicator.VidyaAtrLength);
            var vidyaSmooth = new RollingAverage(indicator.VidyaSmoothingLength);
            var momentum = new RollingValues(indicator.VidyaMomentum);
            decimal? previousClose = null, previousFtcUpper = null, previousFtcLower = null;
            decimal? previousVidUpper = null, previousVidLower = null;
            decimal? vidya = null;
            var ftcTrend = false;
            var isVidUp = false;
            var alpha = 2m / (indicator.VidyaLength + 1m);

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
                .GroupBy(x => new DateTime(x.Local.Year, x.Local.Month, x.Local.Day, x.Local.Hour, x.Local.Minute - x.Local.Minute % minutes, 0))
                .OrderBy(x => x.Key);
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

        private void UpdateExcursions(HistoricalBar bar, RangeFilterResearchDirection direction, decimal entryPrice,
            ref decimal mfe, ref decimal mae)
        {
            var favorable = direction == RangeFilterResearchDirection.Long
                ? (bar.High - entryPrice) / config.IndicatorConfig.TickSize
                : (entryPrice - bar.Low) / config.IndicatorConfig.TickSize;
            var adverse = direction == RangeFilterResearchDirection.Long
                ? (entryPrice - bar.Low) / config.IndicatorConfig.TickSize
                : (bar.High - entryPrice) / config.IndicatorConfig.TickSize;
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
