using ISE.MarketStructure;
using ISE.OrderFlow;
using ISE.Trend;

namespace ISE.Signal;

/// <summary>Contains normalized analytical evidence consumed by the Signal Engine.</summary>
public sealed class SignalInput
{
    /// <summary>Creates a validated Signal Engine input.</summary>
    public SignalInput(
        bool tradingPermitted,
        TrendDirection trendDirection,
        StructureDirection structureDirection,
        bool sellSideLiquidityReclaimed,
        bool buySideLiquidityReclaimed,
        OrderFlowBias orderFlowBias,
        int minimumConfidence = 70)
    {
        if (minimumConfidence < 1 || minimumConfidence > 100)
            throw new ArgumentOutOfRangeException(nameof(minimumConfidence), "Minimum confidence must be between 1 and 100.");

        TradingPermitted = tradingPermitted;
        TrendDirection = trendDirection;
        StructureDirection = structureDirection;
        SellSideLiquidityReclaimed = sellSideLiquidityReclaimed;
        BuySideLiquidityReclaimed = buySideLiquidityReclaimed;
        OrderFlowBias = orderFlowBias;
        MinimumConfidence = minimumConfidence;
    }

    /// <summary>Gets whether the active session allows a trade candidate.</summary>
    public bool TradingPermitted { get; }

    /// <summary>Gets the current trend direction.</summary>
    public TrendDirection TrendDirection { get; }

    /// <summary>Gets the current confirmed structure direction.</summary>
    public StructureDirection StructureDirection { get; }

    /// <summary>Gets whether sell-side liquidity was swept and reclaimed.</summary>
    public bool SellSideLiquidityReclaimed { get; }

    /// <summary>Gets whether buy-side liquidity was swept and reclaimed.</summary>
    public bool BuySideLiquidityReclaimed { get; }

    /// <summary>Gets the current order-flow bias.</summary>
    public OrderFlowBias OrderFlowBias { get; }

    /// <summary>Gets the minimum directional score required for an actionable signal.</summary>
    public int MinimumConfidence { get; }
}
