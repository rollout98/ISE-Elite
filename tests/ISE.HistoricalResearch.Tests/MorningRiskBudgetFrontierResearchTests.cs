using System;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningRiskBudgetFrontierResearchTests
    {
        [Theory]
        [InlineData(300, 150, 1)]
        [InlineData(300, 175, 1)]
        [InlineData(300, 300, 2)]
        [InlineData(350, 150, 0)]
        [InlineData(350, 175, 1)]
        [InlineData(350, 350, 2)]
        public void QuantityChangesOnlyWhenBudgetCanFundStructuralRisk(
            double riskTicks,
            double budget,
            int expectedQuantity)
        {
            var analyzer = new MorningRiskBudgetFrontierAnalyzer(
                dollarsPerTickPerContract: 0.50m,
                maximumContracts: 2);

            var quantity = analyzer.ResolveQuantity(
                (decimal)riskTicks,
                (decimal)budget);

            Assert.Equal(expectedQuantity, quantity);
        }

        [Fact]
        public void QuantityCannotDecreaseWhenBudgetIncreases()
        {
            var analyzer = new MorningRiskBudgetFrontierAnalyzer();

            var q150 = analyzer.ResolveQuantity(410m, 150m);
            var q175 = analyzer.ResolveQuantity(410m, 175m);
            var q225 = analyzer.ResolveQuantity(410m, 225m);
            var q300 = analyzer.ResolveQuantity(410m, 300m);

            Assert.True(q175 >= q150);
            Assert.True(q225 >= q175);
            Assert.True(q300 >= q225);
        }

        [Fact]
        public void EmptyBudgetSetIsRejected()
        {
            var analyzer = new MorningRiskBudgetFrontierAnalyzer();

            Assert.Throws<ArgumentException>(() =>
                analyzer.Analyze(
                    Array.Empty<HistoricalBar>(),
                    Array.Empty<MorningDailySequencingCandidate>(),
                    Array.Empty<decimal>()));
        }

        [Fact]
        public void DuplicateBudgetsCollapseToOneFrontierPoint()
        {
            var analyzer = new MorningRiskBudgetFrontierAnalyzer();

            var result = analyzer.Analyze(
                Array.Empty<HistoricalBar>(),
                Array.Empty<MorningDailySequencingCandidate>(),
                new[] { 150m, 150m, 175m });

            Assert.Equal(2, result.Count);
            Assert.Equal(150m, result[0].RiskBudgetDollars);
            Assert.Equal(175m, result[1].RiskBudgetDollars);
        }
    }
}
