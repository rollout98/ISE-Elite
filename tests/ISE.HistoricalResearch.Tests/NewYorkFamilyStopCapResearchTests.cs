using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkFamilyStopCapResearchTests
    {
        [Fact]
        public void DefaultCapGridIsTransparentAndOrdered()
        {
            var config = new NewYorkFamilyStopCapConfig();
            Assert.Equal(new[] { 150m, 200m, 250m, 300m, 350m, 400m, 500m }, config.CapTicks);
        }

        [Fact]
        public void ReportsRetainedAndExcludedWinnersByFamilyAndCap()
        {
            var rows = new List<NewYorkSetupQualityOutcome>
            {
                Row(new DateTime(2026,7,1), NewYorkTradeableEntryType.DirectReversal, 100m, true, true, false, false),
                Row(new DateTime(2026,7,2), NewYorkTradeableEntryType.DirectReversal, 220m, true, true, true, false),
                Row(new DateTime(2026,7,3), NewYorkTradeableEntryType.DirectReversal, 320m, false, false, false, true),
                Row(new DateTime(2026,7,4), NewYorkTradeableEntryType.ContinuationAfterValidatedReset, 180m, false, false, false, true)
            };

            var analyzer = new NewYorkFamilyStopCapAnalyzer(new NewYorkFamilyStopCapConfig(new[] { 200m, 250m }));
            var outcomes = analyzer.Analyze(rows);

            var direct200 = Assert.Single(outcomes, x => x.EntryType == NewYorkTradeableEntryType.DirectReversal && x.CapTicks == 200m);
            Assert.Equal(3, direct200.TotalCandidates);
            Assert.Equal(1, direct200.RetainedCandidates);
            Assert.Equal(2, direct200.ExcludedCandidates);
            Assert.Equal(1, direct200.Hit500BeforeStop);
            Assert.Equal(1, direct200.ExcludedHit500);
            Assert.Equal(1, direct200.ExcludedHit1000);

            var direct250 = Assert.Single(outcomes, x => x.EntryType == NewYorkTradeableEntryType.DirectReversal && x.CapTicks == 250m);
            Assert.Equal(2, direct250.RetainedCandidates);
            Assert.Equal(2, direct250.Hit500BeforeStop);
            Assert.Equal(1, direct250.Hit1000BeforeStop);
            Assert.Equal(0, direct250.ExcludedHit500);
            Assert.Equal(160m, direct250.AverageRetainedRiskTicks);
            Assert.Equal(160m, direct250.MedianRetainedRiskTicks);
        }

        private static NewYorkSetupQualityOutcome Row(DateTime date, NewYorkTradeableEntryType type, decimal risk,
            bool hit300, bool hit500, bool hit1000, bool stopped)
        {
            var t = new DateTimeOffset(date.Date.AddHours(14), TimeSpan.Zero);
            return new NewYorkSetupQualityOutcome(date, type, NewYorkResearchDirection.Long, t, risk,
                0m, 0m, 0m, 0m, 0m, 0m, NewYorkSetupQualityGrade.C,
                hit300 ? t.AddMinutes(1) : (DateTimeOffset?)null,
                hit500 ? t.AddMinutes(2) : (DateTimeOffset?)null,
                hit1000 ? t.AddMinutes(3) : (DateTimeOffset?)null,
                stopped ? t.AddMinutes(4) : (DateTimeOffset?)null);
        }
    }
}
