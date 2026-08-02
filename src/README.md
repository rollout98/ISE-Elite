# Source Modules

The production source tree is organized by bounded responsibility:

- `Core` — shared contracts, context headers, primitives, and event abstractions
- `Intelligence` — Session, Trend, Momentum, Structure, Volatility, and Volume engines
- `ORI` — Opening Range Intelligence
- `Signal` — evidence aggregation and qualification
- `Risk` — account and prop-firm risk governance
- `Safety` — health monitoring and emergency state machine
- `Execution` — broker order lifecycle and reconciliation
- `Analytics` — replay and performance analysis
- `Infrastructure` — persistence, messaging, licensing, and adapters
- `UI` — NinjaTrader and desktop presentation components

Modules may not bypass published contracts to access another module's internal state.
