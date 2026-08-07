# New York 08:45 Transition and Causal Entry Study

## Purpose

This research separates **market-state detection** from **trade entry**. The 08:45 transition layer classifies the opening state as `Continue`, `Reverse`, or `StandAside` using completed one-minute bars only. The causal-entry layer then decides whether a usable entry has actually formed.

This is Historical Research / Lab code only. It does not place orders and is not a production entry rule.

## Transition layer

The opening observation window is 08:30-08:45 Central. From 08:45-09:05 the transition detector evaluates completed bars and retains the first qualifying state:

- `Continue`: opening direction remains structurally active;
- `Reverse`: opening structure materially retraces and crosses the midpoint buffer;
- `StandAside`: neither state is sufficiently resolved.

The first causal study showed that using the next bar after the state signal as an entry was too aggressive, especially for `Continue`. Therefore the state signal is no longer treated as an entry signal.

## Causal entry layer

`NewYorkCausalEntryAnalyzer` searches only after the transition signal and uses completed bars through 09:20 Central. Any standardized research entry occurs at the **next one-minute bar open after setup completion**.

### Continue

A continuation state must first experience a measurable reset/pullback of at least `0.20x` the 08:30-08:45 opening range from the post-signal favorable extreme. After that reset has occurred, continuation setup completion requires a one-minute close through the prior bar's high for Long or through the prior bar's low for Short.

This deliberately prevents `Continue` from meaning "chase the next bar."

### Reverse

A reversal state requires completed-bar confirmation in the reversal direction. The initial research seed requires one bar to close through the prior bar's high for Long or low for Short. The standardized entry reference is the next one-minute bar open.

### StandAside

`StandAside` creates no entry in this study. It means "not yet," not "no trade for the day." A later fresh-cycle detector will handle unresolved mornings and the 09:30 transition.

## Outcome measurement

After a causal reference entry, the study measures through 09:30 Central:

- favorable and adverse excursion in points/ticks;
- whether $500 becomes available with two MNQ contracts;
- whether $1,000 becomes available with two MNQ contracts;
- first target-hit timestamps.

The default research economics remain MNQ tick size `0.25`, point value `$2.00` per contract, and two contracts. Costs and stop execution are not yet included.

## Guardrails

- no future bars are used to decide the transition state or setup completion;
- setup completion and entry are separated by one bar: entry is always the next bar open;
- thresholds are transparent seed values and must not be tuned solely to improve this 42-session sample;
- target availability is not executable P&L;
- no production stops, sizing changes, or account governance are defined here.

## Run

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.CausalEntryStudy -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

## Validation question

Compare the causal-entry output with the earlier immediate-next-bar transition study. The next gate is evidence that waiting for reset completion materially improves adverse excursion and target availability without eliminating too many valid opportunities. If it does, the following research step is a causal 09:30 fresh-cycle detector plus realistic stop/cost and cumulative daily-objective simulation.
