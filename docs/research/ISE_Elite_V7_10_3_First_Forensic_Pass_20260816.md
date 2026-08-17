# ISE Elite V7.10.3 — First Forensic Pass (2026-08-16 Overnight Capture)

**Status:** Research only  
**Branch:** `research/full-session-scalp-engine-v7-9`  
**Production merge:** Forbidden without separate validation and explicit approval  

## Capture integrity

Primary MNQ instance: `49ed3580`  
Primary MGC instance: `87a4fe04`

Both primary recorder instances completed the overnight endurance capture with zero dropped records and zero writer errors. V7.10.2 is therefore considered sufficient for current research use.

## Canonical timestamp rule

For V7.10.3 analysis, the first TSV field — recorder capture UTC timestamp — is canonical. The second ATAS event-time field showed an approximately five-hour normalization offset in V7.10.2 R3 and is not used as the research clock until corrected in a later recorder revision.

## Condition 1 — MGC Asia Opening Expansion

User-labeled successful trade example: early MGC Asia opening long, approximately 7:10–7:15 PM CT on the 2-minute Vector Flow chart, followed by a large directional expansion. The primary MGC MBO recorder began at approximately 9:11 PM CT, so this specific entry does **not** have MBO evidence in the 2026-08-16 capture. It remains a structural/Vector Flow labeled case and establishes the requirement that future MGC collection begin before 7:00 PM CT.

## Condition 2 — MNQ Late Trend Formation

MNQ remained comparatively rotational through the earlier evening and later produced a user-identified valid long transition around the 9:10 PM CT area. The primary MNQ MBO recorder was already active, so this case has full raw MBO/trade/BBO evidence.

Initial minute-level segmentation using recorder capture time:

### Pre-transition / rotation — 20:45–21:08 CT

- Net price: **-9.00 MNQ points**
- Full-window range: **17.00 points**
- Aggressive trade delta: **-595**
- New-order imbalance (`NewBidQty - NewAskQty`): **-9,860**
- Delete imbalance (`DeleteAskQty - DeleteBidQty`): **+9,617**
- Exploratory pressure proxy: **-619.3**

### Trend forming — 21:08–21:21 CT

- Net price: **+12.00 MNQ points**
- Full-window range: **15.50 points**
- Aggressive trade delta: **+719**
- New-order imbalance: **+5,171**
- Delete imbalance: **-5,424**
- Exploratory pressure proxy: **+693.7**

### Expansion burst — 21:21–21:24 CT

- Net price: **+19.25 MNQ points**
- Full-window range: **20.88 points**
- Aggressive trade delta: **+580**
- New-order imbalance: **+3,988**
- Delete imbalance: **-3,895**
- Exploratory pressure proxy: **+589.3**

## Early interpretation

This first case contains a measurable shift from a negative/rotational pre-transition window into positive aggressive-trade flow and positive new-order imbalance before the large 21:21 CT expansion burst. That is consistent with the proposed `ROTATION -> TREND FORMING -> EXPANSION` state sequence.

This result is **not yet an edge** and must not be promoted to live logic. The pressure proxy is an exploratory research feature only. The next task is to decompose the transition into shorter event episodes and test the same signatures on additional untouched sessions.

## V7.10.3 R1 outputs

The new analyzer `tools/research/v7_10_3_forensic_analyzer.py` produces:

- `session_inventory.csv`
- `MNQ_minute_features.csv`
- `MGC_minute_features.csv`
- `condition2_mnq_2045_2135.csv`
- `V7_10_3_INITIAL_REPORT.md`

## Next research steps

1. Build 1-second / 5-second / 10-second event episodes around the MNQ Condition 2 transition.
2. Separate MBO `New`, `Change`, and `Delete` behavior by side and price distance from BBO.
3. Link aggressive trades to passive/aggressor order IDs to identify persistence and repeated execution against the same liquidity.
4. Detect candidate replenishment, depletion, sweeps, failed downside auctions, and price-response efficiency.
5. Build first candidate Trend State Engine features for `ROTATION`, `TREND FORMING`, `EXPANSION`, `HEALTHY PAUSE`, `EXHAUSTION`, and `REVERSAL`.
6. Capture future MGC sessions beginning before 7:00 PM CT so Condition 1 has raw MBO evidence.
7. Require repeated out-of-sample confirmation before any detector is promoted to the Opportunity Engine.
