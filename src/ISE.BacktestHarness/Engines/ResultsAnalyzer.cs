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
                writer.WriteLine("Rank,ConfigId,MaxContracts,ProfitFloor,StopDist,MaxHoldBars,TrendFilter,ExitMode," +
                                "GrossProfit,ReturnPct,TotalTrades,WinRate,WinTrades,LossTrades," +
                                "TradingDays,AvgDaily,MedianDaily,BestDay,WorstDay,LosingDays,PctDays500,PctDays1000," +
                                "AvgPnL,LargestWin,LargestLoss,MaxDD,ProfitFactor,Sharpe,Score");

                // Rows
                int rank = 1;
                foreach (var result in sorted)
                {
                    var exitMode = result.Config.HoldToReversal ? "REVERSAL"
                                 : result.Config.UseTrailingStop ? "TRAIL"
                                 : "FIXED";

                    writer.WriteLine(
                        $"{rank}," +
                        $"{result.Config.ConfigId}," +
                        $"{result.Config.MaximumContracts}," +
                        $"{result.Config.ProfitFloorDollars:F0}," +
                        $"{result.Config.StopDistanceRisk:F2}," +
                        $"{result.Config.LiquidityCapacity:F0}," +
                        $"{result.Config.TrendFilterBars}," +
                        $"{exitMode}," +
                        $"{result.GrossProfit:F2}," +
                        $"{result.ReturnPercent:F2}," +
                        $"{result.TotalTrades}," +
                        $"{result.WinRate:F1}," +
                        $"{result.WinningTrades}," +
                        $"{result.LosingTrades}," +
                        $"{result.TradingDays}," +
                        $"{result.AvgDailyPnL:F2}," +
                        $"{result.MedianDailyPnL:F2}," +
                        $"{result.BestDay:F2}," +
                        $"{result.WorstDay:F2}," +
                        $"{result.LosingDays}," +
                        $"{result.PctDaysAbove(500m):F1}," +
                        $"{result.PctDaysAbove(1000m):F1}," +
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
            var allResults = results.ToList();
            var sorted = allResults
                .OrderByDescending(r => CalculateCompositeScore(r))
                .Take(topN)
                .ToList();

            Console.WriteLine($"\n========== TOP {topN} CONFIGURATIONS ==========\n");

            // Exit-mode head-to-head, held at a fixed contract count so the comparison
            // is not just "whichever mode happened to get swept at 10 contracts wins".
            // Answers the only question that matters here: does holding to the reversal
            // actually beat a fixed target on the same data?
            PrintExitModeComparison(allResults, 3);

            int rank = 1;
            foreach (var result in sorted)
            {
                Console.WriteLine($"#{rank}: {result}");

                // Long/short split. A blended figure can hide one side carrying the
                // other, or one side being broken - both matter more than the total.
                // Trades per day. One real trend a day means a healthy config should
                // be in single digits - 72/day was the engine chasing noise.
                var days = result.Trades.Select(t => t.EntryTimeUtc.Date).Distinct().Count();
                var perDay = days > 0 ? result.Trades.Count / (double)days : 0;
                Console.WriteLine($"     {perDay,6:F1} trades/day over {days} days");

                // Daily distribution. The mean on its own hides the shape: a $900
                // average can be five $300 days and one $3,900 day. Median, worst day
                // and the share of days clearing the target say whether the number is
                // dependable or just one good session doing all the work.
                Console.WriteLine(
                    $"     DAILY  avg ${result.AvgDailyPnL,9:F0} | med ${result.MedianDailyPnL,9:F0} | " +
                    $"best ${result.BestDay,9:F0} | worst ${result.WorstDay,9:F0}");
                Console.WriteLine(
                    $"     DAYS   {result.TradingDays,3} total | {result.LosingDays,3} losing | " +
                    $"{result.PctDaysAbove(500m),5:F0}% >= $500 | {result.PctDaysAbove(1000m),5:F0}% >= $1000");

                // What actually closed the trades. A config that looks like
                // hold-to-reversal but exits mostly on TIMECAP is a time-exit strategy
                // wearing the wrong label, and its numbers say nothing about reversals.
                var byReason = result.Trades
                    .GroupBy(t => t.ExitReason)
                    .OrderByDescending(g => g.Count())
                    .Select(g => $"{g.Key} {g.Count()} (${g.Sum(t => t.PnL - t.Slippage):F0})");
                Console.WriteLine($"     EXITS  {string.Join(" | ", byReason)}");

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

        /// <summary>
        /// Best config per exit mode at one contract count. Sorting the whole sweep by
        /// profit would just rank by position size, so size is pinned and only the exit
        /// rule varies.
        /// </summary>
        private void PrintExitModeComparison(List<BacktestResult> results, int contractsToCompare)
        {
            var pool = results.Where(r => r.Config.MaximumContracts == contractsToCompare).ToList();
            if (pool.Count == 0) return;

            string ModeOf(BacktestResult r) =>
                r.Config.HoldToReversal ? "REVERSAL"
                : r.Config.UseTrailingStop ? "TRAIL"
                : $"FIXED {r.Config.StopDistanceRisk * r.Config.AdaptiveRiskMultiplier:F0}pt";

            Console.WriteLine($"===== EXIT MODE COMPARISON (best config per mode, {contractsToCompare} contracts) =====\n");
            Console.WriteLine("  MODE                 GROSS   MED/DAY   AVG/DAY   WIN%  TRADES  >=500      MAXDD");

            foreach (var g in pool.GroupBy(ModeOf)
                                  .Select(g => g.OrderByDescending(r => r.GrossProfit).First())
                                  .OrderByDescending(r => r.GrossProfit))
            {
                Console.WriteLine(
                    $"  {ModeOf(g),-14} {g.GrossProfit,11:F0} {g.MedianDailyPnL,9:F0} {g.AvgDailyPnL,9:F0} " +
                    $"{g.WinRate,6:F1} {g.TotalTrades,7} {g.PctDaysAbove(500m),5:F0}% {g.MaxDrawdown,10:F0}");
            }
            Console.WriteLine();
        }

    }
}
