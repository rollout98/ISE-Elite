using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    /// <summary>
    /// V6.1 research-only execution-realistic sequencing.
    /// Preserves V6 policy semantics while enforcing one-position-at-a-time lifecycle.
    /// A later candidate that arrives before the selected trade has exited is unavailable,
    /// does not consume an attempt, and is classified separately from a genuinely missed
    /// executable opportunity.
    ///
    /// Entry decisions still use only Entry Efficiency and frozen V5.6 Potential.
    /// Historical ExitUtc is used only to reconstruct whether the position would still
    /// have been open at the later candidate timestamp.
    /// </summary>
    public sealed class MorningExecutionRealisticSequencingConfig
    {
        public MorningExecutionRealisticSequencingConfig(
            int maximumAttempts = 2,
            decimal highEntryMinimum = 70m,
            decimal upperPotentialMinimum = 80m,
            decimal fallbackPotentialMinimum = 70m,
            decimal fallbackEntryMinimum = 85m)
        {
            if (maximumAttempts < 1 || maximumAttempts > 3) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
            if (highEntryMinimum < 0m || highEntryMinimum > 100m) throw new ArgumentOutOfRangeException(nameof(highEntryMinimum));
            if (upperPotentialMinimum < 0m || upperPotentialMinimum > 100m) throw new ArgumentOutOfRangeException(nameof(upperPotentialMinimum));
            if (fallbackPotentialMinimum < 0m || fallbackPotentialMinimum > upperPotentialMinimum) throw new ArgumentOutOfRangeException(nameof(fallbackPotentialMinimum));
            if (fallbackEntryMinimum < highEntryMinimum || fallbackEntryMinimum > 100m) throw new ArgumentOutOfRangeException(nameof(fallbackEntryMinimum));

            MaximumAttempts = maximumAttempts;
            HighEntryMinimum = highEntryMinimum;
            UpperPotentialMinimum = upperPotentialMinimum;
            FallbackPotentialMinimum = fallbackPotentialMinimum;
            FallbackEntryMinimum = fallbackEntryMinimum;
        }

        public int MaximumAttempts { get; }
        public decimal HighEntryMinimum { get; }
        public decimal UpperPotentialMinimum { get; }
        public decimal FallbackPotentialMinimum { get; }
        public decimal FallbackEntryMinimum { get; }
    }

    public sealed class MorningExecutionRealisticDailyOpportunitySequencer
    {
        private readonly MorningExecutionRealisticSequencingConfig config;

        public MorningExecutionRealisticDailyOpportunitySequencer(
            MorningExecutionRealisticSequencingConfig? config = null)
        {
            this.config = config ?? new MorningExecutionRealisticSequencingConfig();
        }

        public IReadOnlyList<MorningDailySequenceDecision> Sequence(
            IReadOnlyList<MorningDailySequencingCandidate> candidates,
            MorningDailySequencingPolicy policy)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            var result = new List<MorningDailySequenceDecision>();

            foreach (var day in candidates
                .OrderBy(x => x.EntryUtc)
                .GroupBy(x => x.SessionDateCentral)
                .OrderBy(x => x.Key))
            {
                var selectedCount = 0;
                var fallbackUsed = false;
                DateTimeOffset? openUntil = null;

                foreach (var candidate in day.OrderBy(x => x.EntryUtc))
                {
                    if (openUntil.HasValue && candidate.EntryUtc < openUntil.Value)
                    {
                        result.Add(new MorningDailySequenceDecision(
                            candidate,
                            policy,
                            false,
                            0,
                            "position-open"));
                        continue;
                    }

                    var decision = Decide(candidate, policy, selectedCount, fallbackUsed);

                    if (decision.Selected)
                    {
                        selectedCount++;
                        openUntil = candidate.Entry.Source.Source.ExitUtc;
                        if (decision.Reason == "fallback-first-slot")
                            fallbackUsed = true;
                    }

                    result.Add(new MorningDailySequenceDecision(
                        candidate,
                        policy,
                        decision.Selected,
                        decision.Selected ? selectedCount : 0,
                        decision.Reason));
                }
            }

            return result;
        }

        public static bool IsExecutionAvailableMiss(
            MorningDailySequenceDecision decision,
            decimal highEntryMinimum = 70m)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));

            return !decision.Selected
                && decision.Reason != "position-open"
                && decision.Candidate.EntryEfficiencyScore >= highEntryMinimum;
        }

        public static bool IsOverlapUnavailable(MorningDailySequenceDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            return !decision.Selected && decision.Reason == "position-open";
        }

        private (bool Selected, string Reason) Decide(
            MorningDailySequencingCandidate candidate,
            MorningDailySequencingPolicy policy,
            int selectedCount,
            bool fallbackUsed)
        {
            if (selectedCount >= config.MaximumAttempts)
                return (false, "attempt-limit");

            if (candidate.EntryEfficiencyScore < config.HighEntryMinimum)
                return (false, "entry-below-high");

            switch (policy)
            {
                case MorningDailySequencingPolicy.ControlFirstTwoHighEntry:
                    return (true, "control-high-entry");

                case MorningDailySequencingPolicy.StrictUpper80:
                    return candidate.PotentialScore >= config.UpperPotentialMinimum
                        ? (true, "upper80")
                        : (false, "potential-below-80");

                case MorningDailySequencingPolicy.BalancedReserve:
                    if (candidate.PotentialScore >= config.UpperPotentialMinimum)
                        return (true, "upper80");

                    if (!fallbackUsed
                        && selectedCount == 0
                        && candidate.PotentialScore >= config.FallbackPotentialMinimum
                        && candidate.EntryEfficiencyScore >= config.FallbackEntryMinimum)
                        return (true, "fallback-first-slot");

                    return (false,
                        selectedCount == 0
                            ? "fallback-not-qualified"
                            : "second-slot-reserved-upper80");

                default:
                    throw new ArgumentOutOfRangeException(nameof(policy));
            }
        }
    }
}
