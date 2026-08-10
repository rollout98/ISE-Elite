# Mean Reversion Scalper System

## Overview
Quick-scalping system that trades mean reversion off 2-bar extremes. Opposite of trend following - exploits choppy/ranging market conditions.

## Signal Logic

### Entry Conditions
**SHORT Signal:**
- Current bar closes BELOW the low of the previous 2 bars
- Current bar volume > 120% of 5-bar average volume
- Entry price: Current bar close

**LONG Signal:**
- Current bar closes ABOVE the high of the previous 2 bars
- Current bar volume > 120% of 5-bar average volume
- Entry price: Current bar close

### Exit Conditions
**Tight Scalp Exits:**
- Target: +0.5 points profit (whichever comes first)
- Stop: -0.5 points loss
- Max hold: 8 bars (5 minutes on 1-min chart)

## Performance Targets
| Metric | Target |
|--------|--------|
| Win Rate | 65-70% |
| Avg Win | $10-15/contract |
| Avg Loss | $10-15/contract |
| Daily P&L | $150-250 |
| Sharpe Ratio | 1.5+ |

## Market Regime
**Best Performance:** Choppy/ranging markets (ADX < 25)
**Worst Performance:** Strong trending markets
**Optimal Times:** Mid-day hours (11 AM - 2 PM CT) when ranges widen

## Code Structure
- `MeanReversionSignal.cs` - Core signal logic
- `MeanReversionTester.cs` - Backtest engine

## Testing Status
- [x] Signal logic built
- [x] Backtest engine built
- [ ] Historical backtest (pending)
- [ ] Live chart test (pending)
- [ ] Time-of-day validation (pending)

## Next Steps
1. Run backtest on 5,000 bars of MNQ data
2. Validate win rate meets 65%+ target
3. Test across different market times (8:30 AM, 10 AM, 2 PM opens)
4. Add to NinjaTrader (parallel with Trend Follower)
5. Live test on Sim101
