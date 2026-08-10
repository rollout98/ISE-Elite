# Backtest Runner Integration Guide

## Quick Start

The backtest harness is **built and ready**. To run it:

```bash
cd C:\Users\dlewi\Documents\ISE-Elite

# Run the backtest console app
dotnet run --project tools/BacktestConsole.csproj
```

## Expected Output

```
ISE-Elite Backtest Console
==========================

Step 1: Loading historical bars from NinjaTrader...
✅ Loaded 300,000 bars
   Date range: 2024-03-01T09:30:00 to 2024-09-01T16:00:00

Step 2: Running backtest with 420 parameter configurations...
========================================
  ISE-Elite Backtest Orchestrator
========================================
Account Size: $50,000.00
Historical Bars: 300,000
Output Directory: ./backtest-results

STEP 1: Generating parameter configurations...
✅ Generated 420 configurations in 23ms

STEP 2: Running backtests...
  [50/420] ETA: 45s (107ms per config)
  [100/420] ETA: 34s (105ms per config)
  ...
✅ Backtests completed in 45.2s

STEP 3: Analyzing results...

========== TOP 20 CONFIGURATIONS ==========

#1: Config1: Contracts=2, RiskMult=1.00, StopDist=20.00, Liquidity=100%  | 
    Profit: $12,345.67 (24.69%) | Trades: 127 (68.5% win) | DD: $567.89 | Sharpe: 1.85

#2: Config2: Contracts=3, RiskMult=1.25, StopDist=15.00, Liquidity=100%  | 
    Profit: $11,234.56 (22.47%) | Trades: 142 (66.2% win) | DD: $623.45 | Sharpe: 1.72

...
✅ Backtest Complete!
Results: ./backtest-results/backtest_results.csv
```

## CSV Output

File: `./backtest-results/backtest_results.csv`

Columns:
- `Rank`: 1-420
- `ConfigId`: Parameter set identifier
- `MaxContracts`: 1-4
- `RiskMult`: 0.5-2.0
- `StopDist`: 10-30 ticks
- `Liquidity`: 50%, 100%, 150%
- `GrossProfit`: Dollar P&L
- `ReturnPct`: Percent return on $50k
- `TotalTrades`: Number of trades
- `WinRate`: % of winning trades
- `WinTrades`, `LossTrades`: Counts
- `AvgPnL`: Average trade profit
- `LargestWin`, `LargestLoss`: Trade extremes
- `MaxDD`: Peak-to-trough drawdown
- `ProfitFactor`: Wins / Losses ratio
- `Sharpe`: Risk-adjusted return
- `Score`: Composite ranking (higher = better)

## Integration Steps

### Step 1: Implement LoadHistoricalBars()

Edit: `tools/BacktestConsole.cs`

Replace the TODO placeholder with actual NT8 probe integration:

```csharp
private static List<HistoricalBar> LoadHistoricalBars()
{
    // Create probe instance
    var probe = new ISEEliteHistoricalBarsRequestProbe();
    var allBars = new List<HistoricalBar>();

    // Load MNQ 1-minute bars (6 months)
    Console.WriteLine("   Loading MNQ 1-minute bars...");
    var mnq1min = probe.RequestBars(
        instrument: "MNQ",
        startDate: new DateTime(2024, 3, 1),
        endDate: new DateTime(2024, 9, 1),
        intervalSeconds: 60);
    allBars.AddRange(mnq1min);

    // Load MNQ 5-minute bars (6 months)
    Console.WriteLine("   Loading MNQ 5-minute bars...");
    var mnq5min = probe.RequestBars(
        instrument: "MNQ",
        startDate: new DateTime(2024, 3, 1),
        endDate: new DateTime(2024, 9, 1),
        intervalSeconds: 300);
    allBars.AddRange(mnq5min);

    // Load MGC 1-minute bars (6 months)
    Console.WriteLine("   Loading MGC 1-minute bars...");
    var mgc1min = probe.RequestBars(
        instrument: "MGC",
        startDate: new DateTime(2024, 3, 1),
        endDate: new DateTime(2024, 9, 1),
        intervalSeconds: 60);
    allBars.AddRange(mgc1min);

    // Load MGC 5-minute bars (6 months)
    Console.WriteLine("   Loading MGC 5-minute bars...");
    var mgc5min = probe.RequestBars(
        instrument: "MGC",
        startDate: new DateTime(2024, 3, 1),
        endDate: new DateTime(2024, 9, 1),
        intervalSeconds: 300);
    allBars.AddRange(mgc5min);

    // Filter to NY session (9:30-16:00 CT)
    Console.WriteLine("   Filtering to NY session...");
    var extractor = new NewYorkSessionDatasetExtractor();
    var nyBars = extractor.FilterToNySession(allBars);

    return nyBars;
}
```

### Step 2: Verify NT8 Connection

Before running, ensure:
- NinjaTrader 8 is running
- ISEEliteHistoricalBarsRequestProbe is accessible
- Historical data for MNQ/MGC is available for 2024-03-01 to 2024-09-01

### Step 3: Run the Backtest

```bash
dotnet run --project tools/BacktestConsole.csproj
```

Expected runtime: **~1 minute** for 300k bars + 420 configs

### Step 4: Analyze Results

Open: `./backtest-results/backtest_results.csv`

Look for configs with:
- ✅ Win rate > 60%
- ✅ Sharpe > 1.2
- ✅ Max DD < $1,000
- ✅ Return > 15%

## Architecture

```
BacktestConsole (main entry point)
  └─ LoadHistoricalBars()
     ├─ ISEEliteHistoricalBarsRequestProbe (NT8 data)
     ├─ NewYorkSessionDatasetExtractor (filter NY hours)
     └─ Returns: List<HistoricalBar>

  └─ BacktestOrchestrator.Run(bars)
     ├─ ConfigurationSweeper (generates 420 combos)
     ├─ BacktestExecutionEngine (runs each config)
     │  ├─ IntegratedTradingBrain (generates signals)
     │  ├─ Position tracking (entry/exit/P&L)
     │  ├─ Equity/DD calculation
     │  └─ Returns: BacktestResult
     ├─ ResultsAnalyzer (ranks by composite score)
     └─ CSV export (./backtest-results/backtest_results.csv)
```

## Troubleshooting

**Issue:** "No bars loaded"
- Solution: Verify NT8 is running and probe is accessible
- Check date range (2024-03-01 to 2024-09-01)
- Ensure MNQ/MGC historical data exists

**Issue:** Slow backtest (>5 minutes)
- Solution: Reduce data range (e.g., 3 months instead of 6)
- Reduce config count (modify ConfigurationSweeper)
- Use 5-minute bars only (remove 1-minute)

**Issue:** Low win rate (<40%)
- Solution: Check TradingBrain signal logic
- May indicate system needs parameter tuning
- Backtest results inform what to adjust

## Next Steps

1. ✅ Implement `LoadHistoricalBars()`
2. ✅ Run backtest
3. ✅ Analyze CSV for best configs
4. ✅ Test top 3 configs in Sim101 (paper trade)
5. ✅ Go-live decision based on Sim101 results

## Questions?

- Check `BacktestConsole.cs` for usage template
- Review `ISE.BacktestHarness/` module classes
- See Git history: `git log --oneline | grep -i backtest`
