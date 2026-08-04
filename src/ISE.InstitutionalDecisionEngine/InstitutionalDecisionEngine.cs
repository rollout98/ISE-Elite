using System;
using System.Collections.Generic;

namespace ISE.InstitutionalDecisionEngine;

public enum InstitutionalDecisionAction
{
    Execute,
    ExecuteReduced,
    Wait,
    StandAside,
    Blocked
}

public enum OpportunityGrade
{
    APlus,
    A,
    B,
    C,
    Rejected
}

public sealed class InstitutionalDecisionInput
{
    public double MarketQuality { get; set; }
    public double ContextQuality { get; set; }
    public double NarrativeQuality { get; set; }
    public double HistoricalEvidence { get; set; }
    public double TimeframeAlignment { get; set; }
    public double ExecutionQuality { get; set; }
    public double DecisionConfidence { get; set; }
    public double RiskMultiplier { get; set; }
    public bool ContextTransitioning { get; set; }
    public bool AuthoritativeRiskBlock { get; set; }
}

public sealed class InstitutionalDecision
{
    public InstitutionalDecision(
        InstitutionalDecisionAction action,
        OpportunityGrade grade,
        double compositeScore,
        double participationMultiplier,
        IReadOnlyList<string> reasons)
    {
        Action = action;
        Grade = grade;
        CompositeScore = compositeScore;
        ParticipationMultiplier = participationMultiplier;
        Reasons = reasons;
    }

    public InstitutionalDecisionAction Action { get; }
    public OpportunityGrade Grade { get; }
    public double CompositeScore { get; }
    public double ParticipationMultiplier { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class InstitutionalDecisionEngine
{
    public InstitutionalDecision Evaluate(InstitutionalDecisionInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        if (input.AuthoritativeRiskBlock || input.RiskMultiplier <= 0)
        {
            return new InstitutionalDecision(
                InstitutionalDecisionAction.Blocked,
                OpportunityGrade.Rejected,
                0,
                0,
                new[] { "Authoritative risk controls blocked participation." });
        }

        var reasons = new List<string>();
        var score = Clamp(
            (input.MarketQuality * 0.15) +
            (input.ContextQuality * 0.12) +
            (input.NarrativeQuality * 0.13) +
            (input.HistoricalEvidence * 0.10) +
            (input.TimeframeAlignment * 0.15) +
            (input.ExecutionQuality * 0.15) +
            (input.DecisionConfidence * 0.20));

        if (input.ContextTransitioning)
        {
            score = Clamp(score - 12);
            reasons.Add("Market context is transitioning; participation was delayed or reduced.");
        }

        var weakest = Math.Min(
            Math.Min(input.MarketQuality, input.ContextQuality),
            Math.Min(input.ExecutionQuality, input.TimeframeAlignment));

        reasons.Add($"Composite institutional quality is {score:0.0}.");
        reasons.Add($"Weakest required component is {weakest:0.0}.");

        if (score >= 90 && weakest >= 80 && input.RiskMultiplier >= 0.85 && !input.ContextTransitioning)
            return Decision(InstitutionalDecisionAction.Execute, OpportunityGrade.APlus, score, input.RiskMultiplier, reasons);

        if (score >= 82 && weakest >= 70 && !input.ContextTransitioning)
            return Decision(InstitutionalDecisionAction.Execute, OpportunityGrade.A, score, Math.Min(input.RiskMultiplier, 0.85), reasons);

        if (score >= 72 && weakest >= 60)
            return Decision(InstitutionalDecisionAction.ExecuteReduced, OpportunityGrade.B, score, Math.Min(input.RiskMultiplier, 0.60), reasons);

        if (score >= 62)
            return Decision(InstitutionalDecisionAction.Wait, OpportunityGrade.C, score, 0, reasons);

        return Decision(InstitutionalDecisionAction.StandAside, OpportunityGrade.Rejected, score, 0, reasons);
    }

    private static InstitutionalDecision Decision(
        InstitutionalDecisionAction action,
        OpportunityGrade grade,
        double score,
        double multiplier,
        IReadOnlyList<string> reasons) =>
        new InstitutionalDecision(action, grade, score, Clamp01(multiplier), reasons);

    private static double Clamp(double value) => Math.Max(0, Math.Min(100, value));
    private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
}
