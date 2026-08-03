using System;

namespace ISE.Strategy;

/// <summary>Defines configurable qualification rules for a strategy playbook.</summary>
public sealed class StrategyProfile
{
    /// <summary>Creates a validated strategy profile.</summary>
    public StrategyProfile(StrategyId strategyId, int minimumConfidence, bool requiresLiquidityEvent, bool requiresStructureAlignment, bool requiresOrderFlowAlignment)
    {
        if (minimumConfidence < 0 || minimumConfidence > 100)
            throw new ArgumentOutOfRangeException(nameof(minimumConfidence));

        StrategyId = strategyId;
        MinimumConfidence = minimumConfidence;
        RequiresLiquidityEvent = requiresLiquidityEvent;
        RequiresStructureAlignment = requiresStructureAlignment;
        RequiresOrderFlowAlignment = requiresOrderFlowAlignment;
    }

    /// <summary>Gets the strategy identifier.</summary>
    public StrategyId StrategyId { get; }
    /// <summary>Gets the minimum acceptable signal confidence.</summary>
    public int MinimumConfidence { get; }
    /// <summary>Gets whether a liquidity event is required.</summary>
    public bool RequiresLiquidityEvent { get; }
    /// <summary>Gets whether market structure alignment is required.</summary>
    public bool RequiresStructureAlignment { get; }
    /// <summary>Gets whether order-flow alignment is required.</summary>
    public bool RequiresOrderFlowAlignment { get; }
}
