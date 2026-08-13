using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningProtectedPositionMode
    {
        Scalp = 0,
        Core = 1,
        Runner = 2
    }

    public enum MorningProtectedPositionExitReason
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

    public sealed class MorningProtectedPositionConfig
    {
        public MorningProtectedPositionConfig(
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
            int nonAlignedBreakevenTriggerTicks = 100,
            int extensionProfitFloorTicks = 100,
            decimal coreRetentionFraction = 0.40m,
            int runnerThresholdTicks = 300,
            int runnerAlignedBars = 2,
            int runnerTrailTicks = 250,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2m,
            int contracts = 2)
        {
            if (vectorTimeframeMinutes < 1) throw new ArgumentOutOfRangeException(nameof(vectorTimeframeMinutes));
            if (ftcLength < 2 || ftcAtrLength < 2 || ftcAtrHighestLookback < 2) throw new ArgumentOutOfRangeException(nameof(ftcLength));
            if (vidyaLength < 2 || vidyaMomentum < 2 || vidyaSmoothingLength < 1 || vidyaAtrLength < 2 || vidyaBandDistance <= 0m)
                throw new ArgumentOutOfRangeException(nameof(vidyaLength));
            if (scalpTargetTicks < 1 || scalpTimeoutMinutes < 1) throw new ArgumentOutOfRangeException(nameof(scalpTargetTicks));
            if (nonAlignedBreakevenTriggerTicks < 1 || nonAlignedBreakevenTriggerTicks >= scalpTargetTicks)
                throw new ArgumentOutOfRangeException(nameof(nonAlignedBreakevenTriggerTicks));
            if (extensionProfitFloorTicks < 0 || extensionProfitFloorTicks > scalpTargetTicks)
                throw new ArgumentOutOfRangeException(nameof(extensionProfitFloorTicks));
            if (coreRetentionFraction <= 0m || coreRetentionFraction >= 1m)
                throw new ArgumentOutOfRangeException(nameof(coreRetentionFraction));
            if (runnerThresholdTicks <= scalpTargetTicks || runnerAlignedBars < 1 || runnerTrailTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(runnerThresholdTicks));
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
            NonAlignedBreakevenTriggerTicks = nonAlignedBreakevenTriggerTicks;
            ExtensionProfitFloorTicks = extensionProfitFloorTicks;
            CoreRetentionFraction = coreRetentionFraction;
            RunnerThresholdTicks = runnerThresholdTicks;
            RunnerAlignedBars = runnerAlignedBars;
            RunnerTrailTicks = runnerTrailTicks;
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
        public int NonAlignedBreakevenTriggerTicks { get; }
        public int ExtensionProfitFloorTicks { get; }
        public decimal CoreRetentionFraction { get; }
        public int RunnerThresholdTicks { get; }
        public int RunnerAlignedBars { get; }
        public int RunnerTrailTicks { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal DollarsPerTick => TickSize * PointValuePerContract * Contracts;
    }

    public sealed class MorningProtectedManagedTrade
    {
        public MorningProtectedManagedTrade(
            MorningDailySequencingCandidate candidate,
            MorningProtectedPositionMode finalMode,
            MorningProtectedPositionExitReason exitReason,
            DateTimeOffset exitUtc,
            decimal exitPrice,
            decimal realizedTicks,
            decimal realizedDollars,
            decimal maxFavorableTicks,
            decimal maxAdverseTicks,
            bool extensionActivated,
            bool adaptiveBreakevenActivated,
            decimal bestProtectedTicks,
            int maximumAlignedFiveMinuteBars)
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
            ExtensionActivated = extensionActivated;
            AdaptiveBreakevenActivated = adaptiveBreakevenActivated;
            BestProtectedTicks = bestProtectedTicks;
            MaximumAlignedFiveMinuteBars = maximumAlignedFiveMinuteBars;
        }

        public MorningDailySequencingCandidate Candidate { get; }
        public MorningProtectedPositionMode FinalMode { get; }
        public MorningProtectedPositionExitReason ExitReason { get; }
        public DateTimeOffset ExitUtc { get; }
        public decimal ExitPrice { get; }
        public decimal RealizedTicks { get; }
        public decimal RealizedDollars { get; }
        public decimal MaxFavorableTicks { get; }
        public decimal MaxAdverseTicks { get; }
        public bool ExtensionActivated { get; }
        public bool AdaptiveBreakevenActivated { get; }
        public decimal BestProtectedTicks { get; }
        public int MaximumAlignedFiveMinuteBars { get; }
    }

    public sealed class MorningProtectedReplayResult
    {
        public MorningProtectedReplayResult(
            IReadOnlyList<MorningProtectedManagedTrade> selectedTrades,
            int rejectedPositionOpen,
            int rejectedAttemptLimit,
            int rejectedEntryQuality,
            int rejectedPotential)
        {
            SelectedTrades = selectedTrades ?? throw new ArgumentNullException(nameof(selectedTrades));
            RejectedPositionOpen = rejectedPositionOpen;
            RejectedAttemptLimit = rejectedAttemptLimit;
            RejectedEntryQuality = rejectedEntryQuality;
            RejectedPotential = rejectedPotential;
        }

        public IReadOnlyList<MorningProtectedManagedTrade> SelectedTrades { get; }
        public int RejectedPositionOpen { get; }
        public int RejectedAttemptLimit { get; }
        public int RejectedEntryQuality { get; }
        public int RejectedPotential { get; }
    }

    /// <summary>
    /// V7.1 post-entry manager.
    ///
    /// Entry authority remains V6.1 StrictUpper80:
    /// Entry Efficiency >= 70 and frozen V5.6 Potential >= 80.
    ///
    /// Five-minute VectorFlow has NO entry authority. Alignment alone cannot promote a trade.
    /// The trade remains Scalp until the +150 tick scalp target is actually reached.
    /// Only a target touch with completed five-minute VectorFlow alignment earns Core extension.
    ///
    /// Before extension:
    /// - non-aligned +100 MFE may tighten to breakeven;
    /// - non-aligned target touch realizes the scalp;
    /// - timeout realizes the scalp.
    ///
    /// After extension:
    /// - at least +100 ticks are protected;
    /// - protection retains 40% of peak MFE;
    /// - Runner requires >=300 MFE and >=2 consecutive aligned completed five-minute states;
    /// - Runner protection is max(core floor, MFE - 250 ticks);
    /// - completed five-minute bias loss exits the extension.
    ///
    /// Same-bar ambiguity is conservative: an active stop is checked before target/extension logic.
    /// </summary>
    public sealed class MorningProtectedPositionIntelligenceAnalyzer
    {
        private static readonly TimeSpan ResearchWindowEnd = new TimeSpan(11, 0, 0);
        private readonly MorningProtectedPositionConfig config;

        public MorningProtectedPositionIntelligenceAnalyzer(
            MorningProtectedPositionConfig? config = null)
        {
            this.config = config ?? new MorningProtectedPositionConfig();
        }

        public MorningProtectedManagedTrade? Manage(
            IReadOnlyList<HistoricalBar> oneMinuteBars,
            MorningDailySequencingCandidate candidate)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));

            var central = ResolveCentralTimeZone();
            var ordered = oneMinuteBars.OrderBy(x => x.TimestampUtc).ToList();
            var vectors = BuildVectorStates(Aggregate(ordered, config.VectorTimeframeMinutes, central));

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
                return null;

            return ManagePath(candidate, path, vectors);
        }

        public MorningProtectedReplayResult ReplayFrozenStrict(
            IReadOnlyList<HistoricalBar> oneMinuteBars,
            IReadOnlyList<MorningDailySequencingCandidate> candidates,
            int maximumAttempts = 2,
            decimal highEntryMinimum = 70m,
            decimal upperPotentialMinimum = 80m)
        {
            if (oneMinuteBars == null) throw new ArgumentNullException(nameof(oneMinuteBars));
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

            var selected = new List<MorningProtectedManagedTrade>();
            var positionOpenRejects = 0;
            var attemptRejects = 0;
            var entryRejects = 0;
            var potentialRejects = 0;

            foreach (var day in candidates
                .OrderBy(x => x.EntryUtc)
                .GroupBy(x => x.SessionDateCentral)
                .OrderBy(x => x.Key))
            {
                var attempts = 0;
                DateTimeOffset? openUntil = null;

                foreach (var candidate in day.OrderBy(x => x.EntryUtc))
                {
                    if (openUntil.HasValue && candidate.EntryUtc < openUntil.Value)
                    {
                        positionOpenRejects++;
                        continue;
                    }

                    if (attempts >= maximumAttempts)
                    {
                        attemptRejects++;
                        continue;
                    }

                    if (candidate.EntryEfficiencyScore < highEntryMinimum)
                    {
                        entryRejects++;
                        continue;
                    }

                    if (candidate.PotentialScore < upperPotentialMinimum)
                    {
                        potentialRejects++;
                        continue;
                    }

                    var managed = Manage(oneMinuteBars, candidate);
                    if (managed == null)
                        continue;

                    attempts++;
                    selected.Add(managed);
                    openUntil = managed.ExitUtc;
                }
            }

            return new MorningProtectedReplayResult(
                selected,
                positionOpenRejects,
                attemptRejects,
                entryRejects,
                potentialRejects);
        }

        private MorningProtectedManagedTrade ManagePath(
            MorningDailySequencingCandidate candidate,
            IReadOnlyList<HistoricalBar> path,
            IReadOnlyList<VectorStatePoint> vectors)
        {
            var source = candidate.Entry.Source.Source;
            var direction = source.Direction;
            var entryPrice = source.EntryPrice;
            var targetPrice = direction == NewYorkResearchDirection.Long
                ? entryPrice + config.ScalpTargetTicks * config.TickSize
                : entryPrice - config.ScalpTargetTicks * config.TickSize;

            var deadline = source.EntryUtc.AddMinutes(config.ScalpTimeoutMinutes);
            var activeStop = source.StopPrice;
            var activeStopReason = MorningProtectedPositionExitReason.StructuralStop;

            var mode = MorningProtectedPositionMode.Scalp;
            var extension = false;
            var adaptiveBreakeven = false;
            var alignedBars = 0;
            var maximumAlignedBars = 0;
            DateTimeOffset? lastVectorEnd = null;

            decimal mfe = 0m;
            decimal mae = 0m;
            decimal bestProtectedTicks = 0m;

            foreach (var bar in path)
            {
                var latest = LatestVectorStateBefore(vectors, bar.TimestampUtc);
                var aligned = latest != null && IsAligned(direction, latest.Bias);

                if (latest != null && (!lastVectorEnd.HasValue || latest.EndUtc > lastVectorEnd.Value))
                {
                    lastVectorEnd = latest.EndUtc;

                    if (aligned)
                    {
                        alignedBars++;
                        maximumAlignedBars = Math.Max(maximumAlignedBars, alignedBars);
                    }
                    else
                    {
                        alignedBars = 0;

                        if (extension)
                        {
                            return Build(
                                candidate,
                                mode,
                                MorningProtectedPositionExitReason.VectorFlowBiasLoss,
                                bar.TimestampUtc,
                                bar.Open,
                                mfe,
                                mae,
                                extension,
                                adaptiveBreakeven,
                                bestProtectedTicks,
                                maximumAlignedBars);
                        }
                    }
                }

                if (TouchesStop(bar, direction, activeStop))
                {
                    return Build(
                        candidate,
                        mode,
                        activeStopReason,
                        bar.TimestampUtc,
                        activeStop,
                        mfe,
                        mae,
                        extension,
                        adaptiveBreakeven,
                        bestProtectedTicks,
                        maximumAlignedBars);
                }

                UpdateExcursions(bar, direction, entryPrice, ref mfe, ref mae);

                if (!extension)
                {
                    if (!aligned && mfe >= config.NonAlignedBreakevenTriggerTicks)
                    {
                        adaptiveBreakeven = true;
                        TightenStop(
                            direction,
                            entryPrice,
                            MorningProtectedPositionExitReason.AdaptiveBreakeven,
                            ref activeStop,
                            ref activeStopReason);
                    }

                    if (TouchesTarget(bar, direction, targetPrice))
                    {
                        if (!aligned)
                        {
                            return Build(
                                candidate,
                                MorningProtectedPositionMode.Scalp,
                                MorningProtectedPositionExitReason.ScalpCapture,
                                bar.TimestampUtc,
                                targetPrice,
                                mfe,
                                mae,
                                false,
                                adaptiveBreakeven,
                                bestProtectedTicks,
                                maximumAlignedBars);
                        }

                        extension = true;
                        mode = MorningProtectedPositionMode.Core;
                        bestProtectedTicks = Math.Max(
                            bestProtectedTicks,
                            config.ExtensionProfitFloorTicks);

                        TightenStop(
                            direction,
                            PriceAtTicks(direction, entryPrice, config.ExtensionProfitFloorTicks),
                            MorningProtectedPositionExitReason.ExtensionFloor,
                            ref activeStop,
                            ref activeStopReason);
                    }
                    else if (bar.TimestampUtc >= deadline)
                    {
                        return Build(
                            candidate,
                            MorningProtectedPositionMode.Scalp,
                            MorningProtectedPositionExitReason.ScalpTimeout,
                            bar.TimestampUtc,
                            bar.Close,
                            mfe,
                            mae,
                            false,
                            adaptiveBreakeven,
                            bestProtectedTicks,
                            maximumAlignedBars);
                    }
                }

                if (extension)
                {
                    if (mfe >= config.RunnerThresholdTicks
                        && alignedBars >= config.RunnerAlignedBars)
                    {
                        mode = MorningProtectedPositionMode.Runner;
                    }

                    var coreFloorTicks = Math.Max(
                        (decimal)config.ExtensionProfitFloorTicks,
                        Math.Floor(mfe * config.CoreRetentionFraction));

                    var protectedTicks = coreFloorTicks;
                    var reason = MorningProtectedPositionExitReason.ExtensionFloor;

                    if (mode == MorningProtectedPositionMode.Runner)
                    {
                        protectedTicks = Math.Max(
                            coreFloorTicks,
                            mfe - config.RunnerTrailTicks);
                        reason = MorningProtectedPositionExitReason.RunnerTrail;
                    }

                    if (protectedTicks > bestProtectedTicks)
                    {
                        bestProtectedTicks = protectedTicks;
                        TightenStop(
                            direction,
                            PriceAtTicks(direction, entryPrice, protectedTicks),
                            reason,
                            ref activeStop,
                            ref activeStopReason);
                    }
                }
            }

            var last = path[path.Count - 1];
            return Build(
                candidate,
                mode,
                MorningProtectedPositionExitReason.ResearchWindowEnd,
                last.TimestampUtc,
                last.Close,
                mfe,
                mae,
                extension,
                adaptiveBreakeven,
                bestProtectedTicks,
                maximumAlignedBars);
        }

        private MorningProtectedManagedTrade Build(
            MorningDailySequencingCandidate candidate,
            MorningProtectedPositionMode mode,
            MorningProtectedPositionExitReason reason,
            DateTimeOffset exitUtc,
            decimal exitPrice,
            decimal mfe,
            decimal mae,
            bool extension,
            bool adaptiveBreakeven,
            decimal bestProtectedTicks,
            int maximumAlignedBars)
        {
            var source = candidate.Entry.Source.Source;
            var ticks = source.Direction == NewYorkResearchDirection.Long
                ? (exitPrice - source.EntryPrice) / config.TickSize
                : (source.EntryPrice - exitPrice) / config.TickSize;

            return new MorningProtectedManagedTrade(
                candidate,
                mode,
                reason,
                exitUtc,
                exitPrice,
                ticks,
                ticks * config.DollarsPerTick,
                mfe,
                mae,
                extension,
                adaptiveBreakeven,
                bestProtectedTicks,
                maximumAlignedBars);
        }

        private decimal PriceAtTicks(
            NewYorkResearchDirection direction,
            decimal entryPrice,
            decimal ticks)
        {
            return direction == NewYorkResearchDirection.Long
                ? entryPrice + ticks * config.TickSize
                : entryPrice - ticks * config.TickSize;
        }

        private static void TightenStop(
            NewYorkResearchDirection direction,
            decimal proposedStop,
            MorningProtectedPositionExitReason proposedReason,
            ref decimal activeStop,
            ref MorningProtectedPositionExitReason activeReason)
        {
            if (direction == NewYorkResearchDirection.Long)
            {
                if (proposedStop > activeStop)
                {
                    activeStop = proposedStop;
                    activeReason = proposedReason;
                }
            }
            else if (proposedStop < activeStop)
            {
                activeStop = proposedStop;
                activeReason = proposedReason;
            }
        }

        private IReadOnlyList<VectorStatePoint> BuildVectorStates(
            IReadOnlyList<AggregatedBar> bars)
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

                if (previousClose.HasValue
                    && previousFtcUpper.HasValue
                    && previousFtcLower.HasValue)
                {
                    if (bar.Close > ftcUpper
                        && previousClose.Value <= previousFtcUpper.Value)
                        ftcTrend = true;

                    if (bar.Close < ftcLower
                        && previousClose.Value >= previousFtcLower.Value)
                        ftcTrend = false;
                }

                var change = previousClose.HasValue
                    ? bar.Close - previousClose.Value
                    : 0m;

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

                if (previousClose.HasValue
                    && previousVidUpper.HasValue
                    && previousVidLower.HasValue)
                {
                    if (bar.Close > vidUpper
                        && previousClose.Value <= previousVidUpper.Value)
                        vidyaUp = true;
                    else if (bar.Close < vidLower
                        && previousClose.Value >= previousVidLower.Value)
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
            var grouped = bars
                .Select(x => new
                {
                    Bar = x,
                    Local = TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime
                })
                .GroupBy(x => new DateTime(
                    x.Local.Year,
                    x.Local.Month,
                    x.Local.Day,
                    x.Local.Hour,
                    x.Local.Minute - x.Local.Minute % minutes,
                    0))
                .OrderBy(x => x.Key);

            var result = new List<AggregatedBar>();

            foreach (var group in grouped)
            {
                var ordered = group.OrderBy(x => x.Bar.TimestampUtc).ToList();
                if (ordered.Count != minutes)
                    continue;

                result.Add(new AggregatedBar(
                    ordered[ordered.Count - 1].Bar.TimestampUtc,
                    ordered[0].Bar.Open,
                    ordered.Max(x => x.Bar.High),
                    ordered.Min(x => x.Bar.Low),
                    ordered[ordered.Count - 1].Bar.Close));
            }

            return result;
        }

        private static VectorStatePoint? LatestVectorStateBefore(
            IReadOnlyList<VectorStatePoint> states,
            DateTimeOffset utc)
        {
            VectorStatePoint? latest = null;

            for (var i = 0; i < states.Count; i++)
            {
                if (states[i].EndUtc >= utc)
                    break;

                latest = states[i];
            }

            return latest;
        }

        private static bool IsAligned(
            NewYorkResearchDirection direction,
            VectorFlowResearchBias bias)
        {
            return direction == NewYorkResearchDirection.Long
                    && bias == VectorFlowResearchBias.Bullish
                || direction == NewYorkResearchDirection.Short
                    && bias == VectorFlowResearchBias.Bearish;
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
            public AggregatedBar(
                DateTimeOffset endUtc,
                decimal open,
                decimal high,
                decimal low,
                decimal close)
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
            public VectorStatePoint(
                DateTimeOffset endUtc,
                VectorFlowResearchBias bias)
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

            public decimal Update(
                decimal high,
                decimal low,
                decimal close)
            {
                var trueRange = previousClose.HasValue
                    ? Math.Max(
                        high - low,
                        Math.Max(
                            Math.Abs(high - previousClose.Value),
                            Math.Abs(low - previousClose.Value)))
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
