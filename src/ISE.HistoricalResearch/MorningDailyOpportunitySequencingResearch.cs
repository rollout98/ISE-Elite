using System;
using System.Collections.Generic;
using System.Linq;

namespace ISE.HistoricalResearch
{
    /// <summary>
    /// V6 research-only daily opportunity sequencing.
    ///
    /// The sequencer does not create entry signals. Range/structural opportunity creation remains
    /// upstream entry authority. V5.2 Entry Efficiency answers whether the current location is
    /// structurally efficient. Frozen V5.6 Potential answers whether enough usable expansion may
    /// remain. V6 decides whether a causal, already-observed opportunity deserves one of the day's
    /// limited attempt slots.
    ///
    /// Selection inputs are restricted to EntryUtc, Entry Efficiency and frozen V5.6 Potential.
    /// Future MFE/MAE/realized P&L are diagnostics only and never participate in selection.
    /// VectorFlow remains post-entry position intelligence and is not a V6 entry/selection input.
    /// </summary>
    public enum MorningDailySequencingPolicy
    {
        ControlFirstTwoHighEntry = 0,
        StrictUpper80 = 1,
        BalancedReserve = 2
    }

    public sealed class MorningDailyOpportunitySequencingConfig
    {
        public MorningDailyOpportunitySequencingConfig(
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

    public sealed class MorningDailySequencingCandidate
    {
        public MorningDailySequencingCandidate(
            MorningEntryEfficiencyObservation entry,
            MorningStabilityWeightedPotentialObservation potential)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            Potential = potential ?? throw new ArgumentNullException(nameof(potential));
            if (!ReferenceEquals(entry.Source, potential.Source))
                throw new ArgumentException("Entry Efficiency and V5.6 Potential must reference the same causal opportunity.");
        }

        public MorningEntryEfficiencyObservation Entry { get; }
        public MorningStabilityWeightedPotentialObservation Potential { get; }
        public DateTime SessionDateCentral => Entry.Source.Source.SessionDateCentral.Date;
        public DateTimeOffset EntryUtc => Entry.Source.Source.EntryUtc;
        public decimal EntryEfficiencyScore => Entry.EntryEfficiencyScore;
        public decimal PotentialScore => Potential.StabilityWeightedScore;
        public decimal PriorityScore => (0.45m * EntryEfficiencyScore) + (0.55m * PotentialScore);
    }

    public sealed class MorningDailySequenceDecision
    {
        public MorningDailySequenceDecision(
            MorningDailySequencingCandidate candidate,
            MorningDailySequencingPolicy policy,
            bool selected,
            int attemptNumber,
            string reason)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Policy = policy;
            Selected = selected;
            AttemptNumber = attemptNumber;
            Reason = reason ?? string.Empty;
        }

        public MorningDailySequencingCandidate Candidate { get; }
        public MorningDailySequencingPolicy Policy { get; }
        public bool Selected { get; }
        public int AttemptNumber { get; }
        public string Reason { get; }
    }

    public sealed class MorningDailySequencingSummary
    {
        public MorningDailySequencingSummary(
            string period,
            MorningDailySequencingPolicy policy,
            int sessions,
            int sessionsTraded,
            int selectedTrades,
            decimal averageDailyRealized,
            decimal averageSelectedRealized,
            decimal selectedPositiveRate,
            decimal averageMfeTicks,
            decimal averageMaeTicks,
            int daysAtLeast300,
            int daysAtLeast500,
            int daysAtLeast1000,
            int selectedHit300,
            int selectedHit500,
            int missedEligibleHit300,
            int missedEligibleHit500)
        {
            Period = period;
            Policy = policy;
            Sessions = sessions;
            SessionsTraded = sessionsTraded;
            SelectedTrades = selectedTrades;
            AverageDailyRealized = averageDailyRealized;
            AverageSelectedRealized = averageSelectedRealized;
            SelectedPositiveRate = selectedPositiveRate;
            AverageMfeTicks = averageMfeTicks;
            AverageMaeTicks = averageMaeTicks;
            DaysAtLeast300 = daysAtLeast300;
            DaysAtLeast500 = daysAtLeast500;
            DaysAtLeast1000 = daysAtLeast1000;
            SelectedHit300 = selectedHit300;
            SelectedHit500 = selectedHit500;
            MissedEligibleHit300 = missedEligibleHit300;
            MissedEligibleHit500 = missedEligibleHit500;
        }

        public string Period { get; }
        public MorningDailySequencingPolicy Policy { get; }
        public int Sessions { get; }
        public int SessionsTraded { get; }
        public int SelectedTrades { get; }
        public decimal AverageDailyRealized { get; }
        public decimal AverageSelectedRealized { get; }
        public decimal SelectedPositiveRate { get; }
        public decimal AverageMfeTicks { get; }
        public decimal AverageMaeTicks { get; }
        public int DaysAtLeast300 { get; }
        public int DaysAtLeast500 { get; }
        public int DaysAtLeast1000 { get; }
        public int SelectedHit300 { get; }
        public int SelectedHit500 { get; }
        public int MissedEligibleHit300 { get; }
        public int MissedEligibleHit500 { get; }
    }

    public sealed class MorningDailyOpportunitySequencer
    {
        private readonly MorningDailyOpportunitySequencingConfig config;

        public MorningDailyOpportunitySequencer(MorningDailyOpportunitySequencingConfig? config = null)
        {
            this.config = config ?? new MorningDailyOpportunitySequencingConfig();
        }

        public IReadOnlyList<MorningDailySequencingCandidate> BuildCandidates(
            IReadOnlyList<MorningEntryEfficiencyObservation> entryObservations,
            IReadOnlyList<MorningStabilityWeightedPotentialObservation> potentialObservations)
        {
            if (entryObservations == null) throw new ArgumentNullException(nameof(entryObservations));
            if (potentialObservations == null) throw new ArgumentNullException(nameof(potentialObservations));

            var potentialBySource = potentialObservations.ToDictionary(x => x.Source);
            var result = new List<MorningDailySequencingCandidate>();
            foreach (var entry in entryObservations.OrderBy(x => x.Source.Source.EntryUtc))
            {
                if (potentialBySource.TryGetValue(entry.Source, out var potential))
                    result.Add(new MorningDailySequencingCandidate(entry, potential));
            }
            return result;
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
                var selected = 0;
                var fallbackUsed = false;
                foreach (var candidate in day.OrderBy(x => x.EntryUtc))
                {
                    var decision = Decide(candidate, policy, selected, fallbackUsed);
                    if (decision.Selected)
                    {
                        selected++;
                        if (decision.Reason == "fallback-first-slot") fallbackUsed = true;
                    }
                    result.Add(new MorningDailySequenceDecision(
                        candidate,
                        policy,
                        decision.Selected,
                        decision.Selected ? selected : 0,
                        decision.Reason));
                }
            }
            return result;
        }

        public IReadOnlyList<MorningDailySequencingSummary> Summarize(
            IReadOnlyList<MorningDailySequenceDecision> decisions)
        {
            if (decisions == null) throw new ArgumentNullException(nameof(decisions));
            var result = new List<MorningDailySequencingSummary>();

            foreach (var policyGroup in decisions.GroupBy(x => x.Policy).OrderBy(x => x.Key))
            {
                var allDates = policyGroup.Select(x => x.Candidate.SessionDateCentral).Distinct().OrderBy(x => x).ToList();
                foreach (var period in BuildPeriods(allDates))
                {
                    var members = policyGroup.Where(x => period.Contains(x.Candidate.SessionDateCentral)).ToList();
                    result.Add(BuildSummary(period.Label, policyGroup.Key, members));
                }
            }
            return result;
        }

        public decimal SelectionScore(decimal entryEfficiencyScore, decimal potentialScore)
        {
            return (0.45m * entryEfficiencyScore) + (0.55m * potentialScore);
        }

        private (bool Selected, string Reason) Decide(
            MorningDailySequencingCandidate candidate,
            MorningDailySequencingPolicy policy,
            int selectedCount,
            bool fallbackUsed)
        {
            if (selectedCount >= config.MaximumAttempts) return (false, "attempt-limit");
            if (candidate.EntryEfficiencyScore < config.HighEntryMinimum) return (false, "entry-below-high");

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

                    if (!fallbackUsed && selectedCount == 0 &&
                        candidate.PotentialScore >= config.FallbackPotentialMinimum &&
                        candidate.EntryEfficiencyScore >= config.FallbackEntryMinimum)
                        return (true, "fallback-first-slot");

                    return (false, selectedCount == 0 ? "fallback-not-qualified" : "second-slot-reserved-upper80");

                default:
                    throw new ArgumentOutOfRangeException(nameof(policy));
            }
        }

        private MorningDailySequencingSummary BuildSummary(
            string period,
            MorningDailySequencingPolicy policy,
            IReadOnlyList<MorningDailySequenceDecision> members)
        {
            var dates = members.Select(x => x.Candidate.SessionDateCentral).Distinct().OrderBy(x => x).ToList();
            var selected = members.Where(x => x.Selected).ToList();
            var selectedDays = selected.GroupBy(x => x.Candidate.SessionDateCentral).ToList();
            var dailyRealized = dates.Select(date => selected
                .Where(x => x.Candidate.SessionDateCentral == date)
                .Sum(x => x.Candidate.Entry.Source.Source.RealizedDollars)).ToList();

            var eligibleUnselected = members.Where(x =>
                !x.Selected &&
                x.Candidate.EntryEfficiencyScore >= config.HighEntryMinimum).ToList();

            return new MorningDailySequencingSummary(
                period,
                policy,
                dates.Count,
                selectedDays.Count,
                selected.Count,
                dates.Count == 0 ? 0m : dailyRealized.Average(),
                selected.Count == 0 ? 0m : selected.Average(x => x.Candidate.Entry.Source.Source.RealizedDollars),
                selected.Count == 0 ? 0m : (decimal)selected.Count(x => x.Candidate.Entry.Source.Source.RealizedDollars > 0m) / selected.Count,
                selected.Count == 0 ? 0m : selected.Average(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks),
                selected.Count == 0 ? 0m : selected.Average(x => x.Candidate.Entry.Source.Source.MaxAdverseTicks),
                dailyRealized.Count(x => x >= 300m),
                dailyRealized.Count(x => x >= 500m),
                dailyRealized.Count(x => x >= 1000m),
                selected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 300m),
                selected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 500m),
                eligibleUnselected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 300m),
                eligibleUnselected.Count(x => x.Candidate.Entry.Source.Source.MaxFavorableTicks >= 500m));
        }

        private static IReadOnlyList<Period> BuildPeriods(IReadOnlyList<DateTime> dates)
        {
            var result = new List<Period>();
            foreach (var month in dates.Select(x => new DateTime(x.Year, x.Month, 1)).Distinct().OrderBy(x => x))
            {
                var next = month.AddMonths(1);
                result.Add(new Period(month.ToString("yyyy-MM"), month, next));
                result.Add(new Period(month.ToString("yyyy-MM") + "-H1", month, new DateTime(month.Year, month.Month, 16)));
                result.Add(new Period(month.ToString("yyyy-MM") + "-H2", new DateTime(month.Year, month.Month, 16), next));
            }
            return result;
        }

        private sealed class Period
        {
            public Period(string label, DateTime start, DateTime endExclusive)
            {
                Label = label;
                Start = start;
                EndExclusive = endExclusive;
            }
            public string Label { get; }
            public DateTime Start { get; }
            public DateTime EndExclusive { get; }
            public bool Contains(DateTime date) => date >= Start && date < EndExclusive;
        }
    }
}

