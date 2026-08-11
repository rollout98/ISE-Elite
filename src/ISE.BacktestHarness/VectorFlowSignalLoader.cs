using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ISE.BacktestHarness
{
    /// <summary>
    /// Loads pre-generated signals (BUY/SELL) from a TradingView CSV export
    /// with VectorFlow indicator labels. Maps signals to bar timestamps.
    /// </summary>
    public sealed class VectorFlowSignalLoader
    {
        public sealed class SignalRecord
        {
            public DateTime TimestampUtc { get; set; }
            public string Signal { get; set; } // "BUY", "SELL", or "NONE"
        }

        /// <summary>
        /// Load signals from a CSV exported from TradingView.
        /// Expected columns: time, open, high, low, close, volume, ..., BUY, SELL
        /// The BUY and SELL columns contain 1 (signal fired) or blank/0.
        /// </summary>
        public static IReadOnlyList<SignalRecord> LoadFromCsv(string csvPath, string timeZoneId = "Central Standard Time")
        {
            if (!File.Exists(csvPath))
                throw new FileNotFoundException($"Signal CSV not found: {csvPath}");

            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var records = new List<SignalRecord>();

            using (var reader = new StreamReader(csvPath))
            {
                string headerLine = reader.ReadLine();
                if (headerLine == null)
                    throw new InvalidOperationException("CSV is empty");

                // Auto-detect delimiter: comma or tab
                char delimiter = ',';
                if (headerLine.Contains('	'))
                    delimiter = '	';
                
                var headers = headerLine.Split(delimiter);
                int timeIdx = Array.IndexOf(headers, "time");
                int buyIdx = Array.IndexOf(headers, "BUY");
                int sellIdx = Array.IndexOf(headers, "SELL");

                if (timeIdx < 0 || (buyIdx < 0 && sellIdx < 0))
                {
                    throw new InvalidOperationException(
                        $"CSV missing required columns. Expected: time, BUY, SELL. " +
                        $"Found: {string.Join(", ", headers)}");
                }

                string line;
                int lineNum = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNum++;
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var fields = line.Split(delimiter);
                    if (fields.Length <= Math.Max(buyIdx, sellIdx))
                        continue;

                    if (!DateTime.TryParse(fields[timeIdx], out var localTime))
                        continue;

                    // Convert to UTC
                    var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
                    var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);

                    string signal = "NONE";
                    if (buyIdx >= 0 && !string.IsNullOrWhiteSpace(fields[buyIdx]) && fields[buyIdx] != "0")
                        signal = "BUY";
                    else if (sellIdx >= 0 && !string.IsNullOrWhiteSpace(fields[sellIdx]) && fields[sellIdx] != "0")
                        signal = "SELL";

                    records.Add(new SignalRecord { TimestampUtc = utc, Signal = signal });
                }
            }

            if (records.Count == 0)
                throw new InvalidOperationException("No valid signal records found in CSV");

            Console.WriteLine($"✅ Loaded {records.Count} signal records from {Path.GetFileName(csvPath)} (delimiter: {(headerLine.Contains('	') ? 'TAB' : 'COMMA')})");
            Console.WriteLine($"   Range: {records[0].TimestampUtc:yyyy-MM-dd HH:mm:ss Z} to {records[records.Count - 1].TimestampUtc:yyyy-MM-dd HH:mm:ss Z}");

            var signalFireCount = records.Count(r => r.Signal != "NONE");
            Console.WriteLine($"   Signals: {signalFireCount} fires ({signalFireCount * 100.0 / records.Count:F1}%)");

            return records.AsReadOnly();
        }
    }
}
