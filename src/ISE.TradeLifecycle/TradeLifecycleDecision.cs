using System;

namespace ISE.TradeLifecycle;

/// <summary>Represents the next deterministic lifecycle action.</summary>
public sealed class TradeLifecycleDecision
{
    /// <summary>Initializes a lifecycle decision.</summary>
    public TradeLifecycleDecision(TradeLifecycleState state, TradeLifecycleAction action, int remainingQuantity, decimal stopPrice, string reason)
    {
        if (remainingQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(remainingQuantity));
        if (stopPrice <= 0m)
            throw new ArgumentOutOfRangeException(nameof(stopPrice));

        State = state;
        Action = action;
        RemainingQuantity = remainingQuantity;
        StopPrice = stopPrice;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>Gets the resulting lifecycle state.</summary>
    public TradeLifecycleState State { get; }
    /// <summary>Gets the action to execute.</summary>
    public TradeLifecycleAction Action { get; }
    /// <summary>Gets the quantity remaining after the action.</summary>
    public int RemainingQuantity { get; }
    /// <summary>Gets the resulting protective stop price.</summary>
    public decimal StopPrice { get; }
    /// <summary>Gets an explainable decision reason.</summary>
    public string Reason { get; }
}
