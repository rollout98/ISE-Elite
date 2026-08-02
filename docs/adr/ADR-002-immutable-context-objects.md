# ADR-002: Immutable Context Objects

- **Status:** Accepted
- **Date:** 2026-08-02

## Context

Direct access to another engine's mutable internal state would create hidden coupling, inconsistent replay behavior, and difficult-to-test code.

## Decision

Engines communicate only through immutable, timestamped, versioned, serializable context objects. Every context carries a ContextId, CorrelationId, TradingDayId, TimestampUtc, EngineVersion, and ConfigurationVersion.

Recalculation creates a new context; published contexts are never modified.

## Consequences

- Deterministic replay and forensic tracing are supported.
- Engines can be unit tested independently.
- Direct cross-engine state access is an architectural defect.
