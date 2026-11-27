using System;
using UnityEngine;

namespace Relead.CrossPromo
{
    /// <summary>Everything a host game passes to CrossPromo.Init. Only HostGameId is required.</summary>
    public sealed class CrossPromoConfig
    {
        /// <summary>This game's id in the catalog (games[].id), e.g. "gpking". Required.</summary>
        public string HostGameId;

        /// <summary>Game id at api.relead.com (route + envelope). Default: HostGameId.</summary>
        public string TrackingGameId;
        /// <summary>This game's ingest key for api.relead.com. Empty = events stay queued, nothing sent.</summary>
        public string IngestKey;
        public string TrackingEndpoint = "https://api.relead.com";
        /// <summary>Use the host's own analytics instead of ReleadTracker.</summary>
        public ITracker Tracker;

        /// <summary>Content hosts, tried in order (the last one that worked first). ads.* may be DNS-blocked, hence a list.</summary>
        public string[] BaseUrls = { "https://ads.relead.com", "https://lemon-sand-07a4f1003.6.azurestaticapps.net" };

        /// <summary>Language code for texts, e.g. "nl". Default: from Application.systemLanguage.</summary>
        public string Language;
        /// <summary>Host orientation. Default: from Screen.width/height (the host is locked, so this is stable).</summary>
        public Func<PromoOrientation> HostOrientation;
        /// <summary>Physical device orientation. Default: Input.deviceOrientation (Unknown if the legacy Input API is disabled).</summary>
        public Func<DeviceOrientation> DeviceOrientation;

        /// <summary>The host's own stable install id, if it has one (shared with its analytics). Default: our own random id.</summary>
        public string InstallId;
        /// <summary>App Store Connect provider token (pt) for iOS campaign links. Optional.</summary>
        public string IosProviderToken;
        /// <summary>Bundled offline slideshow. Default: Resources/ReleadCrossPromoFallback in the package.</summary>
        public FallbackGameSet Fallback;

        /// <summary>Editor/testing: also show promos whose game has no store id yet ("TODO").</summary>
        public bool RequireStoreLink = true;
        public bool VerboseLogging;
        /// <summary>Optional: return true while the player has the game's sound off; the promo video is then muted too.
        /// AudioListener.volume applies anyway.</summary>
        public Func<bool> IsMuted;
    }
}
