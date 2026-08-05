using System;
using System.Collections.Generic;

namespace ISE.Runtime;

public enum RuntimeState
{
    Stopped,
    Initializing,
    WaitingForMarket,
    PreOpen,
    Monitoring,
    SeekingEntry,
    ExecutingTrade,
    ManagingPosition,
    RunnerMode,
    ForceFlat,
    SessionComplete
}

public enum RuntimeEvent
{
    Initialize,
    InitializationCompleted,
    PreOpenDetected,
    MarketOpened,
    EntrySearchStarted,
    EntryQualified,
    OrderFilled,
    RunnerPromoted,
    PositionClosed,
    ForceFlatTriggered,
    ShutdownRequested
}

public sealed class RuntimeContext
{
    internal RuntimeContext(RuntimeState state, DateTime currentTime)
    {
        State = state;
        CurrentTime = currentTime;
    }

    public RuntimeState State { get; internal set; }
    public DateTime CurrentTime { get; internal set; }
    public bool MarketOpen { get; internal set; }
    public bool PositionOpen { get; internal set; }
    public bool RunnerActive { get; internal set; }
    public bool ShutdownRequested { get; internal set; }
}

public sealed class RuntimeTransition
{
    public RuntimeTransition(RuntimeState from, RuntimeState to, RuntimeEvent runtimeEvent, string reason, DateTime occurredAt)
    {
        From = from;
        To = to;
        Event = runtimeEvent;
        Reason = string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException("Reason is required.", nameof(reason)) : reason;
        OccurredAt = occurredAt;
    }

    public RuntimeState From { get; }
    public RuntimeState To { get; }
    public RuntimeEvent Event { get; }
    public string Reason { get; }
    public DateTime OccurredAt { get; }
}

public sealed class RuntimeEngine
{
    private readonly List<RuntimeTransition> _history = new List<RuntimeTransition>();

    public RuntimeEngine(DateTime? initialTime = null)
    {
        Context = new RuntimeContext(RuntimeState.Stopped, initialTime ?? DateTime.UtcNow);
    }

    public RuntimeContext Context { get; }
    public IReadOnlyList<RuntimeTransition> History => _history.AsReadOnly();

    public RuntimeTransition Handle(RuntimeEvent runtimeEvent, DateTime occurredAt, string? reason = null)
    {
        var from = Context.State;
        var to = ResolveNextState(from, runtimeEvent);
        ApplyContext(runtimeEvent, to, occurredAt);

        var transition = new RuntimeTransition(from, to, runtimeEvent,
            reason ?? BuildDefaultReason(runtimeEvent), occurredAt);
        _history.Add(transition);
        return transition;
    }

    private static RuntimeState ResolveNextState(RuntimeState state, RuntimeEvent runtimeEvent)
    {
        if (runtimeEvent == RuntimeEvent.ForceFlatTriggered && state != RuntimeState.Stopped && state != RuntimeState.SessionComplete)
            return RuntimeState.ForceFlat;

        if (runtimeEvent == RuntimeEvent.ShutdownRequested)
            return state == RuntimeState.Stopped ? RuntimeState.Stopped : RuntimeState.SessionComplete;

        return (state, runtimeEvent) switch
        {
            (RuntimeState.Stopped, RuntimeEvent.Initialize) => RuntimeState.Initializing,
            (RuntimeState.Initializing, RuntimeEvent.InitializationCompleted) => RuntimeState.WaitingForMarket,
            (RuntimeState.WaitingForMarket, RuntimeEvent.PreOpenDetected) => RuntimeState.PreOpen,
            (RuntimeState.WaitingForMarket, RuntimeEvent.MarketOpened) => RuntimeState.Monitoring,
            (RuntimeState.PreOpen, RuntimeEvent.MarketOpened) => RuntimeState.Monitoring,
            (RuntimeState.Monitoring, RuntimeEvent.EntrySearchStarted) => RuntimeState.SeekingEntry,
            (RuntimeState.SeekingEntry, RuntimeEvent.EntryQualified) => RuntimeState.ExecutingTrade,
            (RuntimeState.ExecutingTrade, RuntimeEvent.OrderFilled) => RuntimeState.ManagingPosition,
            (RuntimeState.ManagingPosition, RuntimeEvent.RunnerPromoted) => RuntimeState.RunnerMode,
            (RuntimeState.RunnerMode, RuntimeEvent.PositionClosed) => RuntimeState.Monitoring,
            (RuntimeState.ManagingPosition, RuntimeEvent.PositionClosed) => RuntimeState.Monitoring,
            (RuntimeState.ForceFlat, RuntimeEvent.PositionClosed) => RuntimeState.SessionComplete,
            _ => throw new InvalidOperationException($"Runtime event {runtimeEvent} is invalid while in state {state}.")
        };
    }

    private void ApplyContext(RuntimeEvent runtimeEvent, RuntimeState to, DateTime occurredAt)
    {
        Context.State = to;
        Context.CurrentTime = occurredAt;
        Context.MarketOpen = to == RuntimeState.Monitoring || to == RuntimeState.SeekingEntry ||
                             to == RuntimeState.ExecutingTrade || to == RuntimeState.ManagingPosition ||
                             to == RuntimeState.RunnerMode || to == RuntimeState.ForceFlat;

        if (runtimeEvent == RuntimeEvent.OrderFilled)
            Context.PositionOpen = true;
        if (runtimeEvent == RuntimeEvent.PositionClosed)
            Context.PositionOpen = false;

        Context.RunnerActive = to == RuntimeState.RunnerMode;
        Context.ShutdownRequested = runtimeEvent == RuntimeEvent.ShutdownRequested;
    }

    private static string BuildDefaultReason(RuntimeEvent runtimeEvent)
        => $"Runtime processed {runtimeEvent}.";
}
