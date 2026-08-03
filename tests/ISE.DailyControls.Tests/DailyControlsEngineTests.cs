using ISE.DailyControls;
using Xunit;

namespace ISE.DailyControls.Tests;

public sealed class DailyControlsEngineTests
{
    private static DailyControlProfile Profile() => new(
        preferredDailyProfit: 750m,
        maximumDailyProfit: 10000m,
        dailyLossLimit: 500m,
        maximumConsecutiveLosses: 2,
        maximumTradesPerDay: 4,
        reducedRiskMultiplier: 0.5m,
        allowExceptionalSetupsAfterTarget: true);

    [Fact]
    public void Normal_account_state_allows_full_risk()
    {
        var decision = new DailyControlsEngine().Evaluate(
            new DailyControlInput(Profile(), 250m, 0, 1, false, false, false));

        Assert.Equal(DailyControlAction.AllowTrading, decision.Action);
        Assert.Equal(DailyControlReason.TradingPermitted, decision.Reason);
        Assert.Equal(1m, decision.RiskMultiplier);
    }

    [Fact]
    public void Exceptional_setup_after_target_uses_reduced_risk()
    {
        var decision = new DailyControlsEngine().Evaluate(
            new DailyControlInput(Profile(), 900m, 0, 2, true, false, false));

        Assert.Equal(DailyControlAction.ReduceRisk, decision.Action);
        Assert.Equal(DailyControlReason.ExceptionalSetupPermitted, decision.Reason);
        Assert.Equal(0.5m, decision.RiskMultiplier);
    }

    [Fact]
    public void Consecutive_loss_limit_stops_trading()
    {
        var decision = new DailyControlsEngine().Evaluate(
            new DailyControlInput(Profile(), -200m, 2, 2, false, false, false));

        Assert.False(decision.CanInitiateTrade);
        Assert.Equal(DailyControlReason.ConsecutiveLossLimitReached, decision.Reason);
    }

    [Fact]
    public void Session_shutdown_requires_force_flat()
    {
        var decision = new DailyControlsEngine().Evaluate(
            new DailyControlInput(Profile(), 100m, 0, 1, false, false, true));

        Assert.Equal(DailyControlAction.ForceFlat, decision.Action);
        Assert.Equal(DailyControlReason.SessionShutdown, decision.Reason);
        Assert.Equal(0m, decision.RiskMultiplier);
    }
}
