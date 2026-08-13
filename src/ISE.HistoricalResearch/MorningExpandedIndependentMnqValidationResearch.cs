using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningIndependentMnqWindowKind
    {
        PreCalibration = 0,
        PostCalibration = 1
    }

    public sealed class MorningExpandedIndependentMnqValidationConfig
    {
        public MorningExpandedIndependentMnqValidationConfig()
        {
            PreCalibrationEvaluationStartCentral = new DateTime(2025, 12, 1);
            PreCalibrationEvaluationEndExclusiveCentral = new DateTime(2026, 3, 25);
            PostCalibrationEvaluationStartCentral = new DateTime(2026, 8, 1);

            FundedRiskBudgetDollars = 175m;
            CombineRiskBudgetDollars = 250m;
            DollarsPerTickPerContract = 0.50m;
            MaximumAttempts = 2;
            EntryEfficiencyMinimum = 70m;
            PotentialMinimum = 80m;
        }

        public DateTime PreCalibrationEvaluationStartCentral { get; }
        public DateTime PreCalibrationEvaluationEndExclusiveCentral { get; }
        public DateTime PostCalibrationEvaluationStartCentral { get; }

        public decimal FundedRiskBudgetDollars { get; }
        public decimal CombineRiskBudgetDollars { get; }
        public decimal DollarsPerTickPerContract { get; }
        public int MaximumAttempts { get; }
        public decimal EntryEfficiencyMinimum { get; }
        public decimal PotentialMinimum { get; }

        public decimal BudgetFor(
            MorningFrozenRiskBudgetProfileKind profile)
        {
            return profile == MorningFrozenRiskBudgetProfileKind.Funded175
                ? FundedRiskBudgetDollars
                : CombineRiskBudgetDollars;
        }
    }

    public sealed class MorningExpandedIndependentMnqWindowResult
    {
        public MorningExpandedIndependentMnqWindowResult(
            MorningIndependentMnqWindowKind window,
            MorningFrozenRiskBudgetProfileKind profile,
            decimal riskBudgetDollars,
            DateTime firstEvaluationSessionCentral,
            DateTime lastEvaluationSessionCentral,
            int evaluationSessionCount,
            int evaluationCandidateCount,
            MorningRiskSizedExecutionLifecycleResult lifecycle)
        {
            Window = window;
            Profile = profile;
            RiskBudgetDollars = riskBudgetDollars;
            FirstEvaluationSessionCentral = firstEvaluationSessionCentral;
            LastEvaluationSessionCentral = lastEvaluationSessionCentral;
            EvaluationSessionCount = evaluationSessionCount;
            EvaluationCandidateCount = evaluationCandidateCount;
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        public MorningIndependentMnqWindowKind Window { get; }
        public MorningFrozenRiskBudgetProfileKind Profile { get; }
        public decimal RiskBudgetDollars { get; }
        public DateTime FirstEvaluationSessionCentral { get; }
        public DateTime LastEvaluationSessionCentral { get; }
        public int EvaluationSessionCount { get; }
        public int EvaluationCandidateCount { get; }
        public MorningRiskSizedExecutionLifecycleResult Lifecycle { get; }
    }

    /// <summary>
    /// V7.8.2 validation-only coordinator for two independent MNQ windows.
    ///
    /// Pre-calibration:
    ///   warmup bars may begin before 2025-12-01;
    ///   evaluation candidates are restricted to 2025-12-01 through 2026-03-24.
    ///
    /// Post-calibration:
    ///   earlier bars may be used as causal warmup/context;
    ///   evaluation candidates are restricted to 2026-08-01 and later.
    ///
    /// Frozen profiles:
    ///   Funded = strict 2/1/0 at $175.
    ///   Combine = strict 2/1/0 at $250.
    ///
    /// No tuning, structural-stop movement, entry-threshold changes,
    /// Potential changes, Core changes, or Runner changes are allowed.
    /// </summary>
    public sealed class MorningExpandedIndependentMnqValidationAnalyzer
    {
        private readonly MorningExpandedIndependentMnqValidationConfig config;

        public MorningExpandedIndependentMnqValidationAnalyzer(
            MorningExpandedIndependentMnqValidationConfig? config = null)
        {
            this.config = config
                ?? new MorningExpandedIndependentMnqValidationConfig();
        }

        public void RequireMnq(
            IReadOnlyList<HistoricalBar> bars)
        {
            if (bars == null)
                throw new ArgumentNullException(nameof(bars));

            if (bars.Count == 0)
                throw new ArgumentException(
                    "MNQ validation bars cannot be empty.",
                    nameof(bars));

            var invalid = bars
                .Where(x =>
                    string.IsNullOrWhiteSpace(x.Instrument)
                    || !x.Instrument.StartsWith(
                        "MNQ",
                        StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Instrument)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (invalid.Count > 0)
            {
                throw new InvalidOperationException(
                    "V7.8.2 requires MNQ data only. Non-MNQ instrument(s): "
                    + string.Join(", ", invalid));
            }
        }

        public IReadOnlyList<MorningDailySequencingCandidate> EvaluationCandidates(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<MorningDailySequencingCandidate> allCandidates,
            MorningIndependentMnqWindowKind window)
        {
            RequireMnq(bars);

            if (allCandidates == null)
                throw new ArgumentNullException(nameof(allCandidates));

            IEnumerable<MorningDailySequencingCandidate> selected;

            if (window == MorningIndependentMnqWindowKind.PreCalibration)
            {
                selected = allCandidates.Where(x =>
                    x.SessionDateCentral.Date
                        >= config.PreCalibrationEvaluationStartCentral
                    && x.SessionDateCentral.Date
                        < config.PreCalibrationEvaluationEndExclusiveCentral);
            }
            else
            {
                selected = allCandidates.Where(x =>
                    x.SessionDateCentral.Date
                        >= config.PostCalibrationEvaluationStartCentral);
            }

            return selected
                .OrderBy(x => x.EntryUtc)
                .ToList();
        }

        public MorningExpandedIndependentMnqWindowResult Validate(
            IReadOnlyList<HistoricalBar> fullWarmupAndEvaluationBars,
            IReadOnlyList<MorningDailySequencingCandidate> allCandidates,
            MorningIndependentMnqWindowKind window,
            MorningFrozenRiskBudgetProfileKind profile)
        {
            if (fullWarmupAndEvaluationBars == null)
                throw new ArgumentNullException(
                    nameof(fullWarmupAndEvaluationBars));

            if (allCandidates == null)
                throw new ArgumentNullException(nameof(allCandidates));

            RequireMnq(fullWarmupAndEvaluationBars);

            var evaluationCandidates = EvaluationCandidates(
                fullWarmupAndEvaluationBars,
                allCandidates,
                window);

            var sessions = evaluationCandidates
                .Select(x => x.SessionDateCentral.Date)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (sessions.Count == 0)
            {
                throw new InvalidOperationException(
                    "The requested V7.8.2 independent MNQ evaluation window produced no candidates.");
            }

            var budget = config.BudgetFor(profile);

            var lifecycle =
                new MorningRiskControlDecompositionAnalyzer(
                    budget,
                    config.DollarsPerTickPerContract)
                .Replay(
                    fullWarmupAndEvaluationBars,
                    evaluationCandidates,
                    MorningRiskControlPolicy.StrictTwoOneZero,
                    config.MaximumAttempts,
                    config.EntryEfficiencyMinimum,
                    config.PotentialMinimum);

            return new MorningExpandedIndependentMnqWindowResult(
                window,
                profile,
                budget,
                sessions[0],
                sessions[sessions.Count - 1],
                sessions.Count,
                evaluationCandidates.Count,
                lifecycle);
        }
    }
}
