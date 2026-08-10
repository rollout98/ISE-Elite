using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISE.BacktestHarness.Engines;
using ISE.BacktestHarness.Models;
using ISE.HistoricalResearch;

namespace ISE.BacktestHarness
{
    /// <summary>
    /// Orchestrates the complete backtest workflow:
    /// 1. Load historical data
    /// 2. Generate parameter configurations
    /// 3. Run each configuration through BacktestEngine
    /// 4. Analyze and rank results
    /// </summary>
    public sealed class BacktestRunner
    {
        private readonly decimal _accountSize;
        private readonly string _outputDirectory;

        public BacktestRunner(decimal accountSize = 50000m, string outputDirectory = "./backtest-results")
        {
            _accountSize = accountSize;
            _outputDirectory = outputDirectory;
            System.IO.Directory.CreateDirectory(_outputDirectory);
        }

        /// <summary>
        /// Run complete backtest suite
        /// </summary>
        public void Run(IReadOnlyList<HistoricalBar> historicalBars)
        {
            Console.WriteLine($"=== ISE-Elite Backtest Runner ===");
            Console.WriteLine($"Account Size: ${_accountSize:F2}");
            Console.WriteLine($"Historical Bars: {historicalBars.Count}");
            Console.WriteLine($"Date Range: {historicalBars[0].TimestampUtc:O} to {historicalBars[historicalBars.Count-1].TimestampUtc:O}");
            Console.WriteLine();

            // Step 1: Generate configurations
            Console.WriteLine("Generating parameter configurations...");
            var sweeper = new ConfigurationSweeper();
            var configs = sweeper.GenerateConfigurations();
            Console.WriteLine($"Generated {configs.Count} configurations to test\n");

            // Step 2: Run backtests
            Console.WriteLine("Running backtests...");
            var sw = Stopwatch.StartNew();
            var engine = new BacktestEngine(_accountSize);
            var results = new List<BacktestResult>();

            int count = 0;
            foreach (var config in configs)
            {
                var result = engine.Run(config, historicalBars);
                results.Add(result);
                count++;
                if (count % 50 == 0)
                    Console.WriteLine($"  Completed {count}/{configs.Count}...");
            }
            sw.Stop();
            Console.WriteLine($"Backtests completed in {sw.Elapsed.TotalSeconds:F1}s\n");

            // Step 3: Analyze and export
            Console.WriteLine("Analyzing results...");
            var analyzer = new ResultsAnalyzer();
            var outputPath = System.IO.Path.Combine(_outputDirectory, "backtest_results.csv");
            analyzer.ExportResultsCsv(results, outputPath);
            analyzer.PrintTopResults(results, 20);
        }
    }
}
