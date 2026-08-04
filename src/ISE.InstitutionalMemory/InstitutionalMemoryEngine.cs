using System;
using System.Collections.Generic;
using System.Linq;
using ISE.MarketMemory;

namespace ISE.InstitutionalMemory;

public enum InstitutionalMemoryStatus { Ready, InsufficientHistory, Blocked }

public sealed class InstitutionalTradeRecord
{
    public InstitutionalTradeRecord(MarketFingerprint fingerprint, string playbook, string brainVersion,
        bool thesisConfirmed, decimal resultR, decimal maximumFavorableExcursion,
        decimal maximumAdverseExcursion, int holdMinutes)
    {
        Fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
        Playbook = Required(playbook, nameof(playbook));
        BrainVersion = Required(brainVersion, nameof(brainVersion));
        if (maximumFavorableExcursion < 0) throw new ArgumentOutOfRangeException(nameof(maximumFavorableExcursion));
        if (maximumAdverseExcursion < 0) throw new ArgumentOutOfRangeException(nameof(maximumAdverseExcursion));
        if (holdMinutes < 0) throw new ArgumentOutOfRangeException(nameof(holdMinutes));
        ThesisConfirmed = thesisConfirmed;
        ResultR = resultR;
        MaximumFavorableExcursion = maximumFavorableExcursion;
        MaximumAdverseExcursion = maximumAdverseExcursion;
        HoldMinutes = holdMinutes;
    }

    public MarketFingerprint Fingerprint { get; }
    public string Playbook { get; }
    public string BrainVersion { get; }
    public bool ThesisConfirmed { get; }
    public decimal ResultR { get; }
    public decimal MaximumFavorableExcursion { get; }
    public decimal MaximumAdverseExcursion { get; }
    public int HoldMinutes { get; }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }
}

public sealed class InstitutionalMemoryDecision
{
    public InstitutionalMemoryDecision(InstitutionalMemoryStatus status, int sampleSize,
        decimal weightedWinRate, decimal averageResultR, decimal averageFavorableExcursion,
        decimal averageAdverseExcursion, decimal thesisConfirmationRate, int averageHoldMinutes,
        int confidenceAdjustment, IReadOnlyList<string> reasons)
    {
        Status = status;
        SampleSize = sampleSize;
        WeightedWinRate = weightedWinRate;
        AverageResultR = averageResultR;
        AverageFavorableExcursion = averageFavorableExcursion;
        AverageAdverseExcursion = averageAdverseExcursion;
        ThesisConfirmationRate = thesisConfirmationRate;
        AverageHoldMinutes = averageHoldMinutes;
        ConfidenceAdjustment = confidenceAdjustment;
        Reasons = reasons;
    }

    public InstitutionalMemoryStatus Status { get; }
    public int SampleSize { get; }
    public decimal WeightedWinRate { get; }
    public decimal AverageResultR { get; }
    public decimal AverageFavorableExcursion { get; }
    public decimal AverageAdverseExcursion { get; }
    public decimal ThesisConfirmationRate { get; }
    public int AverageHoldMinutes { get; }
    public int ConfidenceAdjustment { get; }
    public IReadOnlyList<string> Reasons { get; }
}

public sealed class InstitutionalMemoryEngine
{
    private sealed class WeightedTradeMatch
    {
        public WeightedTradeMatch(InstitutionalTradeRecord record, decimal similarity)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            Similarity = similarity;
        }

        public InstitutionalTradeRecord Record { get; }
        public decimal Similarity { get; }
    }

    private readonly MarketMemoryEngine _marketMemory = new MarketMemoryEngine();

    public InstitutionalMemoryDecision Evaluate(MarketFingerprint current, string playbook,
        IEnumerable<InstitutionalTradeRecord> history, bool authoritativeRiskBlock = false,
        int maxMatches = 50)
    {
        if (current == null) throw new ArgumentNullException(nameof(current));
        if (string.IsNullOrWhiteSpace(playbook)) throw new ArgumentException("Value is required.", nameof(playbook));
        if (history == null) throw new ArgumentNullException(nameof(history));
        if (maxMatches < 1) throw new ArgumentOutOfRangeException(nameof(maxMatches));

        if (authoritativeRiskBlock)
            return Decision(InstitutionalMemoryStatus.Blocked, 0, 0, 0, 0, 0, 0, 0, 0,
                "Institutional memory cannot override an authoritative risk block.");

        var matches = history
            .Where(x => string.Equals(x.Playbook, playbook, StringComparison.OrdinalIgnoreCase))
            .Select(x => new WeightedTradeMatch(x, _marketMemory.Similarity(current, x.Fingerprint)))
            .Where(x => x.Similarity >= 0.60m)
            .OrderByDescending(x => x.Similarity)
            .Take(maxMatches)
            .ToArray();

        if (matches.Length < 3)
            return Decision(InstitutionalMemoryStatus.InsufficientHistory, matches.Length, 0, 0, 0, 0, 0, 0, 0,
                "At least three comparable completed trades are required.");

        decimal totalWeight = matches.Sum(x => x.Similarity);
        decimal winRate = Weighted(matches, x => x.ResultR > 0 ? 1m : 0m, totalWeight);
        decimal avgR = Weighted(matches, x => x.ResultR, totalWeight);
        decimal avgMfe = Weighted(matches, x => x.MaximumFavorableExcursion, totalWeight);
        decimal avgMae = Weighted(matches, x => x.MaximumAdverseExcursion, totalWeight);
        decimal thesisRate = Weighted(matches, x => x.ThesisConfirmed ? 1m : 0m, totalWeight);
        int hold = (int)Math.Round(Weighted(matches, x => x.HoldMinutes, totalWeight), MidpointRounding.AwayFromZero);
        int adjustment = ConfidenceAdjustment(matches.Length, winRate, avgR, thesisRate);

        return Decision(InstitutionalMemoryStatus.Ready, matches.Length, winRate, avgR, avgMfe, avgMae,
            thesisRate, hold, adjustment,
            $"{matches.Length} comparable {playbook.Trim()} trades produced a {winRate:P0} weighted win rate.");
    }

    private static decimal Weighted(IEnumerable<WeightedTradeMatch> items,
        Func<InstitutionalTradeRecord, decimal> selector, decimal totalWeight)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (selector == null) throw new ArgumentNullException(nameof(selector));
        if (totalWeight == 0) return 0;

        return Math.Round(
            items.Sum(x => selector(x.Record) * x.Similarity) / totalWeight,
            4,
            MidpointRounding.AwayFromZero);
    }

    private static int ConfidenceAdjustment(int count, decimal winRate, decimal averageR, decimal thesisRate)
    {
        if (count >= 10 && winRate >= 0.65m && averageR >= 0.5m && thesisRate >= 0.70m) return 6;
        if (count >= 5 && winRate >= 0.55m && averageR > 0 && thesisRate >= 0.60m) return 3;
        if (winRate < 0.40m || averageR < 0) return -4;
        return 1;
    }

    private static InstitutionalMemoryDecision Decision(InstitutionalMemoryStatus status, int sampleSize,
        decimal winRate, decimal averageR, decimal averageMfe, decimal averageMae, decimal thesisRate,
        int holdMinutes, int adjustment, string reason)
        => new InstitutionalMemoryDecision(status, sampleSize, winRate, averageR, averageMfe, averageMae,
            thesisRate, holdMinutes, adjustment, new[] { reason });
}
