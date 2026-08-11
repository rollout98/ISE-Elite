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
        /// Load real MNQ historical bars from the probe-exported dataset.
        /// No session filter: full 24h is retained so Asia/London hours are analyzable.
        /// </summary>
        private static List<HistoricalBar> LoadHistoricalBars()
        {
            // Reads the real MNQ dataset written by ISEEliteNewYorkMultiMonthDatasetProbe
            // (NinjaTrader indicator) in HistoricalDataFileStore's tab-delimited schema.
            //
            // Override the path with the ISE_DATASET environment variable:
            //   $env:ISE_DATASET = "C:\path\to\dataset.tsv"
            //
            // NOTE: the previous implementation fell through to GenerateMockBars(), a
            // random walk. Random data contains no trends by construction, so every
            // result produced before 2026-08-11 was an artifact, not a finding.

            var path = Environment.GetEnvironmentVariable("ISE_DATASET");

            if (string.IsNullOrWhiteSpace(path))
            {
                var researchDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "NinjaTrader 8", "ISEEliteResearch");

                if (Directory.Exists(researchDir))
                {
                    // Newest dataset wins when several exports are present.
                    path = Directory.GetFiles(researchDir, "*.tsv")
                        .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                        .FirstOrDefault();
                }
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.WriteLine("   No dataset found.");
                Console.WriteLine("   Run ISEEliteNewYorkMultiMonthDatasetProbe on an MNQ chart in");
                Console.WriteLine("   NinjaTrader, then set ISE_DATASET to the .tsv it prints.");
                return new List<HistoricalBar>();
            }

            Console.WriteLine($"   Dataset: {path}");
            var bars = new HistoricalDataFileStore().Read(path).ToList();

            if (bars.Count > 0)
            {
                Console.WriteLine($"   Bars:    {bars.Count:N0}");
                Console.WriteLine($"   Range:   {bars[0].TimestampUtc:yyyy-MM-dd} to {bars[bars.Count - 1].TimestampUtc:yyyy-MM-dd}");
                Console.WriteLine($"   Source:  {bars[0].SourceName}");
            }

            return bars;
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
