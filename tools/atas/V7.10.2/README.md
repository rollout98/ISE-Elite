# V7.10.2 ATAS Raw Recorder

This folder is reserved for the ATAS-side V7.10.2 raw recorder implementation and deployment scripts.

The implementation must be built against the same ATAS 8.0.14.396 / .NET 10 environment that successfully passed the V7.10.1 capability probe.

Required streams: MBO Snapshot/New/Change/Delete, trades with passive/aggressor exchange order IDs, market depth, best bid/ask/spread, and health telemetry.

See `docs/research/V7_10_2_Raw_MBO_Recorder_Spec.md` for acceptance criteria.
