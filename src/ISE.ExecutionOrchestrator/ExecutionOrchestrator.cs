using System.Collections.Generic;

namespace ISE.ExecutionOrchestrator;

public enum LiveTradeDirective { None, Hold, Protect, Trail, Reduce, Exit }
public enum ExecutionOrchestrationAction { SubmitEntry, Wait, ManageHold, ManageProtect, ManageTrail, ManageReduce, ExitPosition, Blocked }

public sealed class ExecutionOrchestratorInput
{
    public bool PositionOpen { get; set; }
    public bool ParticipationApproved { get; set; }
    public bool ExecutionReady { get; set; }
    public int RecommendedContracts { get; set; }
    public LiveTradeDirective LiveTradeDirective { get; set; }
    public bool OrderChannelAvailable { get; set; }
    public bool DuplicateCommandPending { get; set; }
    public bool AuthoritativeRiskBlock { get; set; }
}

public sealed class ExecutionOrchestratorDecision
{
    public ExecutionOrchestratorDecision(ExecutionOrchestrationAction action, int contracts, IReadOnlyList<string> reasons)
    {
        Action = action;
        Contracts = contracts;
        Reasons = reasons;
    }

    public ExecutionOrchestrationAction Action { get; }
    public int Contracts { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class ExecutionOrchestrator
{
    public ExecutionOrchestratorDecision Evaluate(ExecutionOrchestratorInput input)
    {
        if (input.AuthoritativeRiskBlock)
            return Decision(ExecutionOrchestrationAction.Blocked, 0, "Authoritative risk control blocked execution.");

        if (input.DuplicateCommandPending)
            return Decision(ExecutionOrchestrationAction.Wait, 0, "A prior execution command is still pending.");

        if (input.PositionOpen)
            return EvaluateOpenPosition(input.LiveTradeDirective);

        if (!input.ParticipationApproved)
            return Decision(ExecutionOrchestrationAction.Wait, 0, "Institutional participation is not approved.");

        if (input.RecommendedContracts <= 0)
            return Decision(ExecutionOrchestrationAction.Wait, 0, "Position sizing produced no tradable quantity.");

        if (!input.OrderChannelAvailable)
            return Decision(ExecutionOrchestrationAction.Wait, 0, "The execution channel is unavailable.");

        if (!input.ExecutionReady)
            return Decision(ExecutionOrchestrationAction.Wait, 0, "Execution quality has not confirmed entry readiness.");

        return Decision(ExecutionOrchestrationAction.SubmitEntry, input.RecommendedContracts,
            $"Submit an entry for {input.RecommendedContracts} contract(s).");
    }

    private static ExecutionOrchestratorDecision EvaluateOpenPosition(LiveTradeDirective directive)
    {
        switch (directive)
        {
            case LiveTradeDirective.Exit:
                return Decision(ExecutionOrchestrationAction.ExitPosition, 0, "Live trade intelligence invalidated the position.");
            case LiveTradeDirective.Reduce:
                return Decision(ExecutionOrchestrationAction.ManageReduce, 0, "Live trade intelligence recommends reducing exposure.");
            case LiveTradeDirective.Trail:
                return Decision(ExecutionOrchestrationAction.ManageTrail, 0, "Live trade intelligence recommends trailing protection.");
            case LiveTradeDirective.Protect:
                return Decision(ExecutionOrchestrationAction.ManageProtect, 0, "Live trade intelligence recommends protecting the position.");
            default:
                return Decision(ExecutionOrchestrationAction.ManageHold, 0, "Maintain the open position under current management rules.");
        }
    }

    private static ExecutionOrchestratorDecision Decision(ExecutionOrchestrationAction action, int contracts, string reason)
        => new ExecutionOrchestratorDecision(action, contracts, new[] { reason });
}
