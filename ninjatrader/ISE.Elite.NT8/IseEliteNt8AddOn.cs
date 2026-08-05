using System;
using ISE.Elite.NinjaTrader8;
using NinjaTrader.NinjaScript;

namespace ISE.Elite.NinjaTrader8;

public static class IseEliteNt8BridgeRegistry
{
    public static IseEliteNt8Runtime? Runtime { get; internal set; }
}

namespace NinjaTrader.NinjaScript.AddOns;

public sealed class IseEliteNt8AddOn : AddOnBase
{
    protected override void OnStateChange()
    {
        if (State == State.SetDefaults)
        {
            Name = "ISE Elite NT8 Bridge";
            return;
        }

        if (State == State.Active)
        {
            StartBridge();
            return;
        }

        if (State == State.Terminated)
            StopBridge();
    }

    private static void StartBridge()
    {
        if (IseEliteNt8BridgeRegistry.Runtime != null)
            return;

        try
        {
            var options = IseEliteNt8Options.Load(IseEliteNt8Options.DefaultConfigurationPath);
            var runtime = new IseEliteNt8Runtime(options);
            runtime.Diagnostic += WriteOutput;
            runtime.BrokerEventReceived += brokerEvent => WriteOutput(
                $"Broker event: {brokerEvent.RequestId} {brokerEvent.State} " +
                $"filled={brokerEvent.FilledQuantity} avg={brokerEvent.AverageFillPrice}");
            runtime.ExecutionReceived += execution => WriteOutput(
                $"Execution: {execution.ExecutionId} {execution.Instrument} " +
                $"qty={execution.Quantity} price={execution.Price}");
            runtime.PositionReceived += position => WriteOutput(
                $"Position: {position.Instrument} {position.MarketPosition} " +
                $"qty={position.Quantity} avg={position.AveragePrice}");

            runtime.Start();
            IseEliteNt8BridgeRegistry.Runtime = runtime;
            WriteOutput("ISE Elite NT8 Bridge started in Sim101-only mode.");
        }
        catch (Exception exception)
        {
            WriteOutput($"ISE Elite NT8 Bridge did not start: {exception.Message}");
            WriteOutput($"Configuration path: {IseEliteNt8Options.DefaultConfigurationPath}");
        }
    }

    private static void StopBridge()
    {
        var runtime = IseEliteNt8BridgeRegistry.Runtime;
        IseEliteNt8BridgeRegistry.Runtime = null;
        if (runtime == null)
            return;

        try
        {
            runtime.Dispose();
            WriteOutput("ISE Elite NT8 Bridge stopped.");
        }
        catch (Exception exception)
        {
            WriteOutput($"ISE Elite NT8 Bridge shutdown error: {exception.Message}");
        }
    }

    private static void WriteOutput(string message)
    {
        NinjaTrader.Code.Output.Process(
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}",
            PrintTo.OutputTab1);
    }
}
