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
            public string Signal { get; set; } = "NONE"; // "BUY", "SELL", or "NONE"
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
            var delimiterType = "COMMA"; // track delimiter for logging

            using (var reader = new StreamReader(csvPath))
            {
                string? headerLine = reader.ReadLine();
                if (headerLine == null)
                    throw new InvalidOperationException("CSV is empty");

                // Auto-detect delimiter: comma or tab
                char delimiter = ',';
                if (headerLine.Contains('\t'))
                {
                    delimiter = '\t';
                    delimiterType = "TAB";
                }
                
                var headers = headerLine.Split(delimiter);
                int timeIdx = Array.IndexOf(headers, "time");

                // VectorFlow draws its BUY/SELL labels at Pine lines 1389-1390 with
                // plotshape() calls that have NO title= argument. TradingView auto-names
                // untitled plots "Shapes", "Shapes.1", ... in export order, so those two
                // columns ARE the signal - in that order, BUY then SELL.
                //
                // The columns literally named "Buy Signal"/"Sell Signal" belong to some
                // OTHER indicator on the chart; that string appears nowhere in the
                // VectorFlow source. Reading them fired at 13.2/day against VectorFlow's
                // 6.15/day and produced runs of six consecutive BUYs, which a latched
                // edge signal cannot do. Every backtest before this fix was driven by
                // the wrong signal.
                var buyColumn = Environment.GetEnvironmentVariable("ISE_SIGNAL_BUY_COL") ?? "Shapes";
                var sellColumn = Environment.GetEnvironmentVariable("ISE_SIGNAL_SELL_COL") ?? "Shapes";

                // TradingView emits BOTH untitled plots under the IDENTICAL header
                // "Shapes" - the CSV really does contain two columns with the same name.
                // (Pandas silently renames the second to "Shapes.1"; the raw file does
                // not, which is why looking up "Shapes.1" found nothing.) Resolve by
                // OCCURRENCE: first is BUY (Pine line 1389), second is SELL (line 1390).
                int buyIdx = Array.IndexOf(headers, buyColumn);
                int sellIdx = (buyColumn == sellColumn)
                    ? (buyIdx >= 0 ? Array.IndexOf(headers, sellColumn, buyIdx + 1) : -1)
                    : Array.IndexOf(headers, sellColumn);

                if (timeIdx < 0 || buyIdx < 0 || sellIdx < 0)
                {
                    throw new InvalidOperationException(
                        $"CSV missing required columns. Need 'time', plus two occurrences of " +
                        $"'{buyColumn}' (1st = BUY, 2nd = SELL). Resolved buyIdx={buyIdx}, sellIdx={sellIdx}. " +
                        $"Override with ISE_SIGNAL_BUY_COL / ISE_SIGNAL_SELL_COL. " +
                        $"Found: {string.Join(", ", headers)}");
                }

                Console.WriteLine($"   Signal columns: BUY='{buyColumn}'[{buyIdx}] SELL='{sellColumn}'[{sellIdx}]");

                string? line;
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

            Console.WriteLine($"✅ Loaded {records.Count} signal records from {Path.GetFileName(csvPath)} (delimiter: {delimiterType})");
            Console.WriteLine($"   Range: {records[0].TimestampUtc:yyyy-MM-dd HH:mm:ss Z} to {records[records.Count - 1].TimestampUtc:yyyy-MM-dd HH:mm:ss Z}");

            var signalFireCount = records.Count(r => r.Signal != "NONE");
            Console.WriteLine($"   Signals: {signalFireCount} fires ({signalFireCount * 100.0 / records.Count:F1}%)");

            return records.AsReadOnly();
        }
    }
}
