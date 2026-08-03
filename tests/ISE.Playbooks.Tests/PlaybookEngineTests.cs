using ISE.Playbooks;
using Xunit;

namespace ISE.Playbooks.Tests;

public sealed class PlaybookEngineTests
{
    [Fact]
    public void Opening_reversal_has_priority_in_opening_window()
    {
        var result = new PlaybookEngine().Evaluate(new PlaybookInput(true, 0.70m, 0.50m, 0.70m, 0.86m, 0.60m, 0.20m, 0.40m, 0.90m));

        Assert.Equal(PlaybookType.OpeningReversal, result.Playbook);
        Assert.True(result.IsEligible);
    }

    [Fact]
    public void Liquidity_sweep_reversal_requires_rejection_alignment()
    {
        var result = new PlaybookEngine().Evaluate(new PlaybookInput(false, 0.30m, 0.40m, 0.30m, 0.72m, 0.88m, 0.35m, 0.50m, 0.82m));

        Assert.Equal(PlaybookType.LiquiditySweepReversal, result.Playbook);
    }

    [Fact]
    public void Trend_pullback_selects_pullback_continuation()
    {
        var result = new PlaybookEngine().Evaluate(new PlaybookInput(false, 0.84m, 0.76m, 0.40m, 0.20m, 0.20m, 0.20m, 0.30m, 0.86m));

        Assert.Equal(PlaybookType.PullbackContinuation, result.Playbook);
    }

    [Fact]
    public void Weak_confirmation_rejects_all_playbooks()
    {
        var result = new PlaybookEngine().Evaluate(new PlaybookInput(true, 0.95m, 0.90m, 0.95m, 0.90m, 0.90m, 0.80m, 0.80m, 0.40m));

        Assert.Equal(PlaybookType.None, result.Playbook);
        Assert.False(result.IsEligible);
    }
}
