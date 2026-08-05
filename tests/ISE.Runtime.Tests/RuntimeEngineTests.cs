using System;
using ISE.Runtime;
using Xunit;

namespace ISE.Runtime.Tests;

public sealed class RuntimeEngineTests
{
    private static readonly DateTime T0 = new DateTime(2026, 8, 5, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Starts_in_stopped_state()
    {
        var engine = new RuntimeEngine(T0);

        Assert.Equal(RuntimeState.Stopped, engine.Context.State);
        Assert.Empty(engine.History);
    }

    [Fact]
    public void Initialize_transitions_to_initializing()
    {
        var engine = new RuntimeEngine(T0);

        var transition = engine.Handle(RuntimeEvent.Initialize, T0.AddSeconds(1));

        Assert.Equal(RuntimeState.Initializing, engine.Context.State);
        Assert.Equal(RuntimeState.Stopped, transition.From);
    }

    [Fact]
    public void Initialization_completion_waits_for_market()
    {
        var engine = InitializedEngine();

        engine.Handle(RuntimeEvent.InitializationCompleted, T0.AddSeconds(2));

        Assert.Equal(RuntimeState.WaitingForMarket, engine.Context.State);
    }

    [Fact]
    public void Market_open_enters_monitoring_and_synchronizes_context()
    {
        var engine = WaitingEngine();

        engine.Handle(RuntimeEvent.MarketOpened, T0.AddMinutes(1));

        Assert.Equal(RuntimeState.Monitoring, engine.Context.State);
        Assert.True(engine.Context.MarketOpen);
        Assert.Equal(T0.AddMinutes(1), engine.Context.CurrentTime);
    }

    [Fact]
    public void Invalid_transition_is_rejected_without_mutating_state()
    {
        var engine = new RuntimeEngine(T0);

        Assert.Throws<InvalidOperationException>(() =>
            engine.Handle(RuntimeEvent.OrderFilled, T0.AddSeconds(1)));
        Assert.Equal(RuntimeState.Stopped, engine.Context.State);
        Assert.Empty(engine.History);
    }

    [Fact]
    public void Transition_history_records_ordered_audit_trail()
    {
        var engine = WaitingEngine();
        engine.Handle(RuntimeEvent.MarketOpened, T0.AddMinutes(1), "New York market opened.");
        engine.Handle(RuntimeEvent.EntrySearchStarted, T0.AddMinutes(2));

        Assert.Equal(4, engine.History.Count);
        Assert.Equal("New York market opened.", engine.History[2].Reason);
        Assert.Equal(RuntimeState.SeekingEntry, engine.History[3].To);
    }

    [Fact]
    public void Filled_trade_can_promote_to_runner_and_close()
    {
        var engine = MonitoringEngine();
        engine.Handle(RuntimeEvent.EntrySearchStarted, T0.AddMinutes(2));
        engine.Handle(RuntimeEvent.EntryQualified, T0.AddMinutes(3));
        engine.Handle(RuntimeEvent.OrderFilled, T0.AddMinutes(4));
        engine.Handle(RuntimeEvent.RunnerPromoted, T0.AddMinutes(5));

        Assert.Equal(RuntimeState.RunnerMode, engine.Context.State);
        Assert.True(engine.Context.PositionOpen);
        Assert.True(engine.Context.RunnerActive);

        engine.Handle(RuntimeEvent.PositionClosed, T0.AddMinutes(6));
        Assert.Equal(RuntimeState.Monitoring, engine.Context.State);
        Assert.False(engine.Context.PositionOpen);
        Assert.False(engine.Context.RunnerActive);
    }

    [Fact]
    public void Force_flat_then_position_close_completes_session()
    {
        var engine = ManagingPositionEngine();

        engine.Handle(RuntimeEvent.ForceFlatTriggered, T0.AddHours(1));
        Assert.Equal(RuntimeState.ForceFlat, engine.Context.State);

        engine.Handle(RuntimeEvent.PositionClosed, T0.AddHours(1).AddSeconds(1));
        Assert.Equal(RuntimeState.SessionComplete, engine.Context.State);
        Assert.False(engine.Context.PositionOpen);
    }

    private static RuntimeEngine InitializedEngine()
    {
        var engine = new RuntimeEngine(T0);
        engine.Handle(RuntimeEvent.Initialize, T0.AddSeconds(1));
        return engine;
    }

    private static RuntimeEngine WaitingEngine()
    {
        var engine = InitializedEngine();
        engine.Handle(RuntimeEvent.InitializationCompleted, T0.AddSeconds(2));
        return engine;
    }

    private static RuntimeEngine MonitoringEngine()
    {
        var engine = WaitingEngine();
        engine.Handle(RuntimeEvent.MarketOpened, T0.AddMinutes(1));
        return engine;
    }

    private static RuntimeEngine ManagingPositionEngine()
    {
        var engine = MonitoringEngine();
        engine.Handle(RuntimeEvent.EntrySearchStarted, T0.AddMinutes(2));
        engine.Handle(RuntimeEvent.EntryQualified, T0.AddMinutes(3));
        engine.Handle(RuntimeEvent.OrderFilled, T0.AddMinutes(4));
        return engine;
    }
}
