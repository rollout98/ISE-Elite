using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum NewYorkSetupQualityGrade
    {
        None = 0,
        A = 1,
        B = 2,
        C = 3
    }

    public sealed class NewYorkSetupQualityConfig
    {
        public NewYorkSetupQualityConfig(decimal gradeAThreshold = 70m, decimal gradeBThreshold = 55m,
            decimal intermediateObjective = 300m, decimal lowerObjective = 500m, decimal upperObjective = 1000m,
            decimal tickSize = 0.25m, decimal pointValuePerContract = 2m, int contracts = 2)
        {
            if (gradeAThreshold <= gradeBThreshold) throw new ArgumentOutOfRangeException(nameof(gradeAThreshold));
            if (gradeBThreshold <= 0m) throw new ArgumentOutOfRangeException(nameof(gradeBThreshold));
            if (intermediateObjective <= 0m) throw new ArgumentOutOfRangeException(nameof(intermediateObjective));
            if (lowerObjective < intermediateObjective) throw new ArgumentOutOfRangeException(nameof(lowerObjective));
            if (upperObjective < lowerObjective) throw new ArgumentOutOfRangeException(nameof(upperObjective));
            if (tickSize <= 0m) throw new ArgumentOutOfRangeException(nameof(tickSize));
            if (pointValuePerContract <= 0m) throw new ArgumentOutOfRangeException(nameof(pointValuePerContract));
            if (contracts <= 0) throw new ArgumentOutOfRangeException(nameof(contracts));
            GradeAThreshold = gradeAThreshold;
            GradeBThreshold = gradeBThreshold;
            IntermediateObjective = intermediateObjective;
            LowerObjective = lowerObjective;
            UpperObjective = upperObjective;
            TickSize = tickSize;
            PointValuePerContract = pointValuePerContract;
            Contracts = contracts;
        }

        public decimal GradeAThreshold { get; }
        public decimal GradeBThreshold { get; }
        public decimal IntermediateObjective { get; }
        public decimal LowerObjective { get; }
        public decimal UpperObjective { get; }
        public decimal TickSize { get; }
        public decimal PointValuePerContract { get; }
        public int Contracts { get; }
    }

    public sealed class NewYorkSetupQualityOutcome
    {
        public NewYorkSetupQualityOutcome(DateTime sessionDateCentral, NewYorkTradeableEntryType entryType,
            NewYorkResearchDirection direction, DateTimeOffset entryUtc, decimal initialRiskTicks,
            decimal riskScore, decimal bodyScore, decimal closeLocationScore, decimal impulseScore,
            decimal separationScore, decimal totalScore, NewYorkSetupQualityGrade grade,
            DateTimeOffset? first300Utc, DateTimeOffset? first500Utc, DateTimeOffset? first1000Utc,
            DateTimeOffset? stopUtc)
        {
            SessionDateCentral = sessionDateCentral.Date;
            EntryType = entryType;
            Direction = direction;
            EntryUtc = entryUtc;
            InitialRiskTicks = initialRiskTicks;
            RiskScore = riskScore;
            BodyScore = bodyScore;
            CloseLocationScore = closeLocationScore;
            ImpulseScore = impulseScore;
            SeparationScore = separationScore;
            TotalScore = totalScore;
            Grade = grade;
            First300Utc = first300Utc;
            First500Utc = first500Utc;
            First1000Utc = first1000Utc;
            StopUtc = stopUtc;
        }

        public DateTime SessionDateCentral { get; }
        public NewYorkTradeableEntryType EntryType { get; }
        public NewYorkResearchDirection Direction { get; }
        public DateTimeOffset EntryUtc { get; }
        public decimal InitialRiskTicks { get; }
        public decimal RiskScore { get; }
        public decimal BodyScore { get; }
        public decimal CloseLocationScore { get; }
        public decimal ImpulseScore { get; }
        public decimal SeparationScore { get; }
        public decimal TotalScore { get; }
        public NewYorkSetupQualityGrade Grade { get; }
        public DateTimeOffset? First300Utc { get; }
        public DateTimeOffset? First500Utc { get; }
        public DateTimeOffset? First1000Utc { get; }
        public DateTimeOffset? StopUtc { get; }
        public bool Hit300BeforeStop => First300Utc.HasValue;
        public bool Hit500BeforeStop => First500Utc.HasValue;
        public bool Hit1000BeforeStop => First1000Utc.HasValue;
        public bool Preferred => Grade == NewYorkSetupQualityGrade.A || Grade == NewYorkSetupQualityGrade.B;
    }

    /// <summary>
    /// Research-only multi-factor setup-quality layer. It does not impose the prior 200-tick hard cutoff.
    /// Each already-causal tradeable candidate receives a transparent 0-100 score from structural risk,
    /// setup-bar body efficiency, directional close location, setup impulse relative to opening range,
    /// and directional separation from the transition signal. Grades are descriptive research labels,
    /// not production authorization.
    /// </summary>
    public sealed class NewYorkSetupQualityAnalyzer
    {
        private static readonly TimeSpan OutcomeEnd = new TimeSpan(9, 30, 0);
        private readonly NewYorkSetupQualityConfig config;

        public NewYorkSetupQualityAnalyzer(NewYorkSetupQualityConfig? config = null)
        {
            this.config = config ?? new NewYorkSetupQualityConfig();
        }

        public IReadOnlyList<NewYorkSetupQualityOutcome> Analyze(IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<NewYorkEightFortyFiveTransitionOutcome> transitions,
            IReadOnlyList<NewYorkTradeableEntryOutcome> tradeable)
        {
            if (bars == null) throw new ArgumentNullException(nameof(bars));
            if (transitions == null) throw new ArgumentNullException(nameof(transitions));
            if (tradeable == null) throw new ArgumentNullException(nameof(tradeable));
            if (bars.Count == 0 || transitions.Count == 0 || tradeable.Count == 0) return Array.Empty<NewYorkSetupQualityOutcome>();

            var central = ResolveCentralTimeZone();
            var localized = bars.Select(x => new LocalBar(x, TimeZoneInfo.ConvertTime(x.TimestampUtc, central).DateTime))
                .OrderBy(x => x.Local).ToList();
            var byDate = localized.GroupBy(x => x.Local.Date).ToDictionary(x => x.Key, x => x.ToList());
            var transitionByDate = transitions.ToDictionary(x => x.SessionDateCentral.Date, x => x);
            var result = new List<NewYorkSetupQualityOutcome>();

            foreach (var candidate in tradeable.Where(x => x.HasEntry).OrderBy(x => x.SessionDateCentral))
            {
                if (!byDate.TryGetValue(candidate.SessionDateCentral.Date, out var session)) continue;
                if (!transitionByDate.TryGetValue(candidate.SessionDateCentral.Date, out var transition)) continue;
                if (!candidate.SetupCompleteUtc.HasValue || !candidate.EntryUtc.HasValue) continue;

                var setupIndex = session.FindIndex(x => x.Bar.TimestampUtc == candidate.SetupCompleteUtc.Value);
                var signalIndex = transition.SignalTimestampUtc.HasValue
                    ? session.FindIndex(x => x.Bar.TimestampUtc == transition.SignalTimestampUtc.Value)
                    : -1;
                if (setupIndex < 0) continue;

                var setup = session[setupIndex].Bar;
                var prior = setupIndex > 0 ? session[setupIndex - 1].Bar : setup;
                var signal = signalIndex >= 0 ? session[signalIndex].Bar : setup;
                var openingRange = Math.Max(config.TickSize, transition.OpeningRange);
                var riskTicks = candidate.InitialRiskTicks(config.TickSize);

                var riskScore = ScoreRisk(riskTicks);
                var barRange = Math.Max(config.TickSize, setup.High - setup.Low);
                var bodyEfficiency = Math.Min(1m, Math.Abs(setup.Close - setup.Open) / barRange);
                var bodyScore = 20m * bodyEfficiency;
                var directionalClose = candidate.Direction == NewYorkResearchDirection.Long
                    ? (setup.Close - setup.Low) / barRange
                    : (setup.High - setup.Close) / barRange;
                directionalClose = Clamp01(directionalClose);
                var closeScore = 20m * directionalClose;

                var directionalImpulse = candidate.Direction == NewYorkResearchDirection.Long
                    ? setup.Close - prior.Close
                    : prior.Close - setup.Close;
                var impulseRatio = Math.Max(0m, directionalImpulse) / openingRange;
                var impulseScore = 20m * Math.Min(1m, impulseRatio / 0.20m);

                var directionalSeparation = candidate.Direction == NewYorkResearchDirection.Long
                    ? setup.Close - signal.Close
                    : signal.Close - setup.Close;
                var separationRatio = Math.Max(0m, directionalSeparation) / openingRange;
                var separationScore = 10m * Math.Min(1m, separationRatio / 0.25m);

                var total = Math.Min(100m, riskScore + bodyScore + closeScore + impulseScore + separationScore);
                var grade = total >= config.GradeAThreshold ? NewYorkSetupQualityGrade.A
                    : total >= config.GradeBThreshold ? NewYorkSetupQualityGrade.B : NewYorkSetupQualityGrade.C;

                var path = session.Where(x => x.Bar.TimestampUtc >= candidate.EntryUtc.Value && x.Local.TimeOfDay < OutcomeEnd)
                    .OrderBy(x => x.Local).ToList();
                ResolvePath(path, candidate.EntryPrice, candidate.StopPrice, candidate.Direction,
                    out var hit300, out var hit500, out var hit1000, out var stop);

                result.Add(new NewYorkSetupQualityOutcome(candidate.SessionDateCentral, candidate.EntryType, candidate.Direction,
                    candidate.EntryUtc.Value, riskTicks, riskScore, bodyScore, closeScore, impulseScore, separationScore,
                    total, grade, hit300, hit500, hit1000, stop));
            }
            return result;
        }

        private decimal ScoreRisk(decimal riskTicks)
        {
            if (riskTicks <= 100m) return 30m;
            if (riskTicks <= 150m) return 27m;
            if (riskTicks <= 200m) return 24m;
            if (riskTicks <= 250m) return 20m;
            if (riskTicks <= 300m) return 15m;
            if (riskTicks <= 400m) return 8m;
            return 0m;
        }

        private void ResolvePath(IReadOnlyList<LocalBar> bars, decimal entryPrice, decimal stopPrice,
            NewYorkResearchDirection direction, out DateTimeOffset? hit300, out DateTimeOffset? hit500,
            out DateTimeOffset? hit1000, out DateTimeOffset? stop)
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

        private static decimal Clamp01(decimal value) => value < 0m ? 0m : value > 1m ? 1m : value;

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