using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningMarketState
    {
        Neutral = 0,
        Range = 1,
        DevelopingTrend = 2,
        Trending = 3,
        Compressing = 4,
        Resuming = 5,
        Exhausting = 6,
        Reversing = 7
    }

    public enum MorningAdaptiveSetupType
    {
        None = 0,
        PullbackRetest = 1,
        CompressionResumption = 2,
        BreakoutAcceptance = 3,
        FailedBreakoutReversal = 4,
        TrendContinuation = 5,
        RangeResolution = 6
    }

    public enum MorningAdaptiveManagementMode
    {
        Scalp = 0,
        Core = 1,
        Runner = 2
    }

    public enum MorningAdaptiveExitReason
    {
        None = 0,
        StructuralStop = 1,
        ScalpCapture = 2,
        CoreCapture = 3,
        RunnerProtection = 4,
        StructuralDeterioration = 5,
        ResearchWindowEnd = 6
    }

    public sealed class MorningMarketStateAdaptiveConfig
    {
        public MorningMarketStateAdaptiveConfig(
            int contextBars = 30,
            int shortBars = 8,
            int structureBars = 6,
            int cooldownMinutes = 12,
            decimal trendEfficiency = 0.32m,
            decimal strongTrendEfficiency = 0.45m,
            decimal rangeEfficiency = 0.20m,
            decimal compressionFraction = 0.42m,
            decimal exhaustionFraction = 0.70m,
            int maximumHoldMinutes = 120,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2m,
            int contracts = 2,
            decimal scalpCheckpointDollars = 150m,
            decimal coreCheckpointDollars = 300m,
            decimal runnerCheckpointDollars = 500m,
            decimal scalpGivebackDollars = 80m,
            decimal coreGivebackDollars = 120m,
            decimal runnerGivebackDollars = 180m)
        {
            if (contextBars < 20) throw new ArgumentOutOfRangeException(nameof(contextBars));
            if (shortBars < 3 || shortBars >= contextBars) throw new ArgumentOutOfRangeException(nameof(shortBars));
            if (structureBars < 2 || structureBars > shortBars) throw new ArgumentOutOfRangeException(nameof(structureBars));
            if (cooldownMinutes < 1) throw new ArgumentOutOfRangeException(nameof(cooldownMinutes));
            if (rangeEfficiency <= 0m || rangeEfficiency >= trendEfficiency) throw new ArgumentOutOfRangeException(nameof(rangeEfficiency));
            if (trendEfficiency <= 0m || strongTrendEfficiency <= trendEfficiency || strongTrendEfficiency > 1m) throw new ArgumentOutOfRangeException(nameof(trendEfficiency));
            if (compressionFraction <= 0m || compressionFraction >= 1m) throw new ArgumentOutOfRangeException(nameof(compressionFraction));
            if (exhaustionFraction <= 0m) throw new ArgumentOutOfRangeException(nameof(exhaustionFraction));
            if (maximumHoldMinutes < 1) throw new ArgumentOutOfRangeException(nameof(maximumHoldMinutes));
            if (tickSize <= 0m || pointValuePerContract <= 0m || contracts <= 0) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (scalpCheckpointDollars <= 0m || coreCheckpointDollars <= scalpCheckpointDollars || runnerCheckpointDollars <= coreCheckpointDollars)
                throw new ArgumentOutOfRangeException(nameof(scalpCheckpointDollars));
            if (scalpGivebackDollars <= 0m || coreGivebackDollars <= 0m || runnerGivebackDollars <= 0m)
                throw new ArgumentOutOfRangeException(nameof(scalpGivebackDollars));

            ContextBars = contextBars;
            ShortBars = shortBars;
            StructureBars = structureBars;
            CooldownMinutes = cooldownMinutes;
            TrendEfficiency = trendEfficiency;
            StrongTrendEfficiency = strongTrendEfficiency;
            RangeEfficiency = rangeEfficiency;
            CompressionFraction = compressionFraction;
            ExhaustionFraction = exhaustionFraction;
            MaximumHoldMinutes = maximumHoldMinutes;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
            ScalpCheckpointDollars = scalpCheckpointDollars;
            CoreCheckpointDollars = coreCheckpointDollars;
            RunnerCheckpointDollars = runnerCheckpointDollars;
            ScalpGivebackDollars = scalpGivebackDollars;
            CoreGivebackDollars = coreGivebackDollars;
            RunnerGivebackDollars = runnerGivebackDollars;
        }

        public int ContextBars { get; }
        public int ShortBars { get; }
        public int StructureBars { get; }
        public int CooldownMinutes { get; }
        public decimal TrendEfficiency { get; }
        public decimal StrongTrendEfficiency { get; }
        public decimal RangeEfficiency { get; }
        public decimal CompressionFraction { get; }
        public decimal ExhaustionFraction { get; }
        public int MaximumHoldMinutes { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal ScalpCheckpointDollars { get; }
        public decimal CoreCheckpointDollars { get; }
        public decimal RunnerCheckpointDollars { get; }
        public decimal ScalpGivebackDollars { get; }
        public decimal CoreGivebackDollars { get; }
        public decimal RunnerGivebackDollars { get; }
        public decimal DollarsPerTick => TickSize * PointValuePerContract * Contracts / TickSize;
    }

    public sealed class MorningAdaptiveTradeOutcome
    {
        public MorningAdaptiveTradeOutcome(DateTime sessionDateCentral, MorningMarketState state, MorningAdaptiveSetupType setupType,
            NewYorkResearchDirection direction, DateTimeOffset setupUtc, DateTimeOffset entryUtc, decimal entryPrice,
            decimal stopPrice, decimal initialRiskTicks, decimal contextEfficiency, decimal contextRange,
            MorningAdaptiveManagementMode finalMode, MorningAdaptiveExitReason exitReason, DateTimeOffset exitUtc,
            decimal exitPrice, decimal realizedTicks, decimal realizedDollars, decimal maxFavorableTicks, decimal maxAdverseTicks)
        {
            SessionDateCentral = sessionDateCentral.Date;
            State = state;
            SetupType = setupType;
            Direction = direction;
            SetupUtc = setupUtc;
            EntryUtc = entryUtc;
            EntryPrice = entryPrice;
            StopPrice = stopPrice;
            InitialRiskTicks = initialRiskTicks;
            ContextEfficiency = contextEfficiency;
            ContextRange = contextRange;
            FinalMode = finalMode;
            ExitReason = exitReason;
            ExitUtc = exitUtc;
            ExitPrice = exitPrice;
            RealizedTicks = realizedTicks;
            RealizedDollars = realizedDollars;
            MaxFavorableTicks = maxFavorableTicks;
            MaxAdverseTicks = maxAdverseTicks;
        }

        public DateTime SessionDateCentral { get; }
        public MorningMarketState State { get; }
        public MorningAdaptiveSetupType SetupType { get; }
        public NewYorkResearchDirection Direction { get; }
        public DateTimeOffset SetupUtc { get; }
        public DateTimeOffset EntryUtc { get; }
        public decimal EntryPrice { get; }
        public decimal StopPrice { get; }
        public decimal InitialRiskTicks { get; }
        public decimal ContextEfficiency { get; }
        public decimal ContextRange { get; }
        public MorningAdaptiveManagementMode FinalMode { get; }
        public MorningAdaptiveExitReason ExitReason { get; }
        public DateTimeOffset ExitUtc { get; }
        public decimal ExitPrice { get; }
        public decimal RealizedTicks { get; }
        public decimal RealizedDollars { get; }
        public decimal MaxFavorableTicks { get; }
        public decimal MaxAdverseTicks { get; }
        public bool Profitable => RealizedDollars > 0m;
        public bool ReachedFundedObjective => RealizedDollars >= 500m;
        public bool ReachedUpperObjective => RealizedDollars >= 1000m;
    }

    /// <summary>
    /// Research-only broad morning market-state and adaptive-management pass. The analyzer intentionally searches
    /// several causal setup families across 03:00-11:00 CT instead of prescribing a narrow clock window. Entries are
    /// next-bar-open after a completed setup bar. Structural stops are references only. Position management can end as
    /// scalp, core, or runner based on post-entry evidence; same-bar structural stop is handled conservatively first.
    /// Thresholds are transparent seed hypotheses, not production parameters.
    /// </summary>
    public sealed class MorningMarketStateAdaptiveAnalyzer
    {
        private static readonly TimeSpan WindowStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan SetupEnd = new TimeSpan(10, 30, 0);
        private static readonly TimeSpan WindowEnd = new TimeSpan(11, 0, 0);
        private readonly MorningMarketStateAdaptiveConfig config;

        public MorningMarketStateAdaptiveAnalyzer(MorningMarketStateAdaptiveConfig? config = null)
        {
            this.config = config ?? new MorningMarketStateAdaptiveConfig();
        }

        public IReadOnlyList<MorningAdaptiveTradeOutcome> Analyze(IReadOnlyList<HistoricalBar> bars)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (bars.Count == 0) return Array.Empty<MorningAdaptiveTradeOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars.Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .Where(x => x.Local.TimeOfDay >= WindowStart && x.Local.TimeOfDay < WindowEnd)
                .OrderBy(x => x.Local).ToList();
            var outcomes = new List<MorningAdaptiveTradeOutcome>();

            foreach (var group in localized.GroupBy(x => x.Local.Date).OrderBy(x => x.Key))
            {
                var session = group.OrderBy(x => x.Local).ToList();
                DateTimeOffset? lastSetupUtc = null;
                for (var i = config.ContextBars; i + 1 < session.Count; i++)
                {
                    if (session[i].Local.TimeOfDay >= SetupEnd) break;
                    if (lastSetupUtc.HasValue && (session[i].Bar.TimestampUtc - lastSetupUtc.Value).TotalMinutes < config.CooldownMinutes)
                        continue;

                    var context = session.Skip(i - config.ContextBars + 1).Take(config.ContextBars).ToList();
                    var shortWindow = session.Skip(i - config.ShortBars + 1).Take(config.ShortBars).ToList();
                    var current = session[i].Bar;
                    var prior = session[i - 1].Bar;
                    var contextHigh = context.Max(x => x.Bar.High);
                    var contextLow = context.Min(x => x.Bar.Low);
                    var contextRange = Math.Max(config.TickSize, contextHigh - contextLow);
                    var displacement = context[context.Count - 1].Bar.Close - context[0].Bar.Open;
                    var efficiency = DirectionalEfficiency(context);
                    var direction = displacement > 0m ? NewYorkResearchDirection.Long : displacement < 0m ? NewYorkResearchDirection.Short : NewYorkResearchDirection.None;
                    var state = ClassifyState(context, shortWindow, direction, efficiency, contextRange);

                    var setup = DetectSetup(session, i, state, direction, efficiency, contextRange);
                    if (setup.Type == MorningAdaptiveSetupType.None || setup.Direction == NewYorkResearchDirection.None)
                        continue;

                    var entry = session[i + 1];
                    var structureStart = Math.Max(0, i - config.StructureBars + 1);
                    var structure = session.Skip(structureStart).Take(i - structureStart + 1).ToList();
                    var stopPrice = setup.Direction == NewYorkResearchDirection.Long
                        ? structure.Min(x => x.Bar.Low) - config.TickSize
                        : structure.Max(x => x.Bar.High) + config.TickSize;
                    var riskTicks = Math.Abs(entry.Bar.Open - stopPrice) / config.TickSize;

                    var path = session.Where(x => x.Bar.TimestampUtc >= entry.Bar.TimestampUtc
                        && x.Bar.TimestampUtc <= entry.Bar.TimestampUtc.AddMinutes(config.MaximumHoldMinutes)
                        && x.Local.TimeOfDay < WindowEnd).ToList();
                    var managed = Manage(path, entry.Bar.Open, stopPrice, setup.Direction);
                    outcomes.Add(new MorningAdaptiveTradeOutcome(group.Key, state, setup.Type, setup.Direction,
                        current.TimestampUtc, entry.Bar.TimestampUtc, entry.Bar.Open, stopPrice, riskTicks,
                        efficiency, contextRange, managed.Mode, managed.ExitReason, managed.ExitUtc, managed.ExitPrice,
                        managed.RealizedTicks, managed.RealizedDollars, managed.MaxFavorableTicks, managed.MaxAdverseTicks));
                    lastSetupUtc = current.TimestampUtc;
                }
            }

            return outcomes;
        }

        private MorningMarketState ClassifyState(IReadOnlyList<LocalBar> context, IReadOnlyList<LocalBar> shortWindow,
            NewYorkResearchDirection direction, decimal efficiency, decimal contextRange)
        {
            var shortRange = shortWindow.Max(x => x.Bar.High) - shortWindow.Min(x => x.Bar.Low);
            var compressed = shortRange <= contextRange * config.CompressionFraction;
            var shortDisplacement = shortWindow[shortWindow.Count - 1].Bar.Close - shortWindow[0].Bar.Open;
            var opposing = direction == NewYorkResearchDirection.Long ? shortDisplacement < 0m
                : direction == NewYorkResearchDirection.Short ? shortDisplacement > 0m : false;
            var shortEfficiency = DirectionalEfficiency(shortWindow);

            if (direction == NewYorkResearchDirection.None || efficiency <= config.RangeEfficiency)
                return compressed ? MorningMarketState.Compressing : MorningMarketState.Range;

            if (opposing && shortEfficiency >= config.TrendEfficiency)
                return MorningMarketState.Reversing;

            var close = context[context.Count - 1].Bar.Close;
            var start = context[0].Bar.Open;
            var progress = Math.Abs(close - start) / contextRange;
            if (progress >= config.ExhaustionFraction && shortEfficiency < config.RangeEfficiency)
                return MorningMarketState.Exhausting;

            if (compressed) return MorningMarketState.Compressing;
            if (efficiency >= config.StrongTrendEfficiency) return MorningMarketState.Trending;
            return MorningMarketState.DevelopingTrend;
        }

        private SetupSeed DetectSetup(IReadOnlyList<LocalBar> session, int i, MorningMarketState state,
            NewYorkResearchDirection trendDirection, decimal efficiency, decimal contextRange)
        {
            var current = session[i].Bar;
            var prior = session[i - 1].Bar;
            var priorRangeStart = Math.Max(0, i - config.ContextBars);
            var priorContext = session.Skip(priorRangeStart).Take(i - priorRangeStart).ToList();
            if (priorContext.Count < config.ShortBars) return SetupSeed.None;

            var priorHigh = priorContext.Max(x => x.Bar.High);
            var priorLow = priorContext.Min(x => x.Bar.Low);
            var shortStart = Math.Max(0, i - config.ShortBars);
            var shortPrior = session.Skip(shortStart).Take(i - shortStart).ToList();
            var shortHigh = shortPrior.Max(x => x.Bar.High);
            var shortLow = shortPrior.Min(x => x.Bar.Low);
            var barRange = Math.Max(config.TickSize, current.High - current.Low);
            var body = Math.Abs(current.Close - current.Open);
            var strongBody = body / barRange >= 0.55m;

            var priorBrokeHigh = prior.High > priorHigh;
            var priorBrokeLow = prior.Low < priorLow;
            var failedHigh = priorBrokeHigh && current.Close < priorHigh && current.Close < current.Open;
            var failedLow = priorBrokeLow && current.Close > priorLow && current.Close > current.Open;
            if (failedHigh) return new SetupSeed(MorningAdaptiveSetupType.FailedBreakoutReversal, NewYorkResearchDirection.Short);
            if (failedLow) return new SetupSeed(MorningAdaptiveSetupType.FailedBreakoutReversal, NewYorkResearchDirection.Long);

            var breaksHigh = current.Close > priorHigh && strongBody;
            var breaksLow = current.Close < priorLow && strongBody;
            if (state == MorningMarketState.Range || efficiency <= config.RangeEfficiency)
            {
                if (breaksHigh) return new SetupSeed(MorningAdaptiveSetupType.RangeResolution, NewYorkResearchDirection.Long);
                if (breaksLow) return new SetupSeed(MorningAdaptiveSetupType.RangeResolution, NewYorkResearchDirection.Short);
            }

            var shortRange = shortPrior.Max(x => x.Bar.High) - shortPrior.Min(x => x.Bar.Low);
            var compressed = shortRange <= contextRange * config.CompressionFraction;
            if (compressed && trendDirection != NewYorkResearchDirection.None)
            {
                if (trendDirection == NewYorkResearchDirection.Long && current.Close > shortHigh)
                    return new SetupSeed(MorningAdaptiveSetupType.CompressionResumption, NewYorkResearchDirection.Long);
                if (trendDirection == NewYorkResearchDirection.Short && current.Close < shortLow)
                    return new SetupSeed(MorningAdaptiveSetupType.CompressionResumption, NewYorkResearchDirection.Short);
            }

            if (trendDirection != NewYorkResearchDirection.None && efficiency >= config.TrendEfficiency)
            {
                var recent = session.Skip(Math.Max(0, i - 4)).Take(Math.Min(4, i)).ToList();
                if (recent.Count >= 3)
                {
                    var recentMove = recent[recent.Count - 1].Bar.Close - recent[0].Bar.Open;
                    var pulledBack = trendDirection == NewYorkResearchDirection.Long ? recentMove < 0m : recentMove > 0m;
                    var resumed = trendDirection == NewYorkResearchDirection.Long
                        ? current.Close > prior.Close && current.Close > current.Open
                        : current.Close < prior.Close && current.Close < current.Open;
                    if (pulledBack && resumed)
                        return new SetupSeed(MorningAdaptiveSetupType.PullbackRetest, trendDirection);
                }

                if (trendDirection == NewYorkResearchDirection.Long && current.Close > shortHigh && strongBody)
                    return new SetupSeed(MorningAdaptiveSetupType.TrendContinuation, NewYorkResearchDirection.Long);
                if (trendDirection == NewYorkResearchDirection.Short && current.Close < shortLow && strongBody)
                    return new SetupSeed(MorningAdaptiveSetupType.TrendContinuation, NewYorkResearchDirection.Short);
            }

            if (breaksHigh) return new SetupSeed(MorningAdaptiveSetupType.BreakoutAcceptance, NewYorkResearchDirection.Long);
            if (breaksLow) return new SetupSeed(MorningAdaptiveSetupType.BreakoutAcceptance, NewYorkResearchDirection.Short);
            return SetupSeed.None;
        }

        private ManagedPath Manage(IReadOnlyList<LocalBar> path, decimal entryPrice, decimal stopPrice, NewYorkResearchDirection direction)
        {
            if (path.Count == 0)
                return new ManagedPath(MorningAdaptiveManagementMode.Scalp, MorningAdaptiveExitReason.ResearchWindowEnd,
                    DateTimeOffset.MinValue, entryPrice, 0m, 0m, 0m, 0m);

            var dollarsPerTick = config.PointValuePerContract * config.TickSize * config.Contracts;
            var scalpTicks = config.ScalpCheckpointDollars / dollarsPerTick;
            var coreTicks = config.CoreCheckpointDollars / dollarsPerTick;
            var runnerTicks = config.RunnerCheckpointDollars / dollarsPerTick;
            var scalpGivebackTicks = config.ScalpGivebackDollars / dollarsPerTick;
            var coreGivebackTicks = config.CoreGivebackDollars / dollarsPerTick;
            var runnerGivebackTicks = config.RunnerGivebackDollars / dollarsPerTick;

            var mode = MorningAdaptiveManagementMode.Scalp;
            decimal mfe = 0m;
            decimal mae = 0m;
            var closes = new List<decimal>();

            foreach (var item in path)
            {
                var stopHit = direction == NewYorkResearchDirection.Long ? item.Bar.Low <= stopPrice : item.Bar.High >= stopPrice;
                var favorablePrice = direction == NewYorkResearchDirection.Long ? item.Bar.High - entryPrice : entryPrice - item.Bar.Low;
                var adversePrice = direction == NewYorkResearchDirection.Long ? entryPrice - item.Bar.Low : item.Bar.High - entryPrice;
                var favorableTicks = Math.Max(0m, favorablePrice / config.TickSize);
                var adverseTicks = Math.Max(0m, adversePrice / config.TickSize);
                if (favorableTicks > mfe) mfe = favorableTicks;
                if (adverseTicks > mae) mae = adverseTicks;

                if (stopHit)
                {
                    var realizedTicks = direction == NewYorkResearchDirection.Long
                        ? (stopPrice - entryPrice) / config.TickSize
                        : (entryPrice - stopPrice) / config.TickSize;
                    return Finish(mode, MorningAdaptiveExitReason.StructuralStop, item.Bar.TimestampUtc, stopPrice, realizedTicks, mfe, mae);
                }

                closes.Add(item.Bar.Close);
                var closeTicks = direction == NewYorkResearchDirection.Long
                    ? (item.Bar.Close - entryPrice) / config.TickSize
                    : (entryPrice - item.Bar.Close) / config.TickSize;
                var postEfficiency = CloseEfficiency(closes);

                if (mfe >= runnerTicks && postEfficiency >= 0.45m && closeTicks >= mfe - coreGivebackTicks)
                    mode = MorningAdaptiveManagementMode.Runner;
                else if (mfe >= coreTicks)
                    mode = MorningAdaptiveManagementMode.Core;

                if (mode == MorningAdaptiveManagementMode.Runner)
                {
                    if (closeTicks >= coreTicks && mfe - closeTicks >= runnerGivebackTicks)
                        return Finish(mode, MorningAdaptiveExitReason.RunnerProtection, item.Bar.TimestampUtc, item.Bar.Close, closeTicks, mfe, mae);
                }
                else if (mode == MorningAdaptiveManagementMode.Core)
                {
                    if (mfe - closeTicks >= coreGivebackTicks || (closeTicks >= coreTicks && postEfficiency < 0.20m))
                        return Finish(mode, MorningAdaptiveExitReason.CoreCapture, item.Bar.TimestampUtc, item.Bar.Close, closeTicks, mfe, mae);
                }
                else if (mfe >= scalpTicks)
                {
                    if (mfe - closeTicks >= scalpGivebackTicks || postEfficiency < 0.15m)
                        return Finish(mode, MorningAdaptiveExitReason.ScalpCapture, item.Bar.TimestampUtc, item.Bar.Close, closeTicks, mfe, mae);
                }

                if (closes.Count >= 6 && closeTicks > 0m && CloseEfficiency(closes.Skip(Math.Max(0, closes.Count - 6)).ToList()) < 0.08m)
                    return Finish(mode, MorningAdaptiveExitReason.StructuralDeterioration, item.Bar.TimestampUtc, item.Bar.Close, closeTicks, mfe, mae);
            }

            var last = path[path.Count - 1];
            var finalTicks = direction == NewYorkResearchDirection.Long
                ? (last.Bar.Close - entryPrice) / config.TickSize
                : (entryPrice - last.Bar.Close) / config.TickSize;
            return Finish(mode, MorningAdaptiveExitReason.ResearchWindowEnd, last.Bar.TimestampUtc, last.Bar.Close, finalTicks, mfe, mae);
        }

        private ManagedPath Finish(MorningAdaptiveManagementMode mode, MorningAdaptiveExitReason reason,
            DateTimeOffset exitUtc, decimal exitPrice, decimal realizedTicks, decimal mfe, decimal mae)
        {
            var dollarsPerTick = config.PointValuePerContract * config.TickSize * config.Contracts;
            return new ManagedPath(mode, reason, exitUtc, exitPrice, realizedTicks,
                realizedTicks * dollarsPerTick, mfe, mae);
        }

        private static decimal DirectionalEfficiency(IReadOnlyList<LocalBar> bars)
        {
            if (bars.Count < 2) return 0m;
            var displacement = Math.Abs(bars[bars.Count - 1].Bar.Close - bars[0].Bar.Open);
            decimal path = 0m;
            var previous = bars[0].Bar.Open;
            foreach (var item in bars)
            {
                path += Math.Abs(item.Bar.Close - previous);
                previous = item.Bar.Close;
            }
            return path <= 0m ? 0m : Math.Min(1m, displacement / path);
        }

        private static decimal CloseEfficiency(IReadOnlyList<decimal> closes)
        {
            if (closes.Count < 2) return 1m;
            var displacement = Math.Abs(closes[closes.Count - 1] - closes[0]);
            decimal path = 0m;
            for (var i = 1; i < closes.Count; i++) path += Math.Abs(closes[i] - closes[i - 1]);
            return path <= 0m ? 0m : Math.Min(1m, displacement / path);
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }

        private sealed class SetupSeed
        {
            public static readonly SetupSeed None = new SetupSeed(MorningAdaptiveSetupType.None, NewYorkResearchDirection.None);
            public SetupSeed(MorningAdaptiveSetupType type, NewYorkResearchDirection direction) { Type = type; Direction = direction; }
            public MorningAdaptiveSetupType Type { get; }
            public NewYorkResearchDirection Direction { get; }
        }

        private sealed class ManagedPath
        {
            public ManagedPath(MorningAdaptiveManagementMode mode, MorningAdaptiveExitReason exitReason, DateTimeOffset exitUtc,
                decimal exitPrice, decimal realizedTicks, decimal realizedDollars, decimal maxFavorableTicks, decimal maxAdverseTicks)
            {
                Mode = mode; ExitReason = exitReason; ExitUtc = exitUtc; ExitPrice = exitPrice; RealizedTicks = realizedTicks;
                RealizedDollars = realizedDollars; MaxFavorableTicks = maxFavorableTicks; MaxAdverseTicks = maxAdverseTicks;
            }
            public MorningAdaptiveManagementMode Mode { get; }
            public MorningAdaptiveExitReason ExitReason { get; }
            public DateTimeOffset ExitUtc { get; }
            public decimal ExitPrice { get; }
            public decimal RealizedTicks { get; }
            public decimal RealizedDollars { get; }
            public decimal MaxFavorableTicks { get; }
            public decimal MaxAdverseTicks { get; }
        }

        private sealed class LocalBar
        {
            public LocalBar(HistoricalBar bar, DateTime local) { Bar = bar; Local = local; }
            public HistoricalBar Bar { get; }
            public DateTime Local { get; }
        }
    }
}
