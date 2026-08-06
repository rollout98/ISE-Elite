using System;
using System.Collections.Generic;

namespace ISE.TradeSupervisor;

public enum SessionExpansionTarget
{
    ExpandedNewYork,
    AsiaResearch
}

public enum SessionExpansionGateState
{
    HoldCoreNewYork,
    AsiaBlockedPendingNewYorkValidation,
    CostsNotIncluded,
    InsufficientSample,
    WinRateBelowTarget,
    ExpectancyNotProven,
    ProfitFactorNotProven,
    DrawdownNotAcceptable,
    LosingStreakNotAcceptable,
    OutOfSampleNotProven,
    WalkForwardNotProven,
    ReplayNotProven,
    ForwardTestNotProven,
    RegimeCoverageIncomplete,
    GovernanceComplianceFailure,
    ProfitConcentrationFailure,
    NewYorkExpansionApproved,
    AsiaResearchApproved
}

public sealed class SessionExpansionStabilityInput
{
    public SessionExpansionStabilityInput(
        SessionExpansionStabilityPolicy policy,
        SessionExpansionTarget target,
        bool coreNewYorkOperationallyStable,
        int qualifiedTrades,
        decimal winRatePercent,
        decimal netExpectancyPerTrade,
        decimal profitFactor,
        bool commissionsAndSlippageIncluded,
        bool drawdownWithinApprovedLimit,
        bool losingStreakWithinApprovedLimit,
        bool outOfSampleStable,
        bool walkForwardStable,
        bool replayStable,
        bool supervisedForwardStable,
        bool regimeCoverageComplete,
        bool governanceComplianceComplete,
        bool profitConcentrationAcceptable,
        bool expandedNewYorkValidated = false)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (qualifiedTrades < 0)
            throw new ArgumentOutOfRangeException(nameof(qualifiedTrades));
        if (winRatePercent < 0m || winRatePercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(winRatePercent));
        if (profitFactor < 0m)
            throw new ArgumentOutOfRangeException(nameof(profitFactor));

        Target = target;
        CoreNewYorkOperationallyStable = coreNewYorkOperationallyStable;
        QualifiedTrades = qualifiedTrades;
        WinRatePercent = winRatePercent;
        NetExpectancyPerTrade = netExpectancyPerTrade;
        ProfitFactor = profitFactor;
        CommissionsAndSlippageIncluded = commissionsAndSlippageIncluded;
        DrawdownWithinApprovedLimit = drawdownWithinApprovedLimit;
        LosingStreakWithinApprovedLimit = losingStreakWithinApprovedLimit;
        OutOfSampleStable = outOfSampleStable;
        WalkForwardStable = walkForwardStable;
        ReplayStable = replayStable;
        SupervisedForwardStable = supervisedForwardStable;
        RegimeCoverageComplete = regimeCoverageComplete;
        GovernanceComplianceComplete = governanceComplianceComplete;
        ProfitConcentrationAcceptable = profitConcentrationAcceptable;
        ExpandedNewYorkValidated = expandedNewYorkValidated;
    }

    public SessionExpansionStabilityPolicy Policy { get; }
    public SessionExpansionTarget Target { get; }
    public bool CoreNewYorkOperationallyStable { get; }
    public int QualifiedTrades { get; }
    public decimal WinRatePercent { get; }
    public decimal NetExpectancyPerTrade { get; }
    public decimal ProfitFactor { get; }
    public bool CommissionsAndSlippageIncluded { get; }
    public bool DrawdownWithinApprovedLimit { get; }
    public bool LosingStreakWithinApprovedLimit { get; }
    public bool OutOfSampleStable { get; }
    public bool WalkForwardStable { get; }
    public bool ReplayStable { get; }
    public bool SupervisedForwardStable { get; }
    public bool RegimeCoverageComplete { get; }
    public bool GovernanceComplianceComplete { get; }
    public bool ProfitConcentrationAcceptable { get; }
    public bool ExpandedNewYorkValidated { get; }
}

public sealed class SessionExpansionStabilityDecision
{
    public SessionExpansionStabilityDecision(
        SessionExpansionGateState state,
        SessionExpansionTarget target,
        bool expansionApproved,
        IReadOnlyList<string> reasons)
    {
        State = state;
        Target = target;
        ExpansionApproved = expansionApproved;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public SessionExpansionGateState State { get; }
    public SessionExpansionTarget Target { get; }
    public bool ExpansionApproved { get; }
    public IReadOnlyList<string> Reasons { get; }
}

/// <summary>
/// Prevents session expansion until the currently approved New York strategy
/// demonstrates durable, after-cost performance across independent validation layers.
/// This component authorizes research scope only; it does not place or manage orders.
/// </summary>
public sealed class SessionExpansionStabilityGate
{
    public SessionExpansionStabilityDecision Evaluate(SessionExpansionStabilityInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var policy = input.Policy;
        var reasons = new List<string>();

        if (!input.CoreNewYorkOperationallyStable)
            return Block(input, SessionExpansionGateState.HoldCoreNewYork,
                "The core New York session is not yet operationally stable.", reasons);

        if (input.Target == SessionExpansionTarget.AsiaResearch && !input.ExpandedNewYorkValidated)
            return Block(input, SessionExpansionGateState.AsiaBlockedPendingNewYorkValidation,
                "Asia research remains blocked until the expanded New York model is independently validated.", reasons);

        if (!input.CommissionsAndSlippageIncluded)
            return Block(input, SessionExpansionGateState.CostsNotIncluded,
                "Performance evidence must include commissions and realistic slippage.", reasons);

        if (input.QualifiedTrades < policy.MinimumQualifiedTrades)
            return Block(input, SessionExpansionGateState.InsufficientSample,
                $"Only {input.QualifiedTrades} qualified trades are available; at least {policy.MinimumQualifiedTrades} are required.", reasons);

        if (input.WinRatePercent < policy.MinimumWinRatePercent)
            return Block(input, SessionExpansionGateState.WinRateBelowTarget,
                $"The after-cost win rate of {input.WinRatePercent:0.##}% is below the {policy.MinimumWinRatePercent:0.##}% stability threshold.", reasons);

        if (input.NetExpectancyPerTrade <= policy.MinimumNetExpectancyPerTrade)
            return Block(input, SessionExpansionGateState.ExpectancyNotProven,
                "Net expectancy per trade is not positive after costs.", reasons);

        if (input.ProfitFactor < policy.MinimumProfitFactor)
            return Block(input, SessionExpansionGateState.ProfitFactorNotProven,
                $"Profit factor {input.ProfitFactor:0.##} is below the configured minimum of {policy.MinimumProfitFactor:0.##}.", reasons);

        if (!input.DrawdownWithinApprovedLimit)
            return Block(input, SessionExpansionGateState.DrawdownNotAcceptable,
                "Maximum drawdown is outside the approved account and fleet risk limits.", reasons);

        if (!input.LosingStreakWithinApprovedLimit)
            return Block(input, SessionExpansionGateState.LosingStreakNotAcceptable,
                "Observed losing streaks exceed the approved stability limit.", reasons);

        if (!input.OutOfSampleStable)
            return Block(input, SessionExpansionGateState.OutOfSampleNotProven,
                "Out-of-sample performance is not yet stable.", reasons);

        if (!input.WalkForwardStable)
            return Block(input, SessionExpansionGateState.WalkForwardNotProven,
                "Walk-forward performance is not yet stable.", reasons);

        if (!input.ReplayStable)
            return Block(input, SessionExpansionGateState.ReplayNotProven,
                "Historical replay validation is incomplete or unstable.", reasons);

        if (!input.SupervisedForwardStable)
            return Block(input, SessionExpansionGateState.ForwardTestNotProven,
                "Supervised forward testing is incomplete or unstable.", reasons);

        if (!input.RegimeCoverageComplete)
            return Block(input, SessionExpansionGateState.RegimeCoverageIncomplete,
                "Evidence does not yet cover trend, reversal, deep-pullback, volatile-auction, and no-trade regimes.", reasons);

        if (!input.GovernanceComplianceComplete)
            return Block(input, SessionExpansionGateState.GovernanceComplianceFailure,
                "The strategy has not demonstrated full compliance with daily P&L, cooldown, trade-limit, and force-flat governance.", reasons);

        if (!input.ProfitConcentrationAcceptable)
            return Block(input, SessionExpansionGateState.ProfitConcentrationFailure,
                "Performance depends too heavily on a small number of unusually profitable days.", reasons);

        if (input.WinRatePercent > policy.TargetWinRatePercent)
            reasons.Add("Win rate exceeds the target band; approval still requires unchanged risk controls and anti-overfitting review.");
        else
            reasons.Add($"Win rate is within the intended {policy.MinimumWinRatePercent:0.##}-{policy.TargetWinRatePercent:0.##}% target band.");

        reasons.Add("All required after-cost stability, risk, validation, regime, and governance evidence has passed.");

        return input.Target == SessionExpansionTarget.AsiaResearch
            ? new SessionExpansionStabilityDecision(SessionExpansionGateState.AsiaResearchApproved,
                input.Target, true, reasons)
            : new SessionExpansionStabilityDecision(SessionExpansionGateState.NewYorkExpansionApproved,
                input.Target, true, reasons);
    }

    private static SessionExpansionStabilityDecision Block(
        SessionExpansionStabilityInput input,
        SessionExpansionGateState state,
        string reason,
        List<string> reasons)
    {
        reasons.Add(reason);
        reasons.Add("Remain within the currently approved New York session scope.");
        return new SessionExpansionStabilityDecision(state, input.Target, false, reasons);
    }
}
