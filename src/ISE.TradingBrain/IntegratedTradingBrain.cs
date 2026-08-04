using System;
using System.Collections.Generic;
using ISE.ExecutionOrchestrator;
using ISE.InstitutionalDecisionEngine;
using ISE.PositionSizingIntelligence;
using ISE.TradeManagementIntelligence;
using ISE.TradeStateIntelligence;
using InstitutionalEngine = ISE.InstitutionalDecisionEngine.InstitutionalDecisionEngine;
using RuntimeOrchestrator = ISE.ExecutionOrchestrator.ExecutionOrchestrator;
using ManagementEngine = ISE.TradeManagementIntelligence.TradeManagementIntelligenceEngine;
using StateEngine = ISE.TradeStateIntelligence.TradeStateIntelligenceEngine;

namespace ISE.TradingBrain;

public sealed class IntegratedTradingBrainInput
{
    public InstitutionalDecisionInput InstitutionalDecision { get; set; } = new InstitutionalDecisionInput();
    public TradeStateInput LiveTradeState { get; set; } = new TradeStateInput();
    public TradeManagementInput LiveTradeManagement { get; set; } =
        new TradeManagementInput(100, 100, 100, 0m, 0m, 0m, false, false);
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
        TradeStateDecision tradeStateDecision,
        TradeManagementDecision tradeManagementDecision,
        IReadOnlyList<string> reasons)
    {
        InstitutionalDecision = institutionalDecision;
        PositionSizingDecision = positionSizingDecision;
        ExecutionDecision = executionDecision;
        TradeStateDecision = tradeStateDecision;
        TradeManagementDecision = tradeManagementDecision;
        Reasons = reasons;
    }

    public InstitutionalDecision InstitutionalDecision { get; }
    public PositionSizingDecision PositionSizingDecision { get; }
    public ExecutionOrchestratorDecision ExecutionDecision { get; }
    public TradeStateDecision TradeStateDecision { get; }
    public TradeManagementDecision TradeManagementDecision { get; }
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
    private readonly StateEngine _tradeStateEngine;
    private readonly ManagementEngine _tradeManagementEngine;

    public IntegratedTradingBrain()
        : this(
            new InstitutionalEngine(),
            new PositionSizingIntelligenceEngine(),
            new RuntimeOrchestrator(),
            new StateEngine(),
            new ManagementEngine())
    {
    }

    public IntegratedTradingBrain(
        InstitutionalEngine institutionalDecisionEngine,
        PositionSizingIntelligenceEngine positionSizingEngine,
        RuntimeOrchestrator executionOrchestrator,
        StateEngine tradeStateEngine,
        ManagementEngine tradeManagementEngine)
    {
        _institutionalDecisionEngine = institutionalDecisionEngine ?? throw new ArgumentNullException(nameof(institutionalDecisionEngine));
        _positionSizingEngine = positionSizingEngine ?? throw new ArgumentNullException(nameof(positionSizingEngine));
        _executionOrchestrator = executionOrchestrator ?? throw new ArgumentNullException(nameof(executionOrchestrator));
        _tradeStateEngine = tradeStateEngine ?? throw new ArgumentNullException(nameof(tradeStateEngine));
        _tradeManagementEngine = tradeManagementEngine ?? throw new ArgumentNullException(nameof(tradeManagementEngine));
    }

    public IntegratedTradingBrainDecision Evaluate(IntegratedTradingBrainInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.InstitutionalDecision == null) throw new ArgumentNullException(nameof(input.InstitutionalDecision));
        if (input.LiveTradeState == null) throw new ArgumentNullException(nameof(input.LiveTradeState));
        if (input.LiveTradeManagement == null) throw new ArgumentNullException(nameof(input.LiveTradeManagement));

        var authoritativeBlock = input.AuthoritativeRiskBlock ||
                                 input.InstitutionalDecision.AuthoritativeRiskBlock;

        input.InstitutionalDecision.AuthoritativeRiskBlock = authoritativeBlock;

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
            AuthoritativeRiskBlock = authoritativeBlock || institutional.Action == InstitutionalDecisionAction.Blocked
        });

        var tradeState = _tradeStateEngine.Evaluate(new TradeStateInput
        {
            ThesisHealth = input.LiveTradeState.ThesisHealth,
            MomentumHealth = input.LiveTradeState.MomentumHealth,
            StructureIntegrity = input.LiveTradeState.StructureIntegrity,
            TargetProgress = input.LiveTradeState.TargetProgress,
            FavorableExcursion = input.LiveTradeState.FavorableExcursion,
            AdverseExcursion = input.LiveTradeState.AdverseExcursion,
            AuthoritativeRiskBlock = authoritativeBlock
        });

        var liveManagement = input.LiveTradeManagement;
        var tradeManagement = _tradeManagementEngine.Evaluate(new TradeManagementInput(
            liveManagement.ThesisHealth,
            liveManagement.Momentum,
            liveManagement.StructureIntegrity,
            liveManagement.TargetProgress,
            liveManagement.FavorableExcursion,
            liveManagement.AdverseExcursion,
            liveManagement.BreakEvenEligible,
            liveManagement.TrailingEligible,
            authoritativeBlock));

        var derivedDirective = input.PositionOpen
            ? MostProtectiveDirective(tradeState.Action, tradeManagement.Action)
            : LiveTradeDirective.None;

        var effectiveDirective = input.LiveTradeDirective == LiveTradeDirective.None
            ? derivedDirective
            : MoreProtective(input.LiveTradeDirective, derivedDirective);

        var executionBlock = authoritativeBlock ||
                             institutional.Action == InstitutionalDecisionAction.Blocked ||
                             tradeState.Action == TradeStateAction.Blocked ||
                             tradeManagement.Action == TradeManagementAction.Blocked;

        var execution = _executionOrchestrator.Evaluate(new ExecutionOrchestratorInput
        {
            PositionOpen = input.PositionOpen,
            ParticipationApproved = participationApproved,
            ExecutionReady = input.ExecutionReady,
            RecommendedContracts = sizing.Contracts,
            LiveTradeDirective = effectiveDirective,
            OrderChannelAvailable = input.OrderChannelAvailable,
            DuplicateCommandPending = input.DuplicateCommandPending,
            AuthoritativeRiskBlock = executionBlock
        });

        var reasons = new List<string>();
        reasons.AddRange(institutional.Reasons);
        reasons.AddRange(sizing.Reasons);
        if (input.PositionOpen)
        {
            reasons.AddRange(tradeState.Reasons);
            reasons.AddRange(tradeManagement.Reasons);
        }
        reasons.AddRange(execution.Reasons);

        return new IntegratedTradingBrainDecision(
            institutional,
            sizing,
            execution,
            tradeState,
            tradeManagement,
            reasons);
    }

    private static LiveTradeDirective MostProtectiveDirective(
        TradeStateAction stateAction,
        TradeManagementAction managementAction)
    {
        return MoreProtective(Map(stateAction), Map(managementAction));
    }

    private static LiveTradeDirective Map(TradeStateAction action)
    {
        switch (action)
        {
            case TradeStateAction.Exit:
            case TradeStateAction.Blocked:
                return LiveTradeDirective.Exit;
            case TradeStateAction.Reduce:
                return LiveTradeDirective.Reduce;
            case TradeStateAction.Trail:
                return LiveTradeDirective.Trail;
            case TradeStateAction.Protect:
                return LiveTradeDirective.Protect;
            default:
                return LiveTradeDirective.Hold;
        }
    }

    private static LiveTradeDirective Map(TradeManagementAction action)
    {
        switch (action)
        {
            case TradeManagementAction.Exit:
            case TradeManagementAction.Blocked:
                return LiveTradeDirective.Exit;
            case TradeManagementAction.Reduce:
                return LiveTradeDirective.Reduce;
            case TradeManagementAction.Trail:
                return LiveTradeDirective.Trail;
            case TradeManagementAction.Protect:
                return LiveTradeDirective.Protect;
            default:
                return LiveTradeDirective.Hold;
        }
    }

    private static LiveTradeDirective MoreProtective(
        LiveTradeDirective first,
        LiveTradeDirective second)
    {
        return Priority(first) >= Priority(second) ? first : second;
    }

    private static int Priority(LiveTradeDirective directive)
    {
        switch (directive)
        {
            case LiveTradeDirective.Exit: return 5;
            case LiveTradeDirective.Reduce: return 4;
            case LiveTradeDirective.Trail: return 3;
            case LiveTradeDirective.Protect: return 2;
            case LiveTradeDirective.Hold: return 1;
            default: return 0;
        }
    }
}
