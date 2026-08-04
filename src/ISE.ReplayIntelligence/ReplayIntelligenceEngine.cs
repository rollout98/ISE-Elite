using System;
using System.Collections.Generic;
using ISE.ExecutionOrchestrator;
using ISE.InstitutionalMemory;
using ISE.MarketMemory;
using ISE.TradingBrain;

namespace ISE.ReplayIntelligence;

public enum ReplayDecisionQuality { Correct, PartiallyCorrect, Incorrect, Blocked }

public sealed class ReplayObservedOutcome
{
    public ReplayObservedOutcome(bool entryWasValid, bool exitWasRequired, bool tradeCompleted,
        bool thesisConfirmed, decimal resultR, decimal maximumFavorableExcursion,
        decimal maximumAdverseExcursion, int holdMinutes)
    {
        if (maximumFavorableExcursion < 0) throw new ArgumentOutOfRangeException(nameof(maximumFavorableExcursion));
        if (maximumAdverseExcursion < 0) throw new ArgumentOutOfRangeException(nameof(maximumAdverseExcursion));
        if (holdMinutes < 0) throw new ArgumentOutOfRangeException(nameof(holdMinutes));
        EntryWasValid = entryWasValid;
        ExitWasRequired = exitWasRequired;
        TradeCompleted = tradeCompleted;
        ThesisConfirmed = thesisConfirmed;
        ResultR = resultR;
        MaximumFavorableExcursion = maximumFavorableExcursion;
        MaximumAdverseExcursion = maximumAdverseExcursion;
        HoldMinutes = holdMinutes;
    }

    public bool EntryWasValid { get; }
    public bool ExitWasRequired { get; }
    public bool TradeCompleted { get; }
    public bool ThesisConfirmed { get; }
    public decimal ResultR { get; }
    public decimal MaximumFavorableExcursion { get; }
    public decimal MaximumAdverseExcursion { get; }
    public int HoldMinutes { get; }
}

public sealed class ReplaySnapshot
{
    public ReplaySnapshot(string snapshotId, DateTime timestamp, MarketFingerprint fingerprint,
        string playbook, string brainVersion, IntegratedTradingBrainInput brainInput,
        ReplayObservedOutcome observedOutcome)
    {
        SnapshotId = Required(snapshotId, nameof(snapshotId));
        Timestamp = timestamp;
        Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
        Playbook = Required(playbook, nameof(playbook));
        BrainVersion = Required(brainVersion, nameof(brainVersion));
        BrainInput = brainInput ?? throw new ArgumentNullException(nameof(brainInput));
        ObservedOutcome = observedOutcome ?? throw new ArgumentNullException(nameof(observedOutcome));
    }

    public string SnapshotId { get; }
    public DateTime Timestamp { get; }
    public MarketFingerprint Fingerprint { get; }
    public string Playbook { get; }
    public string BrainVersion { get; }
    public IntegratedTradingBrainInput BrainInput { get; }
    public ReplayObservedOutcome ObservedOutcome { get; }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }
}

public sealed class ReplayEvaluation
{
    public ReplayEvaluation(ReplaySnapshot snapshot, IntegratedTradingBrainDecision brainDecision,
        ReplayDecisionQuality quality, int qualityScore, IReadOnlyList<string> reasons,
        IReadOnlyList<InstitutionalTradeRecord> learningRecords)
    {
        Snapshot = snapshot;
        BrainDecision = brainDecision;
        Quality = quality;
        QualityScore = qualityScore;
        Reasons = reasons;
        LearningRecords = learningRecords;
    }

    public ReplaySnapshot Snapshot { get; }
    public IntegratedTradingBrainDecision BrainDecision { get; }
    public ReplayDecisionQuality Quality { get; }
    public int QualityScore { get; }
    public IReadOnlyList<string> Reasons { get; }
    public IReadOnlyList<InstitutionalTradeRecord> LearningRecords { get; }
}

public sealed class ReplayIntelligenceEngine
{
    private readonly IntegratedTradingBrain _brain;

    public ReplayIntelligenceEngine() : this(new IntegratedTradingBrain()) { }

    public ReplayIntelligenceEngine(IntegratedTradingBrain brain)
    {
        _brain = brain ?? throw new ArgumentNullException(nameof(brain));
    }

    public ReplayEvaluation Evaluate(ReplaySnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        var decision = _brain.Evaluate(snapshot.BrainInput);
        var quality = Grade(snapshot.BrainInput.PositionOpen, decision.ExecutionDecision.Action,
            snapshot.ObservedOutcome);
        var reasons = new List<string>
        {
            $"Replay snapshot {snapshot.SnapshotId} produced {decision.ExecutionDecision.Action}.",
            $"Decision quality was graded {quality}."
        };

        var records = new List<InstitutionalTradeRecord>();
        if (snapshot.ObservedOutcome.TradeCompleted)
        {
            records.Add(new InstitutionalTradeRecord(
                snapshot.Fingerprint,
                snapshot.Playbook,
                snapshot.BrainVersion,
                snapshot.ObservedOutcome.ThesisConfirmed,
                snapshot.ObservedOutcome.ResultR,
                snapshot.ObservedOutcome.MaximumFavorableExcursion,
                snapshot.ObservedOutcome.MaximumAdverseExcursion,
                snapshot.ObservedOutcome.HoldMinutes));
        }

        return new ReplayEvaluation(snapshot, decision, quality, Score(quality), reasons, records);
    }

    private static ReplayDecisionQuality Grade(bool positionOpen,
        ExecutionOrchestrationAction action, ReplayObservedOutcome outcome)
    {
        if (action == ExecutionOrchestrationAction.Blocked)
            return ReplayDecisionQuality.Blocked;

        if (!positionOpen)
        {
            if (action == ExecutionOrchestrationAction.SubmitEntry)
                return outcome.EntryWasValid ? ReplayDecisionQuality.Correct : ReplayDecisionQuality.Incorrect;

            if (action == ExecutionOrchestrationAction.Wait)
                return outcome.EntryWasValid ? ReplayDecisionQuality.Incorrect : ReplayDecisionQuality.Correct;

            return ReplayDecisionQuality.Incorrect;
        }

        if (outcome.ExitWasRequired)
        {
            if (action == ExecutionOrchestrationAction.ExitPosition)
                return ReplayDecisionQuality.Correct;

            if (action == ExecutionOrchestrationAction.ManageReduce ||
                action == ExecutionOrchestrationAction.ManageProtect ||
                action == ExecutionOrchestrationAction.ManageTrail)
                return ReplayDecisionQuality.PartiallyCorrect;

            return ReplayDecisionQuality.Incorrect;
        }

        return action == ExecutionOrchestrationAction.ManageHold ||
               action == ExecutionOrchestrationAction.ManageProtect ||
               action == ExecutionOrchestrationAction.ManageTrail ||
               action == ExecutionOrchestrationAction.ManageReduce
            ? ReplayDecisionQuality.Correct
            : ReplayDecisionQuality.Incorrect;
    }

    private static int Score(ReplayDecisionQuality quality)
    {
        switch (quality)
        {
            case ReplayDecisionQuality.Correct: return 100;
            case ReplayDecisionQuality.PartiallyCorrect: return 60;
            case ReplayDecisionQuality.Blocked: return 0;
            default: return 0;
        }
    }
}
