using System;
using System.Collections.Generic;

namespace ISE.TradeSupervisor;

public enum DailyPnlGovernanceState
{
    Monitor,
    EntryEligible,
    Cooldown,
    GreenDayProtection,
    ObjectiveReached,
    UpperObjectiveReached,
    TradeLimitLockout,
    LossLockout,
    RiskLockout,
    ForceFlat
}

public sealed class DailyPnlGovernanceInput
{
    public DailyPnlGovernanceInput(
        DailyPnlGovernancePolicy policy,
        decimal realizedPnl,
        decimal openPnl,
        int completedTradeAttempts,
        int consecutiveLosses,
        bool positionOpen,
        bool runnerQualified,
        bool setupQualified,
        bool exceptionalSetup,
        bool cooldownComplete,
        bool authoritativeRiskBlock = false,
        bool forceFlatWindow = false)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (completedTradeAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(completedTradeAttempts));
        if (consecutiveLosses < 0)
            throw new ArgumentOutOfRangeException(nameof(consecutiveLosses));
        if (runnerQualified && !positionOpen)
            throw new ArgumentException("A qualified runner requires an open position.", nameof(runnerQualified));

        RealizedPnl = realizedPnl;
        OpenPnl = openPnl;
        CompletedTradeAttempts = completedTradeAttempts;
        ConsecutiveLosses = consecutiveLosses;
        PositionOpen = positionOpen;
        RunnerQualified = runnerQualified;
        SetupQualified = setupQualified;
        ExceptionalSetup = exceptionalSetup;
        CooldownComplete = cooldownComplete;
        AuthoritativeRiskBlock = authoritativeRiskBlock;
        ForceFlatWindow = forceFlatWindow;
    }

    public DailyPnlGovernancePolicy Policy { get; }
    public decimal RealizedPnl { get; }
    public decimal OpenPnl { get; }
    public decimal TotalPnl => RealizedPnl + OpenPnl;
    public int CompletedTradeAttempts { get; }
    public int ConsecutiveLosses { get; }
    public bool PositionOpen { get; }
    public bool RunnerQualified { get; }
    public bool SetupQualified { get; }
    public bool ExceptionalSetup { get; }
    public bool CooldownComplete { get; }
    public bool AuthoritativeRiskBlock { get; }
    public bool ForceFlatWindow { get; }
}

public sealed class DailyPnlGovernanceDecision
{
    public DailyPnlGovernanceDecision(
        DailyPnlGovernanceState state,
        bool newEntriesPermitted,
        bool existingRunnerMayContinue,
        bool protectOpenProfit,
        bool flattenImmediately,
        decimal maximumNewTradeRisk,
        decimal protectedDailyPnlFloor,
        IReadOnlyList<string> reasons)
    {
        if (maximumNewTradeRisk < 0m)
            throw new ArgumentOutOfRangeException(nameof(maximumNewTradeRisk));
        if (protectedDailyPnlFloor < 0m)
            throw new ArgumentOutOfRangeException(nameof(protectedDailyPnlFloor));

        State = state;
        NewEntriesPermitted = newEntriesPermitted;
        ExistingRunnerMayContinue = existingRunnerMayContinue;
        ProtectOpenProfit = protectOpenProfit;
        FlattenImmediately = flattenImmediately;
        MaximumNewTradeRisk = maximumNewTradeRisk;
        ProtectedDailyPnlFloor = protectedDailyPnlFloor;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public DailyPnlGovernanceState State { get; }
    public bool NewEntriesPermitted { get; }
    public bool ExistingRunnerMayContinue { get; }
    public bool ProtectOpenProfit { get; }
    public bool FlattenImmediately { get; }
    public decimal MaximumNewTradeRisk { get; }
    public decimal ProtectedDailyPnlFloor { get; }
    public IReadOnlyList<string> Reasons { get; }
}

/// <summary>
/// Applies the ISE Elite green-day, daily-objective, cooldown, and two-attempt production rules.
/// This engine governs permission and risk budget; it does not select entries or widen stops.
/// </summary>
public sealed class DailyPnlGovernanceEngine
{
    public DailyPnlGovernanceDecision Evaluate(DailyPnlGovernanceInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var policy = input.Policy;
        var reasons = new List<string>();

        if (input.ForceFlatWindow)
        {
            reasons.Add("The authoritative force-flat window is active.");
            return Decision(DailyPnlGovernanceState.ForceFlat, false, false, true,
                input.PositionOpen, 0m, 0m, reasons);
        }

        if (input.AuthoritativeRiskBlock)
        {
            reasons.Add("Authoritative risk control blocks all additional exposure.");
            return Decision(DailyPnlGovernanceState.RiskLockout, false, false, true,
                input.PositionOpen, 0m, 0m, reasons);
        }

        if (input.TotalPnl >= policy.UpperDailyObjective)
        {
            reasons.Add("The upper daily objective has been reached; flatten and lock the day.");
            return Decision(DailyPnlGovernanceState.UpperObjectiveReached, false, false, true,
                input.PositionOpen, 0m, policy.UpperDailyObjective, reasons);
        }

        if (input.PositionOpen)
            return EvaluateOpenPosition(input, reasons);

        if (input.RealizedPnl >= policy.LowerDailyObjective)
        {
            reasons.Add("The lower daily objective has been realized; new entries are locked.");
            return Decision(DailyPnlGovernanceState.ObjectiveReached, false, false, false,
                false, 0m, policy.LowerDailyObjective, reasons);
        }

        if (input.ConsecutiveLosses >= policy.MaximumConsecutiveLosses)
        {
            reasons.Add("The maximum consecutive-loss limit has been reached.");
            return Decision(DailyPnlGovernanceState.LossLockout, false, false, false,
                false, 0m, 0m, reasons);
        }

        if (input.CompletedTradeAttempts >= policy.MaximumTradeAttempts)
        {
            reasons.Add("The maximum number of completed trade attempts has been reached.");
            return Decision(DailyPnlGovernanceState.TradeLimitLockout, false, false, false,
                false, 0m, 0m, reasons);
        }

        if (input.ConsecutiveLosses > 0 && !input.CooldownComplete)
        {
            reasons.Add("A completed losing trade requires cooldown before another entry may be considered.");
            return Decision(DailyPnlGovernanceState.Cooldown, false, false, false,
                false, 0m, 0m, reasons);
        }

        if (!input.SetupQualified)
        {
            reasons.Add("No qualified continuation, reversal, or range-resolution setup is present.");
            return Decision(DailyPnlGovernanceState.Monitor, false, false, false,
                false, 0m, 0m, reasons);
        }

        if (input.RealizedPnl >= policy.GreenDayThreshold)
            return EvaluateGreenDayEntry(input, reasons);

        reasons.Add("A qualified setup is present within the daily trade and risk limits.");
        return Decision(DailyPnlGovernanceState.EntryEligible, true, false, false,
            false, policy.BaseRiskPerTrade, 0m, reasons);
    }

    private static DailyPnlGovernanceDecision EvaluateOpenPosition(
        DailyPnlGovernanceInput input,
        List<string> reasons)
    {
        var policy = input.Policy;

        if (input.TotalPnl >= policy.LowerDailyObjective)
        {
            if (input.RunnerQualified)
            {
                reasons.Add("The lower daily objective has been reached by an existing qualified runner.");
                reasons.Add("New entries are locked while the runner may continue toward the upper objective under profit protection.");
                return Decision(DailyPnlGovernanceState.ObjectiveReached, false, true, true,
                    false, 0m, policy.LowerDailyObjective, reasons);
            }

            reasons.Add("The lower daily objective has been reached without a qualified runner; flatten and lock the day.");
            return Decision(DailyPnlGovernanceState.ObjectiveReached, false, false, true,
                true, 0m, policy.LowerDailyObjective, reasons);
        }

        if (input.TotalPnl >= policy.GreenDayThreshold)
        {
            reasons.Add("The session is meaningfully green; protect open profit and prohibit additional entries.");
            return Decision(DailyPnlGovernanceState.GreenDayProtection, false,
                input.RunnerQualified, true, false, 0m, policy.ProtectedGreenFloor, reasons);
        }

        reasons.Add("An open position remains under the normal trade-supervision process.");
        return Decision(DailyPnlGovernanceState.Monitor, false,
            input.RunnerQualified, false, false, 0m, 0m, reasons);
    }

    private static DailyPnlGovernanceDecision EvaluateGreenDayEntry(
        DailyPnlGovernanceInput input,
        List<string> reasons)
    {
        var policy = input.Policy;

        if (!input.ExceptionalSetup)
        {
            reasons.Add("The session is meaningfully green and the proposed setup is not exceptional.");
            reasons.Add("Stand aside rather than risk a respectable green day while chasing the upper objective.");
            return Decision(DailyPnlGovernanceState.GreenDayProtection, false, false, false,
                false, 0m, policy.ProtectedGreenFloor, reasons);
        }

        var availableAboveFloor = input.RealizedPnl - policy.ProtectedGreenFloor;
        var maximumRisk = Math.Min(policy.BaseRiskPerTrade, Math.Max(0m, availableAboveFloor));
        if (maximumRisk <= 0m)
        {
            reasons.Add("No risk budget remains above the protected green-day floor.");
            return Decision(DailyPnlGovernanceState.GreenDayProtection, false, false, false,
                false, 0m, policy.ProtectedGreenFloor, reasons);
        }

        reasons.Add("An exceptional setup is present during green-day protection.");
        reasons.Add("The next-trade risk budget is reduced so the protected daily floor cannot be breached by planned risk.");
        return Decision(DailyPnlGovernanceState.GreenDayProtection, true, false, false,
            false, maximumRisk, policy.ProtectedGreenFloor, reasons);
    }

    private static DailyPnlGovernanceDecision Decision(
        DailyPnlGovernanceState state,
        bool newEntriesPermitted,
        bool runnerMayContinue,
        bool protectOpenProfit,
        bool flattenImmediately,
        decimal maximumNewTradeRisk,
        decimal protectedDailyPnlFloor,
        IReadOnlyList<string> reasons)
        => new DailyPnlGovernanceDecision(state, newEntriesPermitted, runnerMayContinue,
            protectOpenProfit, flattenImmediately, maximumNewTradeRisk,
            protectedDailyPnlFloor, reasons);
}
