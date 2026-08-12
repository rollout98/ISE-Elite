using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ISE.BacktestHarness.Engines;
using ISE.BacktestHarness.Models;
using ISE.HistoricalResearch;

namespace ISE.BacktestHarness
{
    /// <summary>
    /// Main orchestrator for backtest workflow
    /// 1. Accept pre-loaded bars (from NT8 probe or other source)
    /// 2. Generate 400+ parameter configurations
    /// 3. Run each configuration through BacktestExecutionEngine
    /// 4. Analyze and rank results
    /// 5. Export to CSV
    /// </summary>
    public sealed class BacktestOrchestrator
    {
        private readonly decimal _accountSize;
        private readonly string _outputDirectory;
        private readonly string _instrument;

        public BacktestOrchestrator(
            decimal accountSize = 50000m,
            string outputDirectory = "./backtest-results",
            string instrument = "MNQ")
        {
            _accountSize = accountSize;
            _outputDirectory = outputDirectory;
            _instrument = instrument?.ToUpperInvariant() ?? "MNQ";
            System.IO.Directory.CreateDirectory(_outputDirectory);
        }

        /// <summary>
        /// Run complete backtest suite with pre-loaded bars
        /// 
        /// Usage:
        /// var orchestrator = new BacktestOrchestrator(50000m, "./results", "MGC");
        /// var bars = LoadBarsFromNinjaTrader(); // Your implementation
        /// orchestrator.Run(bars);
        /// </summary>
        public void Run(
            IReadOnlyList<HistoricalBar> historicalBars,
            IReadOnlyList<VectorFlowSignalLoader.SignalRecord>? externalSignals = null)
        {
            if (historicalBars == null || historicalBars.Count == 0)
                throw new ArgumentException("Historical bars required.", nameof(historicalBars));

            Console.WriteLine($"\n{'='*60}");
            Console.WriteLine($"  ISE-Elite Backtest Orchestrator ({_instrument})");
            Console.WriteLine($"{'='*60}");
            Console.WriteLine($"Account Size: ${_accountSize:F2}");
            Console.WriteLine($"Historical Bars: {historicalBars.Count:N0}");
            Console.WriteLine($"Date Range: {historicalBars[0].TimestampUtc:O} to {historicalBars[historicalBars.Count-1].TimestampUtc:O}");
            Console.WriteLine($"Output Directory: {_outputDirectory}");
            Console.WriteLine();

            var periodStart = historicalBars[0].TimestampUtc.UtcDateTime;
            var periodEnd = historicalBars[historicalBars.Count - 1].TimestampUtc.UtcDateTime;

            // Step 1: Generate configurations (instrument-specific)
            Console.WriteLine("STEP 1: Generating parameter configurations...");
            var sw = Stopwatch.StartNew();
            var sweeper = new ConfigurationSweeper(_instrument);
            var configs = sweeper.GenerateConfigurations();
            Console.WriteLine($"✅ Generated {configs.Count} configurations in {sw.ElapsedMilliseconds}ms\n");

            // Step 2: Run backtests
            Console.WriteLine("STEP 2: Running backtests...");
            sw.Restart();
            var engine = new BacktestExecutionEngine(_accountSize);

            // If external signals are provided, inject them once at the start
            if (externalSignals != null && externalSignals.Count > 0)
            {
                var signalTuples = externalSignals.Select(sr => (sr.TimestampUtc, sr.Signal));
                engine.LoadExternalSignals(signalTuples);
                Console.WriteLine($"✅ Engine loaded {externalSignals.Count} external signals\n");
            }

            var results = new List<BacktestResult>();

            int completedConfigs = 0;
            var swPerConfig = Stopwatch.StartNew();
            
            foreach (var config in configs)
            {
                var result = engine.Run(config, historicalBars, periodStart, periodEnd);
                results.Add(result);
                completedConfigs++;

                if (completedConfigs % 50 == 0)
                {
                    var avgMs = swPerConfig.ElapsedMilliseconds / (double)completedConfigs;
                    var remainingConfigs = configs.Count - completedConfigs;
                    var estimatedSeconds = remainingConfigs * avgMs / 1000.0;
                    Console.WriteLine($"  [{completedConfigs}/{configs.Count}] " +
                                    $"ETA: {estimatedSeconds:F0}s " +
                                    $"({avgMs:F1}ms per config)");
                }
            }
            sw.Stop();
            var totalSeconds = sw.Elapsed.TotalSeconds;
            Console.WriteLine($"✅ Backtests completed in {totalSeconds:F1}s " +
                            $"({totalSeconds/completedConfigs:F2}s per config)\n");

            // Step 3: Analyze and export
            Console.WriteLine("STEP 3: Analyzing results...");
            var analyzer = new ResultsAnalyzer();
            var outputPath = System.IO.Path.Combine(_outputDirectory, "backtest_results.csv");
            analyzer.ExportResultsCsv(results, outputPath);

            Console.WriteLine();
            analyzer.PrintTopResults(results, 20);

            Console.WriteLine($"\n{'='*60}");
            Console.WriteLine($"Backtest Complete!");
            Console.WriteLine($"Results: {outputPath}");
            Console.WriteLine($"{'='*60}\n");
        }

        /// <summary>
        /// Run backtest on a specific configuration (for testing/debugging)
        /// </summary>
        public BacktestResult RunSingleConfiguration(
            BacktestConfiguration config,
            IReadOnlyList<HistoricalBar> historicalBars)
        {
            var periodStart = historicalBars[0].TimestampUtc.UtcDateTime;
            var periodEnd = historicalBars[historicalBars.Count - 1].TimestampUtc.UtcDateTime;

            var engine = new BacktestExecutionEngine(_accountSize);
            return engine.Run(config, historicalBars, periodStart, periodEnd);
        }
    }
}
