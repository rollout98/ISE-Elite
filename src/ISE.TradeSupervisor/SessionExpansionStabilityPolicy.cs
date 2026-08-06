using System;

namespace ISE.TradeSupervisor;

/// <summary>
/// Defines the deterministic evidence thresholds that must be satisfied before
/// ISE Elite may expand beyond the currently approved New York session scope.
/// </summary>
public sealed class SessionExpansionStabilityPolicy
{
    public SessionExpansionStabilityPolicy(
        int minimumQualifiedTrades,
        decimal minimumWinRatePercent,
        decimal targetWinRatePercent,
        decimal minimumNetExpectancyPerTrade,
        decimal minimumProfitFactor)
    {
        if (minimumQualifiedTrades < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumQualifiedTrades));
        if (minimumWinRatePercent <= 0m || minimumWinRatePercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(minimumWinRatePercent));
        if (targetWinRatePercent < minimumWinRatePercent || targetWinRatePercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(targetWinRatePercent));
        if (minimumNetExpectancyPerTrade < 0m)
            throw new ArgumentOutOfRangeException(nameof(minimumNetExpectancyPerTrade));
        if (minimumProfitFactor <= 1m)
            throw new ArgumentOutOfRangeException(nameof(minimumProfitFactor));

        MinimumQualifiedTrades = minimumQualifiedTrades;
        MinimumWinRatePercent = minimumWinRatePercent;
        TargetWinRatePercent = targetWinRatePercent;
        MinimumNetExpectancyPerTrade = minimumNetExpectancyPerTrade;
        MinimumProfitFactor = minimumProfitFactor;
    }

    /// <summary>
    /// Initial v1 Lab baseline. The 70-80% win-rate band is a target, not a reason
    /// to weaken stops, enlarge losses, or overfit the strategy.
    /// </summary>
    public static SessionExpansionStabilityPolicy ProductionDefault =>
        new SessionExpansionStabilityPolicy(
            minimumQualifiedTrades: 150,
            minimumWinRatePercent: 70m,
            targetWinRatePercent: 80m,
            minimumNetExpectancyPerTrade: 0m,
            minimumProfitFactor: 1.25m);

    public int MinimumQualifiedTrades { get; }
    public decimal MinimumWinRatePercent { get; }
    public decimal TargetWinRatePercent { get; }
    public decimal MinimumNetExpectancyPerTrade { get; }
    public decimal MinimumProfitFactor { get; }
}
