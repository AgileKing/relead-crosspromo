using System;
using UnityEngine;
using UnityEngine.UI;

namespace Relead.CrossPromo
{
    /// <summary>
    /// Small, light "Maxed out ads for today" notice, shown when Show is called with the daily cap reached.
    /// Tap anywhere (or ×) closes it at once; it also closes itself after AutoCloseSec. Unscaled time.
    /// Calls done exactly once, also when destroyed from outside (scene change).
    /// </summary>
    [ExecuteAlways] // so OnDestroy (report once) also runs in EditMode tests
    internal sealed class CappedNotice : MonoBehaviour
    {
        public const float AutoCloseSec = 3f;
        const float MaxDelta = 0.25f;

        Action _done;
        float _elapsed;
        bool _closed;

        public static string Message(string language)
            => PromoUI.T(language, "Maxed out ads for today", "Max aantal ads voor vandaag bereikt");

        public static CappedNotice Show(string language, int sortingOrder, Action done)
        {
            var go = new GameObject("CrossPromo Capped");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            var notice = go.AddComponent<CappedNotice>();
            notice._done = done;
            notice.Build(language, sortingOrder);
            return notice;
        }

        void Build(string language, int sortingOrder)
        {
            var canvas = PromoUI.CreateCanvas("CrossPromo Capped Canvas", sortingOrder);
            canvas.transform.SetParent(transform, false);
            var root = (RectTransform)canvas.transform;

            // Light dim that catches every tap: tap anywhere = close.
            var dim = PromoUI.Panel("Dismiss", root, new Color(0, 0, 0, 0.25f));
            PromoUI.Stretch(dim.rectTransform);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            var size = new Vector2(620, 150);
            var center = new Vector2(0.5f, 0.5f);
            PromoUI.Glow("Glow", root, center, Vector2.zero, size);
            var panel = PromoUI.Panel("Panel", root, new Color(0.05f, 0.05f, 0.08f, 0.9f), true);
            PromoUI.Place(panel.rectTransform, center, center, Vector2.zero, size);
            panel.raycastTarget = false;

            var text = PromoUI.Label("Text", panel.transform, Message(language), 36, Color.white);
            var trt = text.rectTransform;
            PromoUI.Stretch(trt);
            trt.offsetMin = new Vector2(32, 16);
            trt.offsetMax = new Vector2(-80, -16);

            var close = PromoUI.Button("Close", panel.transform, "×", new Color(1, 1, 1, 0.12f), Color.white, 44, Close);
            PromoUI.Place((RectTransform)close.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-20, 0), new Vector2(64, 64));
        }

        void Update()
        {
            Tick(Mathf.Min(Time.unscaledDeltaTime, MaxDelta));
        }

        internal void Tick(float dt)
        {
            if (_closed) return;
            if (dt > 0) _elapsed += dt;
            if (_elapsed >= AutoCloseSec) Close();
        }

        internal void Close()
        {
            if (_closed) return;
            _closed = true;
            var done = _done;
            _done = null;
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            if (done != null)
            {
                try { done(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        void OnDestroy()
        {
            if (_closed) return;
            _closed = true;
            var done = _done;
            _done = null;
            if (done != null)
            {
                try { done(); } catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
