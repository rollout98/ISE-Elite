using System;
using System.Collections.Generic;

namespace ISE.TradeManagementIntelligence
{
    public enum TradeManagementAction { Hold, Protect, Trail, Reduce, Exit, Blocked }

    public sealed class TradeManagementInput
    {
        public TradeManagementInput(int thesisHealth, int momentum, int structureIntegrity,
            decimal targetProgress, decimal favorableExcursion, decimal adverseExcursion,
            bool breakEvenEligible, bool trailingEligible, bool authoritativeRiskBlock = false)
        {
            ThesisHealth = Score(thesisHealth, nameof(thesisHealth));
            Momentum = Score(momentum, nameof(momentum));
            StructureIntegrity = Score(structureIntegrity, nameof(structureIntegrity));
            if (targetProgress < 0 || targetProgress > 1) throw new ArgumentOutOfRangeException(nameof(targetProgress));
            if (favorableExcursion < 0) throw new ArgumentOutOfRangeException(nameof(favorableExcursion));
            if (adverseExcursion < 0) throw new ArgumentOutOfRangeException(nameof(adverseExcursion));
            TargetProgress = targetProgress;
            FavorableExcursion = favorableExcursion;
            AdverseExcursion = adverseExcursion;
            BreakEvenEligible = breakEvenEligible;
            TrailingEligible = trailingEligible;
            AuthoritativeRiskBlock = authoritativeRiskBlock;
        }

        public int ThesisHealth { get; }
        public int Momentum { get; }
        public int StructureIntegrity { get; }
        public decimal TargetProgress { get; }
        public decimal FavorableExcursion { get; }
        public decimal AdverseExcursion { get; }
        public bool BreakEvenEligible { get; }
        public bool TrailingEligible { get; }
        public bool AuthoritativeRiskBlock { get; }

        private static int Score(int value, string name)
        {
            if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(name);
            return value;
        }
    }

    public sealed class TradeManagementDecision
    {
        public TradeManagementDecision(TradeManagementAction action, bool moveToBreakEven,
            bool trailStop, decimal reduceFraction, int confidence, IReadOnlyList<string> reasons)
        {
            Action = action;
            MoveToBreakEven = moveToBreakEven;
            TrailStop = trailStop;
            ReduceFraction = reduceFraction;
            Confidence = confidence;
            Reasons = reasons;
        }

        public TradeManagementAction Action { get; }
        public bool MoveToBreakEven { get; }
        public bool TrailStop { get; }
        public decimal ReduceFraction { get; }
        public int Confidence { get; }
        public IReadOnlyList<string> Reasons { get; }
    }

    public sealed class TradeManagementIntelligenceEngine
    {
        public TradeManagementDecision Evaluate(TradeManagementInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var reasons = new List<string>();

            if (input.AuthoritativeRiskBlock)
                return new TradeManagementDecision(TradeManagementAction.Blocked, false, false, 1m, 100,
                    new[] { "Authoritative risk control requires immediate position closure." });

            int confidence = (input.ThesisHealth * 4 + input.Momentum * 3 + input.StructureIntegrity * 3) / 10;

            if (input.ThesisHealth < 35 || input.StructureIntegrity < 30 || input.AdverseExcursion > input.FavorableExcursion * 1.5m + 1m)
            {
                reasons.Add("The active trade thesis or market structure is no longer valid.");
                return new TradeManagementDecision(TradeManagementAction.Exit, false, false, 1m, confidence, reasons);
            }

            if (input.ThesisHealth < 55 || input.Momentum < 40)
            {
                reasons.Add("Evidence has weakened while the thesis remains partially valid.");
                return new TradeManagementDecision(TradeManagementAction.Reduce, input.BreakEvenEligible, false, 0.5m, confidence, reasons);
            }

            if (input.TargetProgress >= 0.70m && input.TrailingEligible)
            {
                reasons.Add("Target progress and favorable excursion justify protecting open profit.");
                return new TradeManagementDecision(TradeManagementAction.Trail, input.BreakEvenEligible, true, 0m, confidence, reasons);
            }

            if (input.TargetProgress >= 0.35m && input.BreakEvenEligible)
            {
                reasons.Add("The trade has progressed enough to remove initial risk.");
                return new TradeManagementDecision(TradeManagementAction.Protect, true, false, 0m, confidence, reasons);
            }

            reasons.Add("The thesis, momentum, and structure remain healthy.");
            return new TradeManagementDecision(TradeManagementAction.Hold, false, false, 0m, confidence, reasons);
        }
    }
}
