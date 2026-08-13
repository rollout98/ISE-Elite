using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class MorningMnqPostCalibrationValidationConfig
    {
        public MorningMnqPostCalibrationValidationConfig()
        {
            EvaluationStartCentral = new DateTime(2026, 8, 1);
            FundedRiskBudgetDollars = 175m;
            CombineRiskBudgetDollars = 250m;
            DollarsPerTickPerContract = 0.50m;
            MaximumAttempts = 2;
            EntryEfficiencyMinimum = 70m;
            PotentialMinimum = 80m;
        }

        public DateTime EvaluationStartCentral { get; }
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

    public sealed class MorningMnqPostCalibrationValidationResult
    {
        public MorningMnqPostCalibrationValidationResult(
            MorningFrozenRiskBudgetProfileKind profile,
            decimal riskBudgetDollars,
            DateTime evaluationStartCentral,
            DateTime firstEvaluationSessionCentral,
            DateTime lastEvaluationSessionCentral,
            int evaluationSessionCount,
            int evaluationCandidateCount,
            MorningRiskSizedExecutionLifecycleResult lifecycle)
        {
            Profile = profile;
            RiskBudgetDollars = riskBudgetDollars;
            EvaluationStartCentral = evaluationStartCentral;
            FirstEvaluationSessionCentral = firstEvaluationSessionCentral;
            LastEvaluationSessionCentral = lastEvaluationSessionCentral;
            EvaluationSessionCount = evaluationSessionCount;
            EvaluationCandidateCount = evaluationCandidateCount;
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        public MorningFrozenRiskBudgetProfileKind Profile { get; }
        public decimal RiskBudgetDollars { get; }
        public DateTime EvaluationStartCentral { get; }
        public DateTime FirstEvaluationSessionCentral { get; }
        public DateTime LastEvaluationSessionCentral { get; }
        public int EvaluationSessionCount { get; }
        public int EvaluationCandidateCount { get; }
        public MorningRiskSizedExecutionLifecycleResult Lifecycle { get; }
    }

    /// <summary>
    /// V7.8.1 true MNQ post-calibration validation.
    ///
    /// Earlier MNQ bars are permitted strictly as causal warmup/context.
    /// Only candidates whose Central session date is on or after 2026-08-01
    /// are eligible for evaluation. The evaluation profile is frozen:
    /// Funded=$175 and Combine=$250, strict 2/1/0 risk control.
    ///
    /// This analyzer rejects non-MNQ data. It does not retune entry,
    /// Potential, structural stop, Core, Runner, or risk budgets.
    /// </summary>
    public sealed class MorningMnqPostCalibrationValidationAnalyzer
    {
        private readonly MorningMnqPostCalibrationValidationConfig config;

        public MorningMnqPostCalibrationValidationAnalyzer(
            MorningMnqPostCalibrationValidationConfig? config = null)
        {
            this.config = config
                ?? new MorningMnqPostCalibrationValidationConfig();
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
                    "V7.8.1 requires MNQ data only. Non-MNQ instrument(s): "
                    + string.Join(", ", invalid));
            }
        }

        public IReadOnlyList<MorningDailySequencingCandidate> EvaluationCandidates(
            IReadOnlyList<HistoricalBar> bars,
            IReadOnlyList<MorningDailySequencingCandidate> allCandidates)
        {
            RequireMnq(bars);

            if (allCandidates == null)
                throw new ArgumentNullException(nameof(allCandidates));

            return allCandidates
                .Where(x =>
                    x.SessionDateCentral.Date
                    >= config.EvaluationStartCentral)
                .OrderBy(x => x.EntryUtc)
                .ToList();
        }

        public MorningMnqPostCalibrationValidationResult Validate(
            IReadOnlyList<HistoricalBar> fullWarmupAndEvaluationBars,
            IReadOnlyList<MorningDailySequencingCandidate> allCandidates,
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
                allCandidates);

            var sessionDates = evaluationCandidates
                .Select(x => x.SessionDateCentral.Date)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (sessionDates.Count == 0)
            {
                throw new InvalidOperationException(
                    "No post-calibration MNQ candidates exist on or after 2026-08-01.");
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

            return new MorningMnqPostCalibrationValidationResult(
                profile,
                budget,
                config.EvaluationStartCentral,
                sessionDates[0],
                sessionDates[sessionDates.Count - 1],
                sessionDates.Count,
                evaluationCandidates.Count,
                lifecycle);
        }
    }
}
