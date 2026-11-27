using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Relead.CrossPromo
{
    /// <summary>
    /// Shared full-screen overlay: backdrop, rotatable stage, rotate hint, countdown → close button,
    /// progress bar, end card. Subclasses provide the content (video, slideshow, later a web view).
    /// Uses unscaled time, so it works while the host has Time.timeScale = 0.
    /// </summary>
    public abstract class PromoOverlay : MonoBehaviour
    {
        /// <summary>Frame delta cap, so returning from background doesn't jump the clock.</summary>
        const float MaxDelta = 0.25f;

        protected PlaybackRequest Request;
        protected RectTransform Root;
        protected RectTransform Stage;
        protected RectTransform ContentArea;
        protected WatchClock Clock;
        protected StageMode Mode = StageMode.RotateHint;

        Action<PromoResult> _done;
        Canvas _canvas;
        RectTransform _hint;
        RectTransform _phone;
        Button _close;
        Text _countdown;
        Image _progress;
        RectTransform _endCard;
        float _hintElapsed;
        bool _contentStarted;
        bool _contentFinished;
        bool _storeClicked;
        bool _finished;
        bool _hintTracked;
        PromoOrientation _host;

        /// <summary>Orientation the content wants; host orientation = no rotation logic.</summary>
        protected abstract PromoOrientation ContentOrientation { get; }
        protected abstract void BuildContent(RectTransform contentArea);
        /// <summary>Called once the stage mode is known (upright / rotated / letterbox).</summary>
        protected abstract void StartContent();
        /// <summary>True while content is actually progressing (the watch clock ticks only then).</summary>
        protected abstract bool ContentRunning { get; }
        /// <summary>Total length for the progress bar, or null if unknown.</summary>
        protected abstract float? ContentDuration { get; }
        protected virtual PromoTarget CurrentTarget => Request.Targets.Count > 0 ? Request.Targets[0] : null;
        protected virtual bool IsFallback => false;
        protected virtual void TickContent(float dt) { }

        public void Begin(PlaybackRequest request, Action<PromoResult> done)
        {
            Request = request;
            _done = done;
            Clock = new WatchClock(request.SkipAfterSec);
            _host = request.HostOrientation != null ? request.HostOrientation() : OrientationLogic.FromSize(Screen.width, Screen.height);
            try
            {
                BuildFrame();
                BuildContent(ContentArea);
                if (!_finished) UpdateMode(0f);
            }
            catch (Exception e)
            {
                Fail("ui: " + e.Message);
            }
        }

        void BuildFrame()
        {
            _canvas = PromoUI.CreateCanvas("CrossPromo Overlay", Request.SortingOrder);
            _canvas.transform.SetParent(transform, false);
            Root = (RectTransform)_canvas.transform;

            var backdrop = PromoUI.Panel("Backdrop", Root, Color.black);
            PromoUI.Stretch(backdrop.rectTransform);

            Stage = PromoUI.Stretch(PromoUI.Rect("Stage", Root));
            ContentArea = PromoUI.Stretch(PromoUI.Rect("Content", Stage));

            var barBg = PromoUI.Panel("ProgressBg", Stage, new Color(1, 1, 1, 0.15f));
            PromoUI.Place(barBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(0, 8));
            barBg.rectTransform.anchorMin = new Vector2(0, 0);
            barBg.rectTransform.anchorMax = new Vector2(1, 0);
            _progress = PromoUI.Panel("Progress", barBg.transform, new Color(1f, 0.8f, 0.1f, 1f));
            var prt = _progress.rectTransform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = new Vector2(0, 1);
            prt.offsetMin = prt.offsetMax = Vector2.zero;

            _countdown = PromoUI.Label("Countdown", Stage, "", 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            PromoUI.Place(_countdown.rectTransform, Vector2.one, Vector2.one, new Vector2(-32, -32), new Vector2(96, 96));
            var cdBg = PromoUI.Panel("CountdownBg", Stage, new Color(0, 0, 0, 0.55f), true);
            PromoUI.Place(cdBg.rectTransform, Vector2.one, Vector2.one, new Vector2(-32, -32), new Vector2(96, 96));
            cdBg.transform.SetSiblingIndex(_countdown.transform.GetSiblingIndex());
            cdBg.raycastTarget = false;
            _countdown.transform.SetParent(cdBg.transform, false);
            PromoUI.Stretch(_countdown.rectTransform);

            _close = PromoUI.CloseButton(Stage, OnCloseClicked);
            PromoUI.Place((RectTransform)_close.transform, Vector2.one, Vector2.one, new Vector2(-32, -32), new Vector2(96, 96));
            _close.gameObject.SetActive(false);

            _hint = PromoUI.Stretch(PromoUI.Rect("RotateHint", Root));
            var hintBg = PromoUI.Panel("Bg", _hint, new Color(0, 0, 0, 0.85f));
            PromoUI.Stretch(hintBg.rectTransform);
            var phone = PromoUI.Rect("Phone", _hint);
            phone.gameObject.AddComponent<RawImage>().texture = PromoUI.PhoneIcon;
            PromoUI.Place(phone, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(120, 210));
            _phone = phone;
            var hintText = PromoUI.Label("Text", _hint, PromoUI.T(Request.Language, "Turn your phone", "Draai je telefoon"), 48, Color.white);
            PromoUI.Place(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(900, 120));
            _hint.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_finished) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxDelta);
            try
            {
                if (!_contentStarted) UpdateMode(dt);
                else if (Mode == StageMode.Rotated) LayoutStage();

                if (_contentStarted && !_contentFinished)
                {
                    TickContent(dt);
                    if (ContentRunning) Clock.Tick(dt);
                    _close.gameObject.SetActive(Clock.CanSkip);
                    _countdown.transform.parent.gameObject.SetActive(!Clock.CanSkip);
                    _countdown.text = Clock.SecondsUntilSkip.ToString();
                    var dur = ContentDuration;
                    if (dur.HasValue && dur.Value > 0)
                        _progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(Clock.WatchedSec / dur.Value), 1);
                }
                if (_hint.gameObject.activeSelf)
                {
                    // Turn the phone 90° counter-clockwise (portrait → landscape) or back, ease, hold, repeat.
                    float cycle = Mathf.Repeat(Time.unscaledTime, 2f);
                    float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01((cycle - 0.3f) / 0.8f));
                    float from = ContentOrientation == PromoOrientation.Landscape ? 0f : 90f;
                    _phone.localEulerAngles = new Vector3(0, 0, Mathf.Lerp(from, 90f - from, t));
                }
            }
            catch (Exception e)
            {
                Fail("update: " + e.Message);
            }
        }

        void UpdateMode(float dt)
        {
            var device = Request.DeviceOrientation != null ? Request.DeviceOrientation() : DeviceOrientation.Unknown;
            _hintElapsed += dt;
            Mode = OrientationLogic.Next(Mode, _host, ContentOrientation, device, _hintElapsed, Request.RotateHintTimeoutSec);
            bool hint = Mode == StageMode.RotateHint;
            _hint.gameObject.SetActive(hint);
            if (hint && !_hintTracked)
            {
                _hintTracked = true;
                Track("promo_rotate_hint", null);
            }
            if (!hint)
            {
                LayoutStage();
                _contentStarted = true;
                StartContent();
            }
        }

        void LayoutStage()
        {
            if (Mode != StageMode.Rotated)
            {
                PromoUI.Stretch(Stage);
                Stage.localEulerAngles = Vector3.zero;
                return;
            }
            var device = Request.DeviceOrientation != null ? Request.DeviceOrientation() : DeviceOrientation.Unknown;
            var deviceAngle = OrientationLogic.DeviceAngle(device);
            if (deviceAngle == null || !OrientationLogic.DeviceMatches(device, ContentOrientation))
            {
                if (Stage.localEulerAngles.z != 0) return; // keep last good rotation while flat/turning
                deviceAngle = ContentOrientation == PromoOrientation.Landscape ? 90 : 0;
            }
            var screen = Request.ScreenOrientation != null ? Request.ScreenOrientation() : Screen.orientation;
            float z = OrientationLogic.StageRotation(OrientationLogic.ScreenAngle(screen, _host), deviceAngle.Value);
            var size = Root.rect.size;
            Stage.anchorMin = Stage.anchorMax = new Vector2(0.5f, 0.5f);
            Stage.pivot = new Vector2(0.5f, 0.5f);
            Stage.anchoredPosition = Vector2.zero;
            bool quarter = Mathf.Abs(Mathf.Abs(z) - 90f) < 1f;
            Stage.sizeDelta = quarter ? new Vector2(size.y, size.x) : size;
            Stage.localEulerAngles = new Vector3(0, 0, z);
        }

        void OnCloseClicked()
        {
            if (_contentFinished) { Finish(PromoOutcome.Completed, null); return; }
            if (!Clock.CanSkip) return;
            StopContent();
            Finish(PromoOutcome.Skipped, null);
        }

        /// <summary>Subclasses call this when the content reached its end: show the end card.</summary>
        protected void ContentFinished()
        {
            if (_contentFinished || _finished) return;
            _contentFinished = true;
            _progress.rectTransform.anchorMax = Vector2.one;
            _countdown.transform.parent.gameObject.SetActive(false);
            ShowEndCard();
        }

        protected virtual void StopContent() { }

        protected void Fail(string reason)
        {
            if (_finished) return;
            Debug.LogWarning("[CrossPromo] playback failed: " + reason);
            Finish(PromoOutcome.Failed, reason);
        }

        protected void Track(string name, Dictionary<string, object> props)
        {
            if (Request.Track == null) return;
            try { Request.Track(name, props ?? new Dictionary<string, object>()); }
            catch (Exception e) { Debug.LogException(e); }
        }

        protected void ClickStore(PromoTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.StoreUrl)) return;
            _storeClicked = true;
            var props = new Dictionary<string, object> { { "targetGameId", target.Game != null ? target.Game.Id : null } };
            Track("promo_store_click", props);
            try
            {
                if (Request.OpenStore != null) Request.OpenStore(target);
                else Application.OpenURL(target.StoreUrl);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        void ShowEndCard()
        {
            var target = CurrentTarget;
            _endCard = PromoUI.Stretch(PromoUI.Rect("EndCard", Stage));
            var bg = PromoUI.Panel("Bg", _endCard, new Color(0.05f, 0.05f, 0.08f, 0.92f));
            PromoUI.Stretch(bg.rectTransform);

            var card = PromoUI.Rect("Card", _endCard);
            PromoUI.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 760));

            if (target != null && target.Icon != null)
            {
                var icon = PromoUI.Rect("Icon", card);
                icon.gameObject.AddComponent<RawImage>().texture = target.Icon;
                PromoUI.Place(icon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(260, 260));
            }
            var name = PromoUI.Label("Name", card, target != null && target.Game != null ? target.Game.Name : "", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            PromoUI.Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(860, 90));
            var headline = PromoUI.Label("Headline", card, target != null ? target.Headline : "", 44, new Color(1, 1, 1, 0.85f));
            PromoUI.Place(headline.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -380), new Vector2(860, 140));

            if (target != null && !string.IsNullOrEmpty(target.StoreUrl))
            {
                var cta = string.IsNullOrEmpty(target.Cta) ? PromoUI.T(Request.Language, "Get it", "Downloaden") : target.Cta;
                var b = PromoUI.Button("Cta", card, cta, new Color(0.15f, 0.75f, 0.3f, 1f), Color.white, 52, () => ClickStore(target));
                PromoUI.Place((RectTransform)b.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(560, 140));
            }
            _close.transform.SetAsLastSibling();
            _close.gameObject.SetActive(true);
        }

        void Finish(PromoOutcome outcome, string reason)
        {
            if (_finished) return;
            _finished = true;
            try { StopContent(); } catch (Exception e) { Debug.LogException(e); }
            var target = CurrentTarget;
            var result = new PromoResult
            {
                Outcome = outcome,
                StoreClicked = _storeClicked,
                PromoId = Request != null ? Request.PromoId : null,
                TargetGameId = target != null && target.Game != null ? target.Game.Id : null,
                WatchedSec = Clock != null ? Clock.WatchedSec : 0,
                FailReason = reason,
                WasFallback = IsFallback,
            };
            var done = _done;
            _done = null;
            Destroy(gameObject);
            if (done != null)
            {
                try { done(result); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        protected virtual void OnDestroy()
        {
            // Destroyed from outside (scene change): still report once.
            if (!_finished && _done != null)
            {
                _finished = true;
                var done = _done;
                _done = null;
                done(new PromoResult { Outcome = PromoOutcome.Failed, FailReason = "destroyed", PromoId = Request != null ? Request.PromoId : null, WatchedSec = Clock != null ? Clock.WatchedSec : 0, WasFallback = IsFallback });
            }
        }
    }
}
