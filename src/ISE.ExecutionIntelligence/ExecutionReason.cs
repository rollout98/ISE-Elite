namespace ISE.ExecutionIntelligence;

/// <summary>Identifies the primary reason for an execution recommendation.</summary>
public enum ExecutionReason
{
    /// <summary>No specific reason is available.</summary>
    None = 0,
    /// <summary>An authoritative news lock blocks execution.</summary>
    NewsLock = 1,
    /// <summary>An authoritative risk lock blocks execution.</summary>
    RiskLock = 2,
    /// <summary>The spread requires passive execution.</summary>
    WideSpread = 3,
    /// <summary>Extreme volatility requires reduced size.</summary>
    ExtremeVolatility = 4,
    /// <summary>Low liquidity requires passive execution.</summary>
    LowLiquidity = 5,
    /// <summary>Elite conditions support immediate market execution.</summary>
    EliteImmediateExecution = 6,
    /// <summary>Normal conditions support aggressive limit execution.</summary>
    StandardExecution = 7
}
