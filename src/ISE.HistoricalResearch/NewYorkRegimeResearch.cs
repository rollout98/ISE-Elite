using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum NewYorkResearchRegime
    {
        Unclassified = 0,
        OpeningDrive = 1,
        EarlyReversal = 2,
        DeepPullbackContinuation = 3,
        VolatileTwoSidedAuction = 4,
        RangeNoTrade = 5,
        LaterContinuationReversal = 6
    }

    public enum NewYorkResearchDirection
    {
        None = 0,
        Long = 1,
        Short = -1
    }

    public enum NewYorkOpportunitySeedType
    {
        None = 0,
        OpeningDriveContinuation = 1,
        EarlyReversal = 2,
        DeepPullbackContinuation = 3,
        LaterContinuation = 4,
        LaterReversal = 5
    }

    public sealed class NewYorkRegimeResearchConfig
    {
        public NewYorkRegimeResearchConfig(
            decimal openingDriveEfficiency = 0.60m,
            decimal openingDrivePreOpenRangeMultiple = 0.45m,
            decimal earlyReversalOpeningFraction = 0.55m,
            decimal deepPullbackOpeningFraction = 0.45m,
            decimal continuationOpeningFraction = 0.35m,
            decimal auctionEfficiencyCeiling = 0.35m,
            decimal auctionPreOpenRangeMultiple = 0.75m,
            decimal rangeSessionEfficiencyCeiling = 0.25m,
            decimal rangePreOpenRangeMultiple = 1.35m,
            decimal laterMoveOpeningFraction = 0.40m)
        {
            OpeningDriveEfficiency = RequireUnit(openingDriveEfficiency, nameof(openingDriveEfficiency));
            OpeningDrivePreOpenRangeMultiple = RequireNonNegative(openingDrivePreOpenRangeMultiple, nameof(openingDrivePreOpenRangeMultiple));
            EarlyReversalOpeningFraction = RequireNonNegative(earlyReversalOpeningFraction, nameof(earlyReversalOpeningFraction));
            DeepPullbackOpeningFraction = RequireNonNegative(deepPullbackOpeningFraction, nameof(deepPullbackOpeningFraction));
            ContinuationOpeningFraction = RequireNonNegative(continuationOpeningFraction, nameof(continuationOpeningFraction));
            AuctionEfficiencyCeiling = RequireUnit(auctionEfficiencyCeiling, nameof(auctionEfficiencyCeiling));
            AuctionPreOpenRangeMultiple = RequireNonNegative(auctionPreOpenRangeMultiple, nameof(auctionPreOpenRangeMultiple));
            RangeSessionEfficiencyCeiling = RequireUnit(rangeSessionEfficiencyCeiling, nameof(rangeSessionEfficiencyCeiling));
            RangePreOpenRangeMultiple = RequireNonNegative(rangePreOpenRangeMultiple, nameof(rangePreOpenRangeMultiple));
            LaterMoveOpeningFraction = RequireNonNegative(laterMoveOpeningFraction, nameof(laterMoveOpeningFraction));
        }

        public decimal OpeningDriveEfficiency { get; }
        public decimal OpeningDrivePreOpenRangeMultiple { get; }
        public decimal EarlyReversalOpeningFraction { get; }
        public decimal DeepPullbackOpeningFraction { get; }
        public decimal ContinuationOpeningFraction { get; }
        public decimal AuctionEfficiencyCeiling { get; }
        public decimal AuctionPreOpenRangeMultiple { get; }
        public decimal RangeSessionEfficiencyCeiling { get; }
        public decimal RangePreOpenRangeMultiple { get; }
        public decimal LaterMoveOpeningFraction { get; }

        private static decimal RequireUnit(decimal value, string name)
        {
            if (value < 0m || value > 1m) throw new ArgumentOutOfRangeException(name);
            return value;
        }

        private static decimal RequireNonNegative(decimal value, string name)
        {
            if (value < 0m) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class NewYorkSessionResearchFeatures
    {
        public NewYorkSessionResearchFeatures(
            DateTime sessionDateCentral,
            string instrument,
            int intervalSeconds,
            int barCount,
            decimal preOpenRange,
            decimal openingRange,
            decimal openingDisplacement,
            decimal openingEfficiency,
            decimal earlyDisplacement,
            decimal earlyAdverseExcursion,
            decimal laterDisplacement,
            decimal coreRange,
            decimal coreDisplacement,
            decimal coreEfficiency)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Instrument = instrument;
            IntervalSeconds = intervalSeconds;
            BarCount = barCount;
            PreOpenRange = preOpenRange;
            OpeningRange = openingRange;
            OpeningDisplacement = openingDisplacement;
            OpeningEfficiency = openingEfficiency;
            EarlyDisplacement = earlyDisplacement;
            EarlyAdverseExcursion = earlyAdverseExcursion;
            LaterDisplacement = laterDisplacement;
            CoreRange = coreRange;
            CoreDisplacement = coreDisplacement;
            CoreEfficiency = coreEfficiency;
        }

        public DateTime SessionDateCentral { get; }
        public string Instrument { get; }
        public int IntervalSeconds { get; }
        public int BarCount { get; }
        public decimal PreOpenRange { get; }
        public decimal OpeningRange { get; }
        public decimal OpeningDisplacement { get; }
        public decimal OpeningEfficiency { get; }
        public decimal EarlyDisplacement { get; }
        public decimal EarlyAdverseExcursion { get; }
        public decimal LaterDisplacement { get; }
        public decimal CoreRange { get; }
        public decimal CoreDisplacement { get; }
        public decimal CoreEfficiency { get; }
        public NewYorkResearchDirection OpeningDirection => DirectionOf(OpeningDisplacement);
        public NewYorkResearchDirection EarlyDirection => DirectionOf(EarlyDisplacement);
        public NewYorkResearchDirection LaterDirection => DirectionOf(LaterDisplacement);

        private static NewYorkResearchDirection DirectionOf(decimal value)
        {
            if (value > 0m) return NewYorkResearchDirection.Long;
            if (value < 0m) return NewYorkResearchDirection.Short;
            return NewYorkResearchDirection.None;
        }
    }

    public sealed class NewYorkRegimeClassification
    {
        public NewYorkRegimeClassification(NewYorkSessionResearchFeatures features, NewYorkResearchRegime regime, decimal score, string rationale)
        {
            Features = features ?? throw new ArgumentNullException(nameof(features));
            Regime = regime;
            if (score < 0m || score > 1m) throw new ArgumentOutOfRangeException(nameof(score));
            Score = score;
            Rationale = rationale ?? string.Empty;
        }

        public NewYorkSessionResearchFeatures Features { get; }
        public NewYorkResearchRegime Regime { get; }
        public decimal Score { get; }
        public string Rationale { get; }
    }

    public sealed class NewYorkOpportunitySeedLabel
    {
        public NewYorkOpportunitySeedLabel(DateTime sessionDateCentral, NewYorkOpportunitySeedType type, NewYorkResearchDirection direction, TimeSpan windowStartCentral, TimeSpan windowEndCentral, decimal score, string rationale)
        {
            SessionDateCentral = sessionDateCentral.Date;
            Type = type;
            Direction = direction;
            WindowStartCentral = windowStartCentral;
            WindowEndCentral = windowEndCentral;
            if (windowEndCentral <= windowStartCentral) throw new ArgumentException("Opportunity window end must be after start.");
            if (score < 0m || score > 1m) throw new ArgumentOutOfRangeException(nameof(score));
            Score = score;
            Rationale = rationale ?? string.Empty;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkOpportunitySeedType Type { get; }
        public NewYorkResearchDirection Direction { get; }
        public TimeSpan WindowStartCentral { get; }
        public TimeSpan WindowEndCentral { get; }
        public decimal Score { get; }
        public string Rationale { get; }
    }

    public sealed class NewYorkSessionResearchFeatureExtractor
    {
        private static readonly TimeSpan SessionStart = new TimeSpan(6, 0, 0);
        private static readonly TimeSpan PreOpenEnd = new TimeSpan(8, 30, 0);
        private static readonly TimeSpan OpeningEnd = new TimeSpan(9, 5, 0);
        private static readonly TimeSpan EarlyEnd = new TimeSpan(9, 30, 0);
        private static readonly TimeSpan LaterEnd = new TimeSpan(10, 0, 0);
        private static readonly TimeSpan SessionEnd = new TimeSpan(11, 0, 0);

        public IReadOnlyList<NewYorkSessionResearchFeatures> Extract(IReadOnlyList<HistoricalBar> bars)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (bars.Count == 0) return Array.Empty<NewYorkSessionResearchFeatures>();

            var first = bars[0];
            if (bars.Any(x => !string.Equals(x.Instrument, first.Instrument, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("New York regime research requires one instrument per dataset.");
            if (bars.Any(x => x.IntervalSeconds != first.IntervalSeconds))
                throw new InvalidOperationException("New York regime research requires one bar interval per dataset.");

            var central = ResolveCentralTimeZone();
            var localized = bars
                .Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .Where(x => x.Local.TimeOfDay >= SessionStart && x.Local.TimeOfDay < SessionEnd)
                .OrderBy(x => x.Local)
                .ToList();

            var result = new List<NewYorkSessionResearchFeatures>();
            foreach (var group in localized.GroupBy(x => x.Local.Date).OrderBy(x => x.Key))
            {
                var session = group.ToList();
                var preOpen = Window(session, SessionStart, PreOpenEnd);
                var opening = Window(session, PreOpenEnd, OpeningEnd);
                var early = Window(session, OpeningEnd, EarlyEnd);
                var later = Window(session, EarlyEnd, LaterEnd);
                var core = Window(session, PreOpenEnd, LaterEnd);
                if (preOpen.Count == 0 || opening.Count == 0 || early.Count == 0 || later.Count == 0 || core.Count == 0)
                    continue;

                var openingRange = Range(opening);
                var openingDisplacement = Displacement(opening);
                var openingDirection = Math.Sign(openingDisplacement);
                var earlyAdverse = openingDirection == 0 ? 0m : AdverseExcursionFromOpeningClose(early, opening[opening.Count - 1].Bar.Close, openingDirection);

                result.Add(new NewYorkSessionResearchFeatures(
                    group.Key,
                    first.Instrument,
                    first.IntervalSeconds,
                    session.Count,
                    Range(preOpen),
                    openingRange,
                    openingDisplacement,
                    openingRange == 0m ? 0m : Math.Abs(openingDisplacement) / openingRange,
                    Displacement(early),
                    earlyAdverse,
                    Displacement(later),
                    Range(core),
                    Displacement(core),
                    Range(core) == 0m ? 0m : Math.Abs(Displacement(core)) / Range(core)));
            }

            return result;
        }

        private static List<LocalBar> Window(List<LocalBar> bars, TimeSpan start, TimeSpan end)
        {
            return bars.Where(x => x.Local.TimeOfDay >= start && x.Local.TimeOfDay < end).ToList();
        }

        private static decimal Range(IReadOnlyList<LocalBar> bars)
        {
            return bars.Max(x => x.Bar.High) - bars.Min(x => x.Bar.Low);
        }

        private static decimal Displacement(IReadOnlyList<LocalBar> bars)
        {
            return bars[bars.Count - 1].Bar.Close - bars[0].Bar.Open;
        }

        private static decimal AdverseExcursionFromOpeningClose(IReadOnlyList<LocalBar> bars, decimal openingClose, int openingDirection)
        {
            if (openingDirection > 0) return Math.Max(0m, openingClose - bars.Min(x => x.Bar.Low));
            return Math.Max(0m, bars.Max(x => x.Bar.High) - openingClose);
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

    public sealed class NewYorkRegimeSeedClassifier
    {
        private readonly NewYorkRegimeResearchConfig config;

        public NewYorkRegimeSeedClassifier(NewYorkRegimeResearchConfig? config = null)
        {
            this.config = config ?? new NewYorkRegimeResearchConfig();
        }

        public NewYorkRegimeClassification Classify(NewYorkSessionResearchFeatures f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            var pre = Math.Max(f.PreOpenRange, 0.25m);
            var openingMagnitude = Math.Abs(f.OpeningDisplacement);
            var earlyMagnitude = Math.Abs(f.EarlyDisplacement);
            var laterMagnitude = Math.Abs(f.LaterDisplacement);
            var oppositeEarly = Opposite(f.OpeningDirection, f.EarlyDirection);
            var oppositeLater = Opposite(f.OpeningDirection, f.LaterDirection);
            var sameLater = Same(f.OpeningDirection, f.LaterDirection);

            if (f.OpeningDirection != NewYorkResearchDirection.None
                && f.OpeningEfficiency >= config.OpeningDriveEfficiency
                && openingMagnitude >= pre * config.OpeningDrivePreOpenRangeMultiple
                && f.EarlyAdverseExcursion < Math.Max(f.OpeningRange, 0.25m) * config.DeepPullbackOpeningFraction)
            {
                return Result(f, NewYorkResearchRegime.OpeningDrive, Average(Clamp01(f.OpeningEfficiency), Clamp01(openingMagnitude / pre)), "Directional opening displacement with efficient price travel and limited immediate adverse excursion.");
            }

            if (f.OpeningDirection != NewYorkResearchDirection.None
                && oppositeEarly
                && earlyMagnitude >= Math.Max(f.OpeningRange, 0.25m) * config.EarlyReversalOpeningFraction)
            {
                return Result(f, NewYorkResearchRegime.EarlyReversal, Average(Clamp01(earlyMagnitude / Math.Max(f.OpeningRange, 0.25m)), Clamp01(f.OpeningEfficiency)), "Post-open move reverses the opening direction with material displacement.");
            }

            if (f.OpeningDirection != NewYorkResearchDirection.None
                && f.EarlyAdverseExcursion >= Math.Max(f.OpeningRange, 0.25m) * config.DeepPullbackOpeningFraction
                && sameLater
                && laterMagnitude >= Math.Max(f.OpeningRange, 0.25m) * config.ContinuationOpeningFraction)
            {
                return Result(f, NewYorkResearchRegime.DeepPullbackContinuation, Average(Clamp01(f.EarlyAdverseExcursion / Math.Max(f.OpeningRange, 0.25m)), Clamp01(laterMagnitude / Math.Max(f.OpeningRange, 0.25m))), "Opening direction survives a deep early pullback and resumes during the later research window.");
            }

            if (f.OpeningRange >= pre * config.AuctionPreOpenRangeMultiple
                && f.OpeningEfficiency <= config.AuctionEfficiencyCeiling
                && f.CoreRange >= f.OpeningRange * 1.15m)
            {
                return Result(f, NewYorkResearchRegime.VolatileTwoSidedAuction, Average(Clamp01(f.OpeningRange / pre), Clamp01(1m - f.OpeningEfficiency)), "Large opening range with low directional efficiency and continued two-sided expansion.");
            }

            if (f.CoreRange <= pre * config.RangePreOpenRangeMultiple
                && f.CoreEfficiency <= config.RangeSessionEfficiencyCeiling)
            {
                return Result(f, NewYorkResearchRegime.RangeNoTrade, Average(Clamp01(1m - f.CoreEfficiency), Clamp01(1m - (f.CoreRange / Math.Max(pre * config.RangePreOpenRangeMultiple, 0.25m)))), "Core morning range remains contained with low net directional efficiency.");
            }

            if (f.OpeningDirection != NewYorkResearchDirection.None
                && laterMagnitude >= Math.Max(f.OpeningRange, 0.25m) * config.LaterMoveOpeningFraction
                && (sameLater || oppositeLater))
            {
                return Result(f, NewYorkResearchRegime.LaterContinuationReversal, Clamp01(laterMagnitude / Math.Max(f.OpeningRange, 0.25m)), sameLater ? "Material later continuation after a less decisive opening sequence." : "Material later reversal after a less decisive opening sequence.");
            }

            return Result(f, NewYorkResearchRegime.Unclassified, 0m, "No seed-regime rule met. Retain for manual review and later model refinement.");
        }

        private static NewYorkRegimeClassification Result(NewYorkSessionResearchFeatures f, NewYorkResearchRegime regime, decimal score, string rationale)
        {
            return new NewYorkRegimeClassification(f, regime, Clamp01(score), rationale);
        }

        private static bool Opposite(NewYorkResearchDirection a, NewYorkResearchDirection b) => a != NewYorkResearchDirection.None && b != NewYorkResearchDirection.None && a != b;
        private static bool Same(NewYorkResearchDirection a, NewYorkResearchDirection b) => a != NewYorkResearchDirection.None && a == b;
        private static decimal Clamp01(decimal value) => value < 0m ? 0m : value > 1m ? 1m : value;
        private static decimal Average(decimal a, decimal b) => (a + b) / 2m;
    }

    public sealed class NewYorkOpportunitySeedLabeler
    {
        public IReadOnlyList<NewYorkOpportunitySeedLabel> Label(NewYorkRegimeClassification classification)
        {
            if (classification == null) throw new ArgumentNullException(nameof(classification));
            var f = classification.Features;
            var labels = new List<NewYorkOpportunitySeedLabel>();
            switch (classification.Regime)
            {
                case NewYorkResearchRegime.OpeningDrive:
                    labels.Add(new NewYorkOpportunitySeedLabel(f.SessionDateCentral, NewYorkOpportunitySeedType.OpeningDriveContinuation, f.OpeningDirection, new TimeSpan(8, 30, 0), new TimeSpan(9, 5, 0), classification.Score, classification.Rationale));
                    break;
                case NewYorkResearchRegime.EarlyReversal:
                    labels.Add(new NewYorkOpportunitySeedLabel(f.SessionDateCentral, NewYorkOpportunitySeedType.EarlyReversal, f.EarlyDirection, new TimeSpan(8, 45, 0), new TimeSpan(9, 30, 0), classification.Score, classification.Rationale));
                    break;
                case NewYorkResearchRegime.DeepPullbackContinuation:
                    labels.Add(new NewYorkOpportunitySeedLabel(f.SessionDateCentral, NewYorkOpportunitySeedType.DeepPullbackContinuation, f.OpeningDirection, new TimeSpan(9, 5, 0), new TimeSpan(10, 0, 0), classification.Score, classification.Rationale));
                    break;
                case NewYorkResearchRegime.LaterContinuationReversal:
                    var same = f.LaterDirection == f.OpeningDirection;
                    labels.Add(new NewYorkOpportunitySeedLabel(f.SessionDateCentral, same ? NewYorkOpportunitySeedType.LaterContinuation : NewYorkOpportunitySeedType.LaterReversal, f.LaterDirection, new TimeSpan(9, 30, 0), new TimeSpan(10, 30, 0), classification.Score, classification.Rationale));
                    break;
            }
            return labels;
        }
    }
}
