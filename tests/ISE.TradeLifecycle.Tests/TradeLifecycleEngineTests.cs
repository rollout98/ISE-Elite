using Xunit;

namespace ISE.TradeLifecycle.Tests;

public sealed class TradeLifecycleEngineTests
{
    [Fact]
    public void Filled_entry_activates_position()
    {
        var engine = new TradeLifecycleEngine();
        var input = new TradeLifecycleInput(TradeLifecycleState.PendingEntry, 4, 20000m, 20001m, 19980m, 20080m, true, entryFilled: true);

        var result = engine.Evaluate(input);

        Assert.Equal(TradeLifecycleState.Active, result.State);
        Assert.Equal(TradeLifecycleAction.Activate, result.Action);
        Assert.Equal(4, result.RemainingQuantity);
    }

    [Fact]
    public void Break_even_trigger_moves_stop_to_entry_plus_offset()
    {
        var engine = new TradeLifecycleEngine();
        var input = new TradeLifecycleInput(TradeLifecycleState.Active, 4, 20000m, 20030m, 19980m, 20100m, true, breakEvenTrigger: 25m, breakEvenOffset: 2m);

        var result = engine.Evaluate(input);

        Assert.Equal(TradeLifecycleState.BreakEvenProtected, result.State);
        Assert.Equal(TradeLifecycleAction.MoveStop, result.Action);
        Assert.Equal(20002m, result.StopPrice);
    }

    [Fact]
    public void Trailing_stop_never_moves_backward()
    {
        var engine = new TradeLifecycleEngine();
        var input = new TradeLifecycleInput(TradeLifecycleState.Trailing, 2, 20000m, 20020m, 20015m, 20100m, true, trailingDistance: 10m);

        var result = engine.Evaluate(input);

        Assert.Equal(TradeLifecycleAction.None, result.Action);
        Assert.Equal(20015m, result.StopPrice);
    }

    [Fact]
    public void Emergency_exit_closes_all_remaining_quantity()
    {
        var engine = new TradeLifecycleEngine();
        var input = new TradeLifecycleInput(TradeLifecycleState.Trailing, 3, 20000m, 20040m, 20020m, 20100m, true, emergencyExit: true, trailingDistance: 10m);

        var result = engine.Evaluate(input);

        Assert.Equal(TradeLifecycleState.Closed, result.State);
        Assert.Equal(TradeLifecycleAction.Close, result.Action);
        Assert.Equal(0, result.RemainingQuantity);
    }
}
