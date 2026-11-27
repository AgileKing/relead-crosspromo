using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Relead.CrossPromo
{
    /// <summary>
    /// Public entry point.
    /// <code>
    /// CrossPromo.Init(new CrossPromoConfig { HostGameId = "gpking", IngestKey = "…" });
    /// button.interactable = CrossPromo.IsReady;
    /// CrossPromo.Show(r => { if (r.Outcome == PromoOutcome.Completed) GrantReward(); });
    /// </code>
    /// Never blocks: catalog check and downloads run in the background.
    /// </summary>
    public static class CrossPromo
    {
        const string InstallIdKey = "relead.crosspromo.installId";
        const string FirstOpenKey = "relead.crosspromo.firstOpenSent";
        const string LastShownKey = "relead.crosspromo.lastShownPromo";

        static CrossPromoConfig _config;
        static CrossPromoRunner _runner;
        static IHttp _http;
        static CatalogUpdater _catalog;
        static MediaCache _cache;
        static Prefetcher _prefetcher;
        static CachingInstalledChecker _installed;
        static DailyCap _cap;
        static ShowHistory _history;
        static IKeyValueStore _store;
        static ITracker _tracker;
        static PromoPlatform _platform;
        static readonly System.Random Random = new System.Random();
        static bool _showing;

        public static bool IsInitialized => _config != null;
        public static bool IsShowing => _showing;
        public static ITracker Tracker => _tracker;
        public static Catalog Catalog => _catalog != null ? _catalog.Catalog : null;
        public static string InstallId { get; private set; }

        /// <summary>Raised when a promo opens / closes (e.g. to mute the host's music).</summary>
        public static event Action Opened;
        public static event Action<PromoResult> Closed;

        public static void Init(CrossPromoConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.HostGameId)) throw new ArgumentException("CrossPromoConfig.HostGameId is required");
            if (_config != null) { Log("Init called twice; ignored"); return; }
            _config = config;
            _platform = CurrentPlatform();
            if (string.IsNullOrEmpty(config.Language)) config.Language = LanguageCode(Application.systemLanguage);

            var go = new GameObject("Relead CrossPromo");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _runner = go.AddComponent<CrossPromoRunner>();
            _http = new UnityHttp(_runner);
            _store = new PlayerPrefsStore();
            _cap = new DailyCap(_store);
            _history = new ShowHistory(_store);

            var root = Path.Combine(Application.persistentDataPath, "relead-crosspromo");
            _cache = new MediaCache(Path.Combine(root, "media"));
            _catalog = new CatalogUpdater(root, _http, config.BaseUrls, () => DateTime.UtcNow);
            _prefetcher = new Prefetcher(_cache, _http, () => Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork);
            _installed = new CachingInstalledChecker(CreateInstalledChecker(_platform));

            InstallId = !string.IsNullOrEmpty(config.InstallId) ? config.InstallId : StableInstallId();
            _tracker = config.Tracker ?? CreateTracker(root);

            if (config.Fallback == null) config.Fallback = Resources.Load<FallbackGameSet>(FallbackGameSet.ResourceName);

            _catalog.LoadFromDisk();
            WarnIfUnknownHost();
            SendFirstOpenOnce();
            Refresh();
        }

        /// <summary>True when Show would show something: a cached video, or the bundled slideshow.</summary>
        public static bool IsReady => IsInitialized && !_showing && UnderCap() && (SelectVideo() != null || SelectFallback().Count > 0);

        /// <summary>True when a cached video is ready (not just the fallback slideshow).</summary>
        public static bool IsVideoReady => IsInitialized && !_showing && UnderCap() && SelectVideo() != null;

        /// <summary>
        /// True when today's maxPerDay is used up (IsReady is then false). Show still works: it shows the
        /// "Maxed out ads for today" notice and reports PromoOutcome.Capped. Keep a button active while
        /// <c>CrossPromo.IsReady || CrossPromo.IsCappedToday</c>.
        /// </summary>
        public static bool IsCappedToday => IsInitialized && !UnderCap();

        /// <summary>Promos the player can still watch today (local day). 0 before Init.</summary>
        public static int RemainingToday => IsInitialized ? _cap.Remaining(DateTime.Now, MaxPerDay()) : 0;

        /// <summary>
        /// Shows a promo. onResult is always called once. Reward on PromoOutcome.Completed only.
        /// Daily cap reached: shows a small dismissable notice instead and reports PromoOutcome.Capped.
        /// </summary>
        public static void Show(Action<PromoResult> onResult)
        {
            if (!IsInitialized) { Done(onResult, Failed("not initialized")); return; }
            if (_showing) { Done(onResult, Failed("already showing")); return; }
            if (!UnderCap()) { ShowCapped(onResult); return; }
            var choice = SelectVideo();
            if (choice != null) { ShowVideo(choice, _config.HostOrientation != null ? _config.HostOrientation() : HostShape(), onResult); return; }
            var fallback = SelectFallback();
            if (fallback.Count > 0) { ShowFallback(fallback, onResult); return; }
            Track("promo_failed", null, null, new Dictionary<string, object> { { "reason", "nothing ready" } });
            Done(onResult, Failed("nothing ready"));
        }

        /// <summary>Editor/testing: show a specific promo as if the host had the given orientation (ignores cap and filters, needs cached media).</summary>
        public static void ShowPromoForTesting(string promoId, PromoOrientation hostOrientation, Action<PromoResult> onResult)
        {
            if (!IsInitialized || Catalog == null) { Done(onResult, Failed("no catalog")); return; }
            var p = Catalog.FindPromo(promoId);
            if (p == null) { Done(onResult, Failed("unknown promo")); return; }
            var ctx = Context();
            ctx.HostOrientation = hostOrientation;
            var media = PromoSelector.ChooseMedia(p, ctx, true);
            if (media == null) { Done(onResult, Failed("media not cached")); return; }
            ShowVideo(new PromoChoice { Promo = p, Media = media, Game = Catalog.FindGame(p.GameId), NeedsRotation = media.Orientation != hostOrientation }, hostOrientation, onResult);
        }

        public static void ShowFallbackForTesting(Action<PromoResult> onResult)
        {
            var f = SelectFallback();
            if (f.Count == 0) { Done(onResult, Failed("no fallback games")); return; }
            ShowFallback(f, onResult);
        }

        /// <summary>Editor/testing: download a promo's media now (ignores wifi rule).</summary>
        public static void DownloadForTesting(string promoId, Action<bool> done)
        {
            var p = Catalog != null ? Catalog.FindPromo(promoId) : null;
            if (p == null) { if (done != null) done(false); return; }
            var plan = new List<PrefetchItem>();
            foreach (var m in p.Media) plan.Add(new PrefetchItem { Promo = p, Media = m, Game = Catalog.FindGame(p.GameId) });
            new Prefetcher(_cache, _http, () => true).Run(plan, _catalog.ActiveBaseUrl, o => { if (done != null) done(o == PrefetchOutcome.Done || o == PrefetchOutcome.NothingToDo); });
        }

        public static bool IsCached(PromoMedia m) => _cache != null && _cache.IsCached(m);

        /// <summary>Catalog check (if due) then prefetch. Called at Init and on resume; safe to call any time.</summary>
        public static void Refresh()
        {
            if (!IsInitialized) return;
            _catalog.CheckIfDue(src =>
            {
                if (src == CatalogSource.Network)
                    Track("promo_catalog_loaded", null, null, new Dictionary<string, object> { { "promos", Catalog.Promos.Count }, { "baseUrl", _catalog.ActiveBaseUrl } });
                if (_catalog.LastError != null) Log("catalog: " + _catalog.LastError);
                Prefetch();
            });
        }

        static void Prefetch()
        {
            if (Catalog == null) return;
            var plan = PrefetchPlanner.Plan(Catalog, Context(), _history.LastShownUtc, PrefetchPlanner.DefaultSlots);
            _prefetcher.Run(plan, _catalog.ActiveBaseUrl, o =>
            {
                Log("prefetch: " + o + (_prefetcher.LastError != null ? " (" + _prefetcher.LastError + ")" : ""));
            });
        }

        internal static void OnPause(bool paused)
        {
            if (!IsInitialized) return;
            if (paused) { _tracker.Flush(); return; }
            _installed.Clear();
            Refresh();
            _tracker.Flush();
        }

        /// <summary>Capped notice. No Opened/Closed events: no audio, nothing to pause, no reward.</summary>
        static void ShowCapped(Action<PromoResult> onResult)
        {
            _showing = true;
            Track("promo_capped", null, null, new Dictionary<string, object> { { "maxPerDay", MaxPerDay() } });
            Action done = () => { _showing = false; Done(onResult, CappedResult()); };
            try { CappedNotice.Show(_config.Language, new PlaybackRequest().SortingOrder, done); }
            catch (Exception e)
            {
                Debug.LogException(e);
                done();
            }
        }

        internal static PromoResult CappedResult() => new PromoResult { Outcome = PromoOutcome.Capped, FailReason = "daily cap" };

        static void ShowVideo(PromoChoice c,PromoOrientation host, Action<PromoResult> onResult)
        {
            var link = StoreLinks.Build(_platform, c.Game, _config.HostGameId, c.Promo.Id, _config.IosProviderToken);
            var target = new PromoTarget
            {
                Game = c.Game,
                StoreUrl = link != null ? link.Primary : null,
                Headline = c.Promo.Headline.Get(_config.Language),
                Cta = c.Promo.Cta.Get(_config.Language),
                Icon = c.Game != null ? LoadCachedTexture(c.Game.Icon) : null,
            };
            var req = BaseRequest(c.Promo.Id, host);
            req.Media = c.Media;
            req.LocalPath = _cache.PathFor(c.Media.Url);
            req.Targets.Add(target);
            Play(new VideoPromoPlayer(), req, c.Promo.Id, c.Game != null ? c.Game.Id : null, false, onResult);
        }

        static void ShowFallback(List<FallbackGame> games, Action<PromoResult> onResult)
        {
            var req = BaseRequest("fallback", _config.HostOrientation != null ? _config.HostOrientation() : HostShape());
            foreach (var g in games)
            {
                var info = g.ToGameInfo();
                var link = StoreLinks.Build(_platform, info, _config.HostGameId, "fallback", _config.IosProviderToken);
                var t = new PromoTarget { Game = info, StoreUrl = link != null ? link.Primary : null };
                foreach (var s in g.screenshots) if (s != null) t.Screenshots.Add(s);
                req.Targets.Add(t);
            }
            Play(new SlideshowPromoPlayer(), req, "fallback", null, true, onResult);
        }

        static PlaybackRequest BaseRequest(string promoId, PromoOrientation host)
        {
            var rules = Catalog != null ? Catalog.Rules : new CatalogRules();
            return new PlaybackRequest
            {
                PromoId = promoId,
                SkipAfterSec = rules.SkipAfterSec,
                EndCardAtSec = rules.EndCardAtSec,
                RewardAfterSec = rules.RewardAfterSec,
                RotateHintTimeoutSec = rules.RotateHintTimeoutSec,
                Language = _config.Language,
                HostOrientation = () => host,
                DeviceOrientation = _config.DeviceOrientation ?? SafeDeviceOrientation,
                ScreenOrientation = () => Screen.orientation,
            };
        }

        static void Play(IPromoPlayer player, PlaybackRequest req, string promoId, string targetGameId, bool fallback, Action<PromoResult> onResult)
        {
            _showing = true;
            var baseProps = new Dictionary<string, object> { { "fallback", fallback } };
            if (req.Media != null) baseProps["orientation"] = req.Media.Orientation == PromoOrientation.Landscape ? "landscape" : "portrait";
            req.Track = (name, props) =>
            {
                var tg = props != null && props.ContainsKey("targetGameId") ? props["targetGameId"] as string : targetGameId;
                Track(name, promoId, tg, Merge(baseProps, props));
            };
            Track("promo_shown", promoId, targetGameId, baseProps);
            _cap.RecordShown(DateTime.Now);
            if (!fallback)
            {
                _history.RecordShown(promoId, DateTime.UtcNow);
                _store.Set(LastShownKey, promoId);
            }
            RaiseOpened();
            player.Play(req, result =>
            {
                _showing = false;
                if (result.TargetGameId == null) result.TargetGameId = targetGameId;
                var props = Merge(baseProps, new Dictionary<string, object> { { "watchSec", Math.Round(result.WatchedSec, 1) }, { "storeClicked", result.StoreClicked } });
                switch (result.Outcome)
                {
                    case PromoOutcome.Completed: Track("promo_completed", promoId, result.TargetGameId, props); break;
                    case PromoOutcome.Skipped: Track("promo_skipped", promoId, result.TargetGameId, props); break;
                    default: props["reason"] = result.FailReason; Track("promo_failed", promoId, result.TargetGameId, props); break;
                }
                _tracker.Flush();
                Prefetch();
                var closed = Closed;
                if (closed != null) { try { closed(result); } catch (Exception e) { Debug.LogException(e); } }
                Done(onResult, result);
            });
        }

        static PromoChoice SelectVideo()
        {
            if (Catalog == null) return null;
            var ctx = Context();
            ctx.IsMediaCached = _cache.IsCached;
            ctx.LastShownPromoId = _store.Get(LastShownKey);
            return PromoSelector.Select(Catalog, ctx, Random.NextDouble());
        }

        static List<FallbackGame> SelectFallback()
        {
            var set = _config != null ? _config.Fallback : null;
            return FallbackSelector.Select(set != null ? set.games : null, _config.HostGameId, _platform, _installed.IsInstalled, 3, Random, Application.identifier);
        }

        static SelectionContext Context() => new SelectionContext
        {
            HostGameId = _config.HostGameId,
            HostAppIdentifier = Application.identifier,
            Platform = _platform,
            NowUtc = DateTime.UtcNow,
            HostOrientation = _config.HostOrientation != null ? _config.HostOrientation() : HostShape(),
            Language = _config.Language,
            ScreenAspect = PromoSelector.Aspect(Screen.width, Screen.height),
            IsInstalled = _installed.IsInstalled,
            RequireStoreLink = _config.RequireStoreLink,
        };

        static bool UnderCap() => _cap.CanShow(DateTime.Now, MaxPerDay());

        static int MaxPerDay() => Catalog != null ? Catalog.Rules.MaxPerDay : new CatalogRules().MaxPerDay;

        static void Track(string name, string promoId, string targetGameId, Dictionary<string, object> props)
        {
            var p = props != null ? new Dictionary<string, object>(props) : new Dictionary<string, object>();
            if (promoId != null) p["promoId"] = promoId;
            if (targetGameId != null) p["targetGameId"] = targetGameId;
            try { _tracker.Track(name, p); } catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>A typo in HostGameId would make the game promote itself; the package-name check catches most cases, this makes it visible.</summary>
        static void WarnIfUnknownHost()
        {
            bool known = Catalog != null && Catalog.FindGame(_config.HostGameId) != null;
            if (!known && _config.Fallback != null)
                foreach (var g in _config.Fallback.games) if (g != null && g.id == _config.HostGameId) known = true;
            if (!known && (Catalog != null || _config.Fallback != null))
                Debug.LogWarning("[CrossPromo] HostGameId '" + _config.HostGameId + "' is not a known Relead game id (catalog games[].id, e.g. kingofgames, puzzleking, gpking). This game may be promoted to itself.");
        }

        static void SendFirstOpenOnce()
        {
            if (_store.Get(FirstOpenKey) == "1") return;
            var p = new Dictionary<string, object>();
            var vendor = VendorId.Get();
            if (vendor != null) p["vendorId"] = vendor;
            Track("first_open", null, null, p);
            _store.Set(FirstOpenKey, "1");
        }

        static ITracker CreateTracker(string root)
        {
            var common = new Dictionary<string, object>
            {
                { "hostGameId", _config.HostGameId },
                { "platform", PlatformName(_platform) },
                { "packageVersion", CrossPromoInfo.PackageVersion },
            };
            var vendor = VendorId.Get();
            if (vendor != null) common["vendorId"] = vendor;
            var env = new TrackerEnvelope
            {
                GameId = string.IsNullOrEmpty(_config.TrackingGameId) ? _config.HostGameId : _config.TrackingGameId,
                InstallId = InstallId,
                AppVersion = Application.version,
                Platform = _platform == PromoPlatform.IOS ? "iOS" : _platform.ToString(),
                OsVersion = SystemInfo.operatingSystem,
                DeviceModel = SystemInfo.deviceModel,
                Locale = _config.Language,
            };
            return new ReleadTracker(_http, _config.TrackingEndpoint, _config.IngestKey, env, Path.Combine(root, "events.jsonl"), () => DateTime.UtcNow, common);
        }

        static IInstalledChecker CreateInstalledChecker(PromoPlatform p)
        {
#if UNITY_EDITOR
            return new NoInstalledChecker();
#else
            if (p == PromoPlatform.IOS) return new IosInstalledChecker();
            if (p == PromoPlatform.Android) return new AndroidInstalledChecker();
            return new NoInstalledChecker();
#endif
        }

        static string StableInstallId()
        {
            var id = _store.Get(InstallIdKey);
            if (string.IsNullOrEmpty(id)) { id = Guid.NewGuid().ToString(); _store.Set(InstallIdKey, id); }
            return id;
        }

        static PromoPlatform CurrentPlatform()
        {
#if UNITY_IOS
            return PromoPlatform.IOS;
#elif UNITY_ANDROID
            return PromoPlatform.Android;
#else
            return PromoPlatform.Windows;
#endif
        }

        internal static string PlatformName(PromoPlatform p) => p == PromoPlatform.IOS ? "ios" : p == PromoPlatform.Android ? "android" : "windows";

        static PromoOrientation HostShape() => OrientationLogic.FromSize(Screen.width, Screen.height);

        static DeviceOrientation SafeDeviceOrientation()
        {
            try { return Input.deviceOrientation; }
            catch (Exception) { return DeviceOrientation.Unknown; }
        }

        static Texture2D LoadCachedTexture(string relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl) || !_cache.IsCached(relativeUrl)) return null;
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(File.ReadAllBytes(_cache.PathFor(relativeUrl)))) return tex;
                UnityEngine.Object.Destroy(tex);
            }
            catch (Exception e) { Log("icon: " + e.Message); }
            return null;
        }

        internal static string LanguageCode(SystemLanguage l)
        {
            switch (l)
            {
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.German: return "de";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Portuguese: return "pt";
                default: return "en";
            }
        }

        static Dictionary<string, object> Merge(Dictionary<string, object> a, Dictionary<string, object> b)
        {
            var d = new Dictionary<string, object>(a);
            if (b != null) foreach (var kv in b) d[kv.Key] = kv.Value;
            return d;
        }

        static PromoResult Failed(string reason) => new PromoResult { Outcome = PromoOutcome.Failed, FailReason = reason };

        static void Done(Action<PromoResult> cb, PromoResult r)
        {
            if (cb == null) return;
            try { cb(r); } catch (Exception e) { Debug.LogException(e); }
        }

        static void RaiseOpened()
        {
            var o = Opened;
            if (o != null) { try { o(); } catch (Exception e) { Debug.LogException(e); } }
        }

        static void Log(string msg)
        {
            if (_config != null && _config.VerboseLogging) Debug.Log("[CrossPromo] " + msg);
        }

#if UNITY_EDITOR
        /// <summary>Editor: reset static state when entering play mode with domain reload disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _config = null;
            _showing = false;
            Opened = null;
            Closed = null;
        }
#endif
    }

    internal sealed class CrossPromoRunner : MonoBehaviour
    {
        void OnApplicationPause(bool paused) => CrossPromo.OnPause(paused);
    }

    internal sealed class PlayerPrefsStore : IKeyValueStore
    {
        public string Get(string key) => PlayerPrefs.GetString(key, null);
        public void Set(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
    }
}
