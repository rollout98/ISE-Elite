using System;
using ISE.Execution;

namespace ISE.NinjaTraderAdapter;

/// <summary>Translates platform-independent ISE orders into NinjaTrader requests.</summary>
public sealed class NinjaTraderOrderTranslator
{
    private readonly NinjaTraderInstrumentMapper instrumentMapper;

    /// <summary>Initializes the translator.</summary>
    public NinjaTraderOrderTranslator(NinjaTraderInstrumentMapper instrumentMapper)
    {
        this.instrumentMapper = instrumentMapper ?? throw new ArgumentNullException(nameof(instrumentMapper));
    }

    /// <summary>Translates one ISE execution order.</summary>
    public NinjaTraderOrderRequest Translate(ExecutionOrder order, string symbol)
    {
        if (order == null) throw new ArgumentNullException(nameof(order));
        if (!instrumentMapper.TryMap(symbol, out var instrument))
            throw new NotSupportedException($"Unsupported instrument: {symbol}");

        var orderType = order.Role == ExecutionOrderRole.ProtectiveStop
            ? NinjaTraderOrderType.StopMarket
            : NinjaTraderOrderType.Limit;
        var limitPrice = orderType == NinjaTraderOrderType.Limit ? order.Price : 0m;
        var stopPrice = orderType == NinjaTraderOrderType.StopMarket ? order.Price : 0m;
        var signalName = $"ISE-{order.Role}-{order.OrderId:N}";

        return new NinjaTraderOrderRequest(order.OrderId, instrument, order.Side, orderType, order.Quantity, limitPrice, stopPrice, signalName);
    }
}
