using System;
using ISE.Execution;

namespace ISE.NinjaTraderAdapter;

/// <summary>Represents a validated order request ready for a NinjaTrader host.</summary>
public sealed class NinjaTraderOrderRequest
{
    /// <summary>Initializes an order request.</summary>
    public NinjaTraderOrderRequest(Guid commandId, string instrument, ExecutionSide side, NinjaTraderOrderType orderType, int quantity, decimal limitPrice, decimal stopPrice, string signalName)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("Command ID is required.", nameof(commandId));
        if (string.IsNullOrWhiteSpace(instrument)) throw new ArgumentException("Instrument is required.", nameof(instrument));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (string.IsNullOrWhiteSpace(signalName)) throw new ArgumentException("Signal name is required.", nameof(signalName));

        CommandId = commandId;
        Instrument = instrument;
        Side = side;
        OrderType = orderType;
        Quantity = quantity;
        LimitPrice = limitPrice;
        StopPrice = stopPrice;
        SignalName = signalName;
    }

    /// <summary>Gets the originating ISE command identifier.</summary>
    public Guid CommandId { get; }
    /// <summary>Gets the NinjaTrader instrument name.</summary>
    public string Instrument { get; }
    /// <summary>Gets the order side.</summary>
    public ExecutionSide Side { get; }
    /// <summary>Gets the platform order type.</summary>
    public NinjaTraderOrderType OrderType { get; }
    /// <summary>Gets the contract quantity.</summary>
    public int Quantity { get; }
    /// <summary>Gets the limit price when applicable.</summary>
    public decimal LimitPrice { get; }
    /// <summary>Gets the stop price when applicable.</summary>
    public decimal StopPrice { get; }
    /// <summary>Gets the platform signal name.</summary>
    public string SignalName { get; }
}
