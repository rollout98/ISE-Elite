using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningDailyOpportunitySequencingResearchTests
    {
        [Fact]
        public void ControlSelectsAtMostTwoInChronologicalOrder()
        {
            var sequencer = new MorningDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 1);
            var candidates = new[]
            {
                Candidate(day, 9, 10, 90m, 90m, 10m),
                Candidate(day, 9, 0, 90m, 90m, 20m),
                Candidate(day, 9, 5, 90m, 90m, 30m)
            };

            var selected = sequencer.Sequence(candidates, MorningDailySequencingPolicy.ControlFirstTwoHighEntry)
                .Where(x => x.Selected)
                .ToList();

            Assert.Equal(2, selected.Count);
            Assert.Equal(9, selected[0].Candidate.EntryUtc.Hour);
            Assert.Equal(0, selected[0].Candidate.EntryUtc.Minute);
            Assert.Equal(5, selected[1].Candidate.EntryUtc.Minute);
            Assert.Equal(1, selected[0].AttemptNumber);
            Assert.Equal(2, selected[1].AttemptNumber);
        }

        [Fact]
        public void StrictUpper80RejectsHighEntryWhenPotentialIsBelowEighty()
        {
            var sequencer = new MorningDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 2);
            var candidates = new[]
            {
                Candidate(day, 9, 0, 95m, 79.9m, 500m),
                Candidate(day, 9, 5, 72m, 80m, -100m)
            };

            var decisions = sequencer.Sequence(candidates, MorningDailySequencingPolicy.StrictUpper80).ToList();

            Assert.False(decisions[0].Selected);
            Assert.Equal("potential-below-80", decisions[0].Reason);
            Assert.True(decisions[1].Selected);
            Assert.Equal(1, decisions[1].AttemptNumber);
        }

        [Fact]
        public void BalancedReserveUsesAtMostOneFallbackAndReservesRemainingSlotForUpper80()
        {
            var sequencer = new MorningDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 3);
            var candidates = new[]
            {
                Candidate(day, 8, 50, 90m, 75m, 10m),
                Candidate(day, 9, 0, 95m, 78m, 20m),
                Candidate(day, 9, 10, 75m, 85m, 30m),
                Candidate(day, 9, 20, 99m, 95m, 40m)
            };

            var decisions = sequencer.Sequence(candidates, MorningDailySequencingPolicy.BalancedReserve).ToList();
            var selected = decisions.Where(x => x.Selected).ToList();

            Assert.Equal(2, selected.Count);
            Assert.Equal("fallback-first-slot", selected[0].Reason);
            Assert.Equal("upper80", selected[1].Reason);
            Assert.False(decisions[1].Selected);
            Assert.Equal("second-slot-reserved-upper80", decisions[1].Reason);
        }

        [Fact]
        public void FutureOutcomeCannotChangeSelectionForIdenticalCausalScores()
        {
            var sequencer = new MorningDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 4);
            var winner = Candidate(day, 9, 0, 88m, 84m, 1000m, 600m, 20m);
            var loser = Candidate(day, 9, 0, 88m, 84m, -1000m, 10m, 600m);

            var winnerDecision = sequencer.Sequence(new[] { winner }, MorningDailySequencingPolicy.StrictUpper80).Single();
            var loserDecision = sequencer.Sequence(new[] { loser }, MorningDailySequencingPolicy.StrictUpper80).Single();

            Assert.Equal(winnerDecision.Selected, loserDecision.Selected);
            Assert.Equal(winnerDecision.Reason, loserDecision.Reason);
            Assert.Equal(winner.PriorityScore, loser.PriorityScore);
        }

        [Fact]
        public void PriorityScoreUsesOnlyEntryAndFrozenPotentialAxes()
        {
            var sequencer = new MorningDailyOpportunitySequencer();
            Assert.Equal(85.5m, sequencer.SelectionScore(80m, 90m));
        }

        [Fact]
        public void CandidateRequiresEntryAndPotentialToReferenceSameOpportunity()
        {
            var day = new DateTime(2026, 7, 5);
            var a = Candidate(day, 9, 0, 90m, 90m, 0m);
            var b = Candidate(day, 9, 5, 90m, 90m, 0m);

            Assert.Throws<ArgumentException>(() => new MorningDailySequencingCandidate(a.Entry, b.Potential));
        }

        private static MorningDailySequencingCandidate Candidate(
            DateTime day,
            int hour,
            int minute,
            decimal entryScore,
            decimal potentialScore,
            decimal realized,
            decimal mfe = 100m,
            decimal mae = 50m)
        {
            var entryUtc = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, TimeSpan.Zero);
            var outcome = new MorningAdaptiveTradeOutcome(
                day,
                MorningMarketState.Range,
                MorningAdaptiveSetupType.RangeResolution,
                NewYorkResearchDirection.Long,
                entryUtc.AddMinutes(-1),
                entryUtc,
                20000m,
                19990m,
                40m,
                0.30m,
                50m,
                MorningAdaptiveManagementMode.Core,
                MorningAdaptiveExitReason.CoreCapture,
                entryUtc.AddMinutes(10),
                20010m,
                realized / 4m,
                realized,
                mfe,
                mae);

            var potentialFeatures = new MorningOpportunityPotentialFeatures(
                10, 100m, 0.5m, 0.3m, 0.2m, 0.4m, 2, 2m, 1.5m, 0.2m);
            var potentialObservation = new MorningOpportunityPotentialObservation(outcome, potentialFeatures, 50m);
            var entryFeatures = new MorningEntryEfficiencyFeatures(
                40m, 100m, 0.4m, 0.2m, 0.6m, 2, 1, 10m, 0.4m, 1m);
            var entryObservation = new MorningEntryEfficiencyObservation(potentialObservation, entryFeatures, entryScore);
            var stabilityObservation = new MorningStabilityWeightedPotentialObservation(potentialObservation, potentialScore);
            return new MorningDailySequencingCandidate(entryObservation, stabilityObservation);
        }
    }
}
