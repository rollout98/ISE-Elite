using System;
using System.Collections.Generic;
using ISE.MarketOpen;
using ISE.ORBIntelligence;

namespace ISE.MarketNarrative;

public enum NarrativeBias
{
    Neutral,
    Bullish,
    Bearish
}

public enum NarrativePhase
{
    Balance,
    Expansion,
    Pullback,
    Continuation,
    Distribution,
    Reversal,
    StandAside
}

public sealed class MarketNarrativeInput
{
    public MarketNarrativeInput(
        MarketOpenPhase marketOpenPhase,
        ORBState orbState,
        ORBDirection orbDirection,
        decimal structureScore,
        decimal orderFlowScore,
        decimal liquidityScore,
        bool acceptance,
        bool rejection,
        bool pullbackDeveloping,
        bool continuationConfirmed,
        bool authoritativeRiskBlock = false)
    {
        ValidateScore(structureScore, nameof(structureScore));
        ValidateScore(orderFlowScore, nameof(orderFlowScore));
        ValidateScore(liquidityScore, nameof(liquidityScore));

        MarketOpenPhase = marketOpenPhase;
        OrbState = orbState;
        OrbDirection = orbDirection;
        StructureScore = structureScore;
        OrderFlowScore = orderFlowScore;
        LiquidityScore = liquidityScore;
        Acceptance = acceptance;
        Rejection = rejection;
        PullbackDeveloping = pullbackDeveloping;
        ContinuationConfirmed = continuationConfirmed;
        AuthoritativeRiskBlock = authoritativeRiskBlock;
    }

    public MarketOpenPhase MarketOpenPhase { get; }
    public ORBState OrbState { get; }
    public ORBDirection OrbDirection { get; }
    public decimal StructureScore { get; }
    public decimal OrderFlowScore { get; }
    public decimal LiquidityScore { get; }
    public bool Acceptance { get; }
    public bool Rejection { get; }
    public bool PullbackDeveloping { get; }
    public bool ContinuationConfirmed { get; }
    public bool AuthoritativeRiskBlock { get; }

    private static void ValidateScore(decimal value, string parameterName)
    {
        if (value < 0m || value > 100m)
            throw new ArgumentOutOfRangeException(parameterName, "Scores must be between 0 and 100.");
    }
}

public sealed class MarketNarrativeDecision
{
    public MarketNarrativeDecision(NarrativeBias bias, NarrativePhase phase, int strength,
        bool trendHealthy, bool pullbackExpected, bool runnerLikely,
        IReadOnlyList<string> reasons)
    {
        if (strength < 0 || strength > 100)
            throw new ArgumentOutOfRangeException(nameof(strength));

        Bias = bias;
        Phase = phase;
        Strength = strength;
        TrendHealthy = trendHealthy;
        PullbackExpected = pullbackExpected;
        RunnerLikely = runnerLikely;
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public NarrativeBias Bias { get; }
    public NarrativePhase Phase { get; }
    public int Strength { get; }
    public bool TrendHealthy { get; }
    public bool PullbackExpected { get; }
    public bool RunnerLikely { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class MarketNarrativeEngine
{
    public MarketNarrativeDecision Evaluate(MarketNarrativeInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var reasons = new List<string>();

        if (input.AuthoritativeRiskBlock)
        {
            reasons.Add("Authoritative risk control overrides the market narrative.");
            return Decision(NarrativeBias.Neutral, NarrativePhase.StandAside, 0, false, false, false, reasons);
        }

        var bias = ResolveBias(input.OrbDirection);
        var strength = CalculateStrength(input);

        if (input.OrbState == ORBState.Forming || input.OrbState == ORBState.AwaitBreakout)
        {
            reasons.Add("The opening narrative remains unresolved.");
            return Decision(NarrativeBias.Neutral, NarrativePhase.Balance,
                Math.Min(strength, 35), false, false, false, reasons);
        }

        if (input.OrbState == ORBState.LiquiditySweep)
        {
            if (input.Acceptance && !input.Rejection)
            {
                reasons.Add("A liquidity sweep was accepted back in the dominant direction.");
                return Decision(bias, NarrativePhase.Continuation, strength,
                    strength >= 65, true, strength >= 85, reasons);
            }

            reasons.Add("A liquidity sweep was rejected, creating a fade narrative.");
            return Decision(Opposite(bias), NarrativePhase.Distribution,
                Math.Min(strength, 70), false, false, false, reasons);
        }

        if (input.OrbState == ORBState.BreakoutRejected || input.Rejection)
        {
            reasons.Add("Breakout failure and rejection support a reversal narrative.");
            return Decision(Opposite(bias), NarrativePhase.Reversal,
                Math.Min(85, strength + 10), false, false, false, reasons);
        }

        if (input.PullbackDeveloping)
        {
            reasons.Add("The accepted directional narrative is entering a pullback phase.");
            return Decision(bias, NarrativePhase.Pullback, strength,
                strength >= 60, true, strength >= 88, reasons);
        }

        if (input.ContinuationConfirmed)
        {
            reasons.Add("Structure, order flow, and liquidity confirm continuation.");
            return Decision(bias, NarrativePhase.Continuation, strength,
                strength >= 65, false, strength >= 85, reasons);
        }

        if (input.OrbState == ORBState.BreakoutConfirmed || input.OrbState == ORBState.RetestQualified)
        {
            reasons.Add("Accepted ORB evidence supports directional expansion.");
            return Decision(bias, NarrativePhase.Expansion, strength,
                strength >= 65, true, strength >= 88, reasons);
        }

        reasons.Add("Evidence is mixed and does not yet support a directional narrative.");
        return Decision(NarrativeBias.Neutral, NarrativePhase.Balance,
            Math.Min(strength, 45), false, false, false, reasons);
    }

    private static int CalculateStrength(MarketNarrativeInput input)
    {
        decimal raw = input.StructureScore * 0.35m +
                      input.OrderFlowScore * 0.35m +
                      input.LiquidityScore * 0.30m;

        if (input.Acceptance) raw += 8m;
        if (input.ContinuationConfirmed) raw += 7m;
        if (input.Rejection) raw -= 15m;
        if (input.MarketOpenPhase == MarketOpenPhase.ReversalWindow) raw -= 5m;

        return (int)Math.Max(0m, Math.Min(100m,
            Math.Round(raw, 0, MidpointRounding.AwayFromZero)));
    }

    private static NarrativeBias ResolveBias(ORBDirection direction)
        => direction == ORBDirection.Long ? NarrativeBias.Bullish :
           direction == ORBDirection.Short ? NarrativeBias.Bearish : NarrativeBias.Neutral;

    private static NarrativeBias Opposite(NarrativeBias bias)
        => bias == NarrativeBias.Bullish ? NarrativeBias.Bearish :
           bias == NarrativeBias.Bearish ? NarrativeBias.Bullish : NarrativeBias.Neutral;

    private static MarketNarrativeDecision Decision(NarrativeBias bias, NarrativePhase phase,
        int strength, bool trendHealthy, bool pullbackExpected, bool runnerLikely,
        IReadOnlyList<string> reasons)
        => new MarketNarrativeDecision(bias, phase, strength, trendHealthy,
            pullbackExpected, runnerLikely, reasons);
}
