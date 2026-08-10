using System;
using System.Collections.Generic;
using System.IO;
using ISE.BacktestHarness;
using ISE.HistoricalResearch;

namespace ISE.BacktestTools
{
    /// <summary>
    /// Console app to run backtest harness against real NT8 data
    /// 
    /// Usage:
    /// dotnet run --project tools/BacktestConsole.csproj
    /// 
    /// Output: ./backtest-results/backtest_results.csv (ranked 420 configs)
    /// </summary>
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("ISE-Elite Backtest Console");
            Console.WriteLine("==========================\n");

            try
            {
                // Step 1: Load historical bars from NT8 probe
                Console.WriteLine("Step 1: Loading historical bars from NinjaTrader...");
                var bars = LoadHistoricalBars();
                
                if (bars == null || bars.Count == 0)
                {
                    Console.WriteLine("❌ ERROR: No bars loaded. Check NT8 connection and date range.");
                    return;
                }

                Console.WriteLine($"✅ Loaded {bars.Count:N0} bars");
                Console.WriteLine($"   Date range: {bars[0].TimestampUtc:O} to {bars[bars.Count-1].TimestampUtc:O}\n");

                // Step 2: Run backtest orchestrator
                Console.WriteLine("Step 2: Running backtest with 420 parameter configurations...");
                var accountSize = 50000m;
                var orchestrator = new BacktestOrchestrator(accountSize, "./backtest-results");
                orchestrator.Run(bars);

                Console.WriteLine("\n✅ Backtest complete!");
                Console.WriteLine("   Output: ./backtest-results/backtest_results.csv");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// TEMPLATE: Replace this with your NT8 probe implementation
        /// 
        /// Example:
        /// 
        /// private static List<HistoricalBar> LoadHistoricalBars()
        /// {
        ///     var probe = new ISEEliteHistoricalBarsRequestProbe();
        ///     var bars = new List<HistoricalBar>();
        ///     
        ///     // Load MNQ 1-min bars (6 months)
        ///     var mnqBars = probe.RequestBars(
        ///         instrument: "MNQ",
        ///         contract: "202409",
        ///         startDate: new DateTime(2024, 3, 1),
        ///         endDate: new DateTime(2024, 9, 1),
        ///         intervalSeconds: 60);
        ///     bars.AddRange(mnqBars);
        ///     
        ///     // Filter to NY session (9:30-16:00 CT)
        ///     var extractor = new NewYorkSessionDatasetExtractor();
        ///     var nyBars = extractor.FilterToNySession(bars);
        ///     
        ///     return nyBars;
        /// }
        /// </summary>
        private static List<HistoricalBar> LoadHistoricalBars()
        {
            // TODO: Implement actual NT8 probe call
            // For now, return empty list as placeholder
            
            Console.WriteLine("   TODO: Implement ISEEliteHistoricalBarsRequestProbe integration");
            Console.WriteLine("   Expected: Load 6 months MNQ + MGC (1-min, 5-min bars)");
            Console.WriteLine("   Filter: NY session only (9:30-16:00 CT)");
            
            return new List<HistoricalBar>();
        }
    }
}
