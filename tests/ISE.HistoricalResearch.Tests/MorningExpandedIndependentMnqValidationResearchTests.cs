using System;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class MorningExpandedIndependentMnqValidationResearchTests
    {
        [Fact]
        public void FrozenProfilesRemainOneSeventyFiveAndTwoFifty()
        {
            var config =
                new MorningExpandedIndependentMnqValidationConfig();

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
        public void PreCalibrationWindowEndsBeforeCalibrationStarts()
        {
            var config =
                new MorningExpandedIndependentMnqValidationConfig();

            Assert.Equal(
                new DateTime(2025, 12, 1),
                config.PreCalibrationEvaluationStartCentral);

            Assert.Equal(
                new DateTime(2026, 3, 25),
                config.PreCalibrationEvaluationEndExclusiveCentral);
        }

        [Fact]
        public void PostCalibrationWindowStartsAugustFirst()
        {
            var config =
                new MorningExpandedIndependentMnqValidationConfig();

            Assert.Equal(
                new DateTime(2026, 8, 1),
                config.PostCalibrationEvaluationStartCentral);
        }

        [Fact]
        public void FrozenSignalThresholdsRemainUnchanged()
        {
            var config =
                new MorningExpandedIndependentMnqValidationConfig();

            Assert.Equal(70m, config.EntryEfficiencyMinimum);
            Assert.Equal(80m, config.PotentialMinimum);
            Assert.Equal(2, config.MaximumAttempts);
        }
    }
}
