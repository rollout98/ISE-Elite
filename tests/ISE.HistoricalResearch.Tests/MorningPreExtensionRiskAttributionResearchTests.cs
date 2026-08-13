using System;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningPreExtensionRiskAttributionResearchTests
    {
        [Theory]
        [InlineData(75, 2)]
        [InlineData(150, 2)]
        [InlineData(151, 1)]
        [InlineData(300, 1)]
        [InlineData(301, 0)]
        public void MaximumContractsRespectsOneHundredFiftyDollarObjective(
            double riskTicks,
            int expectedContracts)
        {
            var actual = MorningPreExtensionRiskAttributionAnalyzer
                .MaximumContractsWithinRisk(
                    (decimal)riskTicks,
                    riskObjectiveDollars: 150m,
                    dollarsPerTickPerContract: 0.50m,
                    maximumContracts: 2);

            Assert.Equal(expectedContracts, actual);
        }

        [Theory]
        [InlineData(100, "<=100")]
        [InlineData(101, "101-150")]
        [InlineData(150, "101-150")]
        [InlineData(151, "151-200")]
        [InlineData(200, "151-200")]
        [InlineData(201, "201-300")]
        [InlineData(300, "201-300")]
        [InlineData(301, "300+")]
        public void RiskBandUsesDiagnosticBoundaries(
            double riskTicks,
            string expected)
        {
            Assert.Equal(
                expected,
                MorningPreExtensionRiskAttributionAnalyzer.RiskBand((decimal)riskTicks));
        }

        [Theory]
        [InlineData(10, 0, "03:00-05:59")]
        [InlineData(12, 0, "06:00-08:29")]
        [InlineData(14, 0, "08:30-09:29")]
        [InlineData(15, 30, "09:30-10:59")]
        public void EntryTimeSegmentUsesCentralClock(
            int hourUtc,
            int minuteUtc,
            string expected)
        {
            var utc = new DateTimeOffset(
                2026, 7, 15, hourUtc, minuteUtc, 0, TimeSpan.Zero);

            Assert.Equal(
                expected,
                MorningPreExtensionRiskAttributionAnalyzer.EntryTimeSegmentCentral(utc));
        }
    }
}
