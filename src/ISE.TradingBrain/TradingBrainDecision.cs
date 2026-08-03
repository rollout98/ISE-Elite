using System;
using ISE.DecisionOrchestration;
using ISE.Execution;

namespace ISE.TradingBrain;

/// <summary>Represents one complete Trading Brain evaluation.</summary>
public sealed class TradingBrainDecision
{
    /// <summary>Creates a Trading Brain decision.</summary>
    public TradingBrainDecision(Guid tradePlanId, DecisionOrchestrationSnapshot decision, ExecutionCommandSet? executionCommands)
    {
        TradePlanId = tradePlanId;
        Decision = decision ?? throw new ArgumentNullException(nameof(decision));
        ExecutionCommands = executionCommands;
    }

    /// <summary>Gets the trade-plan correlation identifier.</summary>
    public Guid TradePlanId { get; }

    /// <summary>Gets the authoritative final decision.</summary>
    public DecisionOrchestrationSnapshot Decision { get; }

    /// <summary>Gets execution commands when the decision authorized a trade.</summary>
    public ExecutionCommandSet? ExecutionCommands { get; }

    /// <summary>Gets whether executable orders were created.</summary>
    public bool ExecutionPrepared => ExecutionCommands != null && ExecutionCommands.Accepted;
}
