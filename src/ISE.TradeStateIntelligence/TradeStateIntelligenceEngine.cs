using System;
using System.Collections.Generic;

namespace ISE.TradeStateIntelligence;

public enum TradeStateAction { Hold, Protect, Trail, Reduce, Exit, Blocked }
public enum TradeHealth { Strong, Stable, Weakening, Invalidated }

public sealed class TradeStateInput
{
    public double ThesisHealth { get; set; }
    public double MomentumHealth { get; set; }
    public double StructureIntegrity { get; set; }
    public double TargetProgress { get; set; }
    public double FavorableExcursion { get; set; }
    public double AdverseExcursion { get; set; }
    public bool AuthoritativeRiskBlock { get; set; }
}

public sealed class TradeStateDecision
{
    public TradeStateDecision(TradeStateAction action, TradeHealth health, bool moveToBreakEven, bool tightenStop, IReadOnlyList<string> reasons)
    {
        Action = action;
        Health = health;
        MoveToBreakEven = moveToBreakEven;
        TightenStop = tightenStop;
        Reasons = reasons;
    }

    public TradeStateAction Action { get; }
    public TradeHealth Health { get; }
    public bool MoveToBreakEven { get; }
    public bool TightenStop { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class TradeStateIntelligenceEngine
{
    public TradeStateDecision Evaluate(TradeStateInput input)
    {
        if (input.AuthoritativeRiskBlock)
            return new TradeStateDecision(TradeStateAction.Blocked, TradeHealth.Invalidated, false, true,
                new[] { "Authoritative risk control requires immediate protection." });

        var reasons = new List<string>();
        if (input.ThesisHealth < 35 || input.StructureIntegrity < 30)
            return new TradeStateDecision(TradeStateAction.Exit, TradeHealth.Invalidated, false, true,
                new[] { "The trade thesis or supporting structure is invalidated." });

        if (input.AdverseExcursion >= 75 || (input.ThesisHealth < 50 && input.MomentumHealth < 45))
            return new TradeStateDecision(TradeStateAction.Reduce, TradeHealth.Weakening, false, true,
                new[] { "Adverse pressure and weakening evidence require exposure reduction." });

        if (input.TargetProgress >= 85)
            return new TradeStateDecision(TradeStateAction.Trail, TradeHealth.Strong, true, true,
                new[] { "The trade is near its objective; protect gains with a trailing stop." });

        if (input.FavorableExcursion >= 45 || input.TargetProgress >= 50)
            return new TradeStateDecision(TradeStateAction.Protect, TradeHealth.Stable, true, false,
                new[] { "Meaningful progress supports moving protection to break-even." });

        var health = input.ThesisHealth >= 75 && input.MomentumHealth >= 65 && input.StructureIntegrity >= 70
            ? TradeHealth.Strong : TradeHealth.Stable;
        reasons.Add("The thesis and structure remain intact.");
        return new TradeStateDecision(TradeStateAction.Hold, health, false, false, reasons);
    }
}
