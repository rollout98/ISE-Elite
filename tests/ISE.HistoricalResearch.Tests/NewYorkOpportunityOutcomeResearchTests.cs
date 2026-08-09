using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkOpportunityOutcomeResearchTests
    {
        [Fact]
        public void Labeler_ComputesLongMfeMaeAndRunnerEvidence()
        {
            var date = new DateTime(2026, 7, 1);
            var bars = BuildSession(date, i =>
            {
                if (i < 150) return 100m;
                if (i < 180) return 100m + (i - 150) * 0.50m;
                if (i < 210) return 115m - (i - 180) * 0.10m;
                return 112m;
            }).ToList();
            var features = new NewYorkSessionResearchFeatures(date, "MNQ", 60, 300, 10m, 10m, 6m, 0.60m, 1m, 1m, 4m, 20m, 10m, 0.50m);
            var classification = new NewYorkRegimeClassification(features, NewYorkResearchRegime.OpeningDrive, 0.8m, "test");
            var seed = new NewYorkOpportunitySeedLabel(date, NewYorkOpportunitySeedType.OpeningDriveContinuation, NewYorkResearchDirection.Long, new TimeSpan(8, 30, 0), new TimeSpan(9, 5, 0), 0.8m, "test");

            var outcome = Assert.Single(new NewYorkOpportunityOutcomeLabeler().Label(bars, new[] { classification }, new[] { seed }));

            Assert.Equal(100m, outcome.EntryPrice);
            Assert.True(outcome.MfePoints >= 15m);
            Assert.True(outcome.MaePoints <= 1m);
            Assert.True(outcome.ReachedFullOpeningRange);
            Assert.True(outcome.ReachedOneAndHalfOpeningRange);
            Assert.True(outcome.RunnerCandidate);
            Assert.True(outcome.OpportunityWindowClosedFavorable);
            Assert.True(outcome.SessionClosedFavorable);
        }

        [Fact]
        public void Labeler_ComputesShortDirectionCorrectly()
        {
            var date = new DateTime(2026, 7, 2);
            var bars = BuildSession(date, i => i < 150 ? 200m : 200m - (i - 150) * 0.10m).ToList();
            var features = new NewYorkSessionResearchFeatures(date, "MNQ", 60, 300, 10m, 10m, -6m, 0.60m, -2m, 1m, -4m, 20m, -10m, 0.50m);
            var classification = new NewYorkRegimeClassification(features, NewYorkResearchRegime.OpeningDrive, 0.8m, "test");
            var seed = new NewYorkOpportunitySeedLabel(date, NewYorkOpportunitySeedType.OpeningDriveContinuation, NewYorkResearchDirection.Short, new TimeSpan(8, 30, 0), new TimeSpan(9, 5, 0), 0.8m, "test");

            var outcome = Assert.Single(new NewYorkOpportunityOutcomeLabeler().Label(bars, new[] { classification }, new[] { seed }));

            Assert.True(outcome.MfePoints > 0m);
            Assert.True(outcome.SessionEndMovePoints > 0m);
            Assert.True(outcome.GrossSessionEndPnlPerContract > 0m);
        }

        [Fact]
        public void Labeler_AppliesConfiguredResearchCostsWithoutChangingExcursions()
        {
            var date = new DateTime(2026, 7, 6);
            var bars = BuildSession(date, i => i < 150 ? 100m : 101m).ToList();
            var features = new NewYorkSessionResearchFeatures(date, "MNQ", 60, 300, 10m, 10m, 4m, 0.40m, 1m, 1m, 2m, 15m, 5m, 0.33m);
            var classification = new NewYorkRegimeClassification(features, NewYorkResearchRegime.LaterContinuationReversal, 0.5m, "test");
            var seed = new NewYorkOpportunitySeedLabel(date, NewYorkOpportunitySeedType.LaterContinuation, NewYorkResearchDirection.Long, new TimeSpan(9, 30, 0), new TimeSpan(10, 30, 0), 0.5m, "test");
            var config = new NewYorkOpportunityOutcomeConfig(0.25m, 2m, 1.20m, 1m);

            var outcome = Assert.Single(new NewYorkOpportunityOutcomeLabeler(config).Label(bars, new[] { classification }, new[] { seed }));

            Assert.Equal(outcome.GrossSessionEndPnlPerContract - 2.20m, outcome.AfterCostSessionEndPnlPerContract);
        }

        [Fact]
        public void Labeler_RejectsSeedWithoutMatchingClassification()
        {
            var date = new DateTime(2026, 7, 7);
            var bars = BuildSession(date, _ => 100m).ToList();
            var seed = new NewYorkOpportunitySeedLabel(date, NewYorkOpportunitySeedType.OpeningDriveContinuation, NewYorkResearchDirection.Long, new TimeSpan(8, 30, 0), new TimeSpan(9, 5, 0), 0.8m, "test");

            Assert.Throws<InvalidOperationException>(() => new NewYorkOpportunityOutcomeLabeler().Label(bars, Array.Empty<NewYorkRegimeClassification>(), new[] { seed }));
        }

        private static IEnumerable<HistoricalBar> BuildSession(DateTime centralDate, Func<int, decimal> close)
        {
            var central = ResolveCentralTimeZone();
            for (var i = 0; i < 300; i++)
            {
                var local = DateTime.SpecifyKind(centralDate.Date.AddHours(6).AddMinutes(i), DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
                var p = close(i);
                yield return new HistoricalBar("MNQ", "09-26", new DateTimeOffset(utc, TimeSpan.Zero), centralDate.Date, 60, p, p + 0.25m, p - 0.25m, p, 100, HistoricalDataSourceKind.NinjaTraderRepository, "test");
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
