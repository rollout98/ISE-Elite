using System;
using System.Collections.Generic;
using ISE.ExecutionOrchestrator;
using ISE.InstitutionalDecisionEngine;
using ISE.PositionSizingIntelligence;
using InstitutionalEngine = ISE.InstitutionalDecisionEngine.InstitutionalDecisionEngine;
using RuntimeOrchestrator = ISE.ExecutionOrchestrator.ExecutionOrchestrator;

namespace ISE.TradingBrain;

public sealed class IntegratedTradingBrainInput
{
    public InstitutionalDecisionInput InstitutionalDecision { get; set; } = new InstitutionalDecisionInput();
    public int MaximumContracts { get; set; }
    public double AdaptiveRiskMultiplier { get; set; }
    public double StopDistanceRisk { get; set; }
    public double LiquidityCapacity { get; set; }
    public double AccountPressure { get; set; }
    public bool PositionOpen { get; set; }
    public LiveTradeDirective LiveTradeDirective { get; set; }
    public bool ExecutionReady { get; set; }
    public bool OrderChannelAvailable { get; set; }
    public bool DuplicateCommandPending { get; set; }
    public bool AuthoritativeRiskBlock { get; set; }
}

public sealed class IntegratedTradingBrainDecision
{
    public IntegratedTradingBrainDecision(
        InstitutionalDecision institutionalDecision,
        PositionSizingDecision positionSizingDecision,
        ExecutionOrchestratorDecision executionDecision,
        IReadOnlyList<string> reasons)
    {
        InstitutionalDecision = institutionalDecision;
        PositionSizingDecision = positionSizingDecision;
        ExecutionDecision = executionDecision;
        Reasons = reasons;
    }

    public InstitutionalDecision InstitutionalDecision { get; }
    public PositionSizingDecision PositionSizingDecision { get; }
    public ExecutionOrchestratorDecision ExecutionDecision { get; }
    public IReadOnlyList<string> Reasons { get; }
}

/// <summary>
/// Composes specialist decisions into one deterministic runtime recommendation.
/// It does not place orders or duplicate specialist calculations.
/// </summary>
public sealed class IntegratedTradingBrain
{
    private readonly InstitutionalEngine _institutionalDecisionEngine;
    private readonly PositionSizingIntelligenceEngine _positionSizingEngine;
    private readonly RuntimeOrchestrator _executionOrchestrator;

    public IntegratedTradingBrain()
        : this(
            new InstitutionalEngine(),
            new PositionSizingIntelligenceEngine(),
            new RuntimeOrchestrator())
    {
    }

    public IntegratedTradingBrain(
        InstitutionalEngine institutionalDecisionEngine,
        PositionSizingIntelligenceEngine positionSizingEngine,
        RuntimeOrchestrator executionOrchestrator)
    {
        _institutionalDecisionEngine = institutionalDecisionEngine ?? throw new ArgumentNullException(nameof(institutionalDecisionEngine));
        _positionSizingEngine = positionSizingEngine ?? throw new ArgumentNullException(nameof(positionSizingEngine));
        _executionOrchestrator = executionOrchestrator ?? throw new ArgumentNullException(nameof(executionOrchestrator));
    }

    public IntegratedTradingBrainDecision Evaluate(IntegratedTradingBrainInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.InstitutionalDecision == null) throw new ArgumentNullException(nameof(input.InstitutionalDecision));

        input.InstitutionalDecision.AuthoritativeRiskBlock =
            input.InstitutionalDecision.AuthoritativeRiskBlock || input.AuthoritativeRiskBlock;

        var institutional = _institutionalDecisionEngine.Evaluate(input.InstitutionalDecision);
        var participationApproved = institutional.Action == InstitutionalDecisionAction.Execute ||
                                    institutional.Action == InstitutionalDecisionAction.ExecuteReduced;

        var sizing = _positionSizingEngine.Evaluate(new PositionSizingInput
        {
            MaximumContracts = input.MaximumContracts,
            ParticipationMultiplier = institutional.ParticipationMultiplier,
            AdaptiveRiskMultiplier = input.AdaptiveRiskMultiplier,
            StopDistanceRisk = input.StopDistanceRisk,
            LiquidityCapacity = input.LiquidityCapacity,
            AccountPressure = input.AccountPressure,
            AuthoritativeRiskBlock = input.AuthoritativeRiskBlock || institutional.Action == InstitutionalDecisionAction.Blocked
        });

        var execution = _executionOrchestrator.Evaluate(new ExecutionOrchestratorInput
        {
            PositionOpen = input.PositionOpen,
            ParticipationApproved = participationApproved,
            ExecutionReady = input.ExecutionReady,
            RecommendedContracts = sizing.Contracts,
            LiveTradeDirective = input.LiveTradeDirective,
            OrderChannelAvailable = input.OrderChannelAvailable,
            DuplicateCommandPending = input.DuplicateCommandPending,
            AuthoritativeRiskBlock = input.AuthoritativeRiskBlock || institutional.Action == InstitutionalDecisionAction.Blocked
        });

        var reasons = new List<string>();
        reasons.AddRange(institutional.Reasons);
        reasons.AddRange(sizing.Reasons);
        reasons.AddRange(execution.Reasons);

        return new IntegratedTradingBrainDecision(institutional, sizing, execution, reasons);
    }
}
