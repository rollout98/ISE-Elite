using ISE.Core.Contexts;
using Xunit;

namespace ISE.Core.Tests.Contexts;

public sealed class EngineContextTests
{
    [Fact]
    public void Constructor_rejects_non_utc_timestamp()
    {
        Assert.Throws<ArgumentException>(() => new TestContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "2026-08-03",
            DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local),
            "1.0.0",
            "1.0.0"));
    }

    [Fact]
    public void Published_metadata_is_read_only()
    {
        var context = new TestContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "2026-08-03",
            new DateTime(2026, 8, 3, 1, 0, 0, DateTimeKind.Utc),
            "1.0.0",
            "1.0.0");

        Assert.Equal("2026-08-03", context.TradingDayId);
        Assert.Equal(DateTimeKind.Utc, context.TimestampUtc.Kind);
    }

    private sealed class TestContext : EngineContext
    {
        public TestContext(
            Guid contextId,
            Guid correlationId,
            string tradingDayId,
            DateTime timestampUtc,
            string engineVersion,
            string configurationVersion)
            : base(contextId, correlationId, tradingDayId, timestampUtc, engineVersion, configurationVersion)
        {
        }
    }
}
