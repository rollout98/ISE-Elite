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
        /// MNQ: $2/point (Micro E-mini Nasdaq, 0.25 tick = $0.50/tick)
        /// MGC: $10/point (Micro Gold, 10 troy oz, 0.1 tick = $1.00/tick)
        ///      NOTE: full-size GC is 100 oz = $100/point. MGC is one tenth of that.
        /// </summary>
        public static decimal GetPointValue(string instrument)
        {
            return instrument.ToUpperInvariant() switch
            {
                "MNQ" or "MNQ1!" or "MNQU26" => 2m,
                "MGC" or "MGC1!" or "MGCU26" or "MGCZ26" => 10m,
                _ => throw new NotSupportedException(
                    $"Unknown instrument '{instrument}'. Supported: MNQ ($2/pt), MGC ($10/pt)")
            };
        }

        /// <summary>
        /// Returns the tick size for a given instrument, in price points.
        /// MNQ: 0.25 index points per tick ($0.50/tick)
        /// MGC: 0.1 dollars of gold price per tick ($1.00/tick)
        /// </summary>
        public static decimal GetTickSize(string instrument)
        {
            return instrument.ToUpperInvariant() switch
            {
                "MNQ" or "MNQ1!" or "MNQU26" => 0.25m,
                "MGC" or "MGC1!" or "MGCU26" or "MGCZ26" => 0.1m,
                _ => throw new NotSupportedException($"Unknown instrument '{instrument}'")
            };
        }
    }
}
