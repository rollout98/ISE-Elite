// Read-only NinjaTrader 8 dataset refresh bridge for ISE Elite research.
// Requires ISEEliteHistoricalBarsRequestClient.cs in NinjaTrader bin\Custom.
// This indicator never submits, changes, or cancels orders.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ISE.NinjaTraderHost.HistoricalData;
using ISE.NinjaTraderRuntime.HistoricalData;
using NinjaTrader.NinjaScript;

namespace NinjaTrader.NinjaScript.Indicators
{
    public sealed class ISEEliteMNQUnattendedDatasetRefreshProbe : Indicator
    {
        private const int ExpectedBarsPerSession = 480;
        private const int IntervalSeconds = 60;
        private const int MaximumAttempts = 3;
        private const string TradingHoursTemplate = "CME US Index Futures ETH";
        private static readonly DateTime FromCentral = new DateTime(2026, 8, 10);
        private static readonly TimeSpan WindowStart = new TimeSpan(3, 0, 0);
        private static readonly TimeSpan WindowEnd = new TimeSpan(11, 0, 0);
        private Timer pollTimer;
        private int running;
        private string lastRequestId;
        private string lastRejectedRequestFingerprint;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Read-only unattended MNQ research dataset refresh bridge.";
                Name = "ISEEliteMNQUnattendedDatasetRefreshProbe";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = false;
            }
            else if (State == State.DataLoaded)
            {
                Print("ISE-DATA-REFRESH LOADED read-only=true");
                pollTimer = new Timer(Poll, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
            }
            else if (State == State.Terminated)
            {
                if (pollTimer != null) pollTimer.Dispose();
            }
        }

        private void Poll(object ignored)
        {
            try
            {
                var root = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "ISEEliteResearch");
                var requestPath = Path.Combine(root, "mnq-refresh.request.tsv");
                if (!File.Exists(requestPath)) return;
                var fingerprint = Sha256(requestPath);
                if (fingerprint == lastRejectedRequestFingerprint) return;
                RefreshRequest request;
                try
                {
                    request = ReadRequest(requestPath);
                    lastRejectedRequestFingerprint = null;
                }
                catch (Exception ex)
                {
                    lastRejectedRequestFingerprint = fingerprint;
                    Print("ISE-DATA-REFRESH POLL-ERROR fingerprint=" + fingerprint + " " + ex.Message
                        + " Further errors for this unchanged request are suppressed.");
                    return;
                }
                if (request.Id == lastRequestId) return;
                if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return;
                lastRequestId = request.Id;
                Task.Run(() => Refresh(root, request));
            }
            catch (Exception ex)
            {
                Print("ISE-DATA-REFRESH POLL-ERROR " + ex.Message);
            }
        }

        private void Refresh(string root, RefreshRequest request)
        {
            var statusPath = Path.Combine(root, "mnq-refresh.status.json");
            try
            {
                WriteStatus(statusPath, request.Id, "WAIT", "BarsRequest refresh is running.");
                var client = new ISEEliteHistoricalBarsRequestClient(TimeSpan.FromSeconds(120));
                var selected = new List<SelectedBar>();
                var noDataDates = new List<string>();

                for (var day = FromCentral.Date; day <= request.Through.Date; day = day.AddDays(1))
                {
                    if (day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday) continue;
                    var best = RequestBestDay(client, day);
                    if (best.Records.Count == 0)
                    {
                        noDataDates.Add(day.ToString("yyyy-MM-dd"));
                        continue;
                    }
                    ValidateSession(day, best.Records);
                    selected.AddRange(best.Records.Select(x => new SelectedBar(best.Policy, x)));
                }

                if (selected.Count == 0) throw new InvalidOperationException("No complete MNQ sessions were returned.");
                var sessions = selected.GroupBy(x => x.Record.TimestampLocal.Date).OrderBy(x => x.Key).ToList();
                if (sessions[sessions.Count - 1].Key != request.Through.Date)
                    throw new InvalidOperationException("Latest requested weekday did not return a complete session: " + request.Through.ToString("yyyy-MM-dd"));

                Directory.CreateDirectory(root);
                var target = Path.Combine(root, "morning-MNQ-09-26-continuous-forward-20260810-current-0300-1100-60s.tsv");
                var temp = target + "." + request.Id + ".tmp";
                WriteDataset(temp, selected.OrderBy(x => x.Record.TimestampLocal).ToList(), ResolveCentralTimeZone());
                var hash = Sha256(temp);
                AtomicReplace(temp, target);
                if (!string.Equals(hash, Sha256(target), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Post-replace SHA256 verification failed.");

                var manifest = target + ".ready.json";
                var json = "{\n"
                    + "  \"schemaVersion\": 1,\n"
                    + "  \"status\": \"PASS\",\n"
                    + "  \"requestId\": \"" + J(request.Id) + "\",\n"
                    + "  \"createdUtc\": \"" + DateTimeOffset.UtcNow.ToString("O") + "\",\n"
                    + "  \"instrument\": \"MNQ 09-26\",\n"
                    + "  \"intervalSeconds\": 60,\n"
                    + "  \"windowCentral\": \"03:00-11:00\",\n"
                    + "  \"firstSession\": \"" + sessions[0].Key.ToString("yyyy-MM-dd") + "\",\n"
                    + "  \"lastSession\": \"" + sessions[sessions.Count - 1].Key.ToString("yyyy-MM-dd") + "\",\n"
                    + "  \"barCount\": " + selected.Count.ToString(CultureInfo.InvariantCulture) + ",\n"
                    + "  \"sessionCount\": " + sessions.Count.ToString(CultureInfo.InvariantCulture) + ",\n"
                    + "  \"barsPerSession\": 480,\n"
                    + "  \"sha256\": \"" + hash + "\",\n"
                    + "  \"source\": \"NinjaTrader BarsRequest Repository with Provider fallback\",\n"
                    + "  \"noDataWeekdays\": [" + string.Join(",", noDataDates.Select(x => "\"" + x + "\"")) + "]\n"
                    + "}\n";
                AtomicWrite(manifest, json);
                WriteStatus(statusPath, request.Id, "PASS", "Atomic dataset and ready manifest written; sha256=" + hash);
                Print("ISE-DATA-REFRESH PASS request=" + request.Id + " bars=" + selected.Count + " sessions=" + sessions.Count);
            }
            catch (Exception ex)
            {
                WriteStatus(statusPath, request.Id, ex is TimeoutException ? "WAIT" : "FAIL", ex.GetType().Name + ": " + ex.Message);
                Print("ISE-DATA-REFRESH ERROR request=" + request.Id + " " + ex.Message);
            }
            finally { Interlocked.Exchange(ref running, 0); }
        }

        private DayBars RequestBestDay(ISEEliteHistoricalBarsRequestClient client, DateTime day)
        {
            var repo = RequestWithRetry(client, day, NinjaTraderHistoricalLookupPolicy.Repository);
            if (repo.Count == ExpectedBarsPerSession) return new DayBars(repo, NinjaTraderHistoricalLookupPolicy.Repository);
            var provider = RequestWithRetry(client, day, NinjaTraderHistoricalLookupPolicy.Provider);
            return provider.Count > repo.Count
                ? new DayBars(provider, NinjaTraderHistoricalLookupPolicy.Provider)
                : new DayBars(repo, NinjaTraderHistoricalLookupPolicy.Repository);
        }

        private List<NinjaTraderHistoricalBarRecord> RequestWithRetry(ISEEliteHistoricalBarsRequestClient client, DateTime day, NinjaTraderHistoricalLookupPolicy policy)
        {
            Exception last = null;
            for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
            {
                try
                {
                    return client.Request(new NinjaTraderHistoricalBarsRequest("MNQ 09-26", day, day.AddDays(1), IntervalSeconds, policy, TradingHoursTemplate))
                        .Where(x => x.TimestampLocal >= day && x.TimestampLocal < day.AddDays(1))
                        .Where(x => x.TimestampLocal.TimeOfDay >= WindowStart && x.TimestampLocal.TimeOfDay < WindowEnd)
                        .OrderBy(x => x.TimestampLocal).ToList();
                }
                catch (Exception ex)
                {
                    last = ex;
                    Print("ISE-DATA-REFRESH RETRY date=" + day.ToString("yyyy-MM-dd") + " source=" + policy + " attempt=" + attempt + " error=" + ex.Message);
                    if (attempt < MaximumAttempts) Thread.Sleep(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }
            throw new InvalidOperationException("BarsRequest retries exhausted for " + day.ToString("yyyy-MM-dd") + " " + policy + ".", last);
        }

        private static void ValidateSession(DateTime day, IList<NinjaTraderHistoricalBarRecord> records)
        {
            if (records.Count != ExpectedBarsPerSession)
                throw new InvalidOperationException("Partial session " + day.ToString("yyyy-MM-dd") + ": " + records.Count + " bars; expected 480.");
            if (records.Select(x => x.TimestampLocal).Distinct().Count() != ExpectedBarsPerSession)
                throw new InvalidOperationException("Duplicate timestamps in session " + day.ToString("yyyy-MM-dd") + ".");
            for (var i = 0; i < ExpectedBarsPerSession; i++)
            {
                var expected = day.Add(WindowStart).AddMinutes(i);
                if (records[i].TimestampLocal != expected)
                    throw new InvalidOperationException("Non-contiguous session " + day.ToString("yyyy-MM-dd") + " at " + expected.ToString("HH:mm:ss") + ".");
            }
        }

        private static RefreshRequest ReadRequest(string path)
        {
            var raw = File.ReadAllText(path);
            var lines = raw
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                .Select(x => x.Trim().TrimStart('\uFEFF'))
                .Where(x => x.Length > 0)
                .ToArray();

            if (lines.Length != 2)
                throw InvalidRequest("expected exactly 2 nonblank TSV rows but read " + lines.Length, raw, lines);

            var header = SplitFields(lines[0]);
            if (header.Length != 2)
                throw InvalidRequest("header has " + header.Length + " fields; expected 2", raw, lines);
            if (!string.Equals(header[0], "requestId", StringComparison.Ordinal)
                || !string.Equals(header[1], "throughCentral", StringComparison.Ordinal))
                throw InvalidRequest("header columns were [" + string.Join(", ", header.Select(Visible))
                    + "]; expected [requestId, throughCentral]", raw, lines);

            var values = SplitFields(lines[1]);
            if (values.Length != 2)
                throw InvalidRequest("data row has " + values.Length + " fields; expected 2", raw, lines);

            Guid parsedId;
            if (!Guid.TryParseExact(values[0], "N", out parsedId))
                throw InvalidRequest("requestId must be 32 hexadecimal characters; read " + Visible(values[0]), raw, lines);

            DateTime through;
            if (!DateTime.TryParseExact(values[1], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out through))
                throw InvalidRequest("throughCentral must use yyyy-MM-dd; read " + Visible(values[1]), raw, lines);
            if (through.Date < FromCentral.Date)
                throw InvalidRequest("throughCentral precedes supported start 2026-08-10; read " + Visible(values[1]), raw, lines);

            return new RefreshRequest(parsedId.ToString("N"), through);
        }

        private static string[] SplitFields(string line)
        {
            return line.Split('\t').Select(x => x.Trim().TrimStart('\uFEFF')).ToArray();
        }

        private static InvalidOperationException InvalidRequest(string reason, string raw, string[] lines)
        {
            return new InvalidOperationException("Invalid refresh request: " + reason
                + "; bytes=" + Encoding.UTF8.GetByteCount(raw)
                + "; rows=" + lines.Length
                + "; raw=" + Visible(raw));
        }

        private static string Visible(string value)
        {
            var shown = (value ?? "")
                .Replace("\uFEFF", "<BOM>")
                .Replace("\t", "<TAB>")
                .Replace("\r", "<CR>")
                .Replace("\n", "<LF>");
            if (shown.Length > 512) shown = shown.Substring(0, 512) + "...";
            return "'" + shown + "'";
        }

        private static void WriteDataset(string path, IList<SelectedBar> bars, TimeZoneInfo central)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("instrument\tcontract\ttimestampUtc\ttradingDay\tintervalSeconds\topen\thigh\tlow\tclose\tvolume\tsourceKind\tsourceName\tbid\task");
                foreach (var item in bars)
                {
                    var b = item.Record;
                    var local = DateTime.SpecifyKind(b.TimestampLocal, DateTimeKind.Unspecified);
                    if (central.IsInvalidTime(local) || central.IsAmbiguousTime(local)) throw new InvalidOperationException("DST ambiguity: " + local.ToString("O"));
                    var utc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, central), TimeSpan.Zero);
                    writer.WriteLine(string.Join("\t", new[] { "MNQ", "09-26", utc.ToString("O"), b.TradingDay.ToString("yyyy-MM-dd"), "60",
                        b.Open.ToString(CultureInfo.InvariantCulture), b.High.ToString(CultureInfo.InvariantCulture), b.Low.ToString(CultureInfo.InvariantCulture), b.Close.ToString(CultureInfo.InvariantCulture), b.Volume.ToString(CultureInfo.InvariantCulture),
                        ((int)item.Policy).ToString(), "NinjaTrader BarsRequest " + item.Policy, b.Bid.HasValue ? b.Bid.Value.ToString(CultureInfo.InvariantCulture) : "", b.Ask.HasValue ? b.Ask.Value.ToString(CultureInfo.InvariantCulture) : "" }));
                }
            }
        }

        private static void WriteStatus(string path, string id, string status, string message)
        {
            AtomicWrite(path, "{\n  \"requestId\": \"" + J(id) + "\",\n  \"status\": \"" + J(status) + "\",\n  \"updatedUtc\": \"" + DateTimeOffset.UtcNow.ToString("O") + "\",\n  \"message\": \"" + J(message) + "\"\n}\n");
        }

        private static void AtomicWrite(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            AtomicReplace(temp, path);
        }

        private static void AtomicReplace(string temp, string target)
        {
            if (File.Exists(target))
            {
                var backup = target + ".previous";
                if (File.Exists(backup)) File.Delete(backup);
                File.Replace(temp, target, backup, true);
                if (File.Exists(backup)) File.Delete(backup);
            }
            else File.Move(temp, target);
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
        }

        private static string J(string value) { return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n"); }
        private static TimeZoneInfo ResolveCentralTimeZone() { return TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"); }

        private sealed class RefreshRequest { public RefreshRequest(string id, DateTime through) { Id = id; Through = through.Date; } public string Id { get; private set; } public DateTime Through { get; private set; } }
        private sealed class DayBars { public DayBars(List<NinjaTraderHistoricalBarRecord> records, NinjaTraderHistoricalLookupPolicy policy) { Records = records; Policy = policy; } public List<NinjaTraderHistoricalBarRecord> Records { get; private set; } public NinjaTraderHistoricalLookupPolicy Policy { get; private set; } }
        private sealed class SelectedBar { public SelectedBar(NinjaTraderHistoricalLookupPolicy policy, NinjaTraderHistoricalBarRecord record) { Policy = policy; Record = record; } public NinjaTraderHistoricalLookupPolicy Policy { get; private set; } public NinjaTraderHistoricalBarRecord Record { get; private set; } }
    }
}
