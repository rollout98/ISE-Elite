using System;
using System.Collections.Generic;

namespace ISE.MarketOpen;

public enum MarketOpenPhase
{
    PreMarket,
    OpeningRange,
    ReversalWindow,
    PullbackWindow,
    TrendManagement,
    Closeout,
    ForceFlat,
    Closed
}

public enum MarketOpenAction
{
    Observe,
    AllowEntry,
    ManagePosition,
    ForceExit,
    StandDown
}

public sealed class MarketOpenInput
{
    public MarketOpenInput(TimeSpan centralTime, bool positionOpen, bool openingRangeReady,
        bool setupReady, bool authoritativeRiskBlock = false, bool dailyLockout = false)
    {
        if (centralTime < TimeSpan.Zero || centralTime >= TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(centralTime));

        CentralTime = centralTime;
        PositionOpen = positionOpen;
        OpeningRangeReady = openingRangeReady;
        SetupReady = setupReady;
        AuthoritativeRiskBlock = authoritativeRiskBlock;
        DailyLockout = dailyLockout;
    }

    public TimeSpan CentralTime { get; }
    public bool PositionOpen { get; }
    public bool OpeningRangeReady { get; }
    public bool SetupReady { get; }
    public bool AuthoritativeRiskBlock { get; }
    public bool DailyLockout { get; }
}

public sealed class MarketOpenDecision
{
    public MarketOpenDecision(MarketOpenPhase phase, MarketOpenAction action,
        bool newEntriesPermitted, IReadOnlyList<string> reasons)
    {
        Phase = phase;
        Action = action;
        NewEntriesPermitted = newEntriesPermitted;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public MarketOpenPhase Phase { get; }
    public MarketOpenAction Action { get; }
    public bool NewEntriesPermitted { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class MarketOpenEngine
{
    private static readonly TimeSpan EntryStart = new TimeSpan(7, 0, 0);
    private static readonly TimeSpan OpeningRangeEnd = new TimeSpan(8, 30, 0);
    private static readonly TimeSpan ReversalWindowEnd = new TimeSpan(9, 5, 0);
    private static readonly TimeSpan PullbackWindowStart = new TimeSpan(9, 30, 0);
    private static readonly TimeSpan EntryCutoff = new TimeSpan(10, 0, 0);
    private static readonly TimeSpan CloseoutStart = new TimeSpan(14, 45, 0);
    private static readonly TimeSpan ForceFlatTime = new TimeSpan(15, 0, 0);

    public MarketOpenDecision Evaluate(MarketOpenInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var phase = ResolvePhase(input.CentralTime);
        var reasons = new List<string> { $"New York workflow phase: {phase}." };

        if (input.CentralTime >= ForceFlatTime)
        {
            reasons.Add("The authoritative 3:00 PM CT flat rule is active.");
            return new MarketOpenDecision(phase,
                input.PositionOpen ? MarketOpenAction.ForceExit : MarketOpenAction.StandDown,
                false, reasons);
        }

        if (input.AuthoritativeRiskBlock || input.DailyLockout)
        {
            reasons.Add(input.AuthoritativeRiskBlock
                ? "Authoritative risk control blocks participation."
                : "Daily controls block participation.");
            return new MarketOpenDecision(phase,
                input.PositionOpen ? MarketOpenAction.ForceExit : MarketOpenAction.StandDown,
                false, reasons);
        }

        if (input.PositionOpen)
        {
            reasons.Add("An existing position remains under lifecycle management.");
            return new MarketOpenDecision(phase, MarketOpenAction.ManagePosition, false, reasons);
        }

        bool insideEntryWindow = input.CentralTime >= EntryStart && input.CentralTime < EntryCutoff;
        bool phaseAllowsEntry = phase == MarketOpenPhase.OpeningRange ||
                                phase == MarketOpenPhase.ReversalWindow ||
                                phase == MarketOpenPhase.PullbackWindow;

        if (!insideEntryWindow || !phaseAllowsEntry)
        {
            reasons.Add("New entries are not permitted in the current phase.");
            return new MarketOpenDecision(phase,
                input.CentralTime < EntryStart ? MarketOpenAction.Observe : MarketOpenAction.StandDown,
                false, reasons);
        }

        if (!input.OpeningRangeReady)
        {
            reasons.Add("Opening-range evidence is not ready.");
            return new MarketOpenDecision(phase, MarketOpenAction.Observe, false, reasons);
        }

        if (!input.SetupReady)
        {
            reasons.Add("No qualified setup is ready.");
            return new MarketOpenDecision(phase, MarketOpenAction.Observe, true, reasons);
        }

        reasons.Add("The approved New York entry window and setup requirements are satisfied.");
        return new MarketOpenDecision(phase, MarketOpenAction.AllowEntry, true, reasons);
    }

    private static MarketOpenPhase ResolvePhase(TimeSpan time)
    {
        if (time < EntryStart) return MarketOpenPhase.PreMarket;
        if (time < OpeningRangeEnd) return MarketOpenPhase.OpeningRange;
        if (time < ReversalWindowEnd) return MarketOpenPhase.ReversalWindow;
        if (time < PullbackWindowStart) return MarketOpenPhase.TrendManagement;
        if (time < EntryCutoff) return MarketOpenPhase.PullbackWindow;
        if (time < CloseoutStart) return MarketOpenPhase.TrendManagement;
        if (time < ForceFlatTime) return MarketOpenPhase.Closeout;
        if (time == ForceFlatTime) return MarketOpenPhase.ForceFlat;
        return MarketOpenPhase.Closed;
    }
}
