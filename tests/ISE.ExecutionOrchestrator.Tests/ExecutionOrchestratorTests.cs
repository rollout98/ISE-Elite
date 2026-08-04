using Xunit;

namespace ISE.ExecutionOrchestrator.Tests;

public sealed class ExecutionOrchestratorTests
{
    private readonly ExecutionOrchestrator _orchestrator = new();

    [Fact]
    public void Approved_flat_state_submits_sized_entry()
    {
        var decision = _orchestrator.Evaluate(new ExecutionOrchestratorInput
        {
            ParticipationApproved = true,
            ExecutionReady = true,
            RecommendedContracts = 3,
            OrderChannelAvailable = true
        });

        Assert.Equal(ExecutionOrchestrationAction.SubmitEntry, decision.Action);
        Assert.Equal(3, decision.Contracts);
    }

    [Fact]
    public void Open_position_exit_directive_has_priority()
    {
        var decision = _orchestrator.Evaluate(new ExecutionOrchestratorInput
        {
            PositionOpen = true,
            LiveTradeDirective = LiveTradeDirective.Exit,
            ParticipationApproved = true,
            ExecutionReady = true,
            RecommendedContracts = 5,
            OrderChannelAvailable = true
        });

        Assert.Equal(ExecutionOrchestrationAction.ExitPosition, decision.Action);
        Assert.Equal(0, decision.Contracts);
    }

    [Fact]
    public void Duplicate_pending_command_prevents_new_action()
    {
        var decision = _orchestrator.Evaluate(new ExecutionOrchestratorInput
        {
            ParticipationApproved = true,
            ExecutionReady = true,
            RecommendedContracts = 2,
            OrderChannelAvailable = true,
            DuplicateCommandPending = true
        });

        Assert.Equal(ExecutionOrchestrationAction.Wait, decision.Action);
    }

    [Fact]
    public void Authoritative_block_overrides_everything()
    {
        var decision = _orchestrator.Evaluate(new ExecutionOrchestratorInput
        {
            PositionOpen = true,
            LiveTradeDirective = LiveTradeDirective.Hold,
            ParticipationApproved = true,
            ExecutionReady = true,
            RecommendedContracts = 10,
            OrderChannelAvailable = true,
            AuthoritativeRiskBlock = true
        });

        Assert.Equal(ExecutionOrchestrationAction.Blocked, decision.Action);
        Assert.Equal(0, decision.Contracts);
    }
}
