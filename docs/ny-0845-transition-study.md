# ISE Elite 08:45 Continue / Reverse / Stand-Aside Study

## Purpose

This Historical Research / Lab study tests whether ISE can use only completed one-minute bars to decide whether the 08:30 opening move is continuing, reversing, or not sufficiently resolved to trade, and then qualify a separate causal entry.

The study does not place orders and is not a production strategy.

## Current architecture

- 08:30-08:45 CT: opening observation period.
- 08:45-09:05 CT: causal transition detection.
- State detection and trade entry are separate layers.
- 09:30 CT: end of the first tradeability outcome window.

The transition layer classifies `Continue`, `Reverse`, or `StandAside`. `StandAside` means not yet, not no trade for the day.

## Evidence so far

The first immediate-next-bar transition study showed that state recognition alone was not sufficient for entry. The first reset-based entry study improved reversal quality but continuation entries still had excessive adverse excursion. A structural midpoint/swing filter reduced continuation adverse excursion only modestly and invalidated just one continuation in the 42-session sample.

The research therefore now evaluates the pullback itself and records target-vs-risk sequencing.

## Tradeable-entry study

`NewYorkTradeableEntryAnalyzer` uses transparent, untuned research seeds:

- minimum continuation reset: `0.20x` opening range;
- maximum continuation reset: `0.60x` opening range;
- confirmed local pullback pivot: one bar on each side;
- structural resumption: close through a two-bar micro-swing in the original direction;
- reversal confirmation: one completed bar;
- structural stop: pullback/reversal structure plus one tick buffer;
- MNQ tick size: `0.25`;
- MNQ point value: `$2.00` per contract;
- contracts: `2`;
- lower objective: `$500`;
- upper objective: `$1,000`.

These values are starting research hypotheses and must not be tuned simply to maximize the 42-session development sample.

## Continuation logic

For a `Continue` transition:

1. measure the post-signal favorable extreme;
2. require a pullback between the minimum and maximum reset fractions;
3. require a confirmed local pivot;
4. require structural resumption through the configured micro-swing;
5. use the next one-minute bar open as the standardized entry reference;
6. place the research structural stop beyond the confirmed pivot by the configured tick buffer.

If the reset exceeds the maximum allowed depth before continuation qualifies, continuation is invalidated.

## Continuation-to-reversal handoff

A destructive continuation reset does not end the session. The same session is handed to reversal confirmation in the opposite direction. If reversal confirms causally, the next one-minute bar open becomes a `ContinuationFailureReversal` research entry.

This is intended to model the observed sequence:

`opening impulse -> failed continuation/reset -> structural reversal -> fresh trade opportunity`.

## Direct reversal logic

A transition already classified as `Reverse` retains completed-bar confirmation in the reversal direction. The research stop is placed beyond the post-transition reversal structure with one tick of buffer.

## Target-vs-risk sequencing

After every qualified entry, the study records what happens first through 09:30 CT:

- structural stop;
- `$500` objective;
- `$1,000` objective;
- timeout.

If a one-minute bar touches both stop and target and intrabar order cannot be known, the study resolves the bar conservatively as **stop-first**.

This sequence analysis is more useful than raw MFE/MAE alone because it tests whether the favorable movement was available before the structural risk was violated.

## Run

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.TradeableEntryStudy -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

The CLI reports:

- total entries;
- validated continuation entries;
- direct reversals;
- continuation-failure reversal handoffs;
- invalidated continuations;
- stop-first / lower-objective-first / upper-objective-first / timed-out sequences;
- average initial structural risk in ticks;
- average validated reset fraction;
- one row per session with pivot, entry, invalidation, stop, and target timing.

## Guardrails

- all state, pivot, invalidation, resumption, and reversal decisions use completed bars only;
- every entry reference occurs on the next bar open after setup completion;
- same-bar stop/target ambiguity is handled conservatively;
- target sequencing remains Historical Research evidence, not production P&L;
- no commission/slippage or account-governance rules are applied in this pass;
- the 40/80 protected-fill harness remains execution/safety validation only;
- do not tune this development sample to manufacture higher hit rates.

## Validation gate

1. Historical Research tests pass at the latest branch head.
2. Full Windows solution build remains 0 warnings / 0 errors.
3. Run `ISE.HistoricalResearch.TradeableEntryStudy` over all 42 validated NY sessions.
4. Compare trade count, initial structural risk, stop-first frequency, and target-before-stop frequency by entry type.
5. Determine whether bounded-reset continuation materially improves tradeability and whether continuation-failure reversal handoff recovers useful opportunities.
6. Keep PR #79 draft until this distribution is reviewed.
7. Only then add the causal 09:30 fresh-cycle detector and cumulative daily-objective governance.
