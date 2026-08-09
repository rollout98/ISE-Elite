using System;

namespace ISE.TradeSupervisor;

public enum IseAccountStage
{
    Combine = 0,
    Funded = 1
}

public enum AccountStageObjectivePriority
{
    Completion = 0,
    Preservation = 1
}

/// <summary>
/// Expresses how the same market/opportunity engine should be governed for a combine versus a funded account.
/// It does not change signal generation and it does not authorize live routing by itself.
/// </summary>
public sealed class AccountStageGovernanceProfile
{
    public AccountStageGovernanceProfile(
        IseAccountStage stage,
        AccountStageObjectivePriority objectivePriority,
        decimal riskExpressionMultiplier,
        decimal greenDayThreshold,
        decimal protectedGreenFloor,
        decimal lowerDailyObjective,
        decimal upperDailyObjective,
        int maximumTradeAttempts,
        int maximumConsecutiveLosses,
        bool allowScalpManagement,
        bool allowCoreManagement,
        bool allowRunnerManagement,
        bool newEntriesAfterLowerObjective,
        bool existingRunnerMayContinueAfterLowerObjective)
    {
        if (riskExpressionMultiplier <= 0m || riskExpressionMultiplier > 1.5m)
            throw new ArgumentOutOfRangeException(nameof(riskExpressionMultiplier));
        if (greenDayThreshold <= 0m)
            throw new ArgumentOutOfRangeException(nameof(greenDayThreshold));
        if (protectedGreenFloor < 0m || protectedGreenFloor >= greenDayThreshold)
            throw new ArgumentOutOfRangeException(nameof(protectedGreenFloor));
        if (lowerDailyObjective <= greenDayThreshold)
            throw new ArgumentOutOfRangeException(nameof(lowerDailyObjective));
        if (upperDailyObjective <= lowerDailyObjective)
            throw new ArgumentOutOfRangeException(nameof(upperDailyObjective));
        if (maximumTradeAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumTradeAttempts));
        if (maximumConsecutiveLosses < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumConsecutiveLosses));

        Stage = stage;
        ObjectivePriority = objectivePriority;
        RiskExpressionMultiplier = riskExpressionMultiplier;
        GreenDayThreshold = greenDayThreshold;
        ProtectedGreenFloor = protectedGreenFloor;
        LowerDailyObjective = lowerDailyObjective;
        UpperDailyObjective = upperDailyObjective;
        MaximumTradeAttempts = maximumTradeAttempts;
        MaximumConsecutiveLosses = maximumConsecutiveLosses;
        AllowScalpManagement = allowScalpManagement;
        AllowCoreManagement = allowCoreManagement;
        AllowRunnerManagement = allowRunnerManagement;
        NewEntriesAfterLowerObjective = newEntriesAfterLowerObjective;
        ExistingRunnerMayContinueAfterLowerObjective = existingRunnerMayContinueAfterLowerObjective;
    }

    public IseAccountStage Stage { get; }
    public AccountStageObjectivePriority ObjectivePriority { get; }
    public decimal RiskExpressionMultiplier { get; }
    public decimal GreenDayThreshold { get; }
    public decimal ProtectedGreenFloor { get; }
    public decimal LowerDailyObjective { get; }
    public decimal UpperDailyObjective { get; }
    public int MaximumTradeAttempts { get; }
    public int MaximumConsecutiveLosses { get; }
    public bool AllowScalpManagement { get; }
    public bool AllowCoreManagement { get; }
    public bool AllowRunnerManagement { get; }
    public bool NewEntriesAfterLowerObjective { get; }
    public bool ExistingRunnerMayContinueAfterLowerObjective { get; }

    /// <summary>
    /// Converts the stage profile into the existing deterministic daily-P&amp;L policy.
    /// baseRiskBudget is supplied by the authoritative risk-sizing layer; the stage only scales expression.
    /// </summary>
    public DailyPnlGovernancePolicy CreateDailyPnlPolicy(decimal baseRiskBudget)
    {
        if (baseRiskBudget <= 0m)
            throw new ArgumentOutOfRangeException(nameof(baseRiskBudget));

        return new DailyPnlGovernancePolicy(
            GreenDayThreshold,
            ProtectedGreenFloor,
            LowerDailyObjective,
            UpperDailyObjective,
            baseRiskBudget * RiskExpressionMultiplier,
            MaximumTradeAttempts,
            MaximumConsecutiveLosses);
    }
}

/// <summary>
/// Initial research baselines. These are deliberately stage-level governance hypotheses, not prop-firm-specific rules.
/// Market-state and opportunity detection stay identical across stages.
/// </summary>
public static class AccountStageGovernanceProfiles
{
    public static AccountStageGovernanceProfile Combine => new AccountStageGovernanceProfile(
        stage: IseAccountStage.Combine,
        objectivePriority: AccountStageObjectivePriority.Completion,
        riskExpressionMultiplier: 1.00m,
        greenDayThreshold: 350m,
        protectedGreenFloor: 200m,
        lowerDailyObjective: 500m,
        upperDailyObjective: 1000m,
        maximumTradeAttempts: 2,
        maximumConsecutiveLosses: 2,
        allowScalpManagement: true,
        allowCoreManagement: true,
        allowRunnerManagement: true,
        newEntriesAfterLowerObjective: false,
        existingRunnerMayContinueAfterLowerObjective: true);

    public static AccountStageGovernanceProfile Funded => new AccountStageGovernanceProfile(
        stage: IseAccountStage.Funded,
        objectivePriority: AccountStageObjectivePriority.Preservation,
        riskExpressionMultiplier: 0.75m,
        greenDayThreshold: 300m,
        protectedGreenFloor: 250m,
        lowerDailyObjective: 500m,
        upperDailyObjective: 1000m,
        maximumTradeAttempts: 2,
        maximumConsecutiveLosses: 2,
        allowScalpManagement: true,
        allowCoreManagement: true,
        allowRunnerManagement: true,
        newEntriesAfterLowerObjective: false,
        existingRunnerMayContinueAfterLowerObjective: true);
}

/// <summary>
/// Portfolio-level projection only. It makes correlated copied-account exposure explicit; it is not diversification logic.
/// </summary>
public sealed class FleetObjectiveProjection
{
    public FleetObjectiveProjection(AccountStageGovernanceProfile profile, int accountCount)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        if (accountCount < 1) throw new ArgumentOutOfRangeException(nameof(accountCount));
        AccountCount = accountCount;
    }

    public AccountStageGovernanceProfile Profile { get; }
    public int AccountCount { get; }
    public decimal FleetLowerObjective => Profile.LowerDailyObjective * AccountCount;
    public decimal FleetUpperObjective => Profile.UpperDailyObjective * AccountCount;

    public decimal FleetPlannedRisk(decimal perAccountPlannedRisk)
    {
        if (perAccountPlannedRisk < 0m)
            throw new ArgumentOutOfRangeException(nameof(perAccountPlannedRisk));
        return perAccountPlannedRisk * AccountCount;
    }
}
