using ISE.TradeSupervisor;
using Xunit;

namespace ISE.TradeSupervisor.Tests;

public sealed class AccountStageGovernanceProfileTests
{
    [Fact]
    public void Funded_profile_expresses_less_risk_than_combine_for_same_authoritative_budget()
    {
        const decimal baseRisk = 200m;
        var combine = AccountStageGovernanceProfiles.Combine.CreateDailyPnlPolicy(baseRisk);
        var funded = AccountStageGovernanceProfiles.Funded.CreateDailyPnlPolicy(baseRisk);

        Assert.Equal(200m, combine.BaseRiskPerTrade);
        Assert.Equal(150m, funded.BaseRiskPerTrade);
        Assert.True(funded.BaseRiskPerTrade < combine.BaseRiskPerTrade);
    }

    [Fact]
    public void Funded_profile_treats_five_hundred_as_success_and_blocks_new_entries()
    {
        var profile = AccountStageGovernanceProfiles.Funded;
        var policy = profile.CreateDailyPnlPolicy(200m);
        var engine = new DailyPnlGovernanceEngine();

        var decision = engine.Evaluate(new DailyPnlGovernanceInput(
            policy,
            realizedPnl: 500m,
            openPnl: 0m,
            completedTradeAttempts: 1,
            consecutiveLosses: 0,
            positionOpen: false,
            runnerQualified: false,
            setupQualified: true,
            exceptionalSetup: true,
            cooldownComplete: true));

        Assert.Equal(500m, profile.LowerDailyObjective);
        Assert.False(profile.NewEntriesAfterLowerObjective);
        Assert.Equal(DailyPnlGovernanceState.ObjectiveReached, decision.State);
        Assert.False(decision.NewEntriesPermitted);
    }

    [Fact]
    public void Existing_funded_runner_may_continue_after_five_hundred_under_protection()
    {
        var profile = AccountStageGovernanceProfiles.Funded;
        var engine = new DailyPnlGovernanceEngine();
        var decision = engine.Evaluate(new DailyPnlGovernanceInput(
            profile.CreateDailyPnlPolicy(200m),
            realizedPnl: 100m,
            openPnl: 450m,
            completedTradeAttempts: 1,
            consecutiveLosses: 0,
            positionOpen: true,
            runnerQualified: true,
            setupQualified: false,
            exceptionalSetup: false,
            cooldownComplete: true));

        Assert.True(profile.ExistingRunnerMayContinueAfterLowerObjective);
        Assert.True(decision.ExistingRunnerMayContinue);
        Assert.True(decision.ProtectOpenProfit);
        Assert.False(decision.FlattenImmediately);
    }

    [Fact]
    public void Funded_green_day_preserves_more_of_three_hundred_than_combine_baseline()
    {
        var funded = AccountStageGovernanceProfiles.Funded;
        var combine = AccountStageGovernanceProfiles.Combine;

        Assert.Equal(250m, funded.ProtectedGreenFloor);
        Assert.Equal(200m, combine.ProtectedGreenFloor);
        Assert.True(funded.ProtectedGreenFloor > combine.ProtectedGreenFloor);
    }

    [Fact]
    public void Twenty_funded_accounts_project_ten_thousand_lower_daily_objective()
    {
        var projection = new FleetObjectiveProjection(AccountStageGovernanceProfiles.Funded, 20);

        Assert.Equal(10000m, projection.FleetLowerObjective);
        Assert.Equal(20000m, projection.FleetUpperObjective);
    }

    [Fact]
    public void Fleet_projection_treats_copied_risk_as_correlated_exposure()
    {
        var projection = new FleetObjectiveProjection(AccountStageGovernanceProfiles.Funded, 20);

        Assert.Equal(3000m, projection.FleetPlannedRisk(150m));
    }
}
