using System;

namespace ISE.TradeSupervisor;

/// <summary>
/// Defines the deterministic daily P&amp;L and trade-attempt limits used by the production session supervisor.
/// </summary>
public sealed class DailyPnlGovernancePolicy
{
    public DailyPnlGovernancePolicy(
        decimal greenDayThreshold,
        decimal protectedGreenFloor,
        decimal lowerDailyObjective,
        decimal upperDailyObjective,
        decimal baseRiskPerTrade,
        int maximumTradeAttempts = 2,
        int maximumConsecutiveLosses = 2)
    {
        if (greenDayThreshold <= 0m)
            throw new ArgumentOutOfRangeException(nameof(greenDayThreshold));
        if (protectedGreenFloor < 0m || protectedGreenFloor >= greenDayThreshold)
            throw new ArgumentOutOfRangeException(nameof(protectedGreenFloor));
        if (lowerDailyObjective <= greenDayThreshold)
            throw new ArgumentOutOfRangeException(nameof(lowerDailyObjective));
        if (upperDailyObjective <= lowerDailyObjective)
            throw new ArgumentOutOfRangeException(nameof(upperDailyObjective));
        if (baseRiskPerTrade <= 0m)
            throw new ArgumentOutOfRangeException(nameof(baseRiskPerTrade));
        if (maximumTradeAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumTradeAttempts));
        if (maximumConsecutiveLosses < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumConsecutiveLosses));

        GreenDayThreshold = greenDayThreshold;
        ProtectedGreenFloor = protectedGreenFloor;
        LowerDailyObjective = lowerDailyObjective;
        UpperDailyObjective = upperDailyObjective;
        BaseRiskPerTrade = baseRiskPerTrade;
        MaximumTradeAttempts = maximumTradeAttempts;
        MaximumConsecutiveLosses = maximumConsecutiveLosses;
    }

    /// <summary>Initial production baseline agreed for ISE Elite v1.</summary>
    public static DailyPnlGovernancePolicy ProductionDefault => new DailyPnlGovernancePolicy(
        greenDayThreshold: 300m,
        protectedGreenFloor: 200m,
        lowerDailyObjective: 500m,
        upperDailyObjective: 1000m,
        baseRiskPerTrade: 150m,
        maximumTradeAttempts: 2,
        maximumConsecutiveLosses: 2);

    public decimal GreenDayThreshold { get; }
    public decimal ProtectedGreenFloor { get; }
    public decimal LowerDailyObjective { get; }
    public decimal UpperDailyObjective { get; }
    public decimal BaseRiskPerTrade { get; }
    public int MaximumTradeAttempts { get; }
    public int MaximumConsecutiveLosses { get; }
}
