using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ISE.BacktestHarness.Models;

namespace ISE.BacktestHarness.Engines
{
    /// <summary>
    /// Analyzes backtest results and generates ranking
    /// </summary>
    public sealed class ResultsAnalyzer
    {
        /// <summary>
        /// Rank results by composite score and write to CSV
        /// </summary>
        public void ExportResultsCsv(IEnumerable<BacktestResult> results, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path required.", nameof(outputPath));

            var sorted = results
                .OrderByDescending(r => CalculateCompositeScore(r))
                .ToList();

            using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
            {
                // Header
                writer.WriteLine("Rank,ConfigId,MaxContracts,RiskMult,StopDist,MaxHoldBars,ExitMode," +
                                "GrossProfit,ReturnPct,TotalTrades,WinRate,WinTrades,LossTrades," +
                                "AvgPnL,LargestWin,LargestLoss,MaxDD,ProfitFactor,Sharpe,Score");

                // Rows
                int rank = 1;
                foreach (var result in sorted)
                {
                    writer.WriteLine(
                        $"{rank}," +
                        $"{result.Config.ConfigId}," +
                        $"{result.Config.MaximumContracts}," +
                        $"{result.Config.AdaptiveRiskMultiplier:F2}," +
                        $"{result.Config.StopDistanceRisk:F2}," +
                        $"{result.Config.LiquidityCapacity:F0}," +
                        $"{(result.Config.UseTrailingStop ? "TRAIL" : "FIXED")}," +
                        $"{result.GrossProfit:F2}," +
                        $"{result.ReturnPercent:F2}," +
                        $"{result.TotalTrades}," +
                        $"{result.WinRate:F1}," +
                        $"{result.WinningTrades}," +
                        $"{result.LosingTrades}," +
                        $"{result.AveragePnL:F2}," +
                        $"{result.LargestWin:F2}," +
                        $"{result.LargestLoss:F2}," +
                        $"{result.MaxDrawdown:F2}," +
                        $"{result.ProfitFactor:F2}," +
                        $"{result.SharpeRatio:F2}," +
                        $"{CalculateCompositeScore(result):F2}");
                    rank++;
                }
            }

            Console.WriteLine($"Results exported to {outputPath}");
        }

        /// <summary>
        /// Composite score: prioritize win rate > Sharpe > profit, penalize DD
        /// </summary>
        private double CalculateCompositeScore(BacktestResult result)
        {
            if (result.TotalTrades == 0) return double.NegativeInfinity;

            // Win rate weight (0-1)
            var winRateScore = result.WinRate / 100.0;

            // Sharpe weight (0-2, capped)
            var sharpeScore = Math.Min(result.SharpeRatio / 2.0, 1.0);

            // Profit weight (normalized to account size)
            var profitScore = Math.Min((double)result.GrossProfit / (double)result.StartingEquity, 1.0);

            // Drawdown penalty (higher DD = lower score)
            var ddPenalty = Math.Max(0, 1.0 - ((double)result.MaxDrawdown / (double)result.StartingEquity));

            // Composite: heavily weight win rate and Sharpe, moderate profit, apply DD penalty
            var score = (winRateScore * 0.4) + (sharpeScore * 0.3) + (profitScore * 0.2) + (ddPenalty * 0.1);
            return score;
        }

        /// <summary>
        /// Print top N results to console
        /// </summary>
        public void PrintTopResults(IEnumerable<BacktestResult> results, int topN = 10)
        {
            var sorted = results
                .OrderByDescending(r => CalculateCompositeScore(r))
                .Take(topN)
                .ToList();

            Console.WriteLine($"\n========== TOP {topN} CONFIGURATIONS ==========\n");
            int rank = 1;
            foreach (var result in sorted)
            {
                Console.WriteLine($"#{rank}: {result}");

                // Long/short split. A blended figure can hide one side carrying the
                // other, or one side being broken - both matter more than the total.
                var longs = result.Trades.Where(t => t.Direction == "LONG").ToList();
                var shorts = result.Trades.Where(t => t.Direction == "SHORT").ToList();
                Console.WriteLine(
                    $"     LONG  {longs.Count,5} trades | " +
                    $"{(longs.Count > 0 ? longs.Count(t => t.IsWin) * 100.0 / longs.Count : 0),5:F1}% win | " +
                    $"${longs.Sum(t => t.PnL - t.Slippage),12:F2}");
                Console.WriteLine(
                    $"     SHORT {shorts.Count,5} trades | " +
                    $"{(shorts.Count > 0 ? shorts.Count(t => t.IsWin) * 100.0 / shorts.Count : 0),5:F1}% win | " +
                    $"${shorts.Sum(t => t.PnL - t.Slippage),12:F2}");

                rank++;
            }
            Console.WriteLine();
        }
    }
}
