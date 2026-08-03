using System;

namespace ISE.NinjaTraderAdapter;

/// <summary>Represents an order update received from a NinjaTrader host.</summary>
public sealed class NinjaTraderOrderUpdate
{
    /// <summary>Initializes an order update.</summary>
    public NinjaTraderOrderUpdate(string platformOrderId, int filledQuantity, bool accepted, bool rejected, bool cancelled, string? message)
    {
        if (string.IsNullOrWhiteSpace(platformOrderId)) throw new ArgumentException("Platform order ID is required.", nameof(platformOrderId));
        if (filledQuantity < 0) throw new ArgumentOutOfRangeException(nameof(filledQuantity));

        PlatformOrderId = platformOrderId;
        FilledQuantity = filledQuantity;
        Accepted = accepted;
        Rejected = rejected;
        Cancelled = cancelled;
        Message = message;
    }

    /// <summary>Gets the NinjaTrader order identifier.</summary>
    public string PlatformOrderId { get; }
    /// <summary>Gets cumulative filled quantity.</summary>
    public int FilledQuantity { get; }
    /// <summary>Gets whether the platform accepted the order.</summary>
    public bool Accepted { get; }
    /// <summary>Gets whether the order was rejected.</summary>
    public bool Rejected { get; }
    /// <summary>Gets whether the order was cancelled.</summary>
    public bool Cancelled { get; }
    /// <summary>Gets the platform message.</summary>
    public string? Message { get; }
}
