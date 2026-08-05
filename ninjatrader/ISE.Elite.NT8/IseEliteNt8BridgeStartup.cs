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

            IseEliteNt8Runtime? runtime = null;
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
                output("ISE Elite NT8 Bridge started in Sim101-only mode by safe retry.");
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

                failure = exception.Message;
                output("ISE Elite NT8 Bridge safe startup retry failed: " + exception.Message);
                output("Configuration path: " + IseEliteNt8Options.DefaultConfigurationPath);
                return false;
            }
        }
    }
}
