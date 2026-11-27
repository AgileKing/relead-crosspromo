using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Relead.CrossPromo.Json;

namespace Relead.CrossPromo
{
    public enum CatalogSource { None, Disk, Network, NotModified }

    /// <summary>
    /// Keeps the last good catalog on disk and checks the network for a new one at most once a day.
    /// - Conditional GET (If-None-Match / If-Modified-Since) when we have validators from the same base URL.
    /// - Base URL list: the last one that worked is tried first, then the others in config order; first good answer wins.
    /// - A failed check does not record a check time (so the next launch retries) and is not retried in this session.
    /// </summary>
    public sealed class CatalogUpdater
    {
        public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
        public const string CatalogPath = "catalog/promos.json";

        readonly string _dir;
        readonly IHttp _http;
        readonly IList<string> _baseUrls;
        readonly Func<DateTime> _utcNow;
        bool _attemptedThisSession;

        public Catalog Catalog { get; private set; }
        /// <summary>Base URL that served the current catalog; media is fetched from there.</summary>
        public string ActiveBaseUrl { get; private set; }
        public string LastError { get; private set; }
        public bool IsChecking { get; private set; }

        internal string ETag;
        internal string LastModified;
        internal DateTime? LastCheckUtc;

        public CatalogUpdater(string dir, IHttp http, IList<string> baseUrls, Func<DateTime> utcNow)
        {
            _dir = dir;
            _http = http;
            _baseUrls = baseUrls ?? new List<string>();
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        string CatalogFile => Path.Combine(_dir, "catalog.json");
        string StateFile => Path.Combine(_dir, "catalog-state.json");

        /// <summary>Load the last good catalog from disk. Instant; call at Init.</summary>
        public bool LoadFromDisk()
        {
            ReadState();
            try
            {
                if (!File.Exists(CatalogFile)) return false;
                var r = CatalogParser.Parse(File.ReadAllText(CatalogFile));
                if (!r.Ok) { LastError = "disk catalog: " + r.Error; return false; }
                Catalog = r.Catalog;
                if (string.IsNullOrEmpty(ActiveBaseUrl) && _baseUrls.Count > 0) ActiveBaseUrl = _baseUrls[0];
                return true;
            }
            catch (Exception e) { LastError = "disk catalog: " + e.Message; return false; }
        }

        public bool IsCheckDue()
        {
            if (_attemptedThisSession) return false;
            if (Catalog == null) return true;
            if (LastCheckUtc == null) return true;
            var now = _utcNow();
            if (now < LastCheckUtc.Value) return true; // clock moved back: don't get stuck for days
            return now - LastCheckUtc.Value >= CheckInterval;
        }

        /// <summary>Checks the network if due; otherwise calls back right away with the disk state.</summary>
        public void CheckIfDue(Action<CatalogSource> done)
        {
            if (!IsCheckDue() || IsChecking)
            {
                if (done != null) done(Catalog != null ? CatalogSource.Disk : CatalogSource.None);
                return;
            }
            _attemptedThisSession = true;
            IsChecking = true;
            TryUrl(OrderedBaseUrls(), 0, src =>
            {
                IsChecking = false;
                if (done != null) done(src);
            });
        }

        List<string> OrderedBaseUrls()
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(ActiveBaseUrl) && _baseUrls.Contains(ActiveBaseUrl)) list.Add(ActiveBaseUrl);
            foreach (var u in _baseUrls) if (!list.Contains(u)) list.Add(u);
            return list;
        }

        void TryUrl(List<string> urls, int i, Action<CatalogSource> done)
        {
            if (i >= urls.Count)
            {
                done(Catalog != null ? CatalogSource.Disk : CatalogSource.None);
                return;
            }
            var baseUrl = urls[i];
            var req = new HttpRequest { Url = UrlUtil.Join(baseUrl, CatalogPath) };
            bool sameHost = baseUrl == ActiveBaseUrl && Catalog != null;
            if (sameHost && !string.IsNullOrEmpty(ETag)) req.Headers["If-None-Match"] = ETag;
            if (sameHost && !string.IsNullOrEmpty(LastModified)) req.Headers["If-Modified-Since"] = LastModified;

            _http.Send(req, resp =>
            {
                if (resp.Status == 304 && sameHost)
                {
                    LastCheckUtc = _utcNow();
                    WriteState();
                    done(CatalogSource.NotModified);
                    return;
                }
                if (resp.Status == 200)
                {
                    var text = resp.Text;
                    var r = CatalogParser.Parse(text);
                    if (r.Ok)
                    {
                        Catalog = r.Catalog;
                        ActiveBaseUrl = baseUrl;
                        ETag = resp.Header("ETag");
                        LastModified = resp.Header("Last-Modified");
                        LastCheckUtc = _utcNow();
                        try
                        {
                            Directory.CreateDirectory(_dir);
                            var tmp = CatalogFile + ".tmp";
                            File.WriteAllText(tmp, text);
                            if (File.Exists(CatalogFile)) File.Delete(CatalogFile);
                            File.Move(tmp, CatalogFile);
                        }
                        catch (Exception e) { LastError = "write catalog: " + e.Message; }
                        WriteState();
                        LastError = null;
                        done(CatalogSource.Network);
                        return;
                    }
                    LastError = baseUrl + ": " + r.Error;
                }
                else LastError = baseUrl + ": " + (resp.IsNetworkError ? (resp.Error ?? "network error") : "HTTP " + resp.Status);
                TryUrl(urls, i + 1, done);
            });
        }

        void ReadState()
        {
            try
            {
                if (!File.Exists(StateFile)) return;
                object o;
                string err;
                if (!MiniJson.TryParse(File.ReadAllText(StateFile), out o, out err)) return;
                var d = o as Dictionary<string, object>;
                ETag = JsonRead.Str(d, "etag");
                LastModified = JsonRead.Str(d, "lastModified");
                ActiveBaseUrl = JsonRead.Str(d, "baseUrl");
                var t = JsonRead.Str(d, "lastCheckUtc");
                DateTime dt;
                if (t != null && DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out dt))
                    LastCheckUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            catch (Exception e) { LastError = "state: " + e.Message; }
        }

        void WriteState()
        {
            try
            {
                Directory.CreateDirectory(_dir);
                var d = new Dictionary<string, object>
                {
                    { "etag", ETag },
                    { "lastModified", LastModified },
                    { "baseUrl", ActiveBaseUrl },
                    { "lastCheckUtc", LastCheckUtc.HasValue ? LastCheckUtc.Value.ToString("o", CultureInfo.InvariantCulture) : null },
                };
                File.WriteAllText(StateFile, MiniJson.Serialize(d));
            }
            catch (Exception e) { LastError = "state: " + e.Message; }
        }
    }
}
