using System.Collections.Generic;
using ISE.MarketMemory;
using Xunit;

namespace ISE.InstitutionalMemory.Tests;

public sealed class InstitutionalMemoryEngineTests
{
    private readonly InstitutionalMemoryEngine _engine = new InstitutionalMemoryEngine();

    [Fact]
    public void Strong_comparable_history_increases_confidence()
    {
        var decision = _engine.Evaluate(Current(), "OpeningReversal", StrongHistory(10));
        Assert.Equal(InstitutionalMemoryStatus.Ready, decision.Status);
        Assert.Equal(10, decision.SampleSize);
        Assert.Equal(6, decision.ConfidenceAdjustment);
        Assert.True(decision.WeightedWinRate >= 0.65m);
    }

    [Fact]
    public void Negative_history_reduces_confidence()
    {
        var history = new List<InstitutionalTradeRecord>();
        for (int i = 0; i < 5; i++) history.Add(Record(Current(), "OpeningReversal", false, -1m));
        var decision = _engine.Evaluate(Current(), "OpeningReversal", history);
        Assert.Equal(-4, decision.ConfidenceAdjustment);
    }

    [Fact]
    public void Different_playbooks_are_excluded()
    {
        var history = StrongHistory(5);
        var decision = _engine.Evaluate(Current(), "TrendContinuation", history);
        Assert.Equal(InstitutionalMemoryStatus.InsufficientHistory, decision.Status);
        Assert.Equal(0, decision.SampleSize);
    }

    [Fact]
    public void Insufficient_history_is_reported()
    {
        var decision = _engine.Evaluate(Current(), "OpeningReversal", StrongHistory(2));
        Assert.Equal(InstitutionalMemoryStatus.InsufficientHistory, decision.Status);
    }

    [Fact]
    public void Authoritative_block_overrides_memory()
    {
        var decision = _engine.Evaluate(Current(), "OpeningReversal", StrongHistory(10), true);
        Assert.Equal(InstitutionalMemoryStatus.Blocked, decision.Status);
        Assert.Equal(0, decision.ConfidenceAdjustment);
    }

    [Fact]
    public void Statistics_capture_excursion_and_holding_behavior()
    {
        var decision = _engine.Evaluate(Current(), "OpeningReversal", StrongHistory(3));
        Assert.Equal(2m, decision.AverageFavorableExcursion);
        Assert.Equal(0.5m, decision.AverageAdverseExcursion);
        Assert.Equal(18, decision.AverageHoldMinutes);
        Assert.Equal(1m, decision.ThesisConfirmationRate);
    }

    private static List<InstitutionalTradeRecord> StrongHistory(int count)
    {
        var history = new List<InstitutionalTradeRecord>();
        for (int i = 0; i < count; i++) history.Add(Record(Current(), "OpeningReversal", true, 1m));
        return history;
    }

    private static InstitutionalTradeRecord Record(MarketFingerprint fingerprint, string playbook, bool thesis, decimal resultR)
        => new InstitutionalTradeRecord(fingerprint, playbook, "v3", thesis, resultR, 2m, 0.5m, 18);

    private static MarketFingerprint Current()
        => new MarketFingerprint("MNQ", "NewYork", "Trending", "OpeningDrive", "Bullish",
            "Sweep", "Normal", "Imbalance", 90);
}
