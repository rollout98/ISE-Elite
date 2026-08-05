using System;
using System.Collections.Generic;
using ISE.BrokerExecution;
using ISE.ExecutionCoordinator;
using ISE.NinjaTraderAdapter;
using Xunit;

namespace ISE.BrokerExecution.Tests;

public sealed class BrokerExecutionTests
{
    [Fact]
    public void Submit_maps_MNQ_and_returns_platform_order_id()
    {
        var transport = new FakeTransport();
        var broker = Broker(transport);
        var result = broker.Submit(Request("A", "MNQ"), DateTime.UtcNow);
        Assert.Equal("NT-1", result.PlatformOrderId);
        Assert.Equal("MNQ", transport.LastCommand!.Instrument);
    }

    [Fact]
    public void Macro_symbol_maps_to_micro_contract()
    {
        var transport = new FakeTransport();
        Broker(transport).Submit(Request("B", "NQ"), DateTime.UtcNow);
        Assert.Equal("MNQ", transport.LastCommand!.Instrument);
    }

    [Fact]
    public void Unsupported_symbol_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => Broker(new FakeTransport()).Submit(Request("C", "ABC"), DateTime.UtcNow));
    }

    [Fact]
    public void Duplicate_submission_is_rejected()
    {
        var broker = Broker(new FakeTransport());
        var request = Request("D", "MNQ");
        broker.Submit(request, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => broker.Submit(request, DateTime.UtcNow));
    }

    [Fact]
    public void Order_fields_are_preserved_for_transport()
    {
        var transport = new FakeTransport();
        Broker(transport).Submit(Request("E", "MNQ"), DateTime.UtcNow);
        Assert.Equal(5, transport.LastCommand!.Quantity);
        Assert.Equal(ExecutionOrderType.Market, transport.LastCommand.OrderType);
        Assert.Equal(19900m, transport.LastCommand.StopPrice);
        Assert.Equal(20100m, transport.LastCommand.TargetPrice);
    }

    [Fact]
    public void Cancel_uses_correlated_platform_order_id()
    {
        var transport = new FakeTransport();
        var broker = Broker(transport);
        broker.Submit(Request("F", "MNQ"), DateTime.UtcNow);
        var result = broker.Cancel("F", DateTime.UtcNow);
        Assert.Equal("NT-1", transport.CancelledOrderId);
        Assert.Equal(BrokerOrderState.Cancelled, result.State);
    }

    [Fact]
    public void Unknown_cancel_is_rejected()
    {
        Assert.Throws<KeyNotFoundException>(() => Broker(new FakeTransport()).Cancel("missing", DateTime.UtcNow));
    }

    [Fact]
    public void Platform_updates_are_normalized_with_correlation()
    {
        var broker = Broker(new FakeTransport());
        broker.Submit(Request("H", "MNQ"), DateTime.UtcNow);
        var result = broker.Normalize("H", BrokerOrderState.Filled, 5, 20000m, "Filled", DateTime.UtcNow);
        Assert.Equal("NT-1", result.PlatformOrderId);
        Assert.Equal(5, result.FilledQuantity);
        Assert.Equal(20000m, result.AverageFillPrice);
    }

    private static NinjaTraderExecutionBroker Broker(INinjaTraderTransport transport)
        => new NinjaTraderExecutionBroker(transport, new NinjaTraderInstrumentMapper());

    private static ExecutionRequest Request(string id, string symbol) => new ExecutionRequest(
        id, "NY-OPEN", symbol, ExecutionSide.Buy, 5, ExecutionOrderType.Market,
        null, 19900m, 20100m, DateTime.UtcNow, "EXPL-" + id);

    private sealed class FakeTransport : INinjaTraderTransport
    {
        public BrokerOrderCommand? LastCommand { get; private set; }
        public string? CancelledOrderId { get; private set; }
        public string Submit(BrokerOrderCommand command) { LastCommand = command; return "NT-1"; }
        public void Cancel(string platformOrderId) { CancelledOrderId = platformOrderId; }
    }
}
