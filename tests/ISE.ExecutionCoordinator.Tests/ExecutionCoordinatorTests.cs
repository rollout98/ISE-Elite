using System;
using System.Linq;
using ISE.Runtime;
using Xunit;

namespace ISE.ExecutionCoordinator.Tests;

public sealed class ExecutionCoordinatorTests
{
    [Fact]
    public void Valid_request_is_queued()
    {
        var coordinator = new ExecutionCoordinator();
        var result = coordinator.Submit(Request("A"), RuntimeState.SeekingEntry, true, true);
        Assert.True(result.Accepted);
        Assert.Equal(ExecutionState.Queued, result.State);
        Assert.Single(coordinator.Queue);
    }

    [Fact]
    public void Invalid_runtime_blocks_execution()
    {
        var coordinator = new ExecutionCoordinator();
        var result = coordinator.Submit(Request("B"), RuntimeState.Monitoring, true, true);
        Assert.False(result.Accepted);
        Assert.Empty(coordinator.Queue);
    }

    [Fact]
    public void Risk_rejection_prevents_queueing()
    {
        var coordinator = new ExecutionCoordinator();
        var result = coordinator.Submit(Request("C"), RuntimeState.SeekingEntry, true, false);
        Assert.False(result.Accepted);
        Assert.Contains("risk", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_request_is_ignored()
    {
        var coordinator = new ExecutionCoordinator();
        coordinator.Submit(Request("D"), RuntimeState.SeekingEntry, true, true);
        var duplicate = coordinator.Submit(Request("D"), RuntimeState.SeekingEntry, true, true);
        Assert.False(duplicate.Accepted);
        Assert.Single(coordinator.Queue);
    }

    [Fact]
    public void Dispatch_updates_history_to_submitted()
    {
        var coordinator = new ExecutionCoordinator();
        coordinator.Submit(Request("E"), RuntimeState.SeekingEntry, true, true);
        var request = coordinator.DispatchNext(DateTime.UtcNow);
        Assert.Equal("E", request.RequestId);
        Assert.Equal(ExecutionState.Submitted, coordinator.History.Last().State);
    }

    [Fact]
    public void Broker_acceptance_is_recorded()
    {
        var coordinator = new ExecutionCoordinator();
        var request = Request("F");
        coordinator.Submit(request, RuntimeState.SeekingEntry, true, true);
        coordinator.DispatchNext(DateTime.UtcNow);
        coordinator.RecordBrokerUpdate(request, ExecutionState.Accepted, "Accepted", DateTime.UtcNow);
        Assert.Equal(ExecutionState.Accepted, coordinator.History.Last().State);
    }

    [Fact]
    public void Rejected_order_records_failure()
    {
        var coordinator = new ExecutionCoordinator();
        var request = Request("G");
        coordinator.Submit(request, RuntimeState.SeekingEntry, true, true);
        coordinator.RecordBrokerUpdate(request, ExecutionState.Rejected, "Broker rejected order.", DateTime.UtcNow);
        Assert.Equal(ExecutionState.Rejected, coordinator.History.Last().State);
    }

    [Fact]
    public void Completed_execution_retains_audit_history()
    {
        var coordinator = new ExecutionCoordinator();
        var request = Request("H");
        coordinator.Submit(request, RuntimeState.SeekingEntry, true, true);
        coordinator.DispatchNext(DateTime.UtcNow);
        coordinator.RecordBrokerUpdate(request, ExecutionState.Filled, "Filled", DateTime.UtcNow);
        coordinator.RecordBrokerUpdate(request, ExecutionState.Completed, "Lifecycle complete", DateTime.UtcNow);

        Assert.Equal(5, coordinator.History.Count);
        Assert.Equal(
            new[]
            {
                ExecutionState.Validated,
                ExecutionState.Queued,
                ExecutionState.Submitted,
                ExecutionState.Filled,
                ExecutionState.Completed
            },
            coordinator.History.Select(record => record.State));
    }

    private static ExecutionRequest Request(string id) => new ExecutionRequest(
        id, "NY-OPEN", "MNQ", ExecutionSide.Buy, 5, ExecutionOrderType.Market,
        null, 19900m, 20100m, DateTime.UtcNow, "EXPL-" + id);
}
