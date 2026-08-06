using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ISE.HistoricalResearch
{
    public sealed class HistoricalDataFileStore
    {
        private const string Header = "instrument\tcontract\ttimestampUtc\ttradingDay\tintervalSeconds\topen\thigh\tlow\tclose\tvolume\tsourceKind\tsourceName\tbid\task";

        public void Write(string path, IEnumerable<HistoricalBar> bars)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
            if (bars == null) throw new ArgumentNullException(nameof(bars));

            var normalizer = new HistoricalDataNormalizer();
            var normalized = normalizer.Normalize(bars);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            using (var writer = new StreamWriter(path, false))
            {
                writer.WriteLine(Header);
                foreach (var bar in normalized)
                {
                    ValidateText(bar.Instrument, nameof(bar.Instrument));
                    ValidateText(bar.Contract, nameof(bar.Contract));
                    ValidateText(bar.SourceName, nameof(bar.SourceName));

                    writer.WriteLine(string.Join("\t", new[]
                    {
                        bar.Instrument,
                        bar.Contract,
                        bar.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                        bar.TradingDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        bar.IntervalSeconds.ToString(CultureInfo.InvariantCulture),
                        bar.Open.ToString(CultureInfo.InvariantCulture),
                        bar.High.ToString(CultureInfo.InvariantCulture),
                        bar.Low.ToString(CultureInfo.InvariantCulture),
                        bar.Close.ToString(CultureInfo.InvariantCulture),
                        bar.Volume.ToString(CultureInfo.InvariantCulture),
                        ((int)bar.SourceKind).ToString(CultureInfo.InvariantCulture),
                        bar.SourceName,
                        bar.Bid.HasValue ? bar.Bid.Value.ToString(CultureInfo.InvariantCulture) : string.Empty,
                        bar.Ask.HasValue ? bar.Ask.Value.ToString(CultureInfo.InvariantCulture) : string.Empty
                    }));
                }
            }
        }

        public IReadOnlyList<HistoricalBar> Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("Historical data file was not found.", path);

            var bars = new List<HistoricalBar>();
            using (var reader = new StreamReader(path))
            {
                var header = reader.ReadLine();
                if (!string.Equals(header, Header, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Historical data schema header is invalid or unsupported.");
                }

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0) continue;
                    var parts = line.Split('\t');
                    if (parts.Length != 14) throw new InvalidDataException("Historical data row has an invalid field count.");

                    bars.Add(new HistoricalBar(
                        parts[0],
                        parts[1],
                        DateTimeOffset.ParseExact(parts[2], "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                        DateTime.ParseExact(parts[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None),
                        int.Parse(parts[4], CultureInfo.InvariantCulture),
                        decimal.Parse(parts[5], CultureInfo.InvariantCulture),
                        decimal.Parse(parts[6], CultureInfo.InvariantCulture),
                        decimal.Parse(parts[7], CultureInfo.InvariantCulture),
                        decimal.Parse(parts[8], CultureInfo.InvariantCulture),
                        long.Parse(parts[9], CultureInfo.InvariantCulture),
                        (HistoricalDataSourceKind)int.Parse(parts[10], CultureInfo.InvariantCulture),
                        parts[11],
                        ParseNullableDecimal(parts[12]),
                        ParseNullableDecimal(parts[13])));
                }
            }

            return new HistoricalDataNormalizer().Normalize(bars);
        }

        private static decimal? ParseNullableDecimal(string value)
        {
            return value.Length == 0 ? (decimal?)null : decimal.Parse(value, CultureInfo.InvariantCulture);
        }

        private static void ValidateText(string value, string name)
        {
            if (value.IndexOf('\t') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
            {
                throw new InvalidDataException(name + " cannot contain tabs or line breaks.");
            }
        }
    }
}
