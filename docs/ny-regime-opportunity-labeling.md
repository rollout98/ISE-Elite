# ISE Elite New York Regime, Opportunity, and Outcome Research

## Purpose

This phase turns the validated 06:00-11:00 Central MNQ historical dataset into deterministic research features, seed regime classifications, candidate opportunity labels, and forward-path outcome evidence. It is Historical Research / Lab code only. It does not place orders, size positions, manage accounts, or define production entry rules.

The seed classifier is intentionally transparent and configurable. Its job is to organize historical sessions for research and manual review, not to assert that a regime or opportunity is trade-qualified.

## Research regimes

The initial taxonomy follows the approved research plan:

1. `OpeningDrive`
2. `EarlyReversal`
3. `DeepPullbackContinuation`
4. `VolatileTwoSidedAuction`
5. `RangeNoTrade`
6. `LaterContinuationReversal`
7. `Unclassified` for sessions that do not satisfy a seed rule

`Unclassified` is retained deliberately so the research process does not force every session into a category.

## Research windows

All windows are U.S. Central time and are features/label windows, not production trading permissions:

- 06:00-08:30: pre-open context
- 08:30-09:05: opening behavior
- 09:05-09:30: early pullback/reversal behavior
- 09:30-10:00: later continuation/reversal behavior
- 08:30-10:00: core-morning range/efficiency context
- the source dataset remains 06:00-11:00 so outcome research can measure the forward path through the later morning

## Extracted session features

`NewYorkSessionResearchFeatureExtractor` calculates deterministic OHLC-derived features per Central session date while allowing explicit contract changes across dates:

- pre-open range
- opening range
- opening displacement
- opening directional efficiency
- early displacement
- early adverse excursion from the opening close
- later displacement
- core-morning range
- core-morning displacement
- core directional efficiency

The extractor requires one instrument and one bar interval but permits the explicit `MNQ 06-26` -> `MNQ 09-26` contract transition already validated in PR #78.

## Seed classifier

`NewYorkRegimeSeedClassifier` applies explicit configurable thresholds to the extracted features. Thresholds are normalized to observed range/efficiency where practical rather than being tied to a fixed 40/80 execution harness.

The classification score is a research-ranking aid only. It is not a probability of profit, confidence promise, or production signal strength.

## Opportunity seeds

`NewYorkOpportunitySeedLabeler` creates candidate research labels only for directional regimes:

- opening-drive continuation
- early reversal
- deep-pullback continuation
- later continuation
- later reversal

`RangeNoTrade`, `VolatileTwoSidedAuction`, and `Unclassified` do not automatically create directional opportunity seeds in this pass.

The first supervised 42-session run produced 17 directional seeds. These seeds are still not counted toward the 150+ qualified-trade gate.

## Outcome ledger

`NewYorkOpportunityOutcomeLabeler` converts each directional seed into a deterministic forward-path research row. The entry reference is the open of the first one-minute bar at or after the seed window start, provided that bar still lies inside the seed window. The forward path is measured through 11:00 Central.

Each outcome records:

- entry timestamp and reference price
- MFE and MAE in points and ticks
- minutes to first MFE and first MAE extrema
- directional move at the end of the opportunity window
- directional move at the end of the 11:00 research session
- gross per-contract session-end P&L using configurable point value
- optional after-cost session-end P&L using configurable round-trip commission and slippage assumptions
- favorable and adverse excursion as multiples of that session's opening range
- whether price reached 0.5x, 1.0x, and 1.5x opening range favorably
- whether the opportunity window closed favorable
- whether the 11:00 research session closed favorable
- a research-only runner-candidate flag when favorable excursion reaches at least 1.5x opening range and the opportunity window itself closes favorable

The runner-candidate flag is descriptive evidence, not a production hold rule. Likewise, session-end P&L is not a proposed exit policy; it is one standardized comparison point for research.

The default MNQ research economics are tick size `0.25` and point value `$2.00`. Commission and slippage default to zero and must be supplied explicitly when after-cost analysis is desired. This avoids silently inventing brokerage costs.

## Dataset runs

Run the regime labeler against the validated PR #78 TSV:

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.RegimeLabeler -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

Run the outcome ledger with zero assumed transaction costs:

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.OutcomeLedger -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

Optional arguments are `roundTripCommission`, `slippageTicksPerSide`, and `pointValue`, for example:

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.OutcomeLedger -- "<tsv>" 1.20 1 2.00
```

The outcome CLI prints aggregate counts by opportunity type and one row per seed with MFE/MAE, time-to-extrema, window/session directional moves, standardized per-contract P&L, opening-range multiples, and runner evidence.

## Research guardrails

- the 40/80 protected-fill harness remains execution/safety validation only and is not an optimization target
- do not tune seed thresholds merely to increase the count of opportunities in this 42-session sample
- do not promote runner-candidate evidence into production management rules without Development / Validation / OutOfSample evidence
- do not treat gross or session-end P&L as the production exit objective
- preserve `Unclassified` and no-trade outcomes rather than forcing labels

## Next gate

After the outcome ledger is reviewed on the real 17 seeds:

1. inspect MFE/MAE and runner evidence by opportunity type;
2. add explicit structure-event timestamps and structure-aware exit candidates only where supported by the data;
3. allow multiple candidate opportunities per session where the market genuinely presents them;
4. persist the research trade ledger for larger historical ranges;
5. expand the historical date range until there are at least 150 qualified NY opportunities;
6. partition Development / Validation / OutOfSample before tuning production decision rules.
