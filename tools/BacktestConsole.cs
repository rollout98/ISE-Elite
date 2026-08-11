using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ISE.BacktestHarness;
using ISE.HistoricalResearch;

namespace ISE.BacktestTools
{
    /// <summary>
    /// ISE-Elite Backtest Console Runner
    /// 
    /// Loads MNQ historical data and (optionally) VectorFlow signals from CSV,
    /// runs 420 parameter configurations through the backtest harness,
    /// and outputs ranked results to CSV.
    /// 
    /// Usage:
    ///   $env:ISE_DATASET = "path/to/ny-MNQ-*.tsv"
    ///   $env:ISE_SIGNALS = "path/to/CME_MINI_MNQ1!, 5.csv"
    ///   dotnet run --project tools/BacktestConsole.csproj
    /// 
    /// Output:
    ///   ./backtest-results/backtest_results.csv (420 ranked configurations)
    /// 
    /// Expected runtime: ~1 minute per 1944 configs
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
                // Step 1: Load MNQ historical data
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("STEP 1: Loading Historical Data (MNQ)");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                var bars = LoadHistoricalBars("MNQ");
                if (bars == null || bars.Count == 0)
                {
                    Console.WriteLine("❌ ERROR: No MNQ bars loaded.");
                    return;
                }

                var barsByInterval = bars.GroupBy(b => b.IntervalSeconds).ToList();
                Console.WriteLine($"✅ Loaded {bars.Count:N0} bars\n");
                foreach (var interval in barsByInterval)
                {
                    var intervalMin = interval.Key / 60;
                    Console.WriteLine($"   • {interval.Count():N0} bars ({intervalMin}-minute)");
                }

                var dateRange = bars.Select(b => b.TimestampUtc.UtcDateTime).OrderBy(d => d).ToList();
                Console.WriteLine($"\n   Date range: {dateRange.First():yyyy-MM-dd} to {dateRange.Last():yyyy-MM-dd}");
                Console.WriteLine($"   Trading days: {bars.Select(b => b.TradingDay).Distinct().Count()}\n");

                // Step 1b: Load external signals (optional)
                IReadOnlyList<VectorFlowSignalLoader.SignalRecord>? signals = null;
                var signalCsvPath = Environment.GetEnvironmentVariable("ISE_SIGNALS");
                if (string.IsNullOrWhiteSpace(signalCsvPath))
                    signalCsvPath = Environment.GetEnvironmentVariable("ISE_SIGNALS_MNQ");

                Console.WriteLine($"Signal CSV env var: {(string.IsNullOrWhiteSpace(signalCsvPath) ? "(not set)" : signalCsvPath)}\n");

                if (!string.IsNullOrWhiteSpace(signalCsvPath))
                {
                    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Console.WriteLine("Loading External Signals (MNQ - VectorFlow CSV)");
                    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
                    try
                    {
                        if (!File.Exists(signalCsvPath))
                        {
                            Console.WriteLine($"❌ ERROR: Signal file not found: {signalCsvPath}\n");
                        }
                        else
                        {
                            signals = VectorFlowSignalLoader.LoadFromCsv(signalCsvPath);
                            Console.WriteLine();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ ERROR: Could not load signal CSV: {ex.Message}");
                        Console.WriteLine("   Proceeding with computed signals (5/10 MA crossover)\n");
                    }
                }

                // Step 2: Run backtest orchestrator
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("STEP 2: Running Backtest (1944 Parameter Configurations)");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                var accountSize = 50000m;
                var orchestrator = new BacktestOrchestrator(accountSize, "./backtest-results");

                orchestrator.Run(bars, signals);

                sw.Stop();

                Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("BACKTEST COMPLETE");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                Console.WriteLine($"✅ Total time: {sw.Elapsed.TotalSeconds:F1} seconds");
                Console.WriteLine($"✅ Results: ./backtest-results/backtest_results.csv");
                Console.WriteLine($"\n📊 Next steps:");
                Console.WriteLine($"   1. Open backtest_results.csv");
                Console.WriteLine($"   2. Review top 20 configurations (Rank 1-20)");
                Console.WriteLine($"   3. Look for Trades/Day in single digits");
                Console.WriteLine($"   4. Check if P&L approaches $1,000/day\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ FATAL ERROR: {ex.Message}");
                Console.WriteLine($"   {ex.StackTrace}");
            }
        }

        static List<HistoricalBar> LoadHistoricalBars(string instrument)
        {
            var path = Environment.GetEnvironmentVariable($"ISE_DATASET_{instrument}");
            if (string.IsNullOrWhiteSpace(path))
                path = Environment.GetEnvironmentVariable("ISE_DATASET");

            if (string.IsNullOrWhiteSpace(path) || path == null || !File.Exists(path))
            {
                Console.WriteLine($"   ⚠️  No dataset found for {instrument}");
                Console.WriteLine($"   Set ISE_DATASET_{instrument} to the .tsv path, or place the file in");
                Console.WriteLine($@"   Documents\NinjaTrader 8\ISEEliteResearch\ with '{instrument}' in its name.");
                return new List<HistoricalBar>();
            }

            Console.WriteLine($"   {instrument} dataset: {Path.GetFileName(path)}");
            var bars = new HistoricalDataFileStore().Read(path).ToList();

            return bars;
        }
    }
}
