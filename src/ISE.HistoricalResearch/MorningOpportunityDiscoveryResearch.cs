using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningOpportunityType
    {
        None = 0,
        ContinuationResumption = 1,
        DirectionalTransition = 2
    }

    public sealed class MorningOpportunityDiscoveryConfig
    {
        public MorningOpportunityDiscoveryConfig(
            int trendLookbackBars = 60,
            int compressionLookbackBars = 10,
            int structuralLookbackBars = 10,
            decimal minimumTrendEfficiency = 0.40m,
            decimal compressionRangeFraction = 0.35m,
            decimal transitionDisplacementFraction = 0.25m,
            int cooldownMinutes = 20,
            int maximumOutcomeMinutes = 90,
            decimal tickSize = 0.25m,
            decimal pointValuePerContract = 2m,
            int contracts = 2,
            decimal intermediateObjective = 300m,
            decimal lowerObjective = 500m,
            decimal upperObjective = 1000m)
        {
            if (trendLookbackBars < 20) throw new ArgumentOutOfRangeException(nameof(trendLookbackBars));
            if (compressionLookbackBars < 3 || compressionLookbackBars >= trendLookbackBars) throw new ArgumentOutOfRangeException(nameof(compressionLookbackBars));
            if (structuralLookbackBars < 2) throw new ArgumentOutOfRangeException(nameof(structuralLookbackBars));
            if (minimumTrendEfficiency <= 0m || minimumTrendEfficiency > 1m) throw new ArgumentOutOfRangeException(nameof(minimumTrendEfficiency));
            if (compressionRangeFraction <= 0m || compressionRangeFraction >= 1m) throw new ArgumentOutOfRangeException(nameof(compressionRangeFraction));
            if (transitionDisplacementFraction <= 0m) throw new ArgumentOutOfRangeException(nameof(transitionDisplacementFraction));
            if (cooldownMinutes < 1) throw new ArgumentOutOfRangeException(nameof(cooldownMinutes));
            if (maximumOutcomeMinutes < 1) throw new ArgumentOutOfRangeException(nameof(maximumOutcomeMinutes));
            if (tickSize <= 0m || pointValuePerContract <= 0m || contracts <= 0) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (intermediateObjective <= 0m || lowerObjective < intermediateObjective || upperObjective < lowerObjective) throw new ArgumentOutOfRangeException(nameof(intermediateObjective));

            TrendLookbackBars = trendLookbackBars;
            CompressionLookbackBars = compressionLookbackBars;
            StructuralLookbackBars = structuralLookbackBars;
            MinimumTrendEfficiency = minimumTrendEfficiency;
            CompressionRangeFraction = compressionRangeFraction;
            TransitionDisplacementFraction = transitionDisplacementFraction;
            CooldownMinutes = cooldownMinutes;
            MaximumOutcomeMinutes = maximumOutcomeMinutes;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
            IntermediateObjective = intermediateObjective;
            LowerObjective = lowerObjective;
            UpperObjective = upperObjective;
        }

        public int TrendLookbackBars { get; }
        public int CompressionLookbackBars { get; }
        public int StructuralLookbackBars { get; }
        public decimal MinimumTrendEfficiency { get; }
        public decimal CompressionRangeFraction { get; }
        public decimal TransitionDisplacementFraction { get; }
        public int CooldownMinutes { get; }
        public int MaximumOutcomeMinutes { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
        public decimal IntermediateObjective { get; }
        public decimal LowerObjective { get; }
        public decimal UpperObjective { get; }
    }

    public sealed class MorningOpportunityOutcome
    {
        public MorningOpportunityOutcome(DateTime sessionDateCentral, MorningOpportunityType type, NewYorkResearchDirection direction,
            DateTimeOffset setupUtc, DateTimeOffset entryUtc, decimal entryPrice, decimal stopPrice, decimal initialRiskTicks,
            DateTimeOffset estimatedOriginUtc, int estimatedMoveAgeMinutes, decimal trendEfficiency, decimal trendRange,
            DateTimeOffset? first300Utc, DateTimeOffset? first500Utc, DateTimeOffset? first1000Utc, DateTimeOffset? stopUtc)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Type = type;
            Direction = direction;
            SetupUtc = setupUtc;
            EntryUtc = entryUtc;
            EntryPrice = entryPrice;
            StopPrice = stopPrice;
            InitialRiskTicks = initialRiskTicks;
            EstimatedOriginUtc = estimatedOriginUtc;
            EstimatedMoveAgeMinutes = estimatedMoveAgeMinutes;
            TrendEfficiency = trendEfficiency;
            TrendRange = trendRange;
            First300Utc = first300Utc;
            First500Utc = first500Utc;
            First1000Utc = first1000Utc;
            StopUtc = stopUtc;
        }

        public DateTime SessionDateCentral { get; }
        public MorningOpportunityType Type { get; }
        public NewYorkResearchDirection Direction { get; }
        public DateTimeOffset SetupUtc { get; }
        public DateTimeOffset EntryUtc { get; }
        public decimal EntryPrice { get; }
        public decimal StopPrice { get; }
        public decimal InitialRiskTicks { get; }
        public DateTimeOffset EstimatedOriginUtc { get; }
        public int EstimatedMoveAgeMinutes { get; }
        public decimal TrendEfficiency { get; }
        public decimal TrendRange { get; }
        public DateTimeOffset? First300Utc { get; }
        public DateTimeOffset? First500Utc { get; }
        public DateTimeOffset? First1000Utc { get; }
        public DateTimeOffset? StopUtc { get; }
        public bool Hit300BeforeStop => First300Utc.HasValue;
        public bool Hit500BeforeStop => First500Utc.HasValue;
        public bool Hit1000BeforeStop => First1000Utc.HasValue;
    }

    /// <summary>
    /// Research-only causal discovery pass across the broad 03:00-11:00 Central morning. It does not prescribe
    /// an entry time. It emits sparse continuation-resumption and directional-transition candidates from completed
    /// one-minute bars, enters at the next bar open, uses recent causal structure for the reference stop, estimates
    /// move age from aligned 15-minute blocks, and records $300/$500/$1000-before-stop evidence.
    /// Thresholds are transparent seed hypotheses and are not production parameters.
    /// </summary>
    public sealed class MorningOpportunityDiscoveryAnalyzer
    {
        private static readonly TimeSpan WindowStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan SetupEnd = new TimeSpan(10, 30, 0);
        private static readonly TimeSpan WindowEnd = new TimeSpan(11, 0, 0);
        private readonly MorningOpportunityDiscoveryConfig config;

        public MorningOpportunityDiscoveryAnalyzer(MorningOpportunityDiscoveryConfig? config = null)
        {
            this.config = config ?? new MorningOpportunityDiscoveryConfig();
        }

        public IReadOnlyList<MorningOpportunityOutcome> Analyze(IReadOnlyList<HistoricalBar> bars)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (bars.Count == 0) return Array.Empty<MorningOpportunityOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars.Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .Where(x => x.Local.TimeOfDay >= WindowStart && x.Local.TimeOfDay < WindowEnd)
                .OrderBy(x => x.Local).ToList();
            var result = new List<MorningOpportunityOutcome>();

            foreach (var sessionGroup in localized.GroupBy(x => x.Local.Date).OrderBy(x => x.Key))
            {
                var session = sessionGroup.OrderBy(x => x.Local).ToList();
                DateTimeOffset? lastCandidateUtc = null;
                for (var i = config.TrendLookbackBars; i + 1 < session.Count; i++)
                {
                    if (session[i].Local.TimeOfDay >= SetupEnd) break;
                    if (lastCandidateUtc.HasValue && (session[i].Bar.TimestampUtc - lastCandidateUtc.Value).TotalMinutes < config.CooldownMinutes) continue;

                    var trend = session.Skip(i - config.TrendLookbackBars + 1).Take(config.TrendLookbackBars).ToList();
                    var trendRange = Math.Max(config.TickSize, trend.Max(x => x.Bar.High) - trend.Min(x => x.Bar.Low));
                    var displacement = trend[trend.Count - 1].Bar.Close - trend[0].Bar.Open;
                    var efficiency = DirectionalEfficiency(trend);
                    if (efficiency < config.MinimumTrendEfficiency || displacement == 0m) continue;

                    var trendDirection = displacement > 0m ? NewYorkResearchDirection.Long : NewYorkResearchDirection.Short;
                    var compression = session.Skip(i - config.CompressionLookbackBars).Take(config.CompressionLookbackBars).ToList();
                    var compressionRange = compression.Max(x => x.Bar.High) - compression.Min(x => x.Bar.Low);
                    var current = session[i].Bar;

                    MorningOpportunityType type = MorningOpportunityType.None;
                    NewYorkResearchDirection direction = NewYorkResearchDirection.None;

                    var compressed = compressionRange <= trendRange * config.CompressionRangeFraction;
                    var resumes = trendDirection == NewYorkResearchDirection.Long
                        ? current.Close > compression.Max(x => x.Bar.High)
                        : current.Close < compression.Min(x => x.Bar.Low);
                    if (compressed && resumes)
                    {
                        type = MorningOpportunityType.ContinuationResumption;
                        direction = trendDirection;
                    }
                    else
                    {
                        var recentStart = Math.Max(0, i - config.CompressionLookbackBars + 1);
                        var recent = session.Skip(recentStart).Take(i - recentStart + 1).ToList();
                        var recentDisplacement = recent[recent.Count - 1].Bar.Close - recent[0].Bar.Open;
                        var opposite = trendDirection == NewYorkResearchDirection.Long ? recentDisplacement < 0m : recentDisplacement > 0m;
                        var largeEnough = Math.Abs(recentDisplacement) >= trendRange * config.TransitionDisplacementFraction;
                        var trendMid = (trend.Max(x => x.Bar.High) + trend.Min(x => x.Bar.Low)) / 2m;
                        var throughMid = trendDirection == NewYorkResearchDirection.Long ? current.Close < trendMid : current.Close > trendMid;
                        if (opposite && largeEnough && throughMid)
                        {
                            type = MorningOpportunityType.DirectionalTransition;
                            direction = trendDirection == NewYorkResearchDirection.Long ? NewYorkResearchDirection.Short : NewYorkResearchDirection.Long;
                        }
                    }

                    if (type == MorningOpportunityType.None) continue;

                    var entry = session[i + 1];
                    var structureStart = Math.Max(0, i - config.StructuralLookbackBars + 1);
                    var structure = session.Skip(structureStart).Take(i - structureStart + 1).ToList();
                    var stopPrice = direction == NewYorkResearchDirection.Long
                        ? structure.Min(x => x.Bar.Low) - config.TickSize
                        : structure.Max(x => x.Bar.High) + config.TickSize;
                    var riskTicks = Math.Abs(entry.Bar.Open - stopPrice) / config.TickSize;
                    var origin = EstimateOrigin(session, i, direction);
                    var age = (int)Math.Max(0d, (session[i].Bar.TimestampUtc - origin).TotalMinutes);
                    var path = session.Where(x => x.Bar.TimestampUtc >= entry.Bar.TimestampUtc
                        && x.Bar.TimestampUtc <= entry.Bar.TimestampUtc.AddMinutes(config.MaximumOutcomeMinutes)
                        && x.Local.TimeOfDay < WindowEnd).ToList();
                    ResolvePath(path, entry.Bar.Open, stopPrice, direction, out var h300, out var h500, out var h1000, out var stop);

                    result.Add(new MorningOpportunityOutcome(sessionGroup.Key, type, direction, current.TimestampUtc, entry.Bar.TimestampUtc,
                        entry.Bar.Open, stopPrice, riskTicks, origin, age, efficiency, trendRange, h300, h500, h1000, stop));
                    lastCandidateUtc = current.TimestampUtc;
                }
            }
            return result;
        }

        private DateTimeOffset EstimateOrigin(IReadOnlyList<LocalBar> session, int setupIndex, NewYorkResearchDirection direction)
        {
            const int block = 15;
            var earliest = session[setupIndex].Bar.TimestampUtc;
            var cursor = setupIndex;
            var blocks = 0;
            while (cursor - block + 1 >= 0 && blocks < 12)
            {
                var segment = session.Skip(cursor - block + 1).Take(block).ToList();
                var displacement = segment[segment.Count - 1].Bar.Close - segment[0].Bar.Open;
                var aligned = direction == NewYorkResearchDirection.Long ? displacement > 0m : displacement < 0m;
                if (!aligned || DirectionalEfficiency(segment) < 0.30m) break;
                earliest = segment[0].Bar.TimestampUtc;
                cursor -= block;
                blocks++;
            }
            return earliest;
        }

        private static decimal DirectionalEfficiency(IReadOnlyList<LocalBar> bars)
        {
            if (bars.Count < 2) return 0m;
            var displacement = Math.Abs(bars[bars.Count - 1].Bar.Close - bars[0].Bar.Open);
            decimal path = 0m;
            var previous = bars[0].Bar.Open;
            foreach (var bar in bars)
            {
                path += Math.Abs(bar.Bar.Close - previous);
                previous = bar.Bar.Close;
            }
            return path <= 0m ? 0m : Math.Min(1m, displacement / path);
        }

        private void ResolvePath(IReadOnlyList<LocalBar> bars, decimal entryPrice, decimal stopPrice, NewYorkResearchDirection direction,
            out DateTimeOffset? hit300, out DateTimeOffset? hit500, out DateTimeOffset? hit1000, out DateTimeOffset? stop)
        {
            hit300 = null; hit500 = null; hit1000 = null; stop = null;
            var p300 = config.IntermediateObjective / (config.PointValuePerContract * config.Contracts);
            var p500 = config.LowerObjective / (config.PointValuePerContract * config.Contracts);
            var p1000 = config.UpperObjective / (config.PointValuePerContract * config.Contracts);
            foreach (var item in bars)
            {
                var stopHit = direction == NewYorkResearchDirection.Long ? item.Bar.Low <= stopPrice : item.Bar.High >= stopPrice;
                if (stopHit) { stop = item.Bar.TimestampUtc; break; }
                var favorable = direction == NewYorkResearchDirection.Long ? item.Bar.High - entryPrice : entryPrice - item.Bar.Low;
                if (!hit300.HasValue && favorable >= p300) hit300 = item.Bar.TimestampUtc;
                if (!hit500.HasValue && favorable >= p500) hit500 = item.Bar.TimestampUtc;
                if (!hit1000.HasValue && favorable >= p1000) hit1000 = item.Bar.TimestampUtc;
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }

        private sealed class LocalBar
        {
            public LocalBar(HistoricalBar bar, DateTime local) { Bar = bar; Local = local; }
            public HistoricalBar Bar { get; }
            public DateTime Local { get; }
        }
    }
}
