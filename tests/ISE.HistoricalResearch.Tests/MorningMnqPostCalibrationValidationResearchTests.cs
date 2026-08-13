using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningMnqPostCalibrationValidationResearchTests
    {
        [Fact]
        public void FrozenBudgetsRemainFunded175AndCombine250()
        {
            var config =
                new MorningMnqPostCalibrationValidationConfig();

            Assert.Equal(
                175m,
                config.BudgetFor(
                    MorningFrozenRiskBudgetProfileKind.Funded175));

            Assert.Equal(
                250m,
                config.BudgetFor(
                    MorningFrozenRiskBudgetProfileKind.Combine250));
        }

        [Fact]
        public void MnqGuardRejectsMgc()
        {
            var analyzer =
                new MorningMnqPostCalibrationValidationAnalyzer();

            var bars = new[]
            {
                Bar(
                    "MGC",
                    new DateTimeOffset(
                        2026, 8, 3, 13, 0, 0, TimeSpan.Zero))
            };

            Assert.Throws<InvalidOperationException>(() =>
                analyzer.RequireMnq(bars));
        }

        [Fact]
        public void MnqGuardAllowsPreCalibrationWarmupBars()
        {
            var analyzer =
                new MorningMnqPostCalibrationValidationAnalyzer();

            var bars = new[]
            {
                Bar(
                    "MNQ",
                    new DateTimeOffset(
                        2026, 6, 8, 13, 0, 0, TimeSpan.Zero)),
                Bar(
                    "MNQ",
                    new DateTimeOffset(
                        2026, 8, 3, 13, 0, 0, TimeSpan.Zero))
            };

            analyzer.RequireMnq(bars);
        }

        [Fact]
        public void EvaluationStartIsAugustFirstWithoutChangingFrozenThresholds()
        {
            var config =
                new MorningMnqPostCalibrationValidationConfig();

            Assert.Equal(
                new DateTime(2026, 8, 1),
                config.EvaluationStartCentral);

            Assert.Equal(70m, config.EntryEfficiencyMinimum);
            Assert.Equal(80m, config.PotentialMinimum);
            Assert.Equal(2, config.MaximumAttempts);
        }

        private static HistoricalBar Bar(
            string instrument,
            DateTimeOffset timestamp)
        {
            return new HistoricalBar(
                instrument,
                "09-26",
                timestamp,
                timestamp.UtcDateTime.Date,
                60,
                100m,
                101m,
                99m,
                100m,
                1000L,
                HistoricalDataSourceKind.ImportedFile,
                "v7-8-1-mnq-validation-test");
        }
    }
}
