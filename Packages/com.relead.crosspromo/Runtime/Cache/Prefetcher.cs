using System;
using System.Collections.Generic;
using System.Globalization;

namespace Relead.CrossPromo
{
    /// <summary>When each promo was last shown (persisted), for prefetch rotation.</summary>
    public sealed class ShowHistory
    {
        const string Prefix = "relead.crosspromo.shown.";
        readonly IKeyValueStore _store;

        public ShowHistory(IKeyValueStore store) { _store = store; }

        public DateTime? LastShownUtc(string promoId)
        {
            var v = _store.Get(Prefix + promoId);
            long ticks;
            if (string.IsNullOrEmpty(v) || !long.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out ticks)) return null;
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        public void RecordShown(string promoId, DateTime utc)
            => _store.Set(Prefix + promoId, utc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
    }

    public sealed class PrefetchItem
    {
        public Promo Promo;
        public PromoMedia Media;
        public GameInfo Game;
    }

    public static class PrefetchPlanner
    {
        public const int DefaultSlots = 2;

        /// <summary>
        /// The media we want on disk: up to <paramref name="slots"/> eligible promos, never-shown first, then least
        /// recently shown, then higher weight, then id. A promo that was just shown therefore rotates out as soon as
        /// another one can take its slot. Uses the host orientation's media when it exists.
        /// </summary>
        public static List<PrefetchItem> Plan(Catalog catalog, SelectionContext ctx, Func<string, DateTime?> lastShown, int slots)
        {
            var noCache = new SelectionContext
            {
                HostGameId = ctx.HostGameId,
                HostAppIdentifier = ctx.HostAppIdentifier,
                Platform = ctx.Platform,
                PackageVersion = ctx.PackageVersion,
                NowUtc = ctx.NowUtc,
                HostOrientation = ctx.HostOrientation,
                PlayableTypes = ctx.PlayableTypes,
                IsInstalled = ctx.IsInstalled,
                RequireStoreLink = ctx.RequireStoreLink,
                IsMediaCached = null,
            };
            var eligible = PromoSelector.Eligible(catalog, noCache);
            eligible.Sort((a, b) =>
            {
                var la = lastShown != null ? lastShown(a.Id) : null;
                var lb = lastShown != null ? lastShown(b.Id) : null;
                if (la.HasValue != lb.HasValue) return la.HasValue ? 1 : -1;
                if (la.HasValue && la.Value != lb.Value) return la.Value.CompareTo(lb.Value);
                int w = b.Weight.CompareTo(a.Weight);
                return w != 0 ? w : string.CompareOrdinal(a.Id, b.Id);
            });
            var plan = new List<PrefetchItem>();
            foreach (var p in eligible)
            {
                if (plan.Count >= slots) break;
                plan.Add(new PrefetchItem { Promo = p, Media = PromoSelector.ChooseMedia(p, noCache, false), Game = catalog.FindGame(p.GameId) });
            }
            return plan;
        }
    }

    public enum PrefetchOutcome { NotOnWifi, Busy, NothingToDo, Done, PartlyFailed }

    /// <summary>
    /// Downloads the planned media (and the target games' icons) one at a time, only while on wifi.
    /// When everything planned is on disk, all other cached files are evicted (disk cap = the slots).
    /// Failures are not retried in a loop; the next trigger (next launch / after the next show) tries again.
    /// </summary>
    public sealed class Prefetcher
    {
        readonly MediaCache _cache;
        readonly IHttp _http;
        readonly Func<bool> _isOnWifi;
        bool _busy;

        public string LastError { get; private set; }
        public bool IsBusy => _busy;

        public Prefetcher(MediaCache cache, IHttp http, Func<bool> isOnWifi)
        {
            _cache = cache;
            _http = http;
            _isOnWifi = isOnWifi;
        }

        public static List<string> KeepSet(List<PrefetchItem> plan)
        {
            var keep = new List<string>();
            foreach (var it in plan)
            {
                if (it.Media != null && !keep.Contains(it.Media.Url)) keep.Add(it.Media.Url);
                if (it.Game != null && !string.IsNullOrEmpty(it.Game.Icon) && !keep.Contains(it.Game.Icon)) keep.Add(it.Game.Icon);
            }
            return keep;
        }

        public void Run(List<PrefetchItem> plan, string baseUrl, Action<PrefetchOutcome> done)
        {
            if (_busy) { if (done != null) done(PrefetchOutcome.Busy); return; }
            if (!_isOnWifi()) { if (done != null) done(PrefetchOutcome.NotOnWifi); return; }

            var jobs = new List<Job>();
            foreach (var it in plan)
            {
                if (it.Media != null && !_cache.IsCached(it.Media.Url))
                    jobs.Add(new Job { Url = it.Media.Url, Bytes = it.Media.Bytes, Sha256 = it.Media.Sha256 });
                if (it.Game != null && !string.IsNullOrEmpty(it.Game.Icon) && !_cache.IsCached(it.Game.Icon) && !jobs.Exists(j => j.Url == it.Game.Icon))
                    jobs.Add(new Job { Url = it.Game.Icon, Optional = true });
            }
            var keep = KeepSet(plan);
            if (jobs.Count == 0)
            {
                _cache.EvictExcept(keep);
                if (done != null) done(PrefetchOutcome.NothingToDo);
                return;
            }
            _busy = true;
            Next(jobs, 0, baseUrl, false, failed =>
            {
                _busy = false;
                bool allRequiredCached = true;
                foreach (var it in plan) if (it.Media != null && !_cache.IsCached(it.Media.Url)) allRequiredCached = false;
                if (allRequiredCached) _cache.EvictExcept(keep);
                if (done != null) done(failed ? PrefetchOutcome.PartlyFailed : PrefetchOutcome.Done);
            });
        }

        sealed class Job
        {
            public string Url;
            public long? Bytes;
            public string Sha256;
            public bool Optional;
        }

        void Next(List<Job> jobs, int i, string baseUrl, bool failed, Action<bool> done)
        {
            if (i >= jobs.Count) { done(failed); return; }
            if (!_isOnWifi()) { LastError = "left wifi"; done(true); return; }
            var job = jobs[i];
            var tmp = _cache.TempPathFor(job.Url);
            var req = new HttpRequest { Url = UrlUtil.Join(baseUrl, job.Url), DownloadToPath = tmp, TimeoutSec = 120 };
            _http.Send(req, resp =>
            {
                bool ok = false;
                string err;
                if (resp.IsSuccess) ok = _cache.Commit(tmp, job.Url, job.Bytes, job.Sha256, out err);
                else err = resp.IsNetworkError ? (resp.Error ?? "network error") : "HTTP " + resp.Status;
                if (!ok)
                {
                    LastError = job.Url + ": " + err;
                    try { if (System.IO.File.Exists(tmp)) System.IO.File.Delete(tmp); } catch (Exception) { }
                    if (!job.Optional) failed = true;
                }
                Next(jobs, i + 1, baseUrl, failed, done);
            });
        }
    }
}
