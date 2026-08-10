using System;
using System.Collections.Generic;
using ISE.OrderFlowAnalysis.Models;

namespace ISE.BacktestHarness.Engines
{
    /// <summary>
    /// Generates realistic mock Level 2 (DOM) data for backtest
    /// Simulates order book clustering and institutional absorption patterns
    /// </summary>
    public sealed class MockDomDataGenerator
    {
        private readonly Random _random = new Random();
        private double _biasDirection = 0.5; // Drifts between 0 (sell) and 1 (buy)

        /// <summary>
        /// Generate a DOM snapshot based on current price
        /// Simulates order book with realistic clustering patterns
        /// </summary>
        public DomSnapshot GenerateDomSnapshot(decimal currentPrice, long baseVolume = 100000)
        {
            // Drift bias randomly to create buy/sell pressure cycles
            _biasDirection += (_random.NextDouble() - 0.5) * 0.1;
            _biasDirection = Math.Max(0, Math.Min(1, _biasDirection));

            // Bid levels (below current price)
            var bidLevels = new List<DomLevel>();
            var clusterBidPrice = currentPrice - 1m; // Cluster 1 full point below
            var clusterBidVolume = (long)(baseVolume * (1.5 + _biasDirection * 2));

            for (decimal offset = 0m; offset <= 2m; offset += 0.25m)
            {
                var price = currentPrice - offset;
                long volume;

                if (Math.Abs(price - clusterBidPrice) < 0.5m)
                    volume = clusterBidVolume; // Heavy cluster
                else
                    volume = (long)(baseVolume * 0.5);

                bidLevels.Add(new DomLevel(price, volume));
            }

            // Ask levels (above current price)
            var askLevels = new List<DomLevel>();
            var clusterAskPrice = currentPrice + 1m; // Cluster 1 full point above
            var clusterAskVolume = (long)(baseVolume * (1.5 + (1 - _biasDirection) * 2));

            for (decimal offset = 0m; offset <= 2m; offset += 0.25m)
            {
                var price = currentPrice + offset;
                long volume;

                if (Math.Abs(price - clusterAskPrice) < 0.5m)
                    volume = clusterAskVolume; // Heavy cluster
                else
                    volume = (long)(baseVolume * 0.5);

                askLevels.Add(new DomLevel(price, volume));
            }

            return new DomSnapshot(DateTime.UtcNow, bidLevels, askLevels);
        }

        /// <summary>
        /// Generate multiple snapshots showing absorption pattern (signal confirmation)
        /// </summary>
        public List<DomSnapshot> GenerateAbsorptionPattern(
            decimal currentPrice,
            string direction,
            int snapshotCount = 5)
        {
            var snapshots = new List<DomSnapshot>();
            var baseVolume = 100000L;

            for (int i = 0; i < snapshotCount; i++)
            {
                // Increase bias in direction of trade
                if (direction.ToUpper() == "BUY")
                    _biasDirection = Math.Min(1, _biasDirection + 0.1);
                else
                    _biasDirection = Math.Max(0, _biasDirection - 0.1);

                snapshots.Add(GenerateDomSnapshot(currentPrice, baseVolume));
            }

            return snapshots;
        }
    }
}
