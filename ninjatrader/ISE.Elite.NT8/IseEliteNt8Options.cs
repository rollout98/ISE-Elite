using System;
using System.Collections.Generic;
using System.IO;

namespace ISE.Elite.NinjaTrader8;

public sealed class IseEliteNt8Options
{
    private IseEliteNt8Options(string accountName, string instrumentRoot, string instrumentFullName)
    {
        AccountName = accountName;
        InstrumentRoot = instrumentRoot;
        InstrumentFullName = instrumentFullName;
    }

    public string AccountName { get; }
    public string InstrumentRoot { get; }
    public string InstrumentFullName { get; }

    public static string DefaultConfigurationPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "NinjaTrader 8", "bin", "Custom", "ISE.Elite.NT8.config");

    public static IseEliteNt8Options Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Configuration path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("ISE Elite NT8 configuration was not found.", path);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
                throw new InvalidDataException("Configuration lines must use Key=Value format.");

            values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
        }

        var accountName = Required(values, "AccountName");
        if (!string.Equals(accountName, "Sim101", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This bridge build is locked to the Sim101 account.");

        var instrumentRoot = Required(values, "InstrumentRoot");
        var instrumentFullName = Required(values, "InstrumentFullName");
        if (!instrumentFullName.StartsWith(instrumentRoot + " ", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(instrumentFullName, instrumentRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("InstrumentFullName must match the configured instrument root.");

        return new IseEliteNt8Options(accountName, instrumentRoot, instrumentFullName);
    }

    public string ResolveInstrument(string requestedInstrument)
    {
        if (string.Equals(requestedInstrument, InstrumentRoot, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedInstrument, InstrumentFullName, StringComparison.OrdinalIgnoreCase))
            return InstrumentFullName;

        throw new InvalidOperationException(
            $"Instrument '{requestedInstrument}' is not authorized. Only {InstrumentRoot} is enabled for this bridge build.");
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Required configuration value '{key}' is missing.");
        return value;
    }
}
