using System;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.HistoricalResearch.Tests
{
    public sealed class HistoricalDatasetPlanTests
    {
        [Fact]
        public void ResolveReturnsExpectedPartitionForBoundaryDates()
        {
            var plan = new HistoricalDatasetPlan(new[]
            {
                new HistoricalDatasetPartition(HistoricalPartitionKind.Development, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)),
                new HistoricalDatasetPartition(HistoricalPartitionKind.Validation, new DateTime(2025, 1, 1), new DateTime(2025, 6, 30)),
                new HistoricalDatasetPartition(HistoricalPartitionKind.OutOfSample, new DateTime(2025, 7, 1), new DateTime(2025, 12, 31))
            });

            Assert.Equal(HistoricalPartitionKind.Development, plan.Resolve(new DateTime(2024, 12, 31)));
            Assert.Equal(HistoricalPartitionKind.Validation, plan.Resolve(new DateTime(2025, 1, 1)));
            Assert.Equal(HistoricalPartitionKind.OutOfSample, plan.Resolve(new DateTime(2025, 12, 31)));
        }

        [Fact]
        public void ResolveReturnsNullOutsideConfiguredHistory()
        {
            var plan = new HistoricalDatasetPlan(new[]
            {
                new HistoricalDatasetPartition(HistoricalPartitionKind.Development, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31))
            });

            Assert.Null(plan.Resolve(new DateTime(2023, 12, 31)));
            Assert.Null(plan.Resolve(new DateTime(2025, 1, 1)));
        }

        [Fact]
        public void ConstructorRejectsOverlappingPartitions()
        {
            var exception = Assert.Throws<ArgumentException>(() => new HistoricalDatasetPlan(new[]
            {
                new HistoricalDatasetPartition(HistoricalPartitionKind.Development, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)),
                new HistoricalDatasetPartition(HistoricalPartitionKind.Validation, new DateTime(2024, 12, 31), new DateTime(2025, 6, 30))
            }));

            Assert.Contains("must not overlap", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PartitionRejectsInvertedDates()
        {
            Assert.Throws<ArgumentException>(() => new HistoricalDatasetPartition(
                HistoricalPartitionKind.Development,
                new DateTime(2025, 1, 2),
                new DateTime(2025, 1, 1)));
        }
    }
}
