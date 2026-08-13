using System;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningExecutionRealisticSequencingResearchTests
    {
        [Fact]
        public void RejectsCandidateWhileSelectedPositionIsStillOpen()
        {
            var sequencer = new MorningExecutionRealisticDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 6);

            var decisions = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 20, 90m, 90m, 100m),
                Candidate(day, 9, 5, 10, 90m, 95m, 500m)
            }, MorningDailySequencingPolicy.StrictUpper80).ToList();

            Assert.True(decisions[0].Selected);
            Assert.False(decisions[1].Selected);
            Assert.Equal("position-open", decisions[1].Reason);
            Assert.Equal(0, decisions[1].AttemptNumber);
        }

        [Fact]
        public void OverlapRejectionDoesNotConsumeSecondAttempt()
        {
            var sequencer = new MorningExecutionRealisticDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 7);

            var selected = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 20, 90m, 90m, 100m),
                Candidate(day, 9, 5, 10, 90m, 95m, 500m),
                Candidate(day, 9, 25, 10, 90m, 85m, 200m)
            }, MorningDailySequencingPolicy.StrictUpper80)
            .Where(x => x.Selected)
            .ToList();

            Assert.Equal(2, selected.Count);
            Assert.Equal(1, selected[0].AttemptNumber);
            Assert.Equal(2, selected[1].AttemptNumber);
            Assert.Equal(25, selected[1].Candidate.EntryUtc.Minute);
        }

        [Fact]
        public void CandidateAtExactExitTimeIsAvailable()
        {
            var sequencer = new MorningExecutionRealisticDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 8);

            var selected = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 15, 90m, 90m, 100m),
                Candidate(day, 9, 15, 10, 90m, 90m, 100m)
            }, MorningDailySequencingPolicy.StrictUpper80)
            .Where(x => x.Selected)
            .ToList();

            Assert.Equal(2, selected.Count);
        }

        [Fact]
        public void StrictUpper80StillRejectsExecutableBelowEightyCandidate()
        {
            var sequencer = new MorningExecutionRealisticDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 9);

            var decision = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 10, 95m, 79.9m, 500m)
            }, MorningDailySequencingPolicy.StrictUpper80).Single();

            Assert.False(decision.Selected);
            Assert.Equal("potential-below-80", decision.Reason);
        }

        [Fact]
        public void RealizedMfeAndMaeDoNotChangeSelectionWhenTimingAndScoresMatch()
        {
            var sequencer = new MorningExecutionRealisticDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 10);

            var winner = Candidate(day, 9, 0, 15, 90m, 90m, 1000m, 600m, 10m);
            var loser = Candidate(day, 9, 0, 15, 90m, 90m, -1000m, 10m, 600m);

            var a = sequencer.Sequence(new[] { winner }, MorningDailySequencingPolicy.StrictUpper80).Single();
            var b = sequencer.Sequence(new[] { loser }, MorningDailySequencingPolicy.StrictUpper80).Single();

            Assert.Equal(a.Selected, b.Selected);
            Assert.Equal(a.Reason, b.Reason);
        }

        [Fact]
        public void MissDiagnosticsSeparateOverlapFromExecutableMiss()
        {
            var sequencer = new MorningExecutionRealisticDailyOpportunitySequencer();
            var day = new DateTime(2026, 7, 11);

            var decisions = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 20, 90m, 90m, 100m),
                Candidate(day, 9, 5, 10, 90m, 95m, 500m),
                Candidate(day, 9, 25, 10, 90m, 75m, 500m)
            }, MorningDailySequencingPolicy.StrictUpper80).ToList();

            Assert.True(MorningExecutionRealisticDailyOpportunitySequencer.IsOverlapUnavailable(decisions[1]));
            Assert.False(MorningExecutionRealisticDailyOpportunitySequencer.IsExecutionAvailableMiss(decisions[1]));
            Assert.True(MorningExecutionRealisticDailyOpportunitySequencer.IsExecutionAvailableMiss(decisions[2]));
        }

        private static MorningDailySequencingCandidate Candidate(
            DateTime day,
            int hour,
            int minute,
            int holdMinutes,
            decimal entryScore,
            decimal potentialScore,
            decimal realized,
            decimal mfe = 100m,
            decimal mae = 50m)
        {
            var entryUtc = new DateTimeOffset(
                day.Year, day.Month, day.Day, hour, minute, 0, TimeSpan.Zero);

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
                entryUtc.AddMinutes(holdMinutes),
                20010m,
                realized / 4m,
                realized,
                mfe,
                mae);

            var potentialFeatures = new MorningOpportunityPotentialFeatures(
                10, 100m, 0.5m, 0.3m, 0.2m, 0.4m, 2, 2m, 1.5m, 0.2m);

            var potentialObservation = new MorningOpportunityPotentialObservation(
                outcome,
                potentialFeatures,
                50m);

            var entryFeatures = new MorningEntryEfficiencyFeatures(
                40m, 100m, 0.4m, 0.2m, 0.6m, 2, 1, 10m, 0.4m, 1m);

            var entryObservation = new MorningEntryEfficiencyObservation(
                potentialObservation,
                entryFeatures,
                entryScore);

            var stabilityObservation = new MorningStabilityWeightedPotentialObservation(
                potentialObservation,
                potentialScore);

            return new MorningDailySequencingCandidate(
                entryObservation,
                stabilityObservation);
        }
    }
}
