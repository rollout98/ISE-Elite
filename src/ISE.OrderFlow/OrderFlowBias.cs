namespace ISE.OrderFlow;

/// <summary>Identifies the dominant order-flow direction.</summary>
public enum OrderFlowBias
{
    /// <summary>No reliable directional advantage.</summary>
    Neutral,

    /// <summary>Aggressive buying is dominant.</summary>
    Bullish,

    /// <summary>Aggressive selling is dominant.</summary>
    Bearish
}
