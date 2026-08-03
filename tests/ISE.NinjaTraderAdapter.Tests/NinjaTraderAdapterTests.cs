using System;
using ISE.Execution;
using Xunit;

namespace ISE.NinjaTraderAdapter.Tests;

public sealed class NinjaTraderAdapterTests
{
    [Fact]
    public void Nq_maps_to_micro_nasdaq()
    {
        var mapper = new NinjaTraderInstrumentMapper();
        Assert.True(mapper.TryMap("NQ", out var instrument));
        Assert.Equal("MNQ", instrument);
    }

    [Fact]
    public void Protective_stop_translates_to_stop_market()
    {
        var order = CreateOrder(ExecutionOrderRole.ProtectiveStop, ExecutionSide.Sell, 3, 19950m);
        var request = new NinjaTraderOrderTranslator(new NinjaTraderInstrumentMapper()).Translate(order, "NQ");

        Assert.Equal(NinjaTraderOrderType.StopMarket, request.OrderType);
        Assert.Equal(19950m, request.StopPrice);
        Assert.Equal(0m, request.LimitPrice);
    }

    [Fact]
    public void Unsupported_instrument_is_rejected()
    {
        var order = CreateOrder(ExecutionOrderRole.Entry, ExecutionSide.Buy, 1, 100m);
        var translator = new NinjaTraderOrderTranslator(new NinjaTraderInstrumentMapper());

        Assert.Throws<NotSupportedException>(() => translator.Translate(order, "UNKNOWN"));
    }

    [Fact]
    public void Correlation_resolves_both_directions_and_rejects_duplicates()
    {
        var commandId = Guid.NewGuid();
        var correlation = new NinjaTraderOrderCorrelation();
        correlation.Register(commandId, "NT-123");

        Assert.True(correlation.TryResolveCommand("NT-123", out var resolvedCommand));
        Assert.Equal(commandId, resolvedCommand);
        Assert.True(correlation.TryResolvePlatform(commandId, out var platformId));
        Assert.Equal("NT-123", platformId);
        Assert.Throws<InvalidOperationException>(() => correlation.Register(commandId, "NT-456"));
    }

    private static ExecutionOrder CreateOrder(ExecutionOrderRole role, ExecutionSide side, int quantity, decimal price) =>
        new ExecutionOrder(Guid.NewGuid(), Guid.NewGuid(), role, side, quantity, price, ExecutionOrderState.Pending, 0, null, null);
}
