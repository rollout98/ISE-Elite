using System;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningOpportunityQualityDiagnosticsTests
    {
        [Fact]
        public void DiagnosticsBuildFiveBucketsAndPreserveScore()
        {
            var row = Observation(90m, 500m, 120m, 100m, 200m);
            var buckets = new MorningOpportunityQualityDiagnosticsAnalyzer().BuildBuckets(new[] { row });
            Assert.Equal(5, buckets.Count);
            Assert.Equal(1, buckets[4].Count);
            Assert.Equal(1, buckets[4].Hit500);
            Assert.Equal(90m, row.PotentialScore);
        }

        [Fact]
        public void DiagnosticsExposeEfficiencyRatios()
        {
            var row = Observation(90m, 400m, 100m, 80m, 200m);
            var bucket = new MorningOpportunityQualityDiagnosticsAnalyzer().BuildBuckets(new[] { row })[4];
            Assert.Equal(4m, bucket.AverageMfeMaeRatio);
            Assert.Equal(5m, bucket.AverageMfeRiskRatio);
        }

        [Fact]
        public void DiagnosticsExposeContextDimensions()
        {
            var row = Observation(72m, 300m, 60m, 100m, 350m);
            var dimensions = new MorningOpportunityQualityDiagnosticsAnalyzer().BuildDimensions(new[] { row });
            Assert.Contains(dimensions, x => x.Dimension == "direction" && x.Value == "Long");
            Assert.Contains(dimensions, x => x.Dimension == "state" && x.Value == "Trending");
            Assert.Contains(dimensions, x => x.Dimension == "setup" && x.Value == "TrendContinuation");
            Assert.Contains(dimensions, x => x.Dimension == "hourCT" && x.Value == "08");
        }

        private static MorningOpportunityPotentialObservation Observation(decimal score, decimal mfe, decimal mae, decimal risk, decimal realized)
        {
            var central = ResolveCentralTimeZone();
            var local = DateTime.SpecifyKind(new DateTime(2026, 7, 1, 8, 0, 0), DateTimeKind.Unspecified);
            var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, central), TimeSpan.Zero);
            var source = new MorningAdaptiveTradeOutcome(new DateTime(2026, 7, 1), MorningMarketState.Trending,
                MorningAdaptiveSetupType.TrendContinuation, NewYorkResearchDirection.Long, utc.AddMinutes(-1), utc,
                100m, 99m, risk, 0.45m, 10m, MorningAdaptiveManagementMode.Core,
                MorningAdaptiveExitReason.CoreCapture, utc.AddMinutes(10), 101m, realized, realized, mfe, mae);
            var features = new MorningOpportunityPotentialFeatures(8, 80m, 0.40m, 0.50m, 0.10m, 0.30m, 2, 1.5m, 1.2m, 0.10m);
            return new MorningOpportunityPotentialObservation(source, features, score);
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
