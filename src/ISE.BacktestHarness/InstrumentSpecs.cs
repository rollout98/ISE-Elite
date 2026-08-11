using System;

namespace ISE.BacktestHarness
{
    /// <summary>
    /// Instrument-specific contract specs. MNQ and MGC have different
    /// tick sizes and point values.
    /// </summary>
    public static class InstrumentSpecs
    {
        /// <summary>
        /// Returns the $ per point for a given instrument.
        /// MNQ: $2/point (Micro E-mini Nasdaq)
        /// MGC: $100/point (Micro E-mini Gold, trades in cents, 1pt = 100 cents = $100)
        /// </summary>
        public static decimal GetPointValue(string instrument)
        {
            return instrument.ToUpperInvariant() switch
            {
                "MNQ" or "MNQ1!" or "MNQU26" => 2m,
                "MGC" or "MGC1!" or "MGCU26" => 100m,
                _ => throw new NotSupportedException(
                    $"Unknown instrument '{instrument}'. Supported: MNQ ($2/pt), MGC ($100/pt)")
            };
        }

        /// <summary>
        /// Returns the tick size for a given instrument (in index points or cents).
        /// MNQ: 0.25 index points per tick
        /// MGC: 0.1 cents per tick ($0.10, since gold = cents)
        /// </summary>
        public static decimal GetTickSize(string instrument)
        {
            return instrument.ToUpperInvariant() switch
            {
                "MNQ" or "MNQ1!" or "MNQU26" => 0.25m,
                "MGC" or "MGC1!" or "MGCU26" => 0.1m,
                _ => throw new NotSupportedException($"Unknown instrument '{instrument}'")
            };
        }
    }
}
