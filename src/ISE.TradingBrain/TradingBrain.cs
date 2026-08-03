using System;
using ISE.DecisionOrchestration;
using ISE.Execution;

namespace ISE.TradingBrain;

/// <summary>Coordinates final trade authorization and execution-command preparation.</summary>
public sealed class TradingBrain
{
    private readonly DecisionOrchestrationEngine _decisionEngine;
    private readonly ExecutionEngine _executionEngine;

    /// <summary>Creates a Trading Brain with default deterministic engines.</summary>
    public TradingBrain()
        : this(new DecisionOrchestrationEngine(), new ExecutionEngine())
    {
    }

    /// <summary>Creates a Trading Brain with explicit engine dependencies.</summary>
    public TradingBrain(DecisionOrchestrationEngine decisionEngine, ExecutionEngine executionEngine)
    {
        _decisionEngine = decisionEngine ?? throw new ArgumentNullException(nameof(decisionEngine));
        _executionEngine = executionEngine ?? throw new ArgumentNullException(nameof(executionEngine));
    }

    /// <summary>Evaluates one candidate and prepares execution commands only when authorized.</summary>
    public TradingBrainDecision Evaluate(TradingBrainInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var decision = _decisionEngine.Evaluate(input.DecisionInput);
        if (!decision.ExecutionAuthorized || decision.TradePlan == null)
            return new TradingBrainDecision(input.TradePlanId, decision, null);

        var commands = _executionEngine.CreateCommands(input.TradePlanId, decision.TradePlan);
        return new TradingBrainDecision(input.TradePlanId, decision, commands);
    }
}
