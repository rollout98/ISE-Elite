using System;

namespace ISE.ExecutionIntelligence;

/// <summary>Provides market and control inputs for execution selection.</summary>
public sealed class ExecutionIntelligenceInput
{
    /// <summary>Initializes an execution-intelligence request.</summary>
    public ExecutionIntelligenceInput(int desiredContracts, decimal spreadTicks, decimal liquidityScore, decimal volatilityScore, decimal confidenceScore, bool newsLock = false, bool riskLock = false)
    {
        if (desiredContracts < 1)
            throw new ArgumentOutOfRangeException(nameof(desiredContracts));
        if (spreadTicks < 0m)
            throw new ArgumentOutOfRangeException(nameof(spreadTicks));

        DesiredContracts = desiredContracts;
        SpreadTicks = spreadTicks;
        LiquidityScore = ValidateScore(liquidityScore, nameof(liquidityScore));
        VolatilityScore = ValidateScore(volatilityScore, nameof(volatilityScore));
        ConfidenceScore = ValidateScore(confidenceScore, nameof(confidenceScore));
        NewsLock = newsLock;
        RiskLock = riskLock;
    }

    /// <summary>Gets the requested contract quantity.</summary>
    public int DesiredContracts { get; }

    /// <summary>Gets the current bid-ask spread in ticks.</summary>
    public decimal SpreadTicks { get; }

    /// <summary>Gets liquidity quality from zero to one hundred.</summary>
    public decimal LiquidityScore { get; }

    /// <summary>Gets volatility intensity from zero to one hundred.</summary>
    public decimal VolatilityScore { get; }

    /// <summary>Gets strategy confidence from zero to one hundred.</summary>
    public decimal ConfidenceScore { get; }

    /// <summary>Gets whether an authoritative news lock is active.</summary>
    public bool NewsLock { get; }

    /// <summary>Gets whether an authoritative risk lock is active.</summary>
    public bool RiskLock { get; }

    private static decimal ValidateScore(decimal value, string name)
    {
        if (value < 0m || value > 100m)
            throw new ArgumentOutOfRangeException(name, "Score must be between zero and one hundred.");
        return value;
    }
}
