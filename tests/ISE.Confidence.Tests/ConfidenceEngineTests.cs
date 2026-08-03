using ISE.Confidence;
using Xunit;

namespace ISE.Confidence.Tests;

public sealed class ConfidenceEngineTests
{
    private readonly ConfidenceEngine engine = new();

    [Fact]
    public void Strong_alignment_produces_elite_approval()
    {
        var result = engine.Evaluate(new ConfidenceInput(0.95m, 0.90m, 0.90m, 0.85m, 0.90m, 0.95m, 0.90m, 0.85m, 0.90m));

        Assert.True(result.Approved);
        Assert.True(result.Score >= 92m);
        Assert.Equal(ConfidenceRating.Elite, result.Rating);
        Assert.Equal(1m, result.SizeMultiplier);
    }

    [Fact]
    public void Acceptable_setup_is_approved_at_reduced_size()
    {
        var result = engine.Evaluate(new ConfidenceInput(0.70m, 0.65m, 0.65m, 0.60m, 0.70m, 0.70m, 0.65m, 0.60m, 0.65m));

        Assert.True(result.Approved);
        Assert.Equal(ConfidenceRating.Acceptable, result.Rating);
        Assert.Equal(0.5m, result.SizeMultiplier);
    }

    [Fact]
    public void Weak_evidence_is_rejected()
    {
        var result = engine.Evaluate(new ConfidenceInput(0.40m, 0.40m, 0.35m, 0.45m, 0.40m, 0.45m, 0.30m, 0.50m, 0.40m));

        Assert.False(result.Approved);
        Assert.Equal(0m, result.SizeMultiplier);
    }

    [Fact]
    public void Hard_risk_block_overrides_high_confidence()
    {
        var result = engine.Evaluate(new ConfidenceInput(1m, 1m, 1m, 1m, 1m, 1m, 1m, 1m, 1m, true));

        Assert.False(result.Approved);
        Assert.Equal(0m, result.Score);
        Assert.Equal(ConfidenceRating.Reject, result.Rating);
    }
}
