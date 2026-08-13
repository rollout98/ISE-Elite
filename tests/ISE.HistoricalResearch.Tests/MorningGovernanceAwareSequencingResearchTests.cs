using System;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningGovernanceAwareSequencingResearchTests
    {
        [Fact]
        public void LowerObjectiveBlocksNewEntriesAfterFiveHundred()
        {
            var sequencer = new MorningGovernanceAwareSequencer();
            var day = new DateTime(2026, 7, 20);

            var decisions = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 10, 90m, 90m, 550m),
                Candidate(day, 9, 20, 10, 90m, 90m, 100m)
            }, MorningGovernanceSequencingPolicy.Objective500).ToList();

            Assert.True(decisions[0].Selected);
            Assert.False(decisions[1].Selected);
            Assert.Equal("lower-objective-lock", decisions[1].Reason);
        }

        [Fact]
        public void ProtectedGreenRejectsRiskThatWouldViolateGreenFloor()
        {
            var config = new MorningGovernanceSequencingConfig(
                maximumAttempts: 2,
                maximumConsecutiveLosses: 2,
                greenDayThresholdDollars: 300m,
                protectedGreenFloorDollars: 200m,
                lowerObjectiveDollars: 500m,
                upperObjectiveDollars: 1000m,
                baseRiskPerTradeDollars: 150m,
                dollarsPerRiskTick: 1m);

            var sequencer = new MorningGovernanceAwareSequencer(config);
            var day = new DateTime(2026, 7, 21);

            var decisions = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 10, 90m, 90m, 320m, riskTicks: 40m),
                Candidate(day, 9, 20, 10, 90m, 90m, 100m, riskTicks: 150m)
            }, MorningGovernanceSequencingPolicy.ProtectedGreen).ToList();

            Assert.True(decisions[0].Selected);
            Assert.False(decisions[1].Selected);
            Assert.Equal("green-floor-risk-block", decisions[1].Reason);
        }

        [Fact]
        public void ProtectedGreenAllowsRiskWithinAvailableGreen()
        {
            var sequencer = new MorningGovernanceAwareSequencer();
            var day = new DateTime(2026, 7, 22);

            var decisions = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 10, 90m, 90m, 400m, riskTicks: 40m),
                Candidate(day, 9, 20, 10, 90m, 90m, 50m, riskTicks: 100m)
            }, MorningGovernanceSequencingPolicy.ProtectedGreen)
            .Where(x => x.Selected)
            .ToList();

            Assert.Equal(2, decisions.Count);
        }

        [Fact]
        public void SingleLossStillAllowsSecondQualifiedAttempt()
        {
            var sequencer = new MorningGovernanceAwareSequencer();
            var day = new DateTime(2026, 7, 23);

            var selected = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 10, 90m, 90m, -100m),
                Candidate(day, 9, 20, 10, 90m, 90m, 150m)
            }, MorningGovernanceSequencingPolicy.ProtectedGreen)
            .Where(x => x.Selected)
            .ToList();

            Assert.Equal(2, selected.Count);
            Assert.Equal(1, selected[0].ConsecutiveLossesAfter);
            Assert.Equal(0, selected[1].ConsecutiveLossesAfter);
        }

        [Fact]
        public void PositionOpenDoesNotConsumeAttempt()
        {
            var sequencer = new MorningGovernanceAwareSequencer();
            var day = new DateTime(2026, 7, 24);

            var decisions = sequencer.Sequence(new[]
            {
                Candidate(day, 9, 0, 20, 90m, 90m, 100m),
                Candidate(day, 9, 5, 10, 90m, 90m, 500m),
                Candidate(day, 9, 25, 10, 90m, 90m, 100m)
            }, MorningGovernanceSequencingPolicy.ProtectedGreen).ToList();

            Assert.Equal("position-open", decisions[1].Reason);
            Assert.Equal(0, decisions[1].AttemptNumber);
            Assert.True(decisions[2].Selected);
            Assert.Equal(2, decisions[2].AttemptNumber);
        }

        [Fact]
        public void OutcomeCannotAffectWhetherCurrentTradeIsSelected()
        {
            var sequencer = new MorningGovernanceAwareSequencer();
            var day = new DateTime(2026, 7, 25);

            var winner = Candidate(day, 9, 0, 10, 90m, 90m, 1000m);
            var loser = Candidate(day, 9, 0, 10, 90m, 90m, -1000m);

            var a = sequencer.Sequence(new[] { winner }, MorningGovernanceSequencingPolicy.ProtectedGreen).Single();
            var b = sequencer.Sequence(new[] { loser }, MorningGovernanceSequencingPolicy.ProtectedGreen).Single();

            Assert.True(a.Selected);
            Assert.True(b.Selected);
            Assert.Equal(a.AttemptNumber, b.AttemptNumber);
        }

        private static MorningDailySequencingCandidate Candidate(
            DateTime day,
            int hour,
            int minute,
            int holdMinutes,
            decimal entryScore,
            decimal potentialScore,
            decimal realized,
            decimal riskTicks = 100m,
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
                riskTicks,
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
                outcome, potentialFeatures, 50m);

            var entryFeatures = new MorningEntryEfficiencyFeatures(
                riskTicks, 100m, 0.4m, 0.2m, 0.6m, 2, 1, 10m, 0.4m, 1m);

            var entryObservation = new MorningEntryEfficiencyObservation(
                potentialObservation, entryFeatures, entryScore);

            var stabilityObservation = new MorningStabilityWeightedPotentialObservation(
                potentialObservation, potentialScore);

            return new MorningDailySequencingCandidate(entryObservation, stabilityObservation);
        }
    }
}
