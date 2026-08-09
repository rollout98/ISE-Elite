namespace ISE.Compliance.Tests
{
    using Xunit;
    using ISE.Compliance.Safety;

    public class NewsEventFilterTests
    {
        private NewsEventFilter _filter;

        public NewsEventFilterTests()
        {
            _filter = new NewsEventFilter();
        }

        [Fact]
        public void RegisterMajorEvent_RecordsEvent()
        {
            // Arrange
            var eventTime = DateTime.Now.AddHours(1);

            // Act
            _filter.RegisterMajorEvent("FOMC Decision", eventTime);

            // Assert
            var events = _filter.GetAllEvents();
            Assert.NotEmpty(events);
        }

        [Fact]
        public void RegisterMinorEvent_RecordsEvent()
        {
            // Arrange
            var eventTime = DateTime.Now.AddHours(1);

            // Act
            _filter.RegisterMinorEvent("Retail Sales", eventTime);

            // Assert
            var events = _filter.GetAllEvents();
            Assert.NotEmpty(events);
        }

        [Fact]
        public void CheckBlackoutStatus_WhenInMajorBlackout_ReturnsFalse()
        {
            // Arrange
            var eventTime = DateTime.Now.AddMinutes(5); // 5 min from now
            _filter.RegisterMajorEvent("Fed Announcement", eventTime);

            // Act
            bool canTrade = _filter.CheckBlackoutStatus(DateTime.Now);

            // Assert
            Assert.False(canTrade); // In blackout (±30 min for major)
        }

        [Fact]
        public void CheckBlackoutStatus_WhenOutsideBlackout_ReturnsTrue()
        {
            // Arrange
            var eventTime = DateTime.Now.AddHours(2); // 2 hours away
            _filter.RegisterMajorEvent("Fed Announcement", eventTime);

            // Act
            bool canTrade = _filter.CheckBlackoutStatus(DateTime.Now);

            // Assert
            Assert.True(canTrade); // Outside blackout window
        }

        [Fact]
        public void MinorEvent_HasShorterBlackout()
        {
            // Arrange
            var eventTime = DateTime.Now.AddMinutes(20); // 20 min from now
            _filter.RegisterMinorEvent("Retail Sales", eventTime);

            // Act
            bool canTrade = _filter.CheckBlackoutStatus(DateTime.Now);

            // Assert
            // Minor events have ±15 min blackout, so 20 min away should be OK
            Assert.True(canTrade);
        }

        [Fact]
        public void GetUpcomingEvents_ListsFutureEvents()
        {
            // Arrange
            _filter.RegisterMajorEvent("FOMC 1", DateTime.Now.AddHours(1));
            _filter.RegisterMinorEvent("Retail", DateTime.Now.AddHours(2));
            _filter.RegisterMajorEvent("FOMC 2", DateTime.Now.AddDays(2));

            // Act
            var upcoming = _filter.GetUpcomingEvents(DateTime.Now, 3);

            // Assert
            Assert.NotEmpty(upcoming);
            Assert.Equal(3, upcoming.Count);
        }

        [Fact]
        public void GetEventsForDate_FiltersCorrectly()
        {
            // Arrange
            var date = DateTime.Now.Date;
            _filter.RegisterMajorEvent("Event 1", date.AddHours(9));
            _filter.RegisterMajorEvent("Event 2", date.AddHours(14));
            _filter.RegisterMajorEvent("Event 3", date.AddDays(1).AddHours(9));

            // Act
            var todayEvents = _filter.GetEventsForDate(date);

            // Assert
            Assert.Equal(2, todayEvents.Count);
        }

        [Fact]
        public void RemoveEvent_DeletesEvent()
        {
            // Arrange
            _filter.RegisterMajorEvent("Test Event", DateTime.Now.AddHours(1));
            var before = _filter.GetAllEvents().Count;

            // Act
            _filter.RemoveEvent("Test Event");
            var after = _filter.GetAllEvents().Count;

            // Assert
            Assert.Equal(before - 1, after);
        }

        [Fact]
        public void BlackoutReason_ProvidesClearnessOnTiming()
        {
            // Arrange
            var eventTime = DateTime.Now.AddMinutes(5);
            _filter.RegisterMajorEvent("Test Event", eventTime);
            _filter.CheckBlackoutStatus(DateTime.Now);

            // Act & Assert
            if (_filter.IsInBlackout)
            {
                Assert.NotNull(_filter.BlackoutReason);
                Assert.True(_filter.BlackoutReason.Length > 0);
            }
        }
    }
}
