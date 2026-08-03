using System;

namespace ISE.Strategy;

/// <summary>Contains normalized evidence used to qualify a strategy candidate.</summary>
public sealed class StrategyInput
{
    /// <summary>Creates a validated strategy input.</summary>
    public StrategyInput(StrategyProfile profile, bool sessionEligible, bool signalEligible, int confidence, bool liquidityEventPresent, bool structureAligned, bool orderFlowAligned, bool trendAligned, bool priorTrendOpposedSignal)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (confidence < 0 || confidence > 100)
            throw new ArgumentOutOfRangeException(nameof(confidence));

        SessionEligible = sessionEligible;
        SignalEligible = signalEligible;
        Confidence = confidence;
        LiquidityEventPresent = liquidityEventPresent;
        StructureAligned = structureAligned;
        OrderFlowAligned = orderFlowAligned;
        TrendAligned = trendAligned;
        PriorTrendOpposedSignal = priorTrendOpposedSignal;
    }

    /// <summary>Gets the selected strategy profile.</summary>
    public StrategyProfile Profile { get; }
    /// <summary>Gets whether the current session permits this playbook.</summary>
    public bool SessionEligible { get; }
    /// <summary>Gets whether the upstream signal is execution eligible.</summary>
    public bool SignalEligible { get; }
    /// <summary>Gets the upstream signal confidence.</summary>
    public int Confidence { get; }
    /// <summary>Gets whether the required liquidity event occurred.</summary>
    public bool LiquidityEventPresent { get; }
    /// <summary>Gets whether market structure supports the candidate direction.</summary>
    public bool StructureAligned { get; }
    /// <summary>Gets whether order flow supports the candidate direction.</summary>
    public bool OrderFlowAligned { get; }
    /// <summary>Gets whether the current trend supports continuation.</summary>
    public bool TrendAligned { get; }
    /// <summary>Gets whether the prior trend opposed the candidate direction.</summary>
    public bool PriorTrendOpposedSignal { get; }
}
