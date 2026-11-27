using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Relead.CrossPromo
{
    /// <summary>IPromoPlayer for media type "video": VideoPlayer → RenderTexture → RawImage, from the local cache. Loops until closed.</summary>
    public sealed class VideoPromoPlayer : IPromoPlayer
    {
        public bool CanPlay(string mediaType) => mediaType == PromoMedia.TypeVideo;

        public void Play(PlaybackRequest request, Action<PromoResult> done)
        {
            var go = new GameObject("CrossPromo Video");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<VideoOverlay>().Begin(request, done);
        }
    }

    internal sealed class VideoOverlay : PromoOverlay
    {
        const float PrepareTimeoutSec = 10f;
        /// <summary>Largest video side we allocate a RenderTexture for (promo videos are ≤ 1920).</summary>
        public const int MaxVideoSide = 4096;

        VideoPlayer _vp;
        AudioSource _audio;
        RenderTexture _rt;
        RawImage _image;
        AspectRatioFitter _fitter;
        bool _prepared;
        bool _wantPlay;
        float _prepareElapsed;
        readonly StallWatch _stall = new StallWatch();

        protected override PromoOrientation ContentOrientation => Request.Media.Orientation;
        protected override bool ContentRunning => _vp != null && _vp.isPlaying;

        protected override void BuildContent(RectTransform area)
        {
            var holder = PromoUI.Stretch(PromoUI.Rect("Video", area));
            _image = holder.gameObject.AddComponent<RawImage>();
            _image.color = Color.black;
            _image.raycastTarget = false;
            _fitter = holder.gameObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _fitter.aspectRatio = Request.Media.Orientation == PromoOrientation.Landscape ? 16f / 9f : 9f / 16f;

            if (string.IsNullOrEmpty(Request.LocalPath) || !System.IO.File.Exists(Request.LocalPath))
            {
                Fail("media not cached");
                return;
            }
            _vp = gameObject.AddComponent<VideoPlayer>();
            _vp.playOnAwake = false;
            // Short videos loop until the download panel / reward; longer ones simply keep playing.
            _vp.isLooping = true;
            _vp.skipOnDrop = true;
            _vp.waitForFirstFrame = true;
            _vp.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            // Through an AudioSource, so AudioListener.volume (the game's master volume / mute) applies; it ignores
            // AudioListener.pause, so a host that pauses its audio on Opened still hears the promo.
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.ignoreListenerPause = true;
            _audio.spatialBlend = 0f;
            _audio.mute = IsMuted();
            _vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _vp.controlledAudioTrackCount = 1;
            _vp.EnableAudioTrack(0, true);
            _vp.SetTargetAudioSource(0, _audio);
            _vp.source = VideoSource.Url;
            _vp.url = "file://" + Request.LocalPath.Replace('\\', '/');
            _vp.renderMode = VideoRenderMode.RenderTexture;
            _vp.errorReceived += (p, msg) => Fail("video: " + msg);
            _vp.prepareCompleted += OnPrepared;
            _vp.Prepare();
        }

        bool IsMuted()
        {
            try { return Request.Muted != null && Request.Muted(); }
            catch (Exception) { return false; }
        }

        void OnPrepared(VideoPlayer p)
        {
            if (p.width == 0 || p.height == 0 || p.width > MaxVideoSide || p.height > MaxVideoSide)
            {
                Fail("video size " + p.width + "x" + p.height);
                return;
            }
            int w = (int)Mathf.Max(16, p.width), h = (int)Mathf.Max(16, p.height);
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "CrossPromoVideo" };
            _rt.Create();
            p.targetTexture = _rt;
            _image.texture = _rt;
            _image.color = Color.white;
            _fitter.aspectRatio = (float)w / h;
            _prepared = true;
            _stall.Reset();
            if (_wantPlay && AppActive) p.Play();
        }

        protected override void StartContent()
        {
            _wantPlay = true;
            if (_prepared && AppActive) _vp.Play();
        }

        /// <summary>Active time only: a paused app never times out or counts as stalled.</summary>
        protected override void TickContent(float dt)
        {
            if (_vp == null) return;
            if (!_prepared)
            {
                _prepareElapsed += dt;
                if (_prepareElapsed > PrepareTimeoutSec) Fail("prepare timeout");
                return;
            }
            if (!_wantPlay) return;
            _audio.mute = IsMuted();
            // Back from the background the platform may leave the decoder stopped: kick it.
            if (!_vp.isPlaying && _stall.StillSec > 0.5f) _vp.Play();
            if (_stall.Tick(dt, _vp.frame)) Fail("stalled");
        }

        protected override void PauseContent()
        {
            if (_vp != null && _prepared) _vp.Pause();
        }

        protected override void ResumeContent()
        {
            _stall.Reset();
            if (_vp != null && _prepared && _wantPlay) _vp.Play();
        }

        protected override void StopContent()
        {
            if (_vp != null) _vp.Stop();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}
