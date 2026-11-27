using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Relead.CrossPromo.Json;

namespace Relead.CrossPromo
{
    /// <summary>Analytics sink. ReleadTracker posts to api.relead.com; a host can plug in its own.</summary>
    public interface ITracker
    {
        void Track(string name, Dictionary<string, object> props);
        /// <summary>Try to send what's queued now (e.g. on pause). Never blocks.</summary>
        void Flush();
    }

    public sealed class NullTracker : ITracker
    {
        public void Track(string name, Dictionary<string, object> props) { }
        public void Flush() { }
    }

    /// <summary>Envelope fields per KOTGApi analytics-platform.md §2 Ingest.</summary>
    public sealed class TrackerEnvelope
    {
        public string GameId;
        public string InstallId;
        public string AppVersion;
        public long? Build;
        public string Platform;
        public string OsVersion;
        public string Locale;
    }

    /// <summary>
    /// File-backed event queue (JSON lines). Survives restarts; oldest events are dropped above the cap.
    /// The batch being sent keeps its batch_id across retries, so a retry after a lost 202 is deduplicated server-side.
    /// </summary>
    public sealed class EventQueue
    {
        public const int MaxEvents = 1000;
        readonly string _file;
        readonly List<string> _lines = new List<string>();
        public string PendingBatchId;
        public int PendingCount;

        public EventQueue(string file)
        {
            _file = file;
            try
            {
                if (File.Exists(_file))
                {
                    var all = File.ReadAllLines(_file);
                    int start = 0;
                    if (all.Length > 0 && all[0].StartsWith("#batch ", StringComparison.Ordinal))
                    {
                        var parts = all[0].Substring(7).Split(' ');
                        PendingBatchId = parts[0];
                        if (parts.Length > 1) int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out PendingCount);
                        start = 1;
                    }
                    for (int i = start; i < all.Length; i++) if (all[i].Length > 0) _lines.Add(all[i]);
                    if (PendingCount > _lines.Count) PendingCount = _lines.Count;
                }
            }
            catch (Exception) { _lines.Clear(); PendingBatchId = null; PendingCount = 0; }
        }

        public int Count => _lines.Count;

        public void Add(string eventJson)
        {
            _lines.Add(eventJson);
            int over = _lines.Count - MaxEvents;
            if (over > 0)
            {
                _lines.RemoveRange(0, over);
                // the pending batch lost events at its head: mint a new batch next time
                PendingBatchId = null;
                PendingCount = 0;
            }
            Save();
        }

        /// <summary>The next batch: the pending one if any, else up to maxEvents / maxBytes from the head.</summary>
        public List<string> TakeBatch(int maxEvents, int maxBytes, out string batchId)
        {
            if (PendingBatchId == null || PendingCount == 0)
            {
                int n = 0, bytes = 0;
                while (n < _lines.Count && n < maxEvents)
                {
                    int b = Encoding.UTF8.GetByteCount(_lines[n]) + 1;
                    if (n > 0 && bytes + b > maxBytes) break;
                    bytes += b;
                    n++;
                }
                PendingBatchId = n > 0 ? Guid.NewGuid().ToString() : null;
                PendingCount = n;
                Save();
            }
            batchId = PendingBatchId;
            return _lines.GetRange(0, PendingCount);
        }

        public void CompleteBatch()
        {
            _lines.RemoveRange(0, Math.Min(PendingCount, _lines.Count));
            PendingBatchId = null;
            PendingCount = 0;
            Save();
        }

        void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_file);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                if (PendingBatchId != null) sb.Append("#batch ").Append(PendingBatchId).Append(' ').Append(PendingCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
                foreach (var l in _lines) sb.Append(l).Append('\n');
                AtomicFile.WriteAllText(_file, sb.ToString());
            }
            catch (Exception) { }
        }
    }

    /// <summary>
    /// POST {endpoint}/v1/games/{game_id}/events with Bearer ingest key.
    /// 202 or 4xx (except 408/429) → batch removed; 5xx / 408 / 429 / network → kept, exponential backoff (30 s … 1 h).
    /// </summary>
    public sealed class ReleadTracker : ITracker
    {
        public const int MaxBatchEvents = 100;
        public const int MaxBatchBytes = 200 * 1024; // server limit 256 KB incl. envelope
        public const int AutoFlushAt = 10;
        static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(30);
        static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

        readonly IHttp _http;
        readonly string _endpoint;
        readonly string _ingestKey;
        readonly TrackerEnvelope _env;
        readonly EventQueue _queue;
        readonly Func<DateTime> _utcNow;
        readonly Dictionary<string, object> _common;
        bool _sending;
        int _failures;
        DateTime _nextAttemptUtc = DateTime.MinValue;

        public string LastError { get; private set; }
        public int Queued => _queue.Count;

        public ReleadTracker(IHttp http, string endpoint, string ingestKey, TrackerEnvelope envelope, string queueFile,
            Func<DateTime> utcNow, Dictionary<string, object> commonProps)
        {
            _http = http;
            _endpoint = (endpoint ?? "").TrimEnd('/');
            _ingestKey = ingestKey;
            _env = envelope;
            _queue = new EventQueue(queueFile);
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _common = commonProps ?? new Dictionary<string, object>();
        }

        public void Track(string name, Dictionary<string, object> props)
        {
            var p = new Dictionary<string, object>(_common);
            if (props != null) foreach (var kv in props) p[kv.Key] = kv.Value;
            var ev = new Dictionary<string, object>
            {
                { "name", name },
                { "ts", _utcNow().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) },
                { "props", p },
            };
            _queue.Add(MiniJson.Serialize(ev));
            if (_queue.Count >= AutoFlushAt) Flush();
        }

        public void Flush()
        {
            if (_sending || _queue.Count == 0) return;
            if (string.IsNullOrEmpty(_endpoint) || string.IsNullOrEmpty(_ingestKey) || _env == null || string.IsNullOrEmpty(_env.GameId)) return;
            if (_utcNow() < _nextAttemptUtc) return;

            string batchId;
            var lines = _queue.TakeBatch(MaxBatchEvents, MaxBatchBytes, out batchId);
            if (lines.Count == 0) return;
            var body = BuildBody(batchId, lines);
            var req = new HttpRequest
            {
                Url = _endpoint + "/v1/games/" + Uri.EscapeDataString(_env.GameId) + "/events",
                Method = "POST",
                Body = Encoding.UTF8.GetBytes(body),
            };
            req.Headers["Authorization"] = "Bearer " + _ingestKey;
            req.Headers["Content-Type"] = "application/json";
            _sending = true;
            _http.Send(req, resp =>
            {
                _sending = false;
                bool retry = resp.IsNetworkError || resp.Status >= 500 || resp.Status == 408 || resp.Status == 429;
                if (!retry)
                {
                    if (!resp.IsSuccess) LastError = "dropped batch: HTTP " + resp.Status;
                    else LastError = null;
                    _queue.CompleteBatch();
                    _failures = 0;
                    _nextAttemptUtc = DateTime.MinValue;
                    if (_queue.Count > 0 && resp.IsSuccess) Flush();
                    return;
                }
                _failures++;
                LastError = resp.IsNetworkError ? (resp.Error ?? "network error") : "HTTP " + resp.Status;
                _nextAttemptUtc = _utcNow() + Backoff(_failures);
            });
        }

        public static TimeSpan Backoff(int failures)
        {
            double sec = MinBackoff.TotalSeconds * Math.Pow(2, Math.Max(0, failures - 1));
            return TimeSpan.FromSeconds(Math.Min(sec, MaxBackoff.TotalSeconds));
        }

        string BuildBody(string batchId, List<string> eventLines)
        {
            var env = new Dictionary<string, object>
            {
                { "game_id", _env.GameId },
                { "install_id", _env.InstallId },
                { "app_version", _env.AppVersion },
                { "platform", _env.Platform },
                { "os_version", _env.OsVersion },
                { "locale", _env.Locale },
                { "queued_at", _utcNow().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) },
            };
            if (_env.Build.HasValue) env["build"] = _env.Build.Value;
            // Events are stored pre-serialised; splice them in rather than re-parse.
            var sb = new StringBuilder();
            sb.Append("{\"batch_id\":").Append(MiniJson.Serialize(batchId));
            sb.Append(",\"envelope\":").Append(MiniJson.Serialize(env));
            sb.Append(",\"events\":[");
            for (int i = 0; i < eventLines.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(eventLines[i]);
            }
            sb.Append("]}");
            return sb.ToString();
        }
    }
}
