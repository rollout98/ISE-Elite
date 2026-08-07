# ISE Elite New York Regime and Opportunity Seed Labeling

## Purpose

This phase turns the validated 06:00-11:00 Central MNQ historical dataset into deterministic research features, seed regime classifications, and candidate opportunity labels. It is Historical Research / Lab code only. It does not place orders, size positions, manage accounts, or define production entry rules.

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
- the source dataset remains 06:00-11:00 so later feature work can use the additional hour

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

`RangeNoTrade`, `VolatileTwoSidedAuction`, and `Unclassified` do not automatically create directional opportunity seeds in this first pass.

These labels are not yet counted toward the 150+ qualified-trade gate. Qualification requires outcome labeling, MFE/MAE, structure-aware exit analysis, after-cost P&L, OOS/walk-forward evidence, and governance review.

## Dataset run

Run the labeler against the validated PR #78 TSV:

```powershell
dotnet run --project .\tools\ISE.HistoricalResearch.RegimeLabeler -- "C:\Users\dlewi\OneDrive\Documents\NinjaTrader 8\ISEEliteResearch\ny-MNQ-contract-aware-20260601-20260731-0600-1100-60s-repository.tsv"
```

The output includes:

- total bars and sessions
- regime counts and average seed scores
- opportunity-seed counts and average seed scores
- one per-session feature/classification line for review

The first real dataset run is used to inspect distribution and identify whether thresholds are over-classifying, under-classifying, or collapsing too many days into one bucket. Threshold refinement must be documented and must not be optimized against the protected-fill 40/80 harness.

## Next gate

After this seed distribution is reviewed:

1. add outcome labeling with forward-path MFE/MAE and structure events;
2. allow multiple candidate opportunities per session where the market genuinely presents them;
3. create the research trade ledger;
4. expand the historical date range until there are at least 150 qualified NY opportunities;
5. partition Development / Validation / OutOfSample before tuning production decision rules.
