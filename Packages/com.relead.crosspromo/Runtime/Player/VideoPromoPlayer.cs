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

        VideoPlayer _vp;
        RenderTexture _rt;
        RawImage _image;
        AspectRatioFitter _fitter;
        bool _prepared;
        bool _wantPlay;
        float _prepareElapsed;

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
            _vp.audioOutputMode = VideoAudioOutputMode.Direct;
            _vp.source = VideoSource.Url;
            _vp.url = "file://" + Request.LocalPath.Replace('\\', '/');
            _vp.renderMode = VideoRenderMode.RenderTexture;
            _vp.errorReceived += (p, msg) => Fail("video: " + msg);
            _vp.prepareCompleted += OnPrepared;
            _vp.Prepare();
        }

        void OnPrepared(VideoPlayer p)
        {
            int w = (int)Mathf.Max(16, p.width), h = (int)Mathf.Max(16, p.height);
            _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "CrossPromoVideo" };
            _rt.Create();
            p.targetTexture = _rt;
            _image.texture = _rt;
            _image.color = Color.white;
            _fitter.aspectRatio = (float)w / h;
            _prepared = true;
            if (_wantPlay) p.Play();
        }

        protected override void StartContent()
        {
            _wantPlay = true;
            if (_prepared) _vp.Play();
        }

        protected override void TickContent(float dt)
        {
            if (_prepared || _vp == null) return;
            _prepareElapsed += dt;
            if (_prepareElapsed > PrepareTimeoutSec) Fail("prepare timeout");
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
