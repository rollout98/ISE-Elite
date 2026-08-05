# ISE Elite NinjaTrader 8 Installed Bridge

This project is the first assembly in the repository that is intended to run **inside NinjaTrader 8**. It implements the `INinjaTraderApi` seam created by `ISE.NinjaTraderHost` and connects the portable ISE Elite execution stack to NinjaTrader account APIs.

## Safety scope

This build is intentionally restricted to:

- `Sim101` only
- One configured instrument root and one exact contract
- One ISE execution request per platform order
- Account-level order, execution, and position event monitoring
- No live-account enablement
- No automatic trade submission at startup
- Smoke-test execution disabled by default
- One smoke-test order maximum per NinjaTrader runtime session

The bridge fails closed if the account, connection, configuration, or instrument contract is unavailable.

## Why this project is outside `ISE-Elite.sln`

The normal solution remains platform independent and can build without NinjaTrader installed. This project targets .NET Framework 4.8 and references NinjaTrader assemblies from the local NinjaTrader installation, so it must be built on a Windows workstation that has NinjaTrader 8 installed.

## Prerequisites

- NinjaTrader 8 installed under `C:\Program Files\NinjaTrader 8`, or provide a different `NinjaTraderInstallDir` MSBuild property.
- Visual Studio with the .NET Framework 4.8 Developer Pack.
- NinjaTrader connected to a data connection associated with `Sim101`.
- The exact futures contract added to the local NinjaTrader instrument database.

## Configuration

Copy `ISE.Elite.NT8.config.template` into the active NinjaTrader **My Documents** data directory as `ISE.Elite.NT8.config`. Windows may resolve My Documents under OneDrive.

Baseline configuration:

```text
AccountName=Sim101
InstrumentRoot=MNQ
InstrumentFullName=MNQ 09-26
SmokeTestEnabled=false
SmokeTestLimitPrice=0
```

`InstrumentFullName` is an example. Confirm the contract currently selected in NinjaTrader before compiling or submitting any paper order.

The smoke test must remain disabled until a non-marketable limit price has been reviewed immediately before the test. The bridge refuses to enable the smoke test with a missing or unrealistic price.

## Build and deploy

Close NinjaTrader before replacing bridge DLLs. Build the bridge as x64 from Visual Studio Developer PowerShell:

```powershell
& $msbuild `
  .\ninjatrader\ISE.Elite.NT8\ISE.Elite.NT8.csproj `
  /restore `
  /p:Configuration=Debug `
  /p:Platform=x64 `
  "/p:NinjaTraderInstallDir=$ntInstall" `
  "/p:NinjaTraderCustomDir=$ntCustom"
```

The post-build target copies the bridge and its ISE runtime dependencies to the configured NinjaTrader `bin\Custom` folder. Restart NinjaTrader after each bridge rebuild.

## Expected startup output

Open the NinjaScript Output window and confirm messages similar to:

```text
ISE Elite NT8 adapter subscribed to Sim101 order, execution, and position events.
ISE Elite NT8 runtime is running on Sim101.
ISE Elite NT8 Bridge started in Sim101-only mode.
Sim101 smoke test is disabled.
```

If startup is blocked, the output includes the reason and configuration path. Do not bypass a startup block.

## Runtime flow

```text
ISE ExecutionRequest
        |
        v
NinjaTraderExecutionBroker
        |
        v
NinjaTraderHostTransport
        |
        v
NinjaTraderApiAdapter
        |
        v
Account.CreateOrder / Account.Submit
        |
        v
OrderUpdate / ExecutionUpdate / PositionUpdate
        |
        v
Normalized ISE broker events
```

## Guarded Sim101 submission-and-cancellation test

This first smoke test proves submission, correlation, account events, and cancellation. It is deliberately designed as a **non-marketable buy-limit order**, not a market fill.

1. Confirm `Sim101` is flat and has no working MNQ orders.
2. Confirm the active contract exactly matches `InstrumentFullName`.
3. Choose a non-marketable buy-limit price and update:

```text
SmokeTestEnabled=true
SmokeTestLimitPrice=<reviewed price>
```

4. Close NinjaTrader, rebuild x64, and restart it.
5. Confirm Output Tab 1 states that the smoke test is enabled but disarmed.
6. Open Control Center **New** and select **ISE Elite: Arm Sim101 Smoke Test**.
7. Read the warning and confirm. Arming does not submit an order.
8. Recheck the configured price and the Control Center Orders tab.
9. Select **ISE Elite: Submit 1 MNQ Buy-Limit Smoke Test**.
10. Accept the final confirmation only when the order is safely non-marketable.
11. Confirm the order appears on `Sim101`, the request and platform order IDs are logged, and no unexpected position appears.
12. Select **ISE Elite: Cancel Smoke-Test Order**.
13. Confirm the broker reports `Cancelled` and Sim101 remains flat.
14. Return `SmokeTestEnabled=false`, rebuild, and restart NinjaTrader after validation.

The controller permits only one smoke-test submission per runtime session. Restarting is required for another test.

## Important limitation

The metadata contains provisional stop and target values because the portable execution request requires them, but the NT8 bridge does **not** yet submit protective stop or target child orders. The smoke-test order must therefore remain non-marketable and should be cancelled after acceptance.

## Current limitations

- The live event loop has not yet been connected to the Trading Brain.
- Protective stop and target child orders are not implemented yet.
- Reconnect recovery and reconciliation are not implemented yet.
- Contract rollover is manual through the configuration file.
- Position updates are observed but not yet reconciled against an authoritative Position Manager.

Those items belong to the supervised paper-trading and hardening sprints. This package proves the actual NT8 execution and event boundary without enabling autonomous trading.
