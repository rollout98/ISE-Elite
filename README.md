# ISE Elite

Commercial Trading Operating System

## Governing requirements

- [Production Session Supervisor and Daily P&L Governance](docs/production-session-supervisor-and-daily-pnl-governance.md)
- [New York Session Stability and Expansion Gate](docs/new-york-session-stability-and-expansion-gate.md)

## Decision authority

ISE Elite owns the trading decision. External TradingView or VectorFlow signals are not entry authority.

Core decision flow:

Market State -> Opportunity Detection -> Entry Qualification -> Risk Authorization -> Execution -> Position Intelligence -> Daily / Account Governance

Authority split:

- 3-minute Range / structural opportunity logic answers: "Can I enter?"
- 5-minute VectorFlow-style directional intelligence answers: "How long should I stay?"
- VectorFlow may support scalp/core/runner management after entry, but it does not issue primary BUY/SELL authority.

## Current development focus

- Sim101-only broker execution and protection
- directional protected-fill validation
- emergency flatten and restart recovery
- deterministic daily P&L and green-day governance
- restore and advance the original ISE market-state and opportunity-selection architecture
- prove the core New York session before expanding New York or beginning Asia research
