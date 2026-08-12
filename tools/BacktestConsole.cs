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

            // Parse instrument from command line (default: MNQ)
            string instrument = "MNQ";
            if (args.Contains("--instrument") && args.Length > Array.IndexOf(args, "--instrument") + 1)
            {
                instrument = args[Array.IndexOf(args, "--instrument") + 1].ToUpperInvariant();
            }

            var sw = Stopwatch.StartNew();

            try
            {
                // Step 1: Load historical data (MNQ or MGC)
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine($"STEP 1: Loading Historical Data ({instrument})");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                var bars = LoadHistoricalBars(instrument);
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
                    signalCsvPath = Environment.GetEnvironmentVariable($"ISE_SIGNALS_{instrument}");

                Console.WriteLine($"Signal CSV env var: {(string.IsNullOrWhiteSpace(signalCsvPath) ? "(not set)" : signalCsvPath)}\n");

                if (!string.IsNullOrWhiteSpace(signalCsvPath))
                {
                    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    Console.WriteLine($"Loading External Signals ({instrument} - VectorFlow CSV)");
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
                        // ABORT. Falling back to the built-in 5/10 MA crossover silently
                        // swapped in a completely different strategy and then ranked 1,620
                        // configs of it under a VectorFlow heading. A failed signal load
                        // must stop the run, not quietly produce plausible-looking numbers
                        // for something nobody asked to test.
                        Console.WriteLine($"\n❌ FATAL: Could not load signal CSV: {ex.Message}\n");
                        Console.WriteLine("   Refusing to fall back to the built-in MA crossover -");
                        Console.WriteLine("   that is a different strategy and its results would be misleading.");
                        Console.WriteLine("   Fix the signal file or column mapping and re-run.\n");
                        Environment.Exit(1);
                    }
                }

                // Step 2: Run backtest orchestrator
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("STEP 2: Running Backtest (parameter sweep)");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                var accountSize = 50000m;
                var orchestrator = new BacktestOrchestrator(accountSize, "./backtest-results", instrument);

                orchestrator.Run(bars, signals);

                sw.Stop();

                Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("BACKTEST COMPLETE");
                Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

                Console.WriteLine($"✅ Total time: {sw.Elapsed.TotalSeconds:F1} seconds");
                Console.WriteLine($"✅ Results: ./backtest-results/backtest_results.csv");
                Console.WriteLine($"\n📊 Next steps:");
                Console.WriteLine($"   1. Open backtest_results.csv");
                Console.WriteLine($"   2. Find the best P&L for each (Contracts, Stop) pair");
                Console.WriteLine($"   3. Look for which combo hits $500+/day");
                Console.WriteLine($"   4. Verify win rate is reasonable (60%+)");
                Console.WriteLine($"   5. Check if Trades/Day is in single digits\n");
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
