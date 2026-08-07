using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkSetupQualityResearchTests
    {
        [Fact]
        public void StrongSetupCanRemainPreferredAboveTwoHundredTicksRisk()
        {
            var date = new DateTime(2026, 7, 17);
            var bars = BuildBars(date, new[]
            {
                (8,54,100m,102m,98m,99m),
                (8,55,99m,110m,98m,109m),
                (8,56,109m,112m,108m,111m),
                (8,57,111m,190m,110m,185m)
            });
            var transition = Transition(date, NewYorkEightFortyFiveState.Reverse, NewYorkResearchDirection.Short,
                NewYorkResearchDirection.Long, bars[0].TimestampUtc, 20m);
            var trade = new NewYorkTradeableEntryOutcome(date, NewYorkEightFortyFiveState.Reverse,
                NewYorkTradeableEntryType.DirectReversal, NewYorkResearchDirection.Long, bars[0].TimestampUtc,
                null, bars[1].TimestampUtc, bars[2].TimestampUtc, 109m, 54m, 0m, false, null,
                NewYorkTradeSequenceResult.TimedOut, bars[3].TimestampUtc, null, null, null);

            var outcome = Assert.Single(new NewYorkSetupQualityAnalyzer().Analyze(bars, new[] { transition }, new[] { trade }));
            Assert.True(outcome.InitialRiskTicks > 200m);
            Assert.True(outcome.Preferred);
            Assert.True(outcome.TotalScore >= 55m);
        }

        [Fact]
        public void WeakConfirmationReceivesCGradeEvenWithSmallRisk()
        {
            var date = new DateTime(2026, 7, 18);
            var bars = BuildBars(date, new[]
            {
                (8,54,100m,101m,99m,100m),
                (8,55,100m,101m,99m,100.1m),
                (8,56,100.1m,101m,99.5m,100.2m)
            });
            var transition = Transition(date, NewYorkEightFortyFiveState.Reverse, NewYorkResearchDirection.Long,
                NewYorkResearchDirection.Short, bars[0].TimestampUtc, 20m);
            var trade = new NewYorkTradeableEntryOutcome(date, NewYorkEightFortyFiveState.Reverse,
                NewYorkTradeableEntryType.DirectReversal, NewYorkResearchDirection.Short, bars[0].TimestampUtc,
                null, bars[1].TimestampUtc, bars[2].TimestampUtc, 100.1m, 105m, 0m, false, null,
                NewYorkTradeSequenceResult.TimedOut, bars[2].TimestampUtc, null, null, null);

            var outcome = Assert.Single(new NewYorkSetupQualityAnalyzer().Analyze(bars, new[] { transition }, new[] { trade }));
            Assert.Equal(NewYorkSetupQualityGrade.C, outcome.Grade);
        }

        [Fact]
        public void ObjectivePathContinuesFromPreferredCandidateUntilStop()
        {
            var date = new DateTime(2026, 7, 20);
            var bars = BuildBars(date, new[]
            {
                (8,53,100m,101m,99m,100m),
                (8,54,100m,101m,90m,91m),
                (8,55,91m,92m,89m,90m),
                (8,56,90m,91m,10m,20m)
            });
            var transition = Transition(date, NewYorkEightFortyFiveState.Reverse, NewYorkResearchDirection.Long,
                NewYorkResearchDirection.Short, bars[0].TimestampUtc, 20m);
            var trade = new NewYorkTradeableEntryOutcome(date, NewYorkEightFortyFiveState.Reverse,
                NewYorkTradeableEntryType.DirectReversal, NewYorkResearchDirection.Short, bars[0].TimestampUtc,
                null, bars[1].TimestampUtc, bars[2].TimestampUtc, 91m, 110m, 0m, false, null,
                NewYorkTradeSequenceResult.TimedOut, bars[3].TimestampUtc, null, null, null);
            var cfg = new NewYorkSetupQualityConfig(intermediateObjective: 20m, lowerObjective: 40m, upperObjective: 80m);

            var outcome = Assert.Single(new NewYorkSetupQualityAnalyzer(cfg).Analyze(bars, new[] { transition }, new[] { trade }));
            Assert.True(outcome.Hit300BeforeStop);
            Assert.True(outcome.Hit500BeforeStop);
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