using System;
using ISE.DecisionOrchestration;

namespace ISE.TradingBrain;

/// <summary>Contains the authoritative inputs required for one Trading Brain evaluation.</summary>
public sealed class TradingBrainInput
{
    /// <summary>Creates a Trading Brain input.</summary>
    public TradingBrainInput(Guid tradePlanId, DecisionOrchestrationInput decisionInput)
    {
        if (tradePlanId == Guid.Empty) throw new ArgumentException("Trade plan ID is required.", nameof(tradePlanId));
        TradePlanId = tradePlanId;
        DecisionInput = decisionInput ?? throw new ArgumentNullException(nameof(decisionInput));
    }

    /// <summary>Gets the stable trade-plan correlation identifier.</summary>
    public Guid TradePlanId { get; }

    /// <summary>Gets the complete decision-orchestration input.</summary>
    public DecisionOrchestrationInput DecisionInput { get; }
}
