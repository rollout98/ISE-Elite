using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    public enum MorningGovernanceSequencingPolicy
    {
        Objective500 = 0,
        ProtectedGreen = 1
    }

    /// <summary>
    /// V6.2 research-only governance profile layered after frozen V6.1 StrictUpper80 selection.
    /// No new signal authority is introduced.
    /// </summary>
    public sealed class MorningGovernanceSequencingConfig
    {
        public MorningGovernanceSequencingConfig(
            int maximumAttempts = 2,
            int maximumConsecutiveLosses = 2,
            decimal greenDayThresholdDollars = 300m,
            decimal protectedGreenFloorDollars = 200m,
            decimal lowerObjectiveDollars = 500m,
            decimal upperObjectiveDollars = 1000m,
            decimal baseRiskPerTradeDollars = 150m,
            decimal dollarsPerRiskTick = 1m,
            decimal highEntryMinimum = 70m,
            decimal upperPotentialMinimum = 80m)
        {
            if (maximumAttempts < 1 || maximumAttempts > 3) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
            if (maximumConsecutiveLosses < 1) throw new ArgumentOutOfRangeException(nameof(maximumConsecutiveLosses));
            if (greenDayThresholdDollars <= 0m) throw new ArgumentOutOfRangeException(nameof(greenDayThresholdDollars));
            if (protectedGreenFloorDollars < 0m || protectedGreenFloorDollars >= greenDayThresholdDollars) throw new ArgumentOutOfRangeException(nameof(protectedGreenFloorDollars));
            if (lowerObjectiveDollars <= greenDayThresholdDollars) throw new ArgumentOutOfRangeException(nameof(lowerObjectiveDollars));
            if (upperObjectiveDollars <= lowerObjectiveDollars) throw new ArgumentOutOfRangeException(nameof(upperObjectiveDollars));
            if (baseRiskPerTradeDollars <= 0m) throw new ArgumentOutOfRangeException(nameof(baseRiskPerTradeDollars));
            if (dollarsPerRiskTick <= 0m) throw new ArgumentOutOfRangeException(nameof(dollarsPerRiskTick));
            if (highEntryMinimum < 0m || highEntryMinimum > 100m) throw new ArgumentOutOfRangeException(nameof(highEntryMinimum));
            if (upperPotentialMinimum < 0m || upperPotentialMinimum > 100m) throw new ArgumentOutOfRangeException(nameof(upperPotentialMinimum));

            MaximumAttempts = maximumAttempts;
            MaximumConsecutiveLosses = maximumConsecutiveLosses;
            GreenDayThresholdDollars = greenDayThresholdDollars;
            ProtectedGreenFloorDollars = protectedGreenFloorDollars;
            LowerObjectiveDollars = lowerObjectiveDollars;
            UpperObjectiveDollars = upperObjectiveDollars;
            BaseRiskPerTradeDollars = baseRiskPerTradeDollars;
            DollarsPerRiskTick = dollarsPerRiskTick;
            HighEntryMinimum = highEntryMinimum;
            UpperPotentialMinimum = upperPotentialMinimum;
        }

        public int MaximumAttempts { get; }
        public int MaximumConsecutiveLosses { get; }
        public decimal GreenDayThresholdDollars { get; }
        public decimal ProtectedGreenFloorDollars { get; }
        public decimal LowerObjectiveDollars { get; }
        public decimal UpperObjectiveDollars { get; }
        public decimal BaseRiskPerTradeDollars { get; }
        public decimal DollarsPerRiskTick { get; }
        public decimal HighEntryMinimum { get; }
        public decimal UpperPotentialMinimum { get; }
    }

    public sealed class MorningGovernedSequenceDecision
    {
        public MorningGovernedSequenceDecision(
            MorningDailySequencingCandidate candidate,
            MorningGovernanceSequencingPolicy policy,
            bool selected,
            int attemptNumber,
            string reason,
            decimal realizedBefore,
            decimal realizedAfter,
            int consecutiveLossesAfter,
            decimal plannedRiskDollars)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Policy = policy;
            Selected = selected;
            AttemptNumber = attemptNumber;
            Reason = reason ?? string.Empty;
            RealizedBefore = realizedBefore;
            RealizedAfter = realizedAfter;
            ConsecutiveLossesAfter = consecutiveLossesAfter;
            PlannedRiskDollars = plannedRiskDollars;
        }

        public MorningDailySequencingCandidate Candidate { get; }
        public MorningGovernanceSequencingPolicy Policy { get; }
        public bool Selected { get; }
        public int AttemptNumber { get; }
        public string Reason { get; }
        public decimal RealizedBefore { get; }
        public decimal RealizedAfter { get; }
        public int ConsecutiveLossesAfter { get; }
        public decimal PlannedRiskDollars { get; }
    }

    /// <summary>
    /// Applies daily governance to executable StrictUpper80 opportunities.
    /// Historical trade outcome is consumed only after the trade has already been selected and exited,
    /// so later governance decisions remain chronological and causal.
    /// </summary>
    public sealed class MorningGovernanceAwareSequencer
    {
        private readonly MorningGovernanceSequencingConfig config;

        public MorningGovernanceAwareSequencer(MorningGovernanceSequencingConfig? config = null)
        {
            this.config = config ?? new MorningGovernanceSequencingConfig();
        }

        public IReadOnlyList<MorningGovernedSequenceDecision> Sequence(
            IReadOnlyList<MorningDailySequencingCandidate> candidates,
            MorningGovernanceSequencingPolicy policy)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            var result = new List<MorningGovernedSequenceDecision>();

            foreach (var day in candidates
                .OrderBy(x => x.EntryUtc)
                .GroupBy(x => x.SessionDateCentral)
                .OrderBy(x => x.Key))
            {
                var selectedCount = 0;
                var consecutiveLosses = 0;
                var realized = 0m;
                DateTimeOffset? openUntil = null;

                foreach (var candidate in day.OrderBy(x => x.EntryUtc))
                {
                    var realizedBefore = realized;
                    var plannedRisk = PlannedRisk(candidate);

                    if (openUntil.HasValue && candidate.EntryUtc < openUntil.Value)
                    {
                        result.Add(Decision(candidate, policy, false, 0, "position-open", realizedBefore, realized, consecutiveLosses, plannedRisk));
                        continue;
                    }

                    if (selectedCount >= config.MaximumAttempts)
                    {
                        result.Add(Decision(candidate, policy, false, 0, "attempt-limit", realizedBefore, realized, consecutiveLosses, plannedRisk));
                        continue;
                    }

                    if (realized >= config.UpperObjectiveDollars)
                    {
                        result.Add(Decision(candidate, policy, false, 0, "upper-objective-lock", realizedBefore, realized, consecutiveLosses, plannedRisk));
                        continue;
                    }

                    if (realized >= config.LowerObjectiveDollars)
                    {
                        result.Add(Decision(candidate, policy, false, 0, "lower-objective-lock", realizedBefore, realized, consecutiveLosses, plannedRisk));
                        continue;
                    }

                    if (consecutiveLosses >= config.MaximumConsecutiveLosses)
                    {
                        result.Add(Decision(candidate, policy, false, 0, "consecutive-loss-lock", realizedBefore, realized, consecutiveLosses, plannedRisk));
                        continue;
                    }

                    if (candidate.EntryEfficiencyScore < config.HighEntryMinimum)
                    {
                        result.Add(Decision(candidate, policy, false, 0, "entry-below-high", realizedBefore, realized, consecutiveLosses, plannedRisk));
                        continue;
                    }

                    if (candidate.PotentialScore < config.UpperPotentialMinimum)
                    {
                        result.Add(Decision(candidate, policy, false, 0, "potential-below-80", realizedBefore, realized, consecutiveLosses, plannedRisk));
                        continue;
                    }

                    if (policy == MorningGovernanceSequencingPolicy.ProtectedGreen
                        && realized >= config.GreenDayThresholdDollars)
                    {
                        var availableRisk = realized - config.ProtectedGreenFloorDollars;
                        if (availableRisk <= 0m || plannedRisk > availableRisk)
                        {
                            result.Add(Decision(candidate, policy, false, 0, "green-floor-risk-block", realizedBefore, realized, consecutiveLosses, plannedRisk));
                            continue;
                        }
                    }

                    selectedCount++;
                    openUntil = candidate.Entry.Source.Source.ExitUtc;

                    var tradeResult = candidate.Entry.Source.Source.RealizedDollars;
                    realized += tradeResult;
                    consecutiveLosses = tradeResult < 0m ? consecutiveLosses + 1 : 0;

                    result.Add(Decision(
                        candidate,
                        policy,
                        true,
                        selectedCount,
                        "selected",
                        realizedBefore,
                        realized,
                        consecutiveLosses,
                        plannedRisk));
                }
            }

            return result;
        }

        private decimal PlannedRisk(MorningDailySequencingCandidate candidate)
        {
            var structuralRisk = Math.Max(
                0m,
                candidate.Entry.Source.Source.InitialRiskTicks * config.DollarsPerRiskTick);

            return Math.Min(config.BaseRiskPerTradeDollars, structuralRisk);
        }

        private static MorningGovernedSequenceDecision Decision(
            MorningDailySequencingCandidate candidate,
            MorningGovernanceSequencingPolicy policy,
            bool selected,
            int attemptNumber,
            string reason,
            decimal realizedBefore,
            decimal realizedAfter,
            int consecutiveLossesAfter,
            decimal plannedRiskDollars)
        {
            return new MorningGovernedSequenceDecision(
                candidate,
                policy,
                selected,
                attemptNumber,
                reason,
                realizedBefore,
                realizedAfter,
                consecutiveLossesAfter,
                plannedRiskDollars);
        }
    }
}
