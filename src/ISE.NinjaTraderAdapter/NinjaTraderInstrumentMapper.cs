using System;
using System.Collections.Generic;

namespace ISE.NinjaTraderAdapter;

/// <summary>Maps canonical ISE symbols to NinjaTrader instrument roots.</summary>
public sealed class NinjaTraderInstrumentMapper
{
    private readonly IReadOnlyDictionary<string, string> mappings;

    /// <summary>Initializes the default futures mappings.</summary>
    public NinjaTraderInstrumentMapper()
    {
        mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ES"] = "MES",
            ["NQ"] = "MNQ",
            ["YM"] = "MYM",
            ["RTY"] = "M2K",
            ["CL"] = "MCL",
            ["GC"] = "MGC",
            ["MES"] = "MES",
            ["MNQ"] = "MNQ",
            ["MYM"] = "MYM",
            ["M2K"] = "M2K",
            ["MCL"] = "MCL",
            ["MGC"] = "MGC"
        };
    }

    /// <summary>Attempts to map a symbol to a supported NinjaTrader instrument root.</summary>
    public bool TryMap(string symbol, out string instrument)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            instrument = string.Empty;
            return false;
        }

        return mappings.TryGetValue(symbol.Trim(), out instrument!);
    }
}
