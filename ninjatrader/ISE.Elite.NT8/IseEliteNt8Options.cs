using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ISE.Elite.NinjaTrader8;

public sealed class IseEliteNt8Options
{
    private IseEliteNt8Options(string accountName, string instrumentRoot, string instrumentFullName,
        bool smokeTestEnabled, decimal smokeTestLimitPrice, bool protectionEnabled,
        int protectiveStopTicks, int protectiveTargetTicks, bool emergencyFlattenOnProtectionFailure)
    {
        AccountName = accountName;
        InstrumentRoot = instrumentRoot;
        InstrumentFullName = instrumentFullName;
        SmokeTestEnabled = smokeTestEnabled;
        SmokeTestLimitPrice = smokeTestLimitPrice;
        ProtectionEnabled = protectionEnabled;
        ProtectiveStopTicks = protectiveStopTicks;
        ProtectiveTargetTicks = protectiveTargetTicks;
        EmergencyFlattenOnProtectionFailure = emergencyFlattenOnProtectionFailure;
    }

    public string AccountName { get; }
    public string InstrumentRoot { get; }
    public string InstrumentFullName { get; }
    public bool SmokeTestEnabled { get; }
    public decimal SmokeTestLimitPrice { get; }
    public bool ProtectionEnabled { get; }
    public int ProtectiveStopTicks { get; }
    public int ProtectiveTargetTicks { get; }
    public bool EmergencyFlattenOnProtectionFailure { get; }

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

        var smokeTestEnabled = OptionalBoolean(values, "SmokeTestEnabled", false);
        var smokeTestLimitPrice = OptionalDecimal(values, "SmokeTestLimitPrice", 0m);
        if (smokeTestEnabled && smokeTestLimitPrice <= 100m)
            throw new InvalidOperationException(
                "SmokeTestLimitPrice must be a realistic positive price when SmokeTestEnabled=true.");

        var protectionEnabled = OptionalBoolean(values, "ProtectionEnabled", false);
        var protectiveStopTicks = OptionalInteger(values, "ProtectiveStopTicks", 40);
        var protectiveTargetTicks = OptionalInteger(values, "ProtectiveTargetTicks", 80);
        var emergencyFlattenOnProtectionFailure = OptionalBoolean(
            values, "EmergencyFlattenOnProtectionFailure", true);

        if (protectiveStopTicks <= 0)
            throw new InvalidOperationException("ProtectiveStopTicks must be greater than zero.");
        if (protectiveTargetTicks <= 0)
            throw new InvalidOperationException("ProtectiveTargetTicks must be greater than zero.");

        return new IseEliteNt8Options(accountName, instrumentRoot, instrumentFullName,
            smokeTestEnabled, smokeTestLimitPrice, protectionEnabled, protectiveStopTicks,
            protectiveTargetTicks, emergencyFlattenOnProtectionFailure);
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

    private static bool OptionalBoolean(IReadOnlyDictionary<string, string> values, string key, bool fallback)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return fallback;
        if (!bool.TryParse(value, out var parsed))
            throw new InvalidDataException($"Configuration value '{key}' must be true or false.");
        return parsed;
    }

    private static decimal OptionalDecimal(IReadOnlyDictionary<string, string> values, string key, decimal fallback)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return fallback;
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"Configuration value '{key}' must be a decimal number using a period.");
        return parsed;
    }

    private static int OptionalInteger(IReadOnlyDictionary<string, string> values, string key, int fallback)
    {
        if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            return fallback;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"Configuration value '{key}' must be an integer.");
        return parsed;
    }
}
