using System;
using System.Collections.Generic;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningFrozenRiskBudgetValidationResearchTests
    {
        [Fact]
        public void FrozenProfilesRemainOneSeventyFiveAndTwoFifty()
        {
            var config = new MorningFrozenRiskBudgetValidationConfig();

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
        public void PostCalibrationWindowIsIndependent()
        {
            var bars = Bars(
                new DateTimeOffset(
                    2026, 8, 3, 13, 0, 0, TimeSpan.Zero),
                3);

            var analyzer =
                new MorningFrozenRiskBudgetValidationAnalyzer();

            Assert.Equal(
                MorningValidationWindowClassification.PostCalibration,
                analyzer.ClassifyWindow(bars));

            Assert.True(analyzer.IsIndependent(bars));
        }

        [Fact]
        public void PreCalibrationWindowIsIndependent()
        {
            var bars = Bars(
                new DateTimeOffset(
                    2026, 2, 2, 13, 0, 0, TimeSpan.Zero),
                3);

            var analyzer =
                new MorningFrozenRiskBudgetValidationAnalyzer();

            Assert.Equal(
                MorningValidationWindowClassification.PreCalibration,
                analyzer.ClassifyWindow(bars));

            Assert.True(analyzer.IsIndependent(bars));
        }

        [Fact]
        public void CalibrationOverlapIsRejected()
        {
            var bars = Bars(
                new DateTimeOffset(
                    2026, 7, 15, 13, 0, 0, TimeSpan.Zero),
                3);

            var analyzer =
                new MorningFrozenRiskBudgetValidationAnalyzer();

            Assert.Equal(
                MorningValidationWindowClassification.OverlapsCalibration,
                analyzer.ClassifyWindow(bars));

            Assert.Throws<InvalidOperationException>(() =>
                analyzer.Validate(
                    bars,
                    Array.Empty<MorningDailySequencingCandidate>(),
                    MorningFrozenRiskBudgetProfileKind.Funded175));
        }

        [Fact]
        public void ValidationDoesNotChangeFrozenEntryThresholds()
        {
            var config = new MorningFrozenRiskBudgetValidationConfig();

            Assert.Equal(70m, config.EntryEfficiencyMinimum);
            Assert.Equal(80m, config.PotentialMinimum);
            Assert.Equal(2, config.MaximumAttempts);
            Assert.Equal(2, config.MaximumContracts);
        }

        private static IReadOnlyList<HistoricalBar> Bars(
            DateTimeOffset start,
            int sessionCount)
        {
            var result = new List<HistoricalBar>();

            for (var i = 0; i < sessionCount; i++)
            {
                var timestamp = start.AddDays(i);

                result.Add(new HistoricalBar(
                    "MNQ",
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
                    "v7-8-validation-test"));
            }

            return result;
        }
    }
}
