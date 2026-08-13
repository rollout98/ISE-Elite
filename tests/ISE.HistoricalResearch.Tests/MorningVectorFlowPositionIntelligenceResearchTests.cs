using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningVectorFlowPositionIntelligenceResearchTests
    {
        [Fact]
        public void AnalyzerCannotCreateTradesWithoutSelectedCandidates()
        {
            var analyzer = new MorningVectorFlowPositionIntelligenceAnalyzer();
            var result = analyzer.Analyze(Array.Empty<HistoricalBar>(), Array.Empty<MorningDailySequencingCandidate>());
            Assert.Empty(result);
        }

        [Fact]
        public void PositionIntelligencePreservesCandidateIdentity()
        {
            var candidate = Candidate(
                new DateTime(2026, 7, 27),
                NewYorkResearchDirection.Long,
                14, 0,
                100m,
                90m,
                90m);

            var bars = Bars(candidate.EntryUtc, 40, 100m, 0.25m);
            var analyzer = new MorningVectorFlowPositionIntelligenceAnalyzer(
                new MorningVectorFlowPositionIntelligenceConfig(
                    ftcLength: 2,
                    ftcAtrLength: 2,
                    ftcAtrHighestLookback: 2,
                    vidyaLength: 2,
                    vidyaMomentum: 2,
                    vidyaSmoothingLength: 1,
                    vidyaAtrLength: 2,
                    scalpTargetTicks: 4,
                    scalpTimeoutMinutes: 5,
                    runnerThresholdTicks: 8));

            var managed = analyzer.Analyze(bars, new[] { candidate }).Single();
            Assert.Same(candidate, managed.Candidate);
        }

        [Fact]
        public void ManagementDoesNotChangeEntryTimeOrEntryPrice()
        {
            var candidate = Candidate(
                new DateTime(2026, 7, 28),
                NewYorkResearchDirection.Short,
                14, 0,
                100m,
                90m,
                90m);

            var entryUtc = candidate.EntryUtc;
            var entryPrice = candidate.Entry.Source.Source.EntryPrice;

            var analyzer = new MorningVectorFlowPositionIntelligenceAnalyzer(
                new MorningVectorFlowPositionIntelligenceConfig(
                    ftcLength: 2,
                    ftcAtrLength: 2,
                    ftcAtrHighestLookback: 2,
                    vidyaLength: 2,
                    vidyaMomentum: 2,
                    vidyaSmoothingLength: 1,
                    vidyaAtrLength: 2,
                    scalpTargetTicks: 4,
                    scalpTimeoutMinutes: 5,
                    runnerThresholdTicks: 8));

            analyzer.Analyze(Bars(entryUtc, 30, 100m, -0.25m), new[] { candidate });

            Assert.Equal(entryUtc, candidate.EntryUtc);
            Assert.Equal(entryPrice, candidate.Entry.Source.Source.EntryPrice);
        }

        private static MorningDailySequencingCandidate Candidate(
            DateTime day,
            NewYorkResearchDirection direction,
            int hourUtc,
            int minuteUtc,
            decimal entryPrice,
            decimal entryScore,
            decimal potentialScore)
        {
            var entryUtc = new DateTimeOffset(
                day.Year, day.Month, day.Day, hourUtc, minuteUtc, 0, TimeSpan.Zero);

            var stop = direction == NewYorkResearchDirection.Long
                ? entryPrice - 5m
                : entryPrice + 5m;

            var outcome = new MorningAdaptiveTradeOutcome(
                day,
                MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation,
                direction,
                entryUtc.AddMinutes(-1),
                entryUtc,
                entryPrice,
                stop,
                20m,
                0.5m,
                20m,
                MorningAdaptiveManagementMode.Core,
                MorningAdaptiveExitReason.CoreCapture,
                entryUtc.AddMinutes(20),
                entryPrice,
                0m,
                0m,
                0m,
                0m);

            var pf = new MorningOpportunityPotentialFeatures(
                5, 20m, 0.2m, 0.4m, 0.2m, 0.5m, 1, 1m, 1.2m, 0.1m);

            var po = new MorningOpportunityPotentialObservation(outcome, pf, 50m);

            var ef = new MorningEntryEfficiencyFeatures(
                20m, 50m, 0.4m, 0.2m, 0.5m, 2, 1, 5m, 0.4m, 1m);

            var eo = new MorningEntryEfficiencyObservation(po, ef, entryScore);
            var sw = new MorningStabilityWeightedPotentialObservation(po, potentialScore);

            return new MorningDailySequencingCandidate(eo, sw);
        }

        private static IReadOnlyList<HistoricalBar> Bars(
            DateTimeOffset start,
            int count,
            decimal startPrice,
            decimal step)
        {
            var result = new List<HistoricalBar>();
            var price = startPrice;

            for (var i = 0; i < count; i++)
            {
                var open = price;
                var close = price + step;
                var high = Math.Max(open, close) + 0.25m;
                var low = Math.Min(open, close) - 0.25m;

                var timestamp = start.AddMinutes(i);
                result.Add(new HistoricalBar(
                    "MNQ",
                    "09-26",
                    timestamp,
                    timestamp.UtcDateTime.Date,
                    60,
                    open,
                    high,
                    low,
                    close,
                    1000L,
                    HistoricalDataSourceKind.ImportedFile,
                    "v7-unit-test"));

                price = close;
            }

            return result;
        }
    }
}

