using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Relead.CrossPromo
{
    /// <summary>
    /// Shared full-screen overlay: backdrop, rotatable stage, rotate hint and the ad timeline (WatchClock):
    /// small Skip top left from skipAfterSec (no reward), download panel over the still-playing content from
    /// endCardAtSec, top-right countdown that turns into × plus an "Earned! Close" button at rewardAfterSec
    /// (Completed). Content loops until the player closes. Subclasses provide the content (video, slideshow,
    /// later a web view). Uses unscaled time, so it works while the host has Time.timeScale = 0.
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
        Button _skip;
        Button _earned;
        Text _countdown;
        Image _progress;
        RectTransform _endCard;
        float _hintElapsed;
        bool _contentStarted;
        bool _earnedTracked;
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
        protected virtual PromoTarget CurrentTarget => Request.Targets.Count > 0 ? Request.Targets[0] : null;
        protected virtual bool IsFallback => false;
        /// <summary>False when the content already has its own store button (slideshow).</summary>
        protected virtual bool ShowsDownloadPanel => true;
        protected virtual void TickContent(float dt) { }

        public void Begin(PlaybackRequest request, Action<PromoResult> done)
        {
            Request = request;
            _done = done;
            Clock = new WatchClock(request.SkipAfterSec, request.EndCardAtSec, request.RewardAfterSec);
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

            _earned = PromoUI.Button("Earned", Stage, PromoUI.T(Request.Language, "Earned! Close", "Verdiend! Sluiten"),
                new Color(0.15f, 0.75f, 0.3f, 1f), Color.white, 30, OnEarnedClicked);
            // Portrait phone ≈ 730 canvas units wide (1080 reference, match 0.5): 32+160 left, 32+96+16+320 right still fit.
            PromoUI.Place((RectTransform)_earned.transform, Vector2.one, Vector2.one, new Vector2(-32 - 96 - 16, -32), new Vector2(320, 96));
            _earned.gameObject.SetActive(false);

            _skip = PromoUI.Button("Skip", Stage, PromoUI.T(Request.Language, "Skip", "Overslaan"),
                new Color(0, 0, 0, 0.45f), new Color(1, 1, 1, 0.85f), 32, OnSkipClicked);
            PromoUI.Place((RectTransform)_skip.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -32), new Vector2(160, 72));
            _skip.gameObject.SetActive(false);

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

                if (_contentStarted)
                {
                    TickContent(dt);
                    if (_finished) return;
                    if (ContentRunning) Clock.Tick(dt);
                    _skip.gameObject.SetActive(Clock.CanSkip);
                    bool earned = Clock.Earned;
                    _close.gameObject.SetActive(earned);
                    _earned.gameObject.SetActive(earned);
                    _countdown.transform.parent.gameObject.SetActive(!earned);
                    _countdown.text = Clock.SecondsUntilReward.ToString();
                    _progress.rectTransform.anchorMax = new Vector2(Clock.RewardProgress, 1);
                    if (Clock.ShowEndCard && _endCard == null && ShowsDownloadPanel) ShowEndCard();
                    if (earned && !_earnedTracked)
                    {
                        _earnedTracked = true;
                        Track("promo_earned", new Dictionary<string, object> { { "watchedSec", Clock.WatchedSec } });
                    }
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

        /// <summary>Top-right ×: only visible once earned.</summary>
        void OnCloseClicked()
        {
            if (!Clock.Earned) return;
            Finish(PromoOutcome.Completed, null);
        }

        void OnEarnedClicked()
        {
            if (!Clock.Earned) return;
            Finish(PromoOutcome.Completed, null);
        }

        /// <summary>Top-left Skip: no reward before rewardAfterSec, Completed after.</summary>
        void OnSkipClicked()
        {
            if (!Clock.CanSkip) return;
            Finish(Clock.Earned ? PromoOutcome.Completed : PromoOutcome.Skipped, null);
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

        /// <summary>
        /// Download panel at the bottom, over the still-playing content: icon, name, headline, store button.
        /// Wide stage: one row (icon | texts | button). Tall stage (portrait, ≈ 730 units wide): icon + texts on top,
        /// full-width button below.
        /// </summary>
        void ShowEndCard()
        {
            var target = CurrentTarget;
            var stageSize = Stage.rect.size;
            bool tall = stageSize.y > stageSize.x;
            bool hasStore = target != null && !string.IsNullOrEmpty(target.StoreUrl);
            bool hasIcon = target != null && target.Icon != null;
            const float pad = 24, iconSize = 176, buttonH = 130;

            var bg = PromoUI.Panel("EndCard", Stage, new Color(0.05f, 0.05f, 0.08f, 0.85f), true);
            _endCard = bg.rectTransform;
            _endCard.anchorMin = new Vector2(0, 0);
            _endCard.anchorMax = new Vector2(1, 0);
            _endCard.pivot = new Vector2(0.5f, 0);
            float height = tall ? pad + iconSize + (hasStore ? pad + buttonH : 0) + pad : pad + iconSize + pad;
            _endCard.offsetMin = new Vector2(32, 32);
            _endCard.offsetMax = new Vector2(-32, 32 + height);

            // Info row: top-anchored, iconSize high.
            float left = pad;
            if (hasIcon)
            {
                var icon = PromoUI.Rect("Icon", _endCard);
                icon.gameObject.AddComponent<RawImage>().texture = target.Icon;
                PromoUI.Place(icon, new Vector2(0, 1), new Vector2(0, 1), new Vector2(pad, -pad), new Vector2(iconSize, iconSize));
                left = pad + iconSize + pad;
            }
            float right = !tall && hasStore ? pad + 400 + pad : pad;
            var name = PromoUI.Label("Name", _endCard, target != null && target.Game != null ? target.Game.Name : "", tall ? 48 : 56, Color.white, TextAnchor.LowerLeft, FontStyle.Bold);
            var nrt = name.rectTransform;
            nrt.anchorMin = new Vector2(0, 1); nrt.anchorMax = new Vector2(1, 1); nrt.pivot = new Vector2(0.5f, 1);
            nrt.offsetMin = new Vector2(left, -pad - iconSize * 0.45f); nrt.offsetMax = new Vector2(-right, -pad);
            var headline = PromoUI.Label("Headline", _endCard, target != null ? target.Headline : "", tall ? 32 : 38, new Color(1, 1, 1, 0.85f), TextAnchor.UpperLeft);
            var hrt = headline.rectTransform;
            hrt.anchorMin = new Vector2(0, 1); hrt.anchorMax = new Vector2(1, 1); hrt.pivot = new Vector2(0.5f, 1);
            hrt.offsetMin = new Vector2(left, -pad - iconSize); hrt.offsetMax = new Vector2(-right, -pad - iconSize * 0.45f - 4);

            if (hasStore)
            {
                var cta = string.IsNullOrEmpty(target.Cta) ? PromoUI.T(Request.Language, "Get it", "Downloaden") : target.Cta;
                var b = PromoUI.Button("Cta", _endCard, cta, new Color(0.15f, 0.75f, 0.3f, 1f), Color.white, 52, () => ClickStore(target));
                var brt = (RectTransform)b.transform;
                if (tall)
                {
                    brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(1, 0); brt.pivot = new Vector2(0.5f, 0);
                    brt.offsetMin = new Vector2(pad, pad); brt.offsetMax = new Vector2(-pad, pad + buttonH);
                }
                else PromoUI.Place(brt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-pad, 0), new Vector2(400, buttonH));
            }
            // Buttons stay on top of the panel.
            _skip.transform.SetAsLastSibling();
            _earned.transform.SetAsLastSibling();
            _countdown.transform.parent.SetAsLastSibling();
            _close.transform.SetAsLastSibling();
            Track("promo_end_card", null);
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
