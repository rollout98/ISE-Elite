using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkRiskQualifiedEntryResearchTests
    {
        [Fact]
        public void RejectsEntryWhenStructuralRiskExceedsCeiling()
        {
            var date = new DateTime(2026, 7, 14);
            var bars = BuildBars(date, new[]
            {
                (8,55,100m,101m,99m,100m),(8,56,100m,102m,98m,101m),(8,57,101m,103m,99m,102m)
            });
            var transition = Transition(date, NewYorkEightFortyFiveState.Reverse, NewYorkResearchDirection.Long, NewYorkResearchDirection.Short, bars[0].TimestampUtc, 20m);
            var prior = new NewYorkTradeableEntryOutcome(date, NewYorkEightFortyFiveState.Reverse, NewYorkTradeableEntryType.DirectReversal,
                NewYorkResearchDirection.Short, bars[0].TimestampUtc, null, bars[0].TimestampUtc, bars[1].TimestampUtc,
                100m, 151m, 0m, false, null, NewYorkTradeSequenceResult.TimedOut, bars[2].TimestampUtc, null, null, null);

            var outcome = Assert.Single(new NewYorkRiskQualifiedEntryAnalyzer().Analyze(bars, new[] { transition }, new[] { prior }));
            Assert.Equal(NewYorkRiskQualifiedDisposition.RejectedRisk, outcome.Disposition);
            Assert.False(outcome.HasAcceptedEntry);
            Assert.Equal(204m, outcome.InitialRiskTicks);
        }

        [Fact]
        public void HandoffRequiresImpulseRetestAndResumptionBeforeEntry()
        {
            var date = new DateTime(2026, 7, 15);
            var bars = BuildBars(date, new[]
            {
                (8,50,100m,101m,99m,100m),
                (8,51,100m,100m,95m,95.5m),
                (8,52,95.5m,97m,94m,96m),
                (8,53,96m,96.5m,93m,93.5m),
                (8,54,93.5m,94m,90m,91m),
                (8,55,91m,92m,88m,89m),
                (8,56,89m,90m,85m,86m),
                (8,57,86m,87m,80m,81m)
            });
            var transition = Transition(date, NewYorkEightFortyFiveState.Continue, NewYorkResearchDirection.Long, NewYorkResearchDirection.Long, bars[0].TimestampUtc, 20m);
            var prior = new NewYorkTradeableEntryOutcome(date, NewYorkEightFortyFiveState.Continue, NewYorkTradeableEntryType.ContinuationFailureReversal,
                NewYorkResearchDirection.Short, bars[0].TimestampUtc, null, bars[1].TimestampUtc, bars[2].TimestampUtc,
                95.5m, 101m, 0m, true, bars[0].TimestampUtc, NewYorkTradeSequenceResult.TimedOut, bars[7].TimestampUtc, null, null, null);

            var config = new NewYorkRiskQualifiedEntryConfig(maximumInitialRiskTicks: 200m, intermediateObjective: 10m, lowerObjective: 20m, upperObjective: 40m);
            var outcome = Assert.Single(new NewYorkRiskQualifiedEntryAnalyzer(config).Analyze(bars, new[] { transition }, new[] { prior }));

            Assert.Equal(NewYorkRiskQualifiedDisposition.Accepted, outcome.Disposition);
            Assert.Equal(NewYorkTradeableEntryType.ContinuationFailureReversal, outcome.EntryType);
            Assert.Equal(NewYorkResearchDirection.Short, outcome.Direction);
            Assert.True(outcome.EntryUtc > bars[2].TimestampUtc);
            Assert.True(outcome.InitialRiskTicks <= 200m);
        }

        [Fact]
        public void SequenceTracksThreeHundredBeforeFiveHundredAndStop()
        {
            var date = new DateTime(2026, 7, 16);
            var bars = BuildBars(date, new[]
            {
                (8,55,100m,101m,99m,100m),
                (8,56,100m,101m,99m,100m),
                (8,57,100m,180m,99m,170m),
                (8,58,170m,240m,169m,230m)
            });
            var transition = Transition(date, NewYorkEightFortyFiveState.Reverse, NewYorkResearchDirection.Short, NewYorkResearchDirection.Long, bars[0].TimestampUtc, 20m);
            var prior = new NewYorkTradeableEntryOutcome(date, NewYorkEightFortyFiveState.Reverse, NewYorkTradeableEntryType.DirectReversal,
                NewYorkResearchDirection.Long, bars[0].TimestampUtc, null, bars[0].TimestampUtc, bars[1].TimestampUtc,
                100m, 90m, 0m, false, null, NewYorkTradeSequenceResult.TimedOut, bars[3].TimestampUtc, null, null, null);

            var config = new NewYorkRiskQualifiedEntryConfig(maximumInitialRiskTicks: 200m, intermediateObjective: 300m, lowerObjective: 500m, upperObjective: 1000m,
                pointValuePerContract: 2m, contracts: 2);
            var outcome = Assert.Single(new NewYorkRiskQualifiedEntryAnalyzer(config).Analyze(bars, new[] { transition }, new[] { prior }));

            Assert.True(outcome.HasAcceptedEntry);
            Assert.Equal(NewYorkRiskSequenceResult.IntermediateObjectiveFirst, outcome.SequenceResult);
            Assert.True(outcome.IntermediateBeforeStop);
            Assert.False(outcome.LowerBeforeStop);
        }

        private static NewYorkEightFortyFiveTransitionOutcome Transition(DateTime date, NewYorkEightFortyFiveState state,
            NewYorkResearchDirection opening, NewYorkResearchDirection trade, DateTimeOffset signal, decimal openingRange)
        {
            return new NewYorkEightFortyFiveTransitionOutcome(date, state, opening, trade, signal, signal,
                openingRange, openingRange * 0.75m, 0.75m, 0m, 0m, null, null);
        }

        private static List<HistoricalBar> BuildBars(DateTime date, (int hour, int minute, decimal open, decimal high, decimal low, decimal close)[] rows)
        {
            var result = new List<HistoricalBar>();
            var central = ResolveCentralTimeZone();
            foreach (var row in rows)
            {
                var local = DateTime.SpecifyKind(date.Date.AddHours(row.hour).AddMinutes(row.minute), DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
                result.Add(new HistoricalBar("MNQ", "09-26", new DateTimeOffset(utc, TimeSpan.Zero), date.Date, 60,
                    row.open, row.high, row.low, row.close, 100, HistoricalDataSourceKind.NinjaTraderRepository, "test"));
            }
            return result;
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
