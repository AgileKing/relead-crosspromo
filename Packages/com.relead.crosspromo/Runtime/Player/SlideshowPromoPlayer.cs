using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Relead.CrossPromo
{
    /// <summary>
    /// Offline fallback: slideshow of screenshots bundled in the package (FallbackGameSet), each slide with the
    /// game name and a store button. Drawn in the host orientation (screenshots letterboxed), no rotate hint.
    /// Same rules as a video: close after skipAfterSec, completion = reward.
    /// </summary>
    public sealed class SlideshowPromoPlayer : IPromoPlayer
    {
        public const float TotalDurationSec = 15f;
        public const int MaxShotsPerGame = 2;

        public bool CanPlay(string mediaType) => mediaType == "slideshow";

        public void Play(PlaybackRequest request, Action<PromoResult> done)
        {
            var go = new GameObject("CrossPromo Slideshow");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<SlideshowOverlay>().Begin(request, done);
        }

        /// <summary>Slide list: up to 2 screenshots per game, games interleaved. Pure, tested.</summary>
        public static List<KeyValuePair<int, Texture2D>> BuildSlides(IList<PromoTarget> targets, int maxShotsPerGame)
        {
            var slides = new List<KeyValuePair<int, Texture2D>>();
            for (int round = 0; round < maxShotsPerGame; round++)
                for (int t = 0; t < targets.Count; t++)
                {
                    var shots = targets[t].Screenshots;
                    int seen = -1;
                    for (int s = 0; s < shots.Count; s++)
                    {
                        if (shots[s] == null) continue;
                        seen++;
                        if (seen == round) { slides.Add(new KeyValuePair<int, Texture2D>(t, shots[s])); break; }
                    }
                }
            return slides;
        }

        /// <summary>Seconds per slide: total spread over the slides, clamped to 3–6 s.</summary>
        public static float SlideSeconds(int slideCount) => slideCount <= 0 ? TotalDurationSec : Mathf.Clamp(TotalDurationSec / slideCount, 3f, 6f);
    }

    internal sealed class SlideshowOverlay : PromoOverlay
    {
        List<KeyValuePair<int, Texture2D>> _slides;
        float _slideSec;
        float _elapsed;
        bool _running;
        int _current = -1;
        RawImage _image;
        AspectRatioFitter _fitter;
        Text _name;
        Button _cta;

        /// <summary>Always the host orientation: no rotate hint for the slideshow.</summary>
        protected override PromoOrientation ContentOrientation => Request.HostOrientation != null
            ? Request.HostOrientation()
            : OrientationLogic.FromSize(Screen.width, Screen.height);
        protected override bool ContentRunning => _running;
        protected override float? ContentDuration => _slides == null ? (float?)null : _slides.Count * _slideSec;
        protected override bool IsFallback => true;
        protected override PromoTarget CurrentTarget
        {
            get
            {
                if (_slides == null || _slides.Count == 0) return base.CurrentTarget;
                int i = Mathf.Clamp(_current, 0, _slides.Count - 1);
                return Request.Targets[_slides[i].Key];
            }
        }

        protected override void BuildContent(RectTransform area)
        {
            _slides = SlideshowPromoPlayer.BuildSlides(Request.Targets, SlideshowPromoPlayer.MaxShotsPerGame);
            if (_slides.Count == 0) { Fail("no screenshots"); return; }
            _slideSec = SlideshowPromoPlayer.SlideSeconds(_slides.Count);

            var holder = PromoUI.Stretch(PromoUI.Rect("Shot", area));
            _image = holder.gameObject.AddComponent<RawImage>();
            _image.raycastTarget = false;
            _fitter = holder.gameObject.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            var bar = PromoUI.Panel("Bar", area, new Color(0, 0, 0, 0.6f));
            bar.rectTransform.anchorMin = new Vector2(0, 0);
            bar.rectTransform.anchorMax = new Vector2(1, 0);
            bar.rectTransform.pivot = new Vector2(0.5f, 0);
            bar.rectTransform.anchoredPosition = new Vector2(0, 8);
            bar.rectTransform.sizeDelta = new Vector2(0, 200);
            _name = PromoUI.Label("Name", bar.transform, "", 56, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            _name.rectTransform.anchorMin = new Vector2(0, 0);
            _name.rectTransform.anchorMax = new Vector2(1, 1);
            _name.rectTransform.offsetMin = new Vector2(48, 0);
            _name.rectTransform.offsetMax = new Vector2(-480, 0);
            _cta = PromoUI.Button("Cta", bar.transform, PromoUI.T(Request.Language, "Get it", "Downloaden"), new Color(0.15f, 0.75f, 0.3f, 1f), Color.white, 48,
                () => ClickStore(CurrentTarget));
            PromoUI.Place((RectTransform)_cta.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-40, 0), new Vector2(400, 130));
            ShowSlide(0);
        }

        void ShowSlide(int i)
        {
            if (i == _current) return;
            _current = i;
            var slide = _slides[i];
            var target = Request.Targets[slide.Key];
            _image.texture = slide.Value;
            _fitter.aspectRatio = slide.Value.height > 0 ? (float)slide.Value.width / slide.Value.height : 1f;
            _name.text = target.Game != null ? target.Game.Name : "";
            _cta.gameObject.SetActive(!string.IsNullOrEmpty(target.StoreUrl));
        }

        protected override void StartContent() { _running = _slides != null && _slides.Count > 0; }

        protected override void TickContent(float dt)
        {
            if (!_running) return;
            _elapsed += dt;
            int i = Mathf.Min(_slides.Count - 1, Mathf.FloorToInt(_elapsed / _slideSec));
            ShowSlide(i);
            if (_elapsed >= _slides.Count * _slideSec)
            {
                _running = false;
                ContentFinished();
            }
        }

        protected override void StopContent() { _running = false; }
    }
}
