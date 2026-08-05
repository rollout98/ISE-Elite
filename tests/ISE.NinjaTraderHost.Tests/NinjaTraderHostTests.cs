using System;
using System.Collections.Generic;
using ISE.BrokerExecution;
using ISE.ExecutionCoordinator;
using ISE.NinjaTraderHost;
using Xunit;

namespace ISE.NinjaTraderHost.Tests;

public sealed class NinjaTraderHostTests
{
    [Fact] public void Host_starts_on_Sim101() { var api = new FakeApi(); var host = new NinjaTraderHostTransport(api); host.Start(); Assert.Equal(HostState.Running, host.State); Assert.Equal("Sim101", host.AccountName); }
    [Fact] public void Disconnected_platform_blocks_start() { var api = new FakeApi { IsConnected = false }; var host = new NinjaTraderHostTransport(api); Assert.Throws<InvalidOperationException>(() => host.Start()); Assert.Equal(HostState.Faulted, host.State); }
    [Fact] public void Missing_account_blocks_start() { var api = new FakeApi(accounts: new[] { "Other" }); var host = new NinjaTraderHostTransport(api); Assert.Throws<InvalidOperationException>(() => host.Start()); }
    [Fact] public void Submit_requires_running_host() { var host = new NinjaTraderHostTransport(new FakeApi()); Assert.Throws<InvalidOperationException>(() => host.Submit(Command("A"))); }
    [Fact] public void Submit_routes_to_configured_account() { var api = new FakeApi(); var host = new NinjaTraderHostTransport(api); host.Start(); var id = host.Submit(Command("B")); Assert.Equal("NT-B", id); Assert.Equal("Sim101", api.LastAccount); }
    [Fact] public void Cancel_routes_correlated_order() { var api = new FakeApi(); var host = new NinjaTraderHostTransport(api); host.Start(); var id = host.Submit(Command("C")); host.Cancel(id); Assert.Equal(id, api.CancelledId); }
    [Fact] public void Unknown_order_update_is_rejected() { var host = new NinjaTraderHostTransport(new FakeApi()); host.Start(); Assert.Throws<KeyNotFoundException>(() => host.HandleOrderUpdate(Update("missing", PlatformOrderState.Filled))); }
    [Fact] public void Filled_update_is_normalized_and_published() { var host = new NinjaTraderHostTransport(new FakeApi()); host.Start(); var id = host.Submit(Command("D")); BrokerOrderEvent? published = null; host.BrokerEvent += e => published = e; var result = host.HandleOrderUpdate(Update(id, PlatformOrderState.Filled)); Assert.Equal(BrokerOrderState.Filled, result.State); Assert.Equal("D", result.RequestId); Assert.NotNull(published); }

    private static BrokerOrderCommand Command(string id) => new BrokerOrderCommand(id, "MNQ", ExecutionSide.Buy, ExecutionOrderType.Market, 1, null, 19900m, 20100m, "ISE-NY");
    private static PlatformOrderUpdate Update(string id, PlatformOrderState state) => new PlatformOrderUpdate(id, state, state == PlatformOrderState.Filled ? 1 : 0, 20000m, state.ToString(), DateTime.UtcNow);

    private sealed class FakeApi : INinjaTraderApi
    {
        public FakeApi(IEnumerable<string>? accounts = null) { AccountNames = new List<string>(accounts ?? new[] { "Sim101" }); }
        public bool IsConnected { get; set; } = true;
        public IReadOnlyCollection<string> AccountNames { get; }
        public string? LastAccount { get; private set; }
        public string? CancelledId { get; private set; }
        public string Submit(string accountName, BrokerOrderCommand command) { LastAccount = accountName; return "NT-" + command.RequestId; }
        public void Cancel(string accountName, string platformOrderId) { LastAccount = accountName; CancelledId = platformOrderId; }
    }
}
