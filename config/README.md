# Versioned Configuration

Configuration is externalized and version controlled.

- `sessions/` — logical trading day and session windows
- `instruments/` — tick values, hours, and calculation parameters
- `risk/` — account, drawdown, and prop-firm profiles
- `strategies/` — intelligence weights and ORI thresholds

Every production profile must include a schema version, effective date, checksum, and change history.
