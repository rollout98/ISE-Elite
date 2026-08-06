using ISE.TradeSupervisor;
using Xunit;

namespace ISE.TradeSupervisor.Tests;

public sealed class SessionExpansionStabilityGateTests
{
    private readonly SessionExpansionStabilityGate _gate = new();

    [Fact]
    public void Unstable_core_new_york_holds_current_scope()
    {
        var result = _gate.Evaluate(Input(coreNewYorkOperationallyStable: false));

        Assert.Equal(SessionExpansionGateState.HoldCoreNewYork, result.State);
        Assert.False(result.ExpansionApproved);
    }

    [Fact]
    public void Asia_is_blocked_until_expanded_new_york_is_validated()
    {
        var result = _gate.Evaluate(Input(
            target: SessionExpansionTarget.AsiaResearch,
            expandedNewYorkValidated: false));

        Assert.Equal(SessionExpansionGateState.AsiaBlockedPendingNewYorkValidation, result.State);
        Assert.False(result.ExpansionApproved);
    }

    [Fact]
    public void Costs_must_be_included()
    {
        var result = _gate.Evaluate(Input(commissionsAndSlippageIncluded: false));

        Assert.Equal(SessionExpansionGateState.CostsNotIncluded, result.State);
        Assert.False(result.ExpansionApproved);
    }

    [Fact]
    public void Minimum_sample_is_enforced()
    {
        var result = _gate.Evaluate(Input(qualifiedTrades: 149));

        Assert.Equal(SessionExpansionGateState.InsufficientSample, result.State);
        Assert.False(result.ExpansionApproved);
    }

    [Fact]
    public void Seventy_percent_is_the_minimum_win_rate()
    {
        var result = _gate.Evaluate(Input(winRatePercent: 69.99m));

        Assert.Equal(SessionExpansionGateState.WinRateBelowTarget, result.State);
        Assert.False(result.ExpansionApproved);
    }

    [Fact]
    public void Positive_after_cost_expectancy_is_required()
    {
        var result = _gate.Evaluate(Input(netExpectancyPerTrade: 0m));

        Assert.Equal(SessionExpansionGateState.ExpectancyNotProven, result.State);
        Assert.False(result.ExpansionApproved);
    }

    [Fact]
    public void Configured_profit_factor_is_enforced()
    {
        var result = _gate.Evaluate(Input(profitFactor: 1.24m));

        Assert.Equal(SessionExpansionGateState.ProfitFactorNotProven, result.State);
        Assert.False(result.ExpansionApproved);
    }

    [Fact]
    public void Drawdown_and_losing_streak_limits_remain_authoritative()
    {
        var drawdown = _gate.Evaluate(Input(drawdownWithinApprovedLimit: false));
        var losingStreak = _gate.Evaluate(Input(losingStreakWithinApprovedLimit: false));

        Assert.Equal(SessionExpansionGateState.DrawdownNotAcceptable, drawdown.State);
        Assert.Equal(SessionExpansionGateState.LosingStreakNotAcceptable, losingStreak.State);
    }

    [Fact]
    public void Independent_validation_layers_are_required()
    {
        var outOfSample = _gate.Evaluate(Input(outOfSampleStable: false));
        var walkForward = _gate.Evaluate(Input(walkForwardStable: false));
        var replay = _gate.Evaluate(Input(replayStable: false));
        var forward = _gate.Evaluate(Input(supervisedForwardStable: false));

        Assert.Equal(SessionExpansionGateState.OutOfSampleNotProven, outOfSample.State);
        Assert.Equal(SessionExpansionGateState.WalkForwardNotProven, walkForward.State);
        Assert.Equal(SessionExpansionGateState.ReplayNotProven, replay.State);
        Assert.Equal(SessionExpansionGateState.ForwardTestNotProven, forward.State);
    }

    [Fact]
    public void Regime_and_governance_coverage_are_required()
    {
        var regime = _gate.Evaluate(Input(regimeCoverageComplete: false));
        var governance = _gate.Evaluate(Input(governanceComplianceComplete: false));

        Assert.Equal(SessionExpansionGateState.RegimeCoverageIncomplete, regime.State);
        Assert.Equal(SessionExpansionGateState.GovernanceComplianceFailure, governance.State);
    }

    [Fact]
    public void Profit_must_not_depend_on_a_few_exceptional_days()
    {
        var result = _gate.Evaluate(Input(profitConcentrationAcceptable: false));

        Assert.Equal(SessionExpansionGateState.ProfitConcentrationFailure, result.State);
        Assert.False(result.ExpansionApproved);
    }

    [Fact]
    public void Passing_core_new_york_evidence_approves_new_york_expansion()
    {
        var result = _gate.Evaluate(Input());

        Assert.Equal(SessionExpansionGateState.NewYorkExpansionApproved, result.State);
        Assert.True(result.ExpansionApproved);
    }

    [Fact]
    public void Win_rate_above_target_band_does_not_bypass_other_controls_or_fail_automatically()
    {
        var result = _gate.Evaluate(Input(winRatePercent: 85m));

        Assert.Equal(SessionExpansionGateState.NewYorkExpansionApproved, result.State);
        Assert.True(result.ExpansionApproved);
        Assert.Contains(result.Reasons, reason => reason.Contains("anti-overfitting"));
    }

    [Fact]
    public void Validated_expanded_new_york_can_approve_asia_research()
    {
        var result = _gate.Evaluate(Input(
            target: SessionExpansionTarget.AsiaResearch,
            expandedNewYorkValidated: true));

        Assert.Equal(SessionExpansionGateState.AsiaResearchApproved, result.State);
        Assert.True(result.ExpansionApproved);
    }

    private static SessionExpansionStabilityInput Input(
        SessionExpansionTarget target = SessionExpansionTarget.ExpandedNewYork,
        bool coreNewYorkOperationallyStable = true,
        int qualifiedTrades = 175,
        decimal winRatePercent = 75m,
        decimal netExpectancyPerTrade = 42m,
        decimal profitFactor = 1.65m,
        bool commissionsAndSlippageIncluded = true,
        bool drawdownWithinApprovedLimit = true,
        bool losingStreakWithinApprovedLimit = true,
        bool outOfSampleStable = true,
        bool walkForwardStable = true,
        bool replayStable = true,
        bool supervisedForwardStable = true,
        bool regimeCoverageComplete = true,
        bool governanceComplianceComplete = true,
        bool profitConcentrationAcceptable = true,
        bool expandedNewYorkValidated = false)
        => new(
            SessionExpansionStabilityPolicy.ProductionDefault,
            target,
            coreNewYorkOperationallyStable,
            qualifiedTrades,
            winRatePercent,
            netExpectancyPerTrade,
            profitFactor,
            commissionsAndSlippageIncluded,
            drawdownWithinApprovedLimit,
            losingStreakWithinApprovedLimit,
            outOfSampleStable,
            walkForwardStable,
            replayStable,
            supervisedForwardStable,
            regimeCoverageComplete,
            governanceComplianceComplete,
            profitConcentrationAcceptable,
            expandedNewYorkValidated);
}
