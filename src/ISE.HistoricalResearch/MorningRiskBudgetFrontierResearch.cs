using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public sealed class MorningRiskBudgetFrontierPoint
    {
        public MorningRiskBudgetFrontierPoint(
            decimal riskBudgetDollars,
            MorningRiskSizedExecutionLifecycleResult lifecycle)
        {
            if (riskBudgetDollars <= 0m)
                throw new ArgumentOutOfRangeException(nameof(riskBudgetDollars));

            RiskBudgetDollars = riskBudgetDollars;
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        public decimal RiskBudgetDollars { get; }
        public MorningRiskSizedExecutionLifecycleResult Lifecycle { get; }
    }

    /// <summary>
    /// V7.7 research-only Risk Budget Frontier.
    ///
    /// This analyzer does not optimize or select a production risk budget.
    /// It replays the same frozen signal/management architecture under a predetermined
    /// set of strict 2/1/0 account-risk budgets so that risk/return tradeoffs can be
    /// observed without changing the structural stop.
    ///
    /// Frozen:
    /// - Entry Efficiency >= 70
    /// - V5.6 Potential >= 80
    /// - one position at a time
    /// - maximum two executed attempts
    /// - V7.3 protected management
    /// - pre-extension BE disabled
    /// - structural stop unchanged
    ///
    /// Variable:
    /// - account risk budget only.
    /// </summary>
    public sealed class MorningRiskBudgetFrontierAnalyzer
    {
        private readonly decimal dollarsPerTickPerContract;
        private readonly int maximumContracts;

        public MorningRiskBudgetFrontierAnalyzer(
            decimal dollarsPerTickPerContract = 0.50m,
            int maximumContracts = 2)
        {
            if (dollarsPerTickPerContract <= 0m)
                throw new ArgumentOutOfRangeException(nameof(dollarsPerTickPerContract));

            if (maximumContracts < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumContracts));

            this.dollarsPerTickPerContract = dollarsPerTickPerContract;
            this.maximumContracts = maximumContracts;
        }

        public IReadOnlyList<MorningRiskBudgetFrontierPoint> Analyze(
            IReadOnlyList<HistoricalBar> oneMinuteBars,
            IReadOnlyList<MorningDailySequencingCandidate> candidates,
            IEnumerable<decimal> riskBudgetsDollars,
            int maximumAttempts = 2,
            decimal highEntryMinimum = 70m,
            decimal upperPotentialMinimum = 80m)
        {
            if (oneMinuteBars == null)
                throw new ArgumentNullException(nameof(oneMinuteBars));

            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            if (riskBudgetsDollars == null)
                throw new ArgumentNullException(nameof(riskBudgetsDollars));

            var budgets = riskBudgetsDollars
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (budgets.Count == 0)
                throw new ArgumentException(
                    "At least one risk budget is required.",
                    nameof(riskBudgetsDollars));

            if (budgets.Any(x => x <= 0m))
                throw new ArgumentOutOfRangeException(
                    nameof(riskBudgetsDollars),
                    "Risk budgets must be positive.");

            var result = new List<MorningRiskBudgetFrontierPoint>();

            foreach (var budget in budgets)
            {
                var replay = new MorningRiskControlDecompositionAnalyzer(
                    budget,
                    dollarsPerTickPerContract)
                    .Replay(
                        oneMinuteBars,
                        candidates,
                        MorningRiskControlPolicy.StrictTwoOneZero,
                        maximumAttempts,
                        highEntryMinimum,
                        upperPotentialMinimum);

                result.Add(new MorningRiskBudgetFrontierPoint(
                    budget,
                    replay));
            }

            return result;
        }

        public int ResolveQuantity(
            decimal initialRiskTicks,
            decimal riskBudgetDollars)
        {
            if (riskBudgetDollars <= 0m)
                throw new ArgumentOutOfRangeException(nameof(riskBudgetDollars));

            if (initialRiskTicks <= 0m)
                return 0;

            var riskPerContract =
                initialRiskTicks * dollarsPerTickPerContract;

            if (riskPerContract <= 0m)
                return 0;

            var affordable =
                (int)Math.Floor(riskBudgetDollars / riskPerContract);

            if (affordable < 0)
                affordable = 0;

            if (affordable > maximumContracts)
                affordable = maximumContracts;

            return affordable;
        }
    }
}
