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
                    Console.WriteLine($"❌ ERROR: No {instrument} bars loaded.");
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
                            // ABORT, same as a parse failure. A missing file previously
                            // printed an error and then silently ran the whole sweep on
                            // the built-in MA crossover, producing a full results table
                            // for a strategy nobody asked to test.
                            Console.WriteLine($"\n❌ FATAL: Signal file not found: {signalCsvPath}\n");
                            Console.WriteLine("   ISE_SIGNALS was set but the file is not there.");
                            Console.WriteLine("   Refusing to fall back to the built-in MA crossover.\n");
                            Environment.Exit(1);
                        }
                        else
                        {
                            signals = VectorFlowSignalLoader.LoadFromCsv(signalCsvPath);
                            Console.WriteLine();

                            // Signal-level verification against the chart. Set
                            // ISE_INSPECT_DATE=2026-06-22 to dump every fire the loader
                            // read for that day, so it can be compared label-for-label
                            // with TradingView. Timestamps are shown in BOTH UTC and CT
                            // because the chart is CT and a timezone slip would look
                            // exactly like a signal-mapping bug.
                            var inspectDate = Environment.GetEnvironmentVariable("ISE_INSPECT_DATE");
                            if (!string.IsNullOrWhiteSpace(inspectDate) && DateTime.TryParse(inspectDate, out var probeDay))
                            {
                                var ct = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                                Console.WriteLine($"===== SIGNAL FIRES READ FOR {probeDay:yyyy-MM-dd} (compare to chart) =====");
                                Console.WriteLine("   UTC TIME          CT TIME           SIGNAL");

                                int shown = 0;
                                foreach (var rec in signals.Where(r => r.Signal != "NONE"))
                                {
                                    var ctTime = TimeZoneInfo.ConvertTimeFromUtc(rec.TimestampUtc, ct);
                                    // Match on CT date, since that is what the chart shows.
                                    if (ctTime.Date != probeDay.Date) continue;
                                    Console.WriteLine($"   {rec.TimestampUtc:yyyy-MM-dd HH:mm}  {ctTime:yyyy-MM-dd HH:mm}  {rec.Signal}");
                                    shown++;
                                }
                                if (shown == 0)
                                    Console.WriteLine("   (no fires read for that CT date)");
                                Console.WriteLine($"   total: {shown} fires\n");
                            }
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

                // Optional date window, so two runs can be forced onto IDENTICAL periods.
                // Without this, comparing the 2m result (29 overlapping days) to the 5m
                // result (42 days) compares two different stretches of market, and any
                // difference is as likely to be the calendar as the timeframe.
                var winStart = Environment.GetEnvironmentVariable("ISE_WINDOW_START");
                var winEnd = Environment.GetEnvironmentVariable("ISE_WINDOW_END");
                if (!string.IsNullOrWhiteSpace(winStart) || !string.IsNullOrWhiteSpace(winEnd))
                {
                    var from = DateTime.TryParse(winStart, out var f) ? f : DateTime.MinValue;
                    var to = DateTime.TryParse(winEnd, out var t) ? t : DateTime.MaxValue;
                    var before = bars.Count;
                    bars = bars.Where(b => b.TradingDay >= from && b.TradingDay <= to).ToList();
                    Console.WriteLine($"   Window filter: {from:yyyy-MM-dd} to {to:yyyy-MM-dd} " +
                                      $"-> {bars.Count:N0} bars (from {before:N0})\n");
                    if (bars.Count == 0)
                    {
                        Console.WriteLine("\n❌ FATAL: Window filter left no bars.\n");
                        Environment.Exit(1);
                    }
                }

                // Final gate. Every catastrophic run in this project shared one shape:
                // signals failed to load, the engine fell back to its built-in 5/10 MA
                // crossover, and a full ranked results table came out looking entirely
                // plausible. There is no legitimate reason to sweep without VectorFlow
                // signals, so refuse rather than produce another convincing fiction.
                if (signals == null || signals.Count == 0)
                {
                    Console.WriteLine("\n❌ FATAL: No VectorFlow signals loaded.\n");
                    Console.WriteLine("   Set ISE_SIGNALS to a TradingView export containing the");
                    Console.WriteLine("   two untitled 'Shapes' columns (1st = BUY, 2nd = SELL).");
                    Console.WriteLine("   The built-in MA crossover is NOT the strategy under test.\n");
                    Environment.Exit(1);
                }

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

            // Distinguish "not configured" from "configured but the file is not there".
            // Collapsing both into one message sent us chasing an env-var problem when
            // the real cause was a download that had not finished.
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine($"   ⚠️  No dataset path set for {instrument}");
                Console.WriteLine($"   Set ISE_DATASET_{instrument} or ISE_DATASET to the .tsv path.");
                return new List<HistoricalBar>();
            }

            if (!File.Exists(path))
            {
                Console.WriteLine($"   ⚠️  Dataset path IS set but the file does not exist:");
                Console.WriteLine($"       {path}");
                Console.WriteLine($"   Check the download completed and the filename matches exactly.");
                return new List<HistoricalBar>();
            }

            Console.WriteLine($"   {instrument} dataset: {Path.GetFileName(path)}");
            var bars = new HistoricalDataFileStore().Read(path).ToList();

            return bars;
        }
    }
}
