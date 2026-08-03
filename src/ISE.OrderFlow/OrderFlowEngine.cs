using System;
using System.Linq;

namespace ISE.OrderFlow;

/// <summary>Evaluates aggregate delta, price-level imbalance, and absorption.</summary>
public sealed class OrderFlowEngine
{
    /// <summary>Evaluates one immutable order-flow request.</summary>
    public OrderFlowSnapshot Evaluate(OrderFlowInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));

        var bid = input.Levels.Sum(level => level.BidVolume);
        var ask = input.Levels.Sum(level => level.AskVolume);
        var bullish = input.Levels.Count(level => level.BidVolume == 0
            ? level.AskVolume > 0
            : (decimal)level.AskVolume / level.BidVolume >= input.ImbalanceRatio);
        var bearish = input.Levels.Count(level => level.AskVolume == 0
            ? level.BidVolume > 0
            : (decimal)level.BidVolume / level.AskVolume >= input.ImbalanceRatio);

        var averageVolume = input.Levels.Average(level => (decimal)level.TotalVolume);
        var absorption = input.Levels.Any(level =>
            level.TotalVolume >= averageVolume * 1.5m &&
            level.TotalVolume > 0 &&
            Math.Abs(level.Delta) <= level.TotalVolume * 0.1m);

        var bias = OrderFlowBias.Neutral;
        if (ask > bid && bullish > bearish) bias = OrderFlowBias.Bullish;
        else if (bid > ask && bearish > bullish) bias = OrderFlowBias.Bearish;

        return new OrderFlowSnapshot(
            input.TimestampUtc,
            input.CorrelationId,
            bid,
            ask,
            bullish,
            bearish,
            absorption,
            bias);
    }
}
