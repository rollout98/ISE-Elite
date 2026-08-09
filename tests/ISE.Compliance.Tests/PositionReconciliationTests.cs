namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance.Safety;

    public class PositionReconciliationTests
    {
        private PositionReconciliation _reconciliation;

        public PositionReconciliationTests()
        {
            _reconciliation = new PositionReconciliation();
        }

        [Fact]
        public void RecordEntry_IncreasesTrackedPosition()
        {
            // Arrange
            int initialPosition = _reconciliation.TrackedPosition;

            // Act
            _reconciliation.RecordEntry(5);

            // Assert
            Assert.Equal(initialPosition + 5, _reconciliation.TrackedPosition);
        }

        [Fact]
        public void RecordExit_DecreasesTrackedPosition()
        {
            // Arrange
            _reconciliation.RecordEntry(5);
            int positionBeforeExit = _reconciliation.TrackedPosition;

            // Act
            _reconciliation.RecordExit(3);

            // Assert
            Assert.Equal(positionBeforeExit - 3, _reconciliation.TrackedPosition);
        }

        [Fact]
        public void UpdateActualPosition_WhenMatched_SetReconciled()
        {
            // Arrange
            _reconciliation.RecordEntry(5);

            // Act
            _reconciliation.UpdateActualPosition(5); // Match tracked

            // Assert
            Assert.True(_reconciliation.IsReconciled);
            Assert.Equal(0, _reconciliation.Discrepancy);
        }

        [Fact]
        public void UpdateActualPosition_WhenMismatched_SetNotReconciled()
        {
            // Arrange
            _reconciliation.RecordEntry(5);

            // Act
            _reconciliation.UpdateActualPosition(3); // Mismatch

            // Assert
            Assert.False(_reconciliation.IsReconciled);
            Assert.Equal(2, _reconciliation.Discrepancy);
        }

        [Fact]
        public void CanTrade_WhenReconciled_ReturnsTrue()
        {
            // Arrange
            _reconciliation.RecordEntry(5);
            _reconciliation.UpdateActualPosition(5);

            // Act & Assert
            Assert.True(_reconciliation.CanTrade);
        }

        [Fact]
        public void CanTrade_WhenNotReconciled_ReturnsFalse()
        {
            // Arrange
            _reconciliation.RecordEntry(5);
            _reconciliation.UpdateActualPosition(3);

            // Act & Assert
            Assert.False(_reconciliation.CanTrade);
        }

        [Fact]
        public void AttemptAutoCorrection_AlignsTrackedToActual()
        {
            // Arrange
            _reconciliation.RecordEntry(5);
            _reconciliation.UpdateActualPosition(3);

            // Act
            _reconciliation.AttemptAutoCorrection();

            // Assert
            Assert.True(_reconciliation.IsReconciled);
            Assert.Equal(3, _reconciliation.TrackedPosition);
        }

        [Fact]
        public void GetHistory_TracksReconciliationEvents()
        {
            // Arrange
            _reconciliation.RecordEntry(5);
            _reconciliation.UpdateActualPosition(5);
            _reconciliation.UpdateActualPosition(4); // Mismatch

            // Act
            var history = _reconciliation.GetHistory();

            // Assert
            Assert.NotEmpty(history);
            Assert.True(history.Count >= 2);
        }
    }
}
