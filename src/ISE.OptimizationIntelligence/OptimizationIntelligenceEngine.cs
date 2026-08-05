using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ISE.ReplaySession;

namespace ISE.OptimizationIntelligence;

public enum OptimizationStatus { Ready, NoCandidates, InsufficientEvidence }

public sealed class OptimizationParameterSet
{
    private readonly IReadOnlyDictionary<string, decimal> _values;

    public OptimizationParameterSet(IEnumerable<KeyValuePair<string, decimal>> values)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        var normalized = new SortedDictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new ArgumentException("Parameter names are required.", nameof(values));
            normalized[pair.Key.Trim()] = pair.Value;
        }
        if (normalized.Count == 0)
            throw new ArgumentException("At least one parameter is required.", nameof(values));
        _values = normalized;
        Signature = BuildSignature(normalized);
    }

    public IReadOnlyDictionary<string, decimal> Values => _values;
    public string Signature { get; }

    private static string BuildSignature(IEnumerable<KeyValuePair<string, decimal>> values)
    {
        var builder = new StringBuilder();
        foreach (var pair in values)
        {
            if (builder.Length > 0) builder.Append('|');
            builder.Append(pair.Key.ToUpperInvariant());
            builder.Append('=');
            builder.Append(pair.Value.ToString("G29", CultureInfo.InvariantCulture));
        }
        return builder.ToString();
    }
}

public sealed class OptimizationCandidate
{
    public OptimizationCandidate(string candidateId, OptimizationParameterSet parameters,
        ReplaySessionReport report)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
            throw new ArgumentException("Candidate id is required.", nameof(candidateId));
        CandidateId = candidateId.Trim();
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public string CandidateId { get; }
    public OptimizationParameterSet Parameters { get; }
    public ReplaySessionReport Report { get; }
}

public sealed class OptimizationScore
{
    public OptimizationScore(OptimizationCandidate candidate, decimal compositeScore,
        bool evidenceSufficient, IReadOnlyList<string> reasons)
    {
        Candidate = candidate;
        CompositeScore = compositeScore;
        EvidenceSufficient = evidenceSufficient;
        Reasons = reasons;
    }

    public OptimizationCandidate Candidate { get; }
    public decimal CompositeScore { get; }
    public bool EvidenceSufficient { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class OptimizationResult
{
    public OptimizationResult(OptimizationStatus status, OptimizationScore? best,
        IReadOnlyList<OptimizationScore> rankedCandidates, int duplicateCandidatesIgnored)
    {
        Status = status;
        Best = best;
        RankedCandidates = rankedCandidates;
        DuplicateCandidatesIgnored = duplicateCandidatesIgnored;
    }

    public OptimizationStatus Status { get; }
    public OptimizationScore? Best { get; }
    public IReadOnlyList<OptimizationScore> RankedCandidates { get; }
    public int DuplicateCandidatesIgnored { get; }
}

public sealed class OptimizationIntelligenceEngine
{
    public OptimizationResult Evaluate(IEnumerable<OptimizationCandidate> candidates,
        int minimumCompletedTrades = 3)
    {
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        if (minimumCompletedTrades < 1) throw new ArgumentOutOfRangeException(nameof(minimumCompletedTrades));

        var unique = new Dictionary<string, OptimizationCandidate>(StringComparer.Ordinal);
        int duplicates = 0;
        foreach (var candidate in candidates)
        {
            if (candidate == null) throw new ArgumentException("Candidates cannot contain null values.", nameof(candidates));
            if (unique.ContainsKey(candidate.Parameters.Signature))
            {
                duplicates++;
                continue;
            }
            unique.Add(candidate.Parameters.Signature, candidate);
        }

        if (unique.Count == 0)
            return new OptimizationResult(OptimizationStatus.NoCandidates, null,
                Array.Empty<OptimizationScore>(), duplicates);

        var ranked = unique.Values
            .Select(x => Score(x, minimumCompletedTrades))
            .OrderByDescending(x => x.EvidenceSufficient)
            .ThenByDescending(x => x.CompositeScore)
            .ThenBy(x => x.Candidate.Report.Metrics.MaximumDrawdownR)
            .ThenByDescending(x => x.Candidate.Report.Metrics.CompletedTrades)
            .ThenBy(x => x.Candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();

        OptimizationScore? best = ranked.FirstOrDefault(x => x.EvidenceSufficient);
        var status = best == null ? OptimizationStatus.InsufficientEvidence : OptimizationStatus.Ready;
        return new OptimizationResult(status, best, ranked, duplicates);
    }

    private static OptimizationScore Score(OptimizationCandidate candidate, int minimumCompletedTrades)
    {
        var metrics = candidate.Report.Metrics;
        bool sufficient = metrics.CompletedTrades >= minimumCompletedTrades;
        decimal profitFactor = metrics.ProfitFactor == decimal.MaxValue ? 5m : Math.Min(metrics.ProfitFactor, 5m);
        decimal sampleFactor = Math.Min(1m, (decimal)metrics.CompletedTrades / Math.Max(minimumCompletedTrades, 10));

        decimal raw =
            metrics.TotalResultR * 8m +
            profitFactor * 10m +
            metrics.WinRate * 20m +
            metrics.AverageDecisionQuality * 0.35m -
            metrics.MaximumDrawdownR * 12m;

        decimal score = Math.Round(raw * (0.50m + 0.50m * sampleFactor), 2,
            MidpointRounding.AwayFromZero);

        var reasons = new List<string>
        {
            $"Total result: {metrics.TotalResultR:F2}R.",
            $"Profit factor: {(metrics.ProfitFactor == decimal.MaxValue ? "unbounded" : metrics.ProfitFactor.ToString("F2", CultureInfo.InvariantCulture))}.",
            $"Maximum drawdown: {metrics.MaximumDrawdownR:F2}R.",
            $"Average decision quality: {metrics.AverageDecisionQuality:F2}."
        };
        if (!sufficient)
            reasons.Add($"Only {metrics.CompletedTrades} completed trade(s); {minimumCompletedTrades} required.");

        return new OptimizationScore(candidate, score, sufficient, reasons);
    }
}
