# ADR-001: Logical Trading Day

- **Status:** Accepted
- **Date:** 2026-08-02

## Context

ISE Elite trades futures across overnight and New York sessions. A midnight reset would split one operational trading day and create inconsistent risk, analytics, and session behavior.

## Decision

The logical trading day runs from **5:00 PM to 3:00 PM America/Chicago** on the following calendar day. The period from **3:00 PM to 5:00 PM America/Chicago** is the maintenance and reconciliation window.

The IANA time-zone identifier `America/Chicago` is mandatory. Fixed CST offsets are prohibited because daylight-saving transitions must be handled automatically.

All engines consume the TradingDayId published by the Session Engine. Daily risk counters, P&L limits, analytics, and lockouts reset at the 5:00 PM boundary.

## Consequences

- Overnight and New York activity share one TradingDayId.
- No subsystem may independently calculate the trading day.
- Boundary, holiday, early-close, and daylight-saving tests are mandatory.
