namespace ISE.Compliance.Safety
{
    /// <summary>
    /// NewsEventFilter - Prevent trading around major economic announcements
    /// Typical blackout: ±15 minutes for minor events, ±30 minutes for major events (Fed, CPI, Jobs)
    /// </summary>
    public class NewsEventFilter
    {
        private List<NewsEvent> _scheduledEvents = new();
        private const int MinorEventBlackoutMinutes = 15;  // ±15 min
        private const int MajorEventBlackoutMinutes = 30;  // ±30 min

        /// <summary>
        /// Is current time in a news blackout?
        /// </summary>
        public bool IsInBlackout { get; private set; } = false;

        /// <summary>
        /// Reason for blackout
        /// </summary>
        public string? BlackoutReason { get; private set; }

        /// <summary>
        /// Time until next event
        /// </summary>
        public TimeSpan? TimeToNextEvent { get; private set; }

        /// <summary>
        /// Register a major event (Fed, CPI, Jobs report, etc)
        /// </summary>
        public void RegisterMajorEvent(string eventName, DateTime eventTime)
        {
            _scheduledEvents.Add(new NewsEvent
            {
                Name = eventName,
                EventTime = eventTime,
                IsMajor = true
            });

            SortEvents();
        }

        /// <summary>
        /// Register a minor event (inflation, consumer sentiment, etc)
        /// </summary>
        public void RegisterMinorEvent(string eventName, DateTime eventTime)
        {
            _scheduledEvents.Add(new NewsEvent
            {
                Name = eventName,
                EventTime = eventTime,
                IsMajor = false
            });

            SortEvents();
        }

        /// <summary>
        /// Check if current time is in a news blackout
        /// </summary>
        public bool CheckBlackoutStatus(DateTime currentTime)
        {
            IsInBlackout = false;
            BlackoutReason = null;
            TimeToNextEvent = null;

            // Find active events (within blackout window)
            foreach (var evt in _scheduledEvents)
            {
                int blackoutMinutes = evt.IsMajor ? MajorEventBlackoutMinutes : MinorEventBlackoutMinutes;
                var blackoutStart = evt.EventTime.AddMinutes(-blackoutMinutes);
                var blackoutEnd = evt.EventTime.AddMinutes(blackoutMinutes);

                // Check if current time is within blackout window
                if (currentTime >= blackoutStart && currentTime <= blackoutEnd)
                {
                    IsInBlackout = true;
                    BlackoutReason = $"{evt.Name} event at {evt.EventTime:HH:mm} " +
                                   $"({evt.GetMinutesUntil(currentTime):+0;-#} min)";
                    return true;
                }

                // Calculate time to next event
                if (currentTime < blackoutStart)
                {
                    TimeToNextEvent = blackoutStart - currentTime;
                    break; // Found first future event
                }
            }

            return IsInBlackout;
        }

        /// <summary>
        /// Can we trade right now?
        /// </summary>
        public bool CanTrade()
        {
            CheckBlackoutStatus(DateTime.Now);
            return !IsInBlackout;
        }

        /// <summary>
        /// Get upcoming events
        /// </summary>
        public List<NewsEvent> GetUpcomingEvents(DateTime currentTime, int daysAhead = 3)
        {
            var cutoff = currentTime.AddDays(daysAhead);
            return _scheduledEvents
                .Where(e => e.EventTime >= currentTime && e.EventTime <= cutoff)
                .OrderBy(e => e.EventTime)
                .ToList();
        }

        /// <summary>
        /// Get events for a specific day
        /// </summary>
        public List<NewsEvent> GetEventsForDate(DateTime date)
        {
            return _scheduledEvents
                .Where(e => e.EventTime.Date == date.Date)
                .OrderBy(e => e.EventTime)
                .ToList();
        }

        /// <summary>
        /// Load standard US economic calendar
        /// </summary>
        public void LoadStandardUSCalendar(DateTime startDate, DateTime endDate)
        {
            // This would load from external source (e.g., database, API)
            // For now, structure is in place
            // Example: Fed meetings, FOMC statements (Major)
            // CPI, Jobs report (Major)
            // Consumer sentiment, retail sales (Minor)
        }

        /// <summary>
        /// Clear all events
        /// </summary>
        public void ClearEvents()
        {
            _scheduledEvents.Clear();
        }

        /// <summary>
        /// Sort events by time
        /// </summary>
        private void SortEvents()
        {
            _scheduledEvents = _scheduledEvents.OrderBy(e => e.EventTime).ToList();
        }

        /// <summary>
        /// Get detailed status
        /// </summary>
        public string GetStatus()
        {
            var upcomingCount = _scheduledEvents.Count(e => e.EventTime > DateTime.Now);
            return $"Blackout: {(IsInBlackout ? "YES" : "NO")} | " +
                   $"Upcoming Events: {upcomingCount} | " +
                   (BlackoutReason != null ? $"Reason: {BlackoutReason}" : "");
        }

        /// <summary>
        /// Get scheduled events
        /// </summary>
        public List<NewsEvent> GetAllEvents()
        {
            return _scheduledEvents.ToList();
        }

        /// <summary>
        /// Remove an event
        /// </summary>
        public void RemoveEvent(string eventName)
        {
            _scheduledEvents.RemoveAll(e => e.Name == eventName);
        }

        /// <summary>
        /// Reset for new session
        /// </summary>
        public void Reset()
        {
            _scheduledEvents.Clear();
            IsInBlackout = false;
            BlackoutReason = null;
            TimeToNextEvent = null;
        }

        public override string ToString()
        {
            return $"News Filter: Blackout={IsInBlackout} | Events={_scheduledEvents.Count}";
        }
    }

    /// <summary>
    /// Economic news event
    /// </summary>
    public class NewsEvent
    {
        public string Name { get; set; } = "";
        public DateTime EventTime { get; set; }
        public bool IsMajor { get; set; }

        /// <summary>
        /// Minutes until/since event (negative = already happened)
        /// </summary>
        public int GetMinutesUntil(DateTime currentTime)
        {
            return (int)(EventTime - currentTime).TotalMinutes;
        }

        /// <summary>
        /// Get blackout window for this event
        /// </summary>
        public (DateTime start, DateTime end) GetBlackoutWindow()
        {
            int minutes = IsMajor ? 30 : 15;
            return (
                EventTime.AddMinutes(-minutes),
                EventTime.AddMinutes(minutes)
            );
        }

        public override string ToString()
        {
            return $"[{(IsMajor ? "MAJOR" : "MINOR")}] {Name} @ {EventTime:HH:mm}";
        }
    }
}
