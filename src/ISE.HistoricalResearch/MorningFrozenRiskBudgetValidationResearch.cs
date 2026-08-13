using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningFrozenRiskBudgetProfileKind
    {
        Funded175 = 0,
        Combine250 = 1
    }

    public enum MorningValidationWindowClassification
    {
        Empty = 0,
        PreCalibration = 1,
        OverlapsCalibration = 2,
        PostCalibration = 3
    }

    public sealed class MorningFrozenRiskBudgetValidationConfig
    {
        public MorningFrozenRiskBudgetValidationConfig()
        {
            CalibrationStartCentral = new DateTime(2026, 3, 25);
            CalibrationEndCentral = new DateTime(2026, 7, 31);
            FundedRiskBudgetDollars = 175m;
            CombineRiskBudgetDollars = 250m;
            DollarsPerTickPerContract = 0.50m;
            MaximumContracts = 2;
            MaximumAttempts = 2;
            EntryEfficiencyMinimum = 70m;
            PotentialMinimum = 80m;
        }

        public DateTime CalibrationStartCentral { get; }
        public DateTime CalibrationEndCentral { get; }
        public decimal FundedRiskBudgetDollars { get; }
        public decimal CombineRiskBudgetDollars { get; }
        public decimal DollarsPerTickPerContract { get; }
        public int MaximumContracts { get; }
        public int MaximumAttempts { get; }
        public decimal EntryEfficiencyMinimum { get; }
        public decimal PotentialMinimum { get; }

        public decimal BudgetFor(MorningFrozenRiskBudgetProfileKind profile)
        {
            return profile == MorningFrozenRiskBudgetProfileKind.Funded175
                ? FundedRiskBudgetDollars
                : CombineRiskBudgetDollars;
        }
    }

    public sealed class MorningFrozenRiskBudgetValidationResult
    {
        public MorningFrozenRiskBudgetValidationResult(
            MorningFrozenRiskBudgetProfileKind profile,
            decimal riskBudgetDollars,
            MorningValidationWindowClassification windowClassification,
            DateTime firstSessionCentral,
            DateTime lastSessionCentral,
            int sessionCount,
            MorningRiskSizedExecutionLifecycleResult lifecycle)
        {
            Profile = profile;
            RiskBudgetDollars = riskBudgetDollars;
            WindowClassification = windowClassification;
            FirstSessionCentral = firstSessionCentral;
            LastSessionCentral = lastSessionCentral;
            SessionCount = sessionCount;
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        public MorningFrozenRiskBudgetProfileKind Profile { get; }
        public decimal RiskBudgetDollars { get; }
        public MorningValidationWindowClassification WindowClassification { get; }
        public DateTime FirstSessionCentral { get; }
        public DateTime LastSessionCentral { get; }
        public int SessionCount { get; }
        public MorningRiskSizedExecutionLifecycleResult Lifecycle { get; }
    }

    /// <summary>
    /// V7.8 validation-only wrapper for the two V7.7 frozen candidate profiles.
    ///
    /// No tuning is allowed:
    /// - Funded candidate = strict 2/1/0 at $175.
    /// - Combine candidate = strict 2/1/0 at $250.
    /// - Entry Efficiency >=70.
    /// - frozen V5.6 Potential >=80.
    /// - one position at a time.
    /// - max two executed attempts.
    /// - V7.3 management with pre-extension BE disabled.
    /// - structural stop unchanged.
    ///
    /// A dataset is accepted as independent validation only when its complete
    /// trading-day range lies entirely before 2026-03-25 or entirely after
    /// 2026-07-31. Any overlap with the V7.7 calibration window is rejected.
    /// </summary>
    public sealed class MorningFrozenRiskBudgetValidationAnalyzer
    {
        private readonly MorningFrozenRiskBudgetValidationConfig config;

        public MorningFrozenRiskBudgetValidationAnalyzer(
            MorningFrozenRiskBudgetValidationConfig? config = null)
        {
            this.config = config ?? new MorningFrozenRiskBudgetValidationConfig();
        }

        public MorningValidationWindowClassification ClassifyWindow(
            IReadOnlyList<HistoricalBar> bars)
        {
            if (bars == null)
                throw new ArgumentNullException(nameof(bars));

            if (bars.Count == 0)
                return MorningValidationWindowClassification.Empty;

            var first = bars.Min(x => x.TradingDay.Date);
            var last = bars.Max(x => x.TradingDay.Date);

            if (last < config.CalibrationStartCentral)
                return MorningValidationWindowClassification.PreCalibration;

            if (first > config.CalibrationEndCentral)
                return MorningValidationWindowClassification.PostCalibration;

            return MorningValidationWindowClassification.OverlapsCalibration;
        }

        public bool IsIndependent(
            IReadOnlyList<HistoricalBar> bars)
        {
            var classification = ClassifyWindow(bars);

            return classification == MorningValidationWindowClassification.PreCalibration
                || classification == MorningValidationWindowClassification.PostCalibration;
        }

        public MorningFrozenRiskBudgetValidationResult Validate(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<MorningDailySequencingCandidate> candidates,
            MorningFrozenRiskBudgetProfileKind profile)
        {
            if (bars == null)
                throw new ArgumentNullException(nameof(bars));

            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            if (bars.Count == 0)
                throw new ArgumentException(
                    "Validation bars cannot be empty.",
                    nameof(bars));

            var classification = ClassifyWindow(bars);

            if (classification == MorningValidationWindowClassification.OverlapsCalibration)
            {
                throw new InvalidOperationException(
                    "V7.8 validation dataset overlaps the V7.7 calibration window 2026-03-25 through 2026-07-31.");
            }

            var budget = config.BudgetFor(profile);

            var lifecycle = new MorningRiskControlDecompositionAnalyzer(
                budget,
                config.DollarsPerTickPerContract)
                .Replay(
                    bars,
                    candidates,
                    MorningRiskControlPolicy.StrictTwoOneZero,
                    config.MaximumAttempts,
                    config.EntryEfficiencyMinimum,
                    config.PotentialMinimum);

            var sessionDates = candidates
                .Select(x => x.SessionDateCentral.Date)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var firstSession = sessionDates.Count > 0
                ? sessionDates[0]
                : bars.Min(x => x.TradingDay.Date);

            var lastSession = sessionDates.Count > 0
                ? sessionDates[sessionDates.Count - 1]
                : bars.Max(x => x.TradingDay.Date);

            return new MorningFrozenRiskBudgetValidationResult(
                profile,
                budget,
                classification,
                firstSession,
                lastSession,
                sessionDates.Count,
                lifecycle);
        }
    }
}
