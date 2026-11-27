using System;
using System.Collections.Generic;
using UnityEngine;

namespace Relead.CrossPromo
{
    public enum PromoOutcome
    {
        /// <summary>Watched to the end. The host grants the reward.</summary>
        Completed,
        /// <summary>Closed early (after skipAfterSec). No reward.</summary>
        Skipped,
        /// <summary>Could not play. No reward.</summary>
        Failed,
    }

    public sealed class PromoResult
    {
        public PromoOutcome Outcome;
        public bool StoreClicked;
        public string PromoId;
        public string TargetGameId;
        public float WatchedSec;
        public string FailReason;
        /// <summary>True when the bundled offline slideshow was shown instead of a video.</summary>
        public bool WasFallback;

        public override string ToString() => Outcome + (StoreClicked ? " +store" : "") + " " + PromoId + " " + WatchedSec.ToString("0.0") + "s" + (FailReason != null ? " (" + FailReason + ")" : "");
    }

    /// <summary>One game shown on a slide or end card.</summary>
    public sealed class PromoTarget
    {
        public GameInfo Game;
        public string StoreUrl;
        public Texture2D Icon;
        public string Headline;
        public string Cta;
        public List<Texture2D> Screenshots = new List<Texture2D>();
    }

    public sealed class PlaybackRequest
    {
        public string PromoId;
        public PromoMedia Media;
        /// <summary>Local file (cached) for video media.</summary>
        public string LocalPath;
        /// <summary>One target for a video; several for the fallback slideshow.</summary>
        public List<PromoTarget> Targets = new List<PromoTarget>();
        public float SkipAfterSec = 5f;
        public float RotateHintTimeoutSec = 3f;
        public string Language = "en";
        public Func<PromoOrientation> HostOrientation;
        public Func<DeviceOrientation> DeviceOrientation;
        public Func<ScreenOrientation> ScreenOrientation;
        /// <summary>Event name + props, forwarded to the tracker.</summary>
        public Action<string, Dictionary<string, object>> Track;
        /// <summary>Opens the store link (Application.OpenURL by default).</summary>
        public Action<PromoTarget> OpenStore;
        public int SortingOrder = 30000;
    }

    /// <summary>Plays one promo. VideoPromoPlayer now; a WebPromoPlayer (option C, playables) later.</summary>
    public interface IPromoPlayer
    {
        bool CanPlay(string mediaType);
        void Play(PlaybackRequest request, Action<PromoResult> done);
    }
}
