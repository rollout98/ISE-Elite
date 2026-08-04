using System;
using System.Collections.Generic;
using System.Linq;
using ISE.InstitutionalMemory;
using ISE.ReplayIntelligence;

namespace ISE.ReplaySession;

public sealed class ReplaySessionInput
{
    public ReplaySessionInput(string sessionId, string instrument,
        IEnumerable<ReplaySnapshot> snapshots, decimal startingBalance = 0m)
    {
        SessionId = Required(sessionId, nameof(sessionId));
        Instrument = Required(instrument, nameof(instrument));
        Snapshots = snapshots?.ToArray() ?? throw new ArgumentNullException(nameof(snapshots));
        if (startingBalance < 0) throw new ArgumentOutOfRangeException(nameof(startingBalance));
        StartingBalance = startingBalance;
    }

    public string SessionId { get; }
    public string Instrument { get; }
    public IReadOnlyList<ReplaySnapshot> Snapshots { get; }
    public decimal StartingBalance { get; }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }
}

public sealed class ReplaySessionStep
{
    public ReplaySessionStep(int sequence, ReplayEvaluation evaluation,
        decimal runningResultR, decimal runningPeakR, decimal runningDrawdownR)
    {
        Sequence = sequence;
        Evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
        RunningResultR = runningResultR;
        RunningPeakR = runningPeakR;
        RunningDrawdownR = runningDrawdownR;
    }

    public int Sequence { get; }
    public ReplayEvaluation Evaluation { get; }
    public decimal RunningResultR { get; }
    public decimal RunningPeakR { get; }
    public decimal RunningDrawdownR { get; }
}

public sealed class ReplaySessionMetrics
{
    public ReplaySessionMetrics(int snapshotsEvaluated, int completedTrades, int winners, int losers,
        decimal totalResultR, decimal winRate, decimal profitFactor, decimal maximumDrawdownR,
        decimal averageDecisionQuality, int correctDecisions, int partialDecisions,
        int incorrectDecisions, int blockedDecisions)
    {
        SnapshotsEvaluated = snapshotsEvaluated;
        CompletedTrades = completedTrades;
        Winners = winners;
        Losers = losers;
        TotalResultR = totalResultR;
        WinRate = winRate;
        ProfitFactor = profitFactor;
        MaximumDrawdownR = maximumDrawdownR;
        AverageDecisionQuality = averageDecisionQuality;
        CorrectDecisions = correctDecisions;
        PartialDecisions = partialDecisions;
        IncorrectDecisions = incorrectDecisions;
        BlockedDecisions = blockedDecisions;
    }

    public int SnapshotsEvaluated { get; }
    public int CompletedTrades { get; }
    public int Winners { get; }
    public int Losers { get; }
    public decimal TotalResultR { get; }
    public decimal WinRate { get; }
    public decimal ProfitFactor { get; }
    public decimal MaximumDrawdownR { get; }
    public decimal AverageDecisionQuality { get; }
    public int CorrectDecisions { get; }
    public int PartialDecisions { get; }
    public int IncorrectDecisions { get; }
    public int BlockedDecisions { get; }
}

public sealed class ReplaySessionReport
{
    public ReplaySessionReport(string sessionId, string instrument,
        IReadOnlyList<ReplaySessionStep> timeline, ReplaySessionMetrics metrics,
        IReadOnlyList<InstitutionalTradeRecord> learningRecords)
    {
        SessionId = sessionId;
        Instrument = instrument;
        Timeline = timeline;
        Metrics = metrics;
        LearningRecords = learningRecords;
    }

    public string SessionId { get; }
    public string Instrument { get; }
    public IReadOnlyList<ReplaySessionStep> Timeline { get; }
    public ReplaySessionMetrics Metrics { get; }
    public IReadOnlyList<InstitutionalTradeRecord> LearningRecords { get; }
}

public sealed class ReplaySessionEngine
{
    private readonly ReplayIntelligenceEngine _replayEngine;

    public ReplaySessionEngine() : this(new ReplayIntelligenceEngine()) { }

    public ReplaySessionEngine(ReplayIntelligenceEngine replayEngine)
    {
        _replayEngine = replayEngine ?? throw new ArgumentNullException(nameof(replayEngine));
    }

    public ReplaySessionReport Evaluate(ReplaySessionInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var ordered = input.Snapshots
            .OrderBy(x => x.Timestamp)
            .ThenBy(x => x.SnapshotId, StringComparer.Ordinal)
            .ToArray();

        var timeline = new List<ReplaySessionStep>();
        var records = new List<InstitutionalTradeRecord>();
        decimal running = 0m;
        decimal peak = 0m;
        decimal maxDrawdown = 0m;

        for (var index = 0; index < ordered.Length; index++)
        {
            var evaluation = _replayEngine.Evaluate(ordered[index]);
            records.AddRange(evaluation.LearningRecords);

            decimal stepResult = evaluation.LearningRecords.Sum(x => x.ResultR);
            running += stepResult;
            if (running > peak) peak = running;
            decimal drawdown = peak - running;
            if (drawdown > maxDrawdown) maxDrawdown = drawdown;

            timeline.Add(new ReplaySessionStep(index + 1, evaluation, running, peak, drawdown));
        }

        var metrics = BuildMetrics(timeline, records, maxDrawdown);
        return new ReplaySessionReport(input.SessionId, input.Instrument,
            timeline, metrics, records);
    }

    private static ReplaySessionMetrics BuildMetrics(
        IReadOnlyList<ReplaySessionStep> timeline,
        IReadOnlyList<InstitutionalTradeRecord> records,
        decimal maximumDrawdown)
    {
        int winners = records.Count(x => x.ResultR > 0);
        int losers = records.Count(x => x.ResultR < 0);
        decimal total = records.Sum(x => x.ResultR);
        decimal grossProfit = records.Where(x => x.ResultR > 0).Sum(x => x.ResultR);
        decimal grossLoss = Math.Abs(records.Where(x => x.ResultR < 0).Sum(x => x.ResultR));
        decimal winRate = records.Count == 0 ? 0m :
            Math.Round((decimal)winners / records.Count, 4, MidpointRounding.AwayFromZero);
        decimal profitFactor = grossLoss == 0m ?
            (grossProfit > 0m ? decimal.MaxValue : 0m) :
            Math.Round(grossProfit / grossLoss, 4, MidpointRounding.AwayFromZero);
        decimal averageQuality = timeline.Count == 0 ? 0m :
            Math.Round(timeline.Average(x => (decimal)x.Evaluation.QualityScore), 2,
                MidpointRounding.AwayFromZero);

        return new ReplaySessionMetrics(
            timeline.Count,
            records.Count,
            winners,
            losers,
            total,
            winRate,
            profitFactor,
            maximumDrawdown,
            averageQuality,
            timeline.Count(x => x.Evaluation.Quality == ReplayDecisionQuality.Correct),
            timeline.Count(x => x.Evaluation.Quality == ReplayDecisionQuality.PartiallyCorrect),
            timeline.Count(x => x.Evaluation.Quality == ReplayDecisionQuality.Incorrect),
            timeline.Count(x => x.Evaluation.Quality == ReplayDecisionQuality.Blocked));
    }
}
