using System;
using ISE.DailyControls;
using ISE.OpportunityScoring;
using ISE.Risk;
using ISE.TradePlanning;

namespace ISE.DecisionOrchestration;

/// <summary>Collects the authoritative decisions required for final trade authorization.</summary>
public sealed class DecisionOrchestrationInput
{
    /// <summary>Creates an orchestration input.</summary>
    public DecisionOrchestrationInput(bool strategyQualified, bool confirmationComplete, OpportunityScoreSnapshot opportunity, DailyControlDecision dailyControls, RiskDecision risk, TradePlan tradePlan)
    {
        Opportunity = opportunity ?? throw new ArgumentNullException(nameof(opportunity));
        DailyControls = dailyControls ?? throw new ArgumentNullException(nameof(dailyControls));
        Risk = risk ?? throw new ArgumentNullException(nameof(risk));
        TradePlan = tradePlan ?? throw new ArgumentNullException(nameof(tradePlan));
        StrategyQualified = strategyQualified;
        ConfirmationComplete = confirmationComplete;
    }

    /// <summary>Gets whether the Strategy Engine qualified the candidate.</summary>
    public bool StrategyQualified { get; }
    /// <summary>Gets whether all required confirmation is complete.</summary>
    public bool ConfirmationComplete { get; }
    /// <summary>Gets the opportunity-quality assessment.</summary>
    public OpportunityScoreSnapshot Opportunity { get; }
    /// <summary>Gets the current account-level daily control decision.</summary>
    public DailyControlDecision DailyControls { get; }
    /// <summary>Gets the Risk Engine decision.</summary>
    public RiskDecision Risk { get; }
    /// <summary>Gets the platform-independent trade plan.</summary>
    public TradePlan TradePlan { get; }
}
