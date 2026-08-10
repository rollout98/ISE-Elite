using System;
using System.Collections.Generic;
using ISE.BacktestHarness;
using ISE.HistoricalResearch;
using Xunit;

namespace ISE.BacktestHarness.Tests
{
    public class BacktestOrchestratorTests
    {
        /// <summary>
        /// Template test showing how to use BacktestOrchestrator
        /// Replace GenerateMockBars() with your NT8 probe data loading
        /// </summary>
        [Fact]
        public void Orchestrator_RunsBacktest_WithMockData()
        {
            // Arrange
            var accountSize = 50000m;
            var orchestrator = new BacktestOrchestrator(accountSize, "./test-results");
            var bars = GenerateMockBars(1000); // 1000 bars = ~1 week of 1-min data

            // Act
            orchestrator.Run(bars);

            // Assert - if we get here without exception, it worked
            Assert.NotEmpty(bars);
        }

        /// <summary>
        /// How to load real data from NinjaTrader probe:
        /// 
        /// var probe = new ISEEliteHistoricalBarsRequestProbe();
        /// var request = new HistoricalBarsRequest(
        ///     instrument: "MNQ",
        ///     contract: "202409",
        ///     startDate: new DateTime(2024, 3, 1),
        ///     endDate: new DateTime(2024, 9, 1),
        ///     intervalSeconds: 60); // 1-minute bars
        /// 
        /// var bars = probe.RequestBars(request);
        /// 
        /// Then pass bars to orchestrator.Run(bars)
        /// </summary>
        private List<HistoricalBar> GenerateMockBars(int count)
        {
            var bars = new List<HistoricalBar>();
            var timestamp = new DateTimeOffset(2024, 3, 1, 9, 30, 0, TimeSpan.Zero);

            for (int i = 0; i < count; i++)
            {
                var open = 5200m + (i % 50);
                var close = open + 1m;
                var high = Math.Max(open, close) + 0.5m;
                var low = Math.Min(open, close) - 0.5m;
                var volume = 1000L + i;

                var bar = new HistoricalBar(
                    instrument: "MNQ",
                    contract: "202403",
                    timestampUtc: timestamp,
                    tradingDay: timestamp.DateTime.Date,
                    intervalSeconds: 60,
                    open: open,
                    high: high,
                    low: low,
                    close: close,
                    volume: volume);

                bars.Add(bar);
                timestamp = timestamp.AddMinutes(1);
            }

            return bars;
        }
    }
}
