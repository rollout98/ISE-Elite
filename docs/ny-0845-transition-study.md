# ISE Elite 08:45 Continue / Reverse / Stand-Aside Study

## Purpose

This Historical Research / Lab study tests the next causal step after the multi-cycle opportunity-envelope analysis. It asks whether ISE can use only information available on completed one-minute bars to decide whether the 08:30 opening move is continuing, reversing, or not sufficiently resolved to trade.

The study does not place orders and is not a production strategy.

## Decision model

- 08:30-08:45 CT: opening observation period.
- 08:45-09:05 CT: causal transition-detection period.
- 09:30 CT: end of the first outcome-measurement period.

The detector first measures opening direction, opening range, opening displacement, and opening efficiency. If the opening is not directional enough, the session remains `StandAside`.

During 08:45-09:05, each completed one-minute bar is evaluated chronologically. The first qualifying state is retained:

- `Continue`: the opening direction extends beyond the opening extreme by the configured fraction of opening range.
- `Reverse`: price retraces a configured fraction of opening displacement and crosses beyond the opening-range midpoint buffer in the opposite direction.
- `StandAside`: neither condition is confirmed by 09:05.

A qualifying signal never uses future bars. The research entry reference is the **next one-minute bar open after the signal bar**. Favorable/adverse excursion and $500/$1,000 target availability are then measured from that reference entry through 09:30 CT.

## Default seed thresholds

The first transparent defaults are intentionally research seeds, not tuned production parameters:

- minimum opening efficiency: 0.35
- continuation extension: 0.15x opening range
- reversal retracement: 0.50x opening displacement
- reversal midpoint buffer: 0.05x opening range
- MNQ tick size: 0.25
- MNQ point value: $2.00 per contract
- contracts: 2
- lower objective: $500
- upper objective: $1,000

Do not tune these thresholds on the 42-session development sample merely to improve the reported results.

## Run

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.EightFortyFiveTransitionStudy -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

The CLI prints overall Continue / Reverse / StandAside counts, target availability, average favorable/adverse ticks by state, and one row per session with signal time, next-bar reference entry, and first $500/$1,000 hit times.

## Interpretation guardrails

- This is causal state labeling but still not executable P&L.
- The reference entry is standardized research behavior, not a final production entry rule.
- No stop, commission, slippage, account governance, or daily lockout is modeled here.
- The 08:45 clock time is a reevaluation point, not a mandatory reversal.
- Same-direction continuation after a pause can still become a fresh trade cycle later; direction alone does not define trade identity.
- The 40/80 protected-fill harness remains execution/safety validation only.

## Validation gate

1. Historical Research tests must pass at the latest branch head.
2. Full Windows solution build must remain 0 warnings / 0 errors.
3. Run this study over all 42 validated NY sessions.
4. Compare Continue, Reverse, and StandAside frequencies and target availability.
5. Inspect signal-time clustering within 08:45-09:05.
6. Only after reviewing these outputs should realistic pullback completion, entry qualification, stop placement, and cumulative two-trade daily-goal logic be proposed.
