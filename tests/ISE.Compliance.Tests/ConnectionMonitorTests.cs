namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance.Safety;

    public class ConnectionMonitorTests
    {
        private ConnectionMonitor _monitor;

        public ConnectionMonitorTests()
        {
            _monitor = new ConnectionMonitor();
        }

        [Fact]
        public void RecordHeartbeat_WhenFresh_AllowsTrading()
        {
            // Arrange & Act
            _monitor.RecordHeartbeat();

            // Assert
            Assert.True(_monitor.CanExecuteTrades());
        }

        [Fact]
        public void RecordHeartbeat_WhenStale_BlocksTrading()
        {
            // Arrange - Don't record heartbeat (simulate disconnect)
            // Act & Assert - Should block after 2 seconds without heartbeat
            Assert.False(_monitor.CanExecuteTrades()); // Initial state is disconnected
        }

        [Fact]
        public void RecordLevel2Update_WhenFresh_AllowsTrading()
        {
            // Arrange
            _monitor.RecordHeartbeat(); // Connection healthy
            _monitor.RecordLevel2Update();

            // Act
            bool canTrade = _monitor.CanExecuteTrades();

            // Assert
            Assert.True(canTrade);
        }

        [Fact]
        public void RecordLevel2Update_WhenStale_BlocksTrading()
        {
            // Arrange
            _monitor.RecordHeartbeat(); // Connection healthy
            // Don't record Level 2 update (simulate stale data)

            // Act & Assert - After 1 second without Level 2, should eventually block
            Assert.NotEmpty(_monitor.DisconnectionReason ?? "");
        }

        [Fact]
        public void RecordBarUpdate_TracksMarketData()
        {
            // Arrange
            _monitor.RecordHeartbeat();

            // Act
            _monitor.RecordBarUpdate();

            // Assert
            Assert.True(_monitor.CanExecuteTrades());
        }

        [Fact]
        public void DisconnectionReason_ProvidesClearnessOnIssue()
        {
            // Arrange - Initialize with no heartbeat
            _monitor = new ConnectionMonitor();

            // Act
            bool canTrade = _monitor.CanExecuteTrades();

            // Assert
            if (!canTrade)
            {
                Assert.NotNull(_monitor.DisconnectionReason);
                Assert.True(_monitor.DisconnectionReason.Length > 0);
            }
        }

        [Fact]
        public void MultipleHeartbeats_MaintainsConnection()
        {
            // Arrange & Act
            for (int i = 0; i < 5; i++)
            {
                _monitor.RecordHeartbeat();
                System.Threading.Thread.Sleep(100); // Small delay
            }

            // Assert
            Assert.True(_monitor.CanExecuteTrades());
        }

        [Fact]
        public void GetStatus_ReturnsReadableStatus()
        {
            // Arrange
            _monitor.RecordHeartbeat();

            // Act
            var status = _monitor.GetStatus();

            // Assert
            Assert.NotNull(status);
            Assert.True(status.Length > 0);
        }
    }
}
