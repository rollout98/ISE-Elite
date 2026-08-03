using System;

namespace ISE.Confidence;

/// <summary>Provides normalized evidence for confidence scoring.</summary>
public sealed class ConfidenceInput
{
    /// <summary>Initializes confidence evidence values from zero to one.</summary>
    public ConfidenceInput(decimal marketState, decimal higherTimeframeBias, decimal trendStrength, decimal liquidity, decimal sessionQuality, decimal playbookQuality, decimal rewardToRisk, decimal volatilityQuality, decimal timeOfDay, bool hardRiskBlock = false)
    {
        MarketState = ValidateUnit(marketState, nameof(marketState));
        HigherTimeframeBias = ValidateUnit(higherTimeframeBias, nameof(higherTimeframeBias));
        TrendStrength = ValidateUnit(trendStrength, nameof(trendStrength));
        Liquidity = ValidateUnit(liquidity, nameof(liquidity));
        SessionQuality = ValidateUnit(sessionQuality, nameof(sessionQuality));
        PlaybookQuality = ValidateUnit(playbookQuality, nameof(playbookQuality));
        RewardToRisk = ValidateUnit(rewardToRisk, nameof(rewardToRisk));
        VolatilityQuality = ValidateUnit(volatilityQuality, nameof(volatilityQuality));
        TimeOfDay = ValidateUnit(timeOfDay, nameof(timeOfDay));
        HardRiskBlock = hardRiskBlock;
    }

    /// <summary>Gets market-state alignment.</summary>
    public decimal MarketState { get; }
    /// <summary>Gets higher-timeframe directional alignment.</summary>
    public decimal HigherTimeframeBias { get; }
    /// <summary>Gets trend quality.</summary>
    public decimal TrendStrength { get; }
    /// <summary>Gets liquidity quality.</summary>
    public decimal Liquidity { get; }
    /// <summary>Gets session quality.</summary>
    public decimal SessionQuality { get; }
    /// <summary>Gets playbook quality.</summary>
    public decimal PlaybookQuality { get; }
    /// <summary>Gets reward-to-risk quality.</summary>
    public decimal RewardToRisk { get; }
    /// <summary>Gets volatility suitability.</summary>
    public decimal VolatilityQuality { get; }
    /// <summary>Gets time-of-day suitability.</summary>
    public decimal TimeOfDay { get; }
    /// <summary>Gets whether an authoritative risk condition blocks the trade.</summary>
    public bool HardRiskBlock { get; }

    private static decimal ValidateUnit(decimal value, string name)
    {
        if (value < 0m || value > 1m)
            throw new ArgumentOutOfRangeException(name, "Value must be between zero and one.");
        return value;
    }
}
