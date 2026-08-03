using System;
using System.Collections.Generic;

namespace ISE.NinjaTraderAdapter;

/// <summary>Maintains the relationship between ISE command IDs and NinjaTrader order IDs.</summary>
public sealed class NinjaTraderOrderCorrelation
{
    private readonly Dictionary<Guid, string> byCommand = new Dictionary<Guid, string>();
    private readonly Dictionary<string, Guid> byPlatform = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a platform order ID for an ISE command.</summary>
    public void Register(Guid commandId, string platformOrderId)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("Command ID is required.", nameof(commandId));
        if (string.IsNullOrWhiteSpace(platformOrderId)) throw new ArgumentException("Platform order ID is required.", nameof(platformOrderId));
        if (byCommand.ContainsKey(commandId) || byPlatform.ContainsKey(platformOrderId))
            throw new InvalidOperationException("Order correlation already exists.");

        byCommand.Add(commandId, platformOrderId);
        byPlatform.Add(platformOrderId, commandId);
    }

    /// <summary>Attempts to resolve an ISE command ID from a NinjaTrader order ID.</summary>
    public bool TryResolveCommand(string platformOrderId, out Guid commandId)
    {
        if (string.IsNullOrWhiteSpace(platformOrderId))
        {
            commandId = Guid.Empty;
            return false;
        }

        return byPlatform.TryGetValue(platformOrderId, out commandId);
    }

    /// <summary>Attempts to resolve a NinjaTrader order ID from an ISE command ID.</summary>
    public bool TryResolvePlatform(Guid commandId, out string platformOrderId) =>
        byCommand.TryGetValue(commandId, out platformOrderId!);
}
