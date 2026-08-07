using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class NewYorkRegimeResearchTests
    {
        [Fact]
        public void Extractor_AllowsExplicitContractRolloverAcrossSessions()
        {
            var bars = new List<HistoricalBar>();
            bars.AddRange(BuildSession(new DateTime(2026, 6, 12), "06-26", i => 100m + i * 0.01m));
            bars.AddRange(BuildSession(new DateTime(2026, 6, 15), "09-26", i => 200m + i * 0.01m));

            var result = new NewYorkSessionResearchFeatureExtractor().Extract(bars);

            Assert.Equal(2, result.Count);
            Assert.Equal(new DateTime(2026, 6, 12), result[0].SessionDateCentral);
            Assert.Equal(new DateTime(2026, 6, 15), result[1].SessionDateCentral);
            Assert.All(result, x => Assert.Equal(300, x.BarCount));
        }

        [Fact]
        public void Classifier_IdentifiesOpeningDrive()
        {
            var f = Features(pre: 20m, openingRange: 16m, openingDisplacement: 12m, openingEfficiency: 0.75m, earlyDisplacement: 1m, adverse: 2m, laterDisplacement: 4m, coreRange: 22m, coreDisplacement: 15m, coreEfficiency: 0.68m);
            var result = new NewYorkRegimeSeedClassifier().Classify(f);
            Assert.Equal(NewYorkResearchRegime.OpeningDrive, result.Regime);
            Assert.True(result.Score > 0m);
        }

        [Fact]
        public void Classifier_IdentifiesEarlyReversal()
        {
            var f = Features(pre: 20m, openingRange: 12m, openingDisplacement: 7m, openingEfficiency: 0.58m, earlyDisplacement: -8m, adverse: 8m, laterDisplacement: -2m, coreRange: 20m, coreDisplacement: -4m, coreEfficiency: 0.20m);
            var result = new NewYorkRegimeSeedClassifier().Classify(f);
            Assert.Equal(NewYorkResearchRegime.EarlyReversal, result.Regime);
        }

        [Fact]
        public void Classifier_IdentifiesDeepPullbackContinuation()
        {
            var f = Features(pre: 20m, openingRange: 14m, openingDisplacement: 7m, openingEfficiency: 0.50m, earlyDisplacement: -3m, adverse: 8m, laterDisplacement: 7m, coreRange: 24m, coreDisplacement: 10m, coreEfficiency: 0.42m);
            var result = new NewYorkRegimeSeedClassifier().Classify(f);
            Assert.Equal(NewYorkResearchRegime.DeepPullbackContinuation, result.Regime);
        }

        [Fact]
        public void Classifier_IdentifiesVolatileTwoSidedAuction()
        {
            var f = Features(pre: 16m, openingRange: 18m, openingDisplacement: 3m, openingEfficiency: 0.17m, earlyDisplacement: -2m, adverse: 7m, laterDisplacement: 2m, coreRange: 24m, coreDisplacement: 2m, coreEfficiency: 0.08m);
            var result = new NewYorkRegimeSeedClassifier().Classify(f);
            Assert.Equal(NewYorkResearchRegime.VolatileTwoSidedAuction, result.Regime);
        }

        [Fact]
        public void Classifier_IdentifiesRangeNoTrade()
        {
            var f = Features(pre: 20m, openingRange: 8m, openingDisplacement: 2m, openingEfficiency: 0.25m, earlyDisplacement: -1m, adverse: 2m, laterDisplacement: 1m, coreRange: 14m, coreDisplacement: 2m, coreEfficiency: 0.14m);
            var result = new NewYorkRegimeSeedClassifier().Classify(f);
            Assert.Equal(NewYorkResearchRegime.RangeNoTrade, result.Regime);
        }

        [Fact]
        public void Classifier_IdentifiesLaterContinuationOrReversal()
        {
            var f = Features(pre: 20m, openingRange: 10m, openingDisplacement: 4m, openingEfficiency: 0.40m, earlyDisplacement: 1m, adverse: 2m, laterDisplacement: -6m, coreRange: 20m, coreDisplacement: -1m, coreEfficiency: 0.05m);
            var result = new NewYorkRegimeSeedClassifier().Classify(f);
            Assert.Equal(NewYorkResearchRegime.LaterContinuationReversal, result.Regime);
        }

        [Fact]
        public void Labeler_DoesNotCreateTradeCandidateForRangeNoTrade()
        {
            var f = Features(pre: 20m, openingRange: 8m, openingDisplacement: 2m, openingEfficiency: 0.25m, earlyDisplacement: -1m, adverse: 2m, laterDisplacement: 1m, coreRange: 14m, coreDisplacement: 2m, coreEfficiency: 0.14m);
            var classification = new NewYorkRegimeSeedClassifier().Classify(f);
            var labels = new NewYorkOpportunitySeedLabeler().Label(classification);
            Assert.Empty(labels);
        }

        [Fact]
        public void Labeler_CreatesDirectionalSeedForEarlyReversal()
        {
            var f = Features(pre: 20m, openingRange: 12m, openingDisplacement: 7m, openingEfficiency: 0.58m, earlyDisplacement: -8m, adverse: 8m, laterDisplacement: -2m, coreRange: 20m, coreDisplacement: -4m, coreEfficiency: 0.20m);
            var classification = new NewYorkRegimeSeedClassifier().Classify(f);
            var labels = new NewYorkOpportunitySeedLabeler().Label(classification);
            var label = Assert.Single(labels);
            Assert.Equal(NewYorkOpportunitySeedType.EarlyReversal, label.Type);
            Assert.Equal(NewYorkResearchDirection.Short, label.Direction);
            Assert.Equal(new TimeSpan(8, 45, 0), label.WindowStartCentral);
        }

        private static NewYorkSessionResearchFeatures Features(decimal pre, decimal openingRange, decimal openingDisplacement, decimal openingEfficiency, decimal earlyDisplacement, decimal adverse, decimal laterDisplacement, decimal coreRange, decimal coreDisplacement, decimal coreEfficiency)
        {
            return new NewYorkSessionResearchFeatures(new DateTime(2026, 7, 1), "MNQ", 60, 300, pre, openingRange, openingDisplacement, openingEfficiency, earlyDisplacement, adverse, laterDisplacement, coreRange, coreDisplacement, coreEfficiency);
        }

        private static IEnumerable<HistoricalBar> BuildSession(DateTime centralDate, string contract, Func<int, decimal> price)
        {
            var central = ResolveCentralTimeZone();
            for (var i = 0; i < 300; i++)
            {
                var local = DateTime.SpecifyKind(centralDate.Date.AddHours(6).AddMinutes(i), DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
                var p = price(i);
                yield return new HistoricalBar("MNQ", contract, new DateTimeOffset(utc, TimeSpan.Zero), centralDate.Date, 60, p, p + 0.25m, p - 0.25m, p, 100, HistoricalDataSourceKind.NinjaTraderRepository, "test");
            }
        }

        private static TimeZoneInfo ResolveCentralTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("America/Chicago"); }
        }
    }
}
