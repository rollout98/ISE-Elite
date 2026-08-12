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
            PrintSurvivableSize(allResults);
            PrintDailyGoalComparison(allResults, 2);
            PrintLiveConfigCheck(allResults);
            PrintDailyStopComparison(allResults, 3);
            PrintSessionBreakdown(allResults, 3);

            int rank = 1;
            foreach (var result in sorted)
            {
                Console.WriteLine($"#{rank}: {result}");

                // Long/short split. A blended figure can hide one side carrying the
                // other, or one side being broken - both matter more than the total.
                // Trades per day. One real trend a day means a healthy config should
                // be in single digits - 72/day was the engine chasing noise.
                var days = result.Trades.Select(t => t.TradingDay).Distinct().Count();
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
        /// <summary>
        /// Prop-firm EOD trailing drawdown limit. Breach it and the account is closed,
        /// so any P&amp;L a config "earns" afterwards is fictional.
        /// </summary>
        private static readonly decimal AccountDrawdownLimit = 2000m;

        /// <summary>
        /// Largest contract count that survives the account limit, per exit mode. This
        /// is the sizing question that actually matters: not what earns most, but what
        /// keeps the worst closing-balance streak inside the threshold.
        /// </summary>
        private void PrintSurvivableSize(List<BacktestResult> results)
        {
            Console.WriteLine($"===== SURVIVABLE SIZE (EOD trailing DD limit ${AccountDrawdownLimit:F0}) =====\n");
            Console.WriteLine("  MODE            MAXSIZE      GROSS   MED/DAY     EOD-DD   HEADROOM");

            string ModeOf(BacktestResult r) =>
                r.Config.HoldToReversal ? "REVERSAL"
                : r.Config.UseTrailingStop ? "TRAIL"
                : $"FIXED {r.Config.StopDistanceRisk * r.Config.AdaptiveRiskMultiplier:F0}pt";

            foreach (var grp in results.GroupBy(ModeOf)
                                       .OrderByDescending(g => g.Where(r => !r.AccountBlown(AccountDrawdownLimit))
                                                                .Select(r => (decimal?)r.GrossProfit).Max() ?? decimal.MinValue)
                                       .Take(8))
            {
                var survivor = grp.Where(r => !r.AccountBlown(AccountDrawdownLimit))
                                  .OrderByDescending(r => r.GrossProfit)
                                  .FirstOrDefault();
                if (survivor == null)
                {
                    Console.WriteLine($"  {grp.Key,-14} {"NONE",7}   - no size survives -");
                    continue;
                }
                Console.WriteLine(
                    $"  {grp.Key,-14} {survivor.Config.MaximumContracts,7} {survivor.GrossProfit,10:F0} " +
                    $"{survivor.MedianDailyPnL,9:F0} {survivor.EodTrailingDrawdown,10:F0} " +
                    $"{AccountDrawdownLimit - survivor.EodTrailingDrawdown,10:F0}");
            }
            Console.WriteLine();
        }

        private void PrintExitModeComparison(List<BacktestResult> results, int contractsToCompare)
        {
            var pool = results.Where(r => r.Config.MaximumContracts == contractsToCompare).ToList();
            if (pool.Count == 0) return;

            string ModeOf(BacktestResult r) =>
                r.Config.HoldToReversal ? "REVERSAL"
                : r.Config.UseTrailingStop ? "TRAIL"
                : $"FIXED {r.Config.StopDistanceRisk * r.Config.AdaptiveRiskMultiplier:F0}pt";

            Console.WriteLine($"===== EXIT MODE COMPARISON (best config per mode, {contractsToCompare} contracts) =====\n");
            Console.WriteLine("  MODE                 GROSS   MED/DAY   AVG/DAY   WIN%  TRADES  >=500     EOD-DD  ALIVE?");

            foreach (var g in pool.GroupBy(ModeOf)
                                  .Select(g => g.OrderByDescending(r => r.GrossProfit).First())
                                  .OrderByDescending(r => r.GrossProfit))
            {
                Console.WriteLine(
                    $"  {ModeOf(g),-14} {g.GrossProfit,11:F0} {g.MedianDailyPnL,9:F0} {g.AvgDailyPnL,9:F0} " +
                    $"{g.WinRate,6:F1} {g.TotalTrades,7} {g.PctDaysAbove(500m),5:F0}% " +
                    $"{g.EodTrailingDrawdown,10:F0}  {(g.AccountBlown(AccountDrawdownLimit) ? "DEAD" : "ok")}");
            }
            Console.WriteLine();
        }


        /// <summary>
        /// Trade-by-trade dump of the worst days for one config. An aggregate says a
        /// day lost $2,400; only the trade list says whether that was one bad trade or
        /// six stop-outs in a row, and those call for different fixes.
        /// </summary>
        public void PrintWorstDays(BacktestResult result, int dayCount = 3)
        {
            if (result.Trades.Count == 0) return;

            var byDay = result.Trades
                .GroupBy(t => t.TradingDay)
                .Select(g => new { Day = g.Key, PnL = g.Sum(t => t.PnL - t.Slippage), Trades = g.OrderBy(t => t.EntryTimeUtc).ToList() })
                .OrderBy(d => d.PnL)
                .Take(dayCount);

            Console.WriteLine($"===== WORST {dayCount} DAYS: {result.Config} =====\n");

            foreach (var d in byDay)
            {
                Console.WriteLine($"  {d.Day:yyyy-MM-dd}  net ${d.PnL:F0}  ({d.Trades.Count} trades)");
                Console.WriteLine("    ENTRY (UTC)        DIR    ENTRY      EXIT    HELD   REASON        P&L   RUNNING");
                decimal running = 0m;
                foreach (var t in d.Trades)
                {
                    running += t.PnL - t.Slippage;
                    var held = t.ExitTimeUtc - t.EntryTimeUtc;
                    Console.WriteLine(
                        $"    {t.EntryTimeUtc:MM-dd HH:mm}  {t.Direction,-5} " +
                        $"{t.EntryPrice,9:F2} {t.ExitPrice,9:F2} {held.TotalMinutes,6:F0}m  " +
                        $"{t.ExitReason,-9} {t.PnL - t.Slippage,10:F0} {running,9:F0}");
                }
                Console.WriteLine();
            }
        }


        /// <summary>
        /// Does stopping at a fixed daily number help? Holds contracts, stop and target
        /// constant and varies ONLY the daily profit halt, so the difference is the rule
        /// and nothing else. Run at 2 contracts because that is what is traded live.
        /// </summary>
        private void PrintDailyGoalComparison(List<BacktestResult> results, int contracts)
        {
            var pool = results.Where(r => r.Config.MaximumContracts == contracts
                                       && !r.Config.HoldToReversal
                                       && !r.Config.UseTrailingStop
                                       && r.Config.BreakevenMovePoints == 0).ToList();
            if (pool.Count == 0) return;

            // Pick the target that does best with no halt, then vary only the halt.
            var baseline = pool.Where(r => r.Config.DailyProfitTargetDollars == 0m)
                               .OrderByDescending(r => r.GrossProfit).FirstOrDefault();
            if (baseline == null) return;

            var stop = baseline.Config.StopDistanceRisk;
            var risk = baseline.Config.AdaptiveRiskMultiplier;
            var hold = baseline.Config.LiquidityCapacity;

            Console.WriteLine($"===== DAILY PROFIT HALT ({contracts} contracts, " +
                              $"{stop:F0}pt stop, {stop * risk:F0}pt target) =====\n");
            Console.WriteLine("  HALT AT      GROSS   MED/DAY   AVG/DAY  DAYS  LOSING   >=500      MAXDD");

            foreach (var r in pool.Where(r => r.Config.StopDistanceRisk == stop
                                           && r.Config.AdaptiveRiskMultiplier == risk
                                           && r.Config.LiquidityCapacity == hold)
                                  .OrderBy(r => r.Config.DailyProfitTargetDollars))
            {
                var label = r.Config.DailyProfitTargetDollars == 0m
                    ? "none" : $"${r.Config.DailyProfitTargetDollars:F0}";
                Console.WriteLine(
                    $"  {label,-8} {r.GrossProfit,10:F0} {r.MedianDailyPnL,9:F0} {r.AvgDailyPnL,9:F0} " +
                    $"{r.TradingDays,5} {r.LosingDays,7} {r.PctDaysAbove(500m),6:F0}% {r.MaxDrawdown,10:F0}");
            }
            Console.WriteLine();
        }


        /// <summary>
        /// Where in the 24h cycle does this instrument actually earn? Splits the best
        /// 3-contract config by entry time in CENTRAL time, which is the clock the
        /// charts use. If gold earns overnight and Nasdaq earns at the NY open, one
        /// account could run gold in Asia and hand off to Nasdaq for London/NY - but
        /// only if the session P&amp;L actually separates that way.
        /// </summary>
        private void PrintSessionBreakdown(List<BacktestResult> results, int contracts)
        {
            var best = results
                .Where(r => r.Config.MaximumContracts == contracts && r.TotalTrades > 20)
                .OrderByDescending(r => r.GrossProfit)
                .FirstOrDefault();
            if (best == null) return;

            // Data covers Jun-Aug, so Central is UTC-5 (CDT) throughout.
            Func<BacktestTrade, int> ctHour = t => (t.EntryTimeUtc.AddHours(-5).Hour + 24) % 24;

            string Session(int h) =>
                (h >= 17 || h < 2) ? "ASIA   (17-02 CT)"
                : h < 7            ? "LONDON (02-07 CT)"
                : h < 15           ? "NY     (07-15 CT)"
                                   : "LATE   (15-17 CT)";

            Console.WriteLine($"===== SESSION BREAKDOWN ({contracts} contracts) =====");
            Console.WriteLine($"  {best.Config}\n");

            Console.WriteLine("  SESSION              TRADES   WIN%        GROSS    AVG/TRADE");
            foreach (var g in best.Trades
                        .GroupBy(t => Session(ctHour(t)))
                        .OrderByDescending(g => g.Sum(t => t.PnL - t.Slippage)))
            {
                var n = g.Count();
                var gross = g.Sum(t => t.PnL - t.Slippage);
                Console.WriteLine($"  {g.Key,-20} {n,6} {g.Count(t => t.IsWin) * 100.0 / n,6:F1} " +
                                  $"{gross,12:F0} {gross / n,12:F0}");
            }

            // DAYS matters as much as TRADES: 10 trades in the 08:00 bucket means ten
            // separate days had an 08:00 entry, not ten trades in one morning. A slot
            // that fires on 10 of 42 days is a thin basis for a sizing decision, and
            // WORST shows what a single bad print in that slot actually costs.
            var totalDays = best.TradingDays;
            Console.WriteLine("\n  HOUR   DAYS  TRADES   WIN%      GROSS  AVG/TRADE      WORST");
            for (int h = 0; h < 24; h++)
            {
                var bucket = best.Trades.Where(t => ctHour(t) == h).ToList();
                if (bucket.Count == 0) continue;
                var days = bucket.Select(t => t.TradingDay).Distinct().Count();
                var gross = bucket.Sum(t => t.PnL - t.Slippage);
                var worst = bucket.Min(t => t.PnL - t.Slippage);
                Console.WriteLine(
                    $"  {h:00}:00  {days,4}/{totalDays,-3} {bucket.Count,5} " +
                    $"{bucket.Count(t => t.IsWin) * 100.0 / bucket.Count,6:F0} " +
                    $"{gross,10:F0} {gross / bucket.Count,10:F0} {worst,10:F0}");
            }
            Console.WriteLine();
        }


        /// <summary>
        /// Does a daily LOSS cap help? Holds everything else constant and varies only
        /// the cap. Note this is the lever that can actually touch max drawdown, unlike
        /// the profit halt - though only partly, since drawdown accumulates across
        /// consecutive losing days and a daily cap bounds just one of them.
        /// </summary>
        private void PrintDailyStopComparison(List<BacktestResult> results, int contracts)
        {
            var pool = results.Where(r => r.Config.MaximumContracts == contracts).ToList();
            if (pool.Count == 0) return;

            var baseline = pool.Where(r => r.Config.DailyLossLimitDollars == 0m)
                               .OrderByDescending(r => r.GrossProfit).FirstOrDefault();
            if (baseline == null) return;

            var peers = pool.Where(r =>
                r.Config.StopDistanceRisk == baseline.Config.StopDistanceRisk &&
                r.Config.AdaptiveRiskMultiplier == baseline.Config.AdaptiveRiskMultiplier &&
                r.Config.LiquidityCapacity == baseline.Config.LiquidityCapacity &&
                r.Config.HoldToReversal == baseline.Config.HoldToReversal &&
                r.Config.UseTrailingStop == baseline.Config.UseTrailingStop &&
                r.Config.BreakevenMovePoints == baseline.Config.BreakevenMovePoints)
                .OrderBy(r => r.Config.DailyLossLimitDollars).ToList();

            Console.WriteLine($"===== DAILY LOSS CAP ({contracts} contracts) =====");
            Console.WriteLine($"  {baseline.Config}\n");
            Console.WriteLine("  CAP AT       GROSS   MED/DAY  LOSING   WORST DAY      MAXDD");
            foreach (var r in peers)
            {
                var label = r.Config.DailyLossLimitDollars == 0m
                    ? "none" : $"${r.Config.DailyLossLimitDollars:F0}";
                Console.WriteLine($"  {label,-8} {r.GrossProfit,10:F0} {r.MedianDailyPnL,9:F0} " +
                                  $"{r.LosingDays,7} {r.WorstDay,11:F0} {r.MaxDrawdown,10:F0}");
            }
            Console.WriteLine();
        }


        /// <summary>
        /// Devon's actual live MNQ setup, at every contract count: 100pt stop (400
        /// ticks), 400pt target (1600 ticks), breakeven armed at 75pt (300 ticks).
        /// Reported on its own because "what does the thing I already trade do" is a
        /// different question from "what is the best config in the sweep", and only
        /// the first one can be checked against real experience.
        /// </summary>
        private void PrintLiveConfigCheck(List<BacktestResult> results)
        {
            var live = results.Where(r =>
                    !r.Config.HoldToReversal && !r.Config.UseTrailingStop &&
                    Math.Abs(r.Config.StopDistanceRisk - 100.0) < 0.01 &&
                    Math.Abs(r.Config.AdaptiveRiskMultiplier - 4.0) < 0.01 &&
                    Math.Abs(r.Config.BreakevenMovePoints - 75.0) < 0.01 &&
                    r.Config.DailyLossLimitDollars == 0m)
                .OrderBy(r => r.Config.MaximumContracts)
                .ToList();
            if (live.Count == 0) return;

            Console.WriteLine("===== LIVE CONFIG: 100pt stop / 400pt target / BE at 75pt =====\n");
            Console.WriteLine("  PAEXIT  SIZE      GROSS   MED/DAY   WIN%  TRADES  >=500     EOD-DD  ALIVE?");
            foreach (var r in live.OrderBy(r => r.Config.UsePaExit)
                                  .ThenBy(r => r.Config.MaximumContracts))
            {
                if (r.Config.MaximumContracts > 4) continue;  // keep the table readable
                Console.WriteLine(
                    $"  {(r.Config.UsePaExit ? "ON " : "off"),-6} {r.Config.MaximumContracts,4} " +
                    $"{r.GrossProfit,10:F0} {r.MedianDailyPnL,9:F0} " +
                    $"{r.WinRate,6:F1} {r.TotalTrades,7} {r.PctDaysAbove(500m),5:F0}% " +
                    $"{r.EodTrailingDrawdown,10:F0}  {(r.AccountBlown(AccountDrawdownLimit) ? "DEAD" : "ok")}");
            }
            Console.WriteLine();
        }

    }
}
