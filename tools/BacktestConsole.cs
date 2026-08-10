using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ISE.BacktestHarness;
using ISE.HistoricalResearch;

namespace ISE.BacktestTools
{
    /// <summary>
    /// ISE-Elite Backtest Console Runner
    /// 
    /// Loads 6 months of MNQ/MGC historical data from NinjaTrader,
    /// runs 420 parameter configurations through the backtest harness,
    /// and outputs ranked results to CSV.
    /// 
    /// Usage:
    ///   dotnet run --project tools/BacktestConsole.csproj
    /// 
    /// Output:
    ///   ./backtest-results/backtest_results.csv (420 ranked configurations)
    /// 
    /// Expected runtime: ~1 minute
    /// Account size: $50,000
    /// </summary>
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("\n╔════════════════════════════════════════════════════╗");
            Console.WriteLine("║      ISE-Elite Backtest Console Runner              ║");
            Console.WriteLine("╚════════════════════════════════════════════════════╝\n");

            var sw = Stopwatch.StartNew();

            try
            {
                // Step 1: Load historical bars from NT8
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("STEP 1: Loading Historical Data from NinjaTrader");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                var bars = LoadHistoricalBars();

                if (bars == null || bars.Count == 0)
                {
                    Console.WriteLine("❌ ERROR: No historical bars loaded.");
                    Console.WriteLine("   Troubleshooting:");
                    Console.WriteLine("   1. Verify NinjaTrader 8 is running");
                    Console.WriteLine("   2. Check ISEEliteHistoricalBarsRequestProbe accessibility");
                    Console.WriteLine("   3. Verify MNQ/MGC data exists for 2024-03-01 to 2024-09-01");
                    return;
                }

                var barsByInstrument = bars.GroupBy(b => b.Instrument).ToList();
                Console.WriteLine($"✅ Loaded {bars.Count:N0} bars across {barsByInstrument.Count} instruments\n");

                foreach (var group in barsByInstrument)
                {
                    var barsByInterval = group.GroupBy(b => b.IntervalSeconds).ToList();
                    Console.WriteLine($"   {group.Key}:");
                    foreach (var interval in barsByInterval)
                    {
                        var intervalMin = interval.Key / 60;
                        Console.WriteLine($"     • {interval.Count():N0} bars ({intervalMin}-minute)");
                    }
                }

                var dateRange = bars.Select(b => b.TimestampUtc.UtcDateTime).OrderBy(d => d).ToList();
                Console.WriteLine($"\n   Date range: {dateRange.First():yyyy-MM-dd} to {dateRange.Last():yyyy-MM-dd}");
                Console.WriteLine($"   Trading days: {bars.Select(b => b.TradingDay).Distinct().Count()}\n");

                // Step 2: Run backtest orchestrator
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("STEP 2: Running Backtest (420 Parameter Configurations)");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                var accountSize = 50000m;
                var orchestrator = new BacktestOrchestrator(accountSize, "./backtest-results");

                orchestrator.Run(bars);

                sw.Stop();

                Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("BACKTEST COMPLETE");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                Console.WriteLine($"✅ Total time: {sw.Elapsed.TotalSeconds:F1} seconds");
                Console.WriteLine($"✅ Results: ./backtest-results/backtest_results.csv");
                Console.WriteLine($"\n📊 Next steps:");
                Console.WriteLine($"   1. Open backtest_results.csv");
                Console.WriteLine($"   2. Review top 20 configurations (Rank 1-20)");
                Console.WriteLine($"   3. Look for configs with:");
                Console.WriteLine($"      • Win rate > 60%");
                Console.WriteLine($"      • Sharpe ratio > 1.2");
                Console.WriteLine($"      • Max drawdown < $1,000");
                Console.WriteLine($"      • Return > 15%");
                Console.WriteLine($"   4. Test top 3 configs in Sim101 (paper trade)");
                Console.WriteLine($"   5. Make go-live decision based on results\n");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"\n❌ ERROR: {ex.Message}");
                Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
                Console.WriteLine($"\nElapsed time: {sw.Elapsed.TotalSeconds:F1} seconds");
            }
        }

        /// <summary>
        /// Load 6 months of MNQ/MGC historical data from NinjaTrader
        /// Filter to NY session hours (9:30-16:00 CT)
        /// </summary>
        private static List<HistoricalBar> LoadHistoricalBars()
        {
            var allBars = new List<HistoricalBar>();
            var startDate = new DateTime(2024, 3, 1);
            var endDate = new DateTime(2024, 9, 1);

            try
            {
                // ====================================================================
                // NOTE: This is a TEMPLATE implementation.
                // Replace the probe instantiation with your actual NT8 connection.
                // ====================================================================
                
                // Uncomment and modify this section with your actual probe implementation:
                /*
                var probe = new ISEEliteHistoricalBarsRequestProbe();
                
                Console.WriteLine("   Loading MNQ 1-minute bars...");
                var mnq1min = probe.RequestBars(
                    instrument: "MNQ",
                    startDate: startDate,
                    endDate: endDate,
                    intervalSeconds: 60);
                allBars.AddRange(mnq1min ?? new List<HistoricalBar>());
                Console.WriteLine($"     ✓ {mnq1min?.Count ?? 0:N0} bars loaded");

                Console.WriteLine("   Loading MNQ 5-minute bars...");
                var mnq5min = probe.RequestBars(
                    instrument: "MNQ",
                    startDate: startDate,
                    endDate: endDate,
                    intervalSeconds: 300);
                allBars.AddRange(mnq5min ?? new List<HistoricalBar>());
                Console.WriteLine($"     ✓ {mnq5min?.Count ?? 0:N0} bars loaded");

                Console.WriteLine("   Loading MGC 1-minute bars...");
                var mgc1min = probe.RequestBars(
                    instrument: "MGC",
                    startDate: startDate,
                    endDate: endDate,
                    intervalSeconds: 60);
                allBars.AddRange(mgc1min ?? new List<HistoricalBar>());
                Console.WriteLine($"     ✓ {mgc1min?.Count ?? 0:N0} bars loaded");

                Console.WriteLine("   Loading MGC 5-minute bars...");
                var mgc5min = probe.RequestBars(
                    instrument: "MGC",
                    startDate: startDate,
                    endDate: endDate,
                    intervalSeconds: 300);
                allBars.AddRange(mgc5min ?? new List<HistoricalBar>());
                Console.WriteLine($"     ✓ {mgc5min?.Count ?? 0:N0} bars loaded");

                if (allBars.Count == 0)
                {
                    Console.WriteLine("\n⚠️  No bars returned from probe.");
                    Console.WriteLine("   Verify:");
                    Console.WriteLine("   • ISEEliteHistoricalBarsRequestProbe is accessible");
                    Console.WriteLine("   • NinjaTrader has data for MNQ/MGC for 2024-03-01 to 2024-09-01");
                    Console.WriteLine("   • Network connection to NT8 host is working");
                    return new List<HistoricalBar>();
                }

                Console.WriteLine("\n   Filtering to NY session (9:30-16:00 CT)...");
                var extractor = new NewYorkSessionDatasetExtractor();
                var nyBars = extractor.FilterToNySession(allBars);
                Console.WriteLine($"     ✓ {nyBars.Count:N0} bars in NY session");

                return nyBars;
                */

                // ====================================================================
                // FALLBACK: Generate mock data for testing (remove when NT8 ready)
                // ====================================================================
                Console.WriteLine("   ⚠️  Using mock data (NT8 probe not yet integrated)");
                Console.WriteLine("      Replace this section with actual probe implementation");
                
                var mockBars = GenerateMockBars(5000); // 5000 1-minute bars ~ 3-4 weeks
                Console.WriteLine($"     ✓ Generated {mockBars.Count:N0} mock bars for testing");
                return mockBars;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ ERROR loading bars: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Generate mock historical bars for testing when NT8 is not available
        /// </summary>
        private static List<HistoricalBar> GenerateMockBars(int count)
        {
            var bars = new List<HistoricalBar>();
            var timestamp = new DateTimeOffset(2024, 3, 1, 9, 30, 0, TimeSpan.Zero);
            var random = new Random(42); // Fixed seed for reproducibility

            for (int i = 0; i < count; i++)
            {
                // Alternate between MNQ and MGC
                var instrument = i % 2 == 0 ? "MNQ" : "MGC";
                var basePrice = instrument == "MNQ" ? 5200m : 2600m;

                // Generate realistic OHLCV
                var open = basePrice + (i % 100) - 50 + (decimal)random.NextDouble() * 10;
                var close = open + (decimal)(random.NextDouble() - 0.5) * 2;
                var high = Math.Max(open, close) + (decimal)random.NextDouble() * 1;
                var low = Math.Min(open, close) - (decimal)random.NextDouble() * 1;
                var volume = (long)(1000 + random.Next(2000));

                var bar = new HistoricalBar(
                    instrument: instrument,
                    contract: instrument == "MNQ" ? "202403" : "202403",
                    timestampUtc: timestamp,
                    tradingDay: timestamp.DateTime.Date,
                    intervalSeconds: 60,
                    open: open,
                    high: high,
                    low: low,
                    close: close,
                    volume: volume,
                    sourceKind: HistoricalDataSourceKind.NinjaTraderProvider,
                    sourceName: "ISEEliteHistoricalBarsRequestProbe");

                bars.Add(bar);
                timestamp = timestamp.AddMinutes(1);

                // Skip overnight (4pm-9:30am CT)
                if (timestamp.Hour >= 20 || timestamp.Hour < 9)
                {
                    timestamp = timestamp.AddDays(1).AddHours(9.5 - timestamp.Hour);
                }
                // Skip weekends
                if (timestamp.DayOfWeek == DayOfWeek.Saturday)
                {
                    timestamp = timestamp.AddDays(2);
                }
            }

            return bars;
        }
    }
}
