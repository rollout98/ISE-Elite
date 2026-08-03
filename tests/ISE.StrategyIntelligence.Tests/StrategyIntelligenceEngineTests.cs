using ISE.Confidence;
using ISE.Playbooks;
using Xunit;

namespace ISE.StrategyIntelligence.Tests;

public sealed class StrategyIntelligenceEngineTests
{
    private readonly StrategyIntelligenceEngine engine = new();

    [Fact]
    public void Elite_playbook_is_approved_at_full_size()
    {
        var input = CreateInput(PlaybookType.OpeningReversal, 94m, ConfidenceRating.Elite, true, 1m, 1m);

        var result = engine.Evaluate(input);

        Assert.True(result.Approved);
        Assert.Equal(StrategyPosture.Elite, result.Posture);
        Assert.Equal(1m, result.SizeMultiplier);
        Assert.Equal(PlaybookType.OpeningReversal, result.Playbook);
    }

    [Fact]
    public void External_control_reduces_an_approved_strategy()
    {
        var input = CreateInput(PlaybookType.PullbackContinuation, 88m, ConfidenceRating.Excellent, true, 0.75m, 0.5m);

        var result = engine.Evaluate(input);

        Assert.True(result.Approved);
        Assert.Equal(StrategyPosture.Reduced, result.Posture);
        Assert.Equal(0.5m, result.SizeMultiplier);
    }

    [Fact]
    public void Missing_playbook_is_rejected()
    {
        var input = CreateInput(PlaybookType.None, 94m, ConfidenceRating.Elite, true, 1m, 1m);

        var result = engine.Evaluate(input);

        Assert.False(result.Approved);
        Assert.Equal(StrategyPosture.Reject, result.Posture);
        Assert.Equal(0m, result.SizeMultiplier);
    }

    [Fact]
    public void Authoritative_block_overrides_elite_strategy()
    {
        var input = CreateInput(PlaybookType.LiquiditySweepReversal, 98m, ConfidenceRating.Institutional, true, 1m, 1m, true);

        var result = engine.Evaluate(input);

        Assert.False(result.Approved);
        Assert.Equal(StrategyPosture.Reject, result.Posture);
        Assert.Equal(0m, result.SizeMultiplier);
    }

    private static StrategyIntelligenceInput CreateInput(
        PlaybookType playbook,
        decimal score,
        ConfidenceRating rating,
        bool approved,
        decimal confidenceSize,
        decimal externalSize,
        bool authoritativeBlock = false)
    {
        var selection = new PlaybookSelection(playbook, playbook == PlaybookType.None ? 0m : 0.9m, "Test selection.");
        var confidence = new ConfidenceResult(score, rating, approved, confidenceSize, new[] { "Test confidence." });
        return new StrategyIntelligenceInput(selection, confidence, externalSize, authoritativeBlock);
    }
}
