using System;

namespace ISE.Execution;

/// <summary>Represents a platform-independent execution order.</summary>
public sealed class ExecutionOrder
{
    /// <summary>Initializes an execution order.</summary>
    public ExecutionOrder(Guid orderId, Guid tradePlanId, ExecutionOrderRole role, int quantity, decimal price, ExecutionOrderState state, int filledQuantity, string? platformOrderId, string? message)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("Order ID is required.", nameof(orderId));
        if (tradePlanId == Guid.Empty) throw new ArgumentException("Trade plan ID is required.", nameof(tradePlanId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (filledQuantity < 0 || filledQuantity > quantity) throw new ArgumentOutOfRangeException(nameof(filledQuantity));

        OrderId = orderId;
        TradePlanId = tradePlanId;
        Role = role;
        Quantity = quantity;
        Price = price;
        State = state;
        FilledQuantity = filledQuantity;
        PlatformOrderId = platformOrderId;
        Message = message;
    }

    /// <summary>Gets the internal order identifier.</summary>
    public Guid OrderId { get; }
    /// <summary>Gets the originating trade-plan identifier.</summary>
    public Guid TradePlanId { get; }
    /// <summary>Gets the order role.</summary>
    public ExecutionOrderRole Role { get; }
    /// <summary>Gets the requested quantity.</summary>
    public int Quantity { get; }
    /// <summary>Gets the planned order price.</summary>
    public decimal Price { get; }
    /// <summary>Gets the current lifecycle state.</summary>
    public ExecutionOrderState State { get; }
    /// <summary>Gets the cumulative filled quantity.</summary>
    public int FilledQuantity { get; }
    /// <summary>Gets the platform-assigned order identifier when available.</summary>
    public string? PlatformOrderId { get; }
    /// <summary>Gets an optional platform or transition message.</summary>
    public string? Message { get; }
}
