using System;
using System.Collections.Generic;
using System.Linq;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class RangeEntryVectorFlowHoldResearchTests
    {
        [Fact]
        public void RangeFilterSignalsCreateCausalNextMinuteEntries()
        {
            var bars = BuildReversalSession(new DateTime(2026, 7, 27));
            var outcomes = new RangeEntryVectorFlowHoldAnalyzer(TestConfig()).Analyze(bars);

            Assert.NotEmpty(outcomes);
            Assert.All(outcomes, x => Assert.True(x.EntryUtc > x.SignalUtc));
            Assert.All(outcomes, x => Assert.True(x.Direction != RangeFilterResearchDirection.None));
            Assert.All(outcomes, x => Assert.True(x.InitialRiskTicks > 0m));
        }

        [Fact]
        public void VectorFlowNeverCreatesAnEntryWithoutARangeFilterFlip()
        {
            var bars = BuildOneWaySession(new DateTime(2026, 7, 28));
            var outcomes = new RangeEntryVectorFlowHoldAnalyzer(TestConfig()).Analyze(bars);

            Assert.True(outcomes.Count <= 1);
        }

        [Fact]
        public void AlignedVectorFlowCanExtendTheSameRangeEntryBeyondScalpControl()
        {
            var bars = BuildReversalSession(new DateTime(2026, 7, 29));
            var outcomes = new RangeEntryVectorFlowHoldAnalyzer(TestConfig()).Analyze(bars);
            var extended = outcomes.FirstOrDefault(x => x.AlignedAtEntry || x.AlignedBeforeScalpExit);

            Assert.NotNull(extended);
            Assert.True(extended!.VectorFlowHold.ExitUtc >= extended.RangeOnlyControl.ExitUtc);
            Assert.True(extended.VectorFlowHold.FinalMode == RangeVectorManagementMode.Core
                || extended.VectorFlowHold.FinalMode == RangeVectorManagementMode.Runner
                || extended.VectorFlowHold.ExitReason == RangeVectorExitReason.VectorFlowBiasLoss);
        }

        private static RangeEntryVectorFlowHoldConfig TestConfig()
        {
            return new RangeEntryVectorFlowHoldConfig(
                rangeTimeframeMinutes: 3,
                vectorTimeframeMinutes: 5,
                rangeSamplingPeriod: 3,
                rangeMultiplier: 0.8m,
                ftcLength: 3,
                ftcAtrLength: 2,
                ftcAtrHighestLookback: 2,
                vidyaLength: 3,
                vidyaMomentum: 2,
                vidyaSmoothingLength: 1,
                vidyaAtrLength: 2,
                vidyaBandDistance: 0.5m,
                structureLookbackRangeBars: 3,
                scalpTargetTicks: 20,
                scalpTimeoutMinutes: 30,
                runnerThresholdTicks: 30,
                runnerAlignedBars: 1);
        }

        private static List<HistoricalBar> BuildReversalSession(DateTime tradingDay)
        {
            var prices = new List<decimal>();
            decimal p = 100m;
            for (var i = 0; i < 900; i++)
            {
                if (i < 620) p -= 0.05m;
                else if (i < 660) p -= 0.20m;
                else p += 0.30m;
                prices.Add(p);
            }
            return Build(tradingDay, prices);
        }

        private static List<HistoricalBar> BuildOneWaySession(DateTime tradingDay)
        {
            var prices = new List<decimal>();
            decimal p = 100m;
            for (var i = 0; i < 900; i++)
            {
                p += 0.08m;
                prices.Add(p);
            }
            return Build(tradingDay, prices);
        }

        private static List<HistoricalBar> Build(DateTime tradingDay, IReadOnlyList<decimal> closes)
        {
            var result = new List<HistoricalBar>();
            var central = ResolveCentralTimeZone();
            var startLocal = tradingDay.Date.AddDays(-1).AddHours(17);
            decimal previous = closes[0];
            for (var i = 0; i < closes.Count; i++)
            {
                var close = closes[i];
                var open = i == 0 ? close : previous;
                var high = Math.Max(open, close) + 0.05m;
                var low = Math.Min(open, close) - 0.05m;
                var local = DateTime.SpecifyKind(startLocal.AddMinutes(i), DateTimeKind.Unspecified);
                var utc = TimeZoneInfo.ConvertTimeToUtc(local, central);
                result.Add(new HistoricalBar("MNQ", "09-26", new DateTimeOffset(utc, TimeSpan.Zero), tradingDay.Date, 60,
                    open, high, low, close, 100, HistoricalDataSourceKind.NinjaTraderRepository, "test"));
                previous = close;
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
