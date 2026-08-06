using System;

namespace ISE.Elite.NinjaTrader8;

internal static class IseEliteNt8BridgeStartup
{
    private static readonly object Sync = new object();

    public static bool TryStart(Action<string> output, out string? failure)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        lock (Sync)
        {
            var existing = IseEliteNt8BridgeRegistry.Runtime;
            if (existing != null && existing.IsStarted)
            {
                failure = null;
                return true;
            }

            return TryCreateAndRegisterRuntime(output,
                "ISE Elite NT8 Bridge started in Sim101-only mode by safe retry.",
                out _, out failure);
        }
    }

    public static bool TryRestartForRecovery(Action<string> output,
        out IseEliteNt8Runtime? restartedRuntime, out string? failure)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));

        lock (Sync)
        {
            restartedRuntime = null;
            var existing = IseEliteNt8BridgeRegistry.Runtime;
            if (existing == null || !existing.IsStarted)
            {
                failure = "An active ISE Elite runtime is required for the recovery restart test.";
                output("ISE runtime recovery restart blocked: " + failure);
                return false;
            }

            IseEliteNt8BridgeRegistry.Runtime = null;
            try
            {
                output(
                    "ISE runtime recovery restart: stopping only the ISE runtime. " +
                    "Broker-held Sim101 positions and working orders are not cancelled.");
                existing.Dispose();
            }
            catch (Exception exception)
            {
                failure = "The existing ISE runtime could not be stopped cleanly: " + exception.Message;
                output("ISE runtime recovery restart failed: " + failure);
                return false;
            }

            return TryCreateAndRegisterRuntime(output,
                "ISE runtime recovery restart completed; validating recovered broker position and protective order IDs.",
                out restartedRuntime, out failure);
        }
    }

    private static bool TryCreateAndRegisterRuntime(Action<string> output, string successMessage,
        out IseEliteNt8Runtime? runtime, out string? failure)
    {
        runtime = null;
        try
        {
            var options = IseEliteNt8Options.Load(IseEliteNt8Options.DefaultConfigurationPath);
            runtime = new IseEliteNt8Runtime(options);
            runtime.Diagnostic += output;
            runtime.BrokerEventReceived += brokerEvent => output(
                $"Broker event: {brokerEvent.RequestId} {brokerEvent.State} " +
                $"filled={brokerEvent.FilledQuantity} avg={brokerEvent.AverageFillPrice}");
            runtime.ExecutionReceived += execution => output(
                $"Execution: {execution.ExecutionId} {execution.Instrument} {execution.OrderAction} " +
                $"qty={execution.Quantity} price={execution.Price}");
            runtime.PositionReceived += position => output(
                $"Position: {position.Instrument} {position.MarketPosition} " +
                $"qty={position.Quantity} avg={position.AveragePrice}");

            runtime.Start();
            IseEliteNt8BridgeRegistry.Runtime = runtime;
            failure = null;
            output(successMessage);
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                runtime?.Dispose();
            }
            catch
            {
                // Preserve the original startup failure.
            }

            runtime = null;
            IseEliteNt8BridgeRegistry.Runtime = null;
            failure = exception.Message;
            output("ISE Elite NT8 Bridge startup failed: " + exception.Message);
            output("Configuration path: " + IseEliteNt8Options.DefaultConfigurationPath);
            return false;
        }
    }
}
