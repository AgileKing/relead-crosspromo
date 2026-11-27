using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Relead.CrossPromo
{
    /// <summary>uGUI built in code: no prefabs, no package art beyond generated textures.</summary>
    internal static class PromoUI
    {
        static Font _font;
        static Sprite _rounded;
        static Texture2D _phone;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        /// <summary>Hosts normally have one. If not, add one with whichever input module this project can run.</summary>
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("CrossPromo EventSystem", typeof(EventSystem));
            var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (inputSystemModule != null) { go.AddComponent(inputSystemModule); return; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<StandaloneInputModule>();
#else
            if (inputSystemModule != null) go.AddComponent(inputSystemModule);
#endif
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, bool rounded = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded) { img.sprite = Rounded; img.type = Image.Type.Sliced; }
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(string name, Transform parent, string text, Color bg, Color fg, int fontSize, Action onClick)
        {
            var img = Panel(name, parent, bg, true);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var label = Label("Label", img.transform, text, fontSize, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform);
            return b;
        }

        /// <summary>Round "×" close button.</summary>
        public static Button CloseButton(Transform parent, Action onClick)
        {
            var b = Button("Close", parent, "×", new Color(0, 0, 0, 0.55f), Color.white, 64, onClick);
            return b;
        }

        public static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int s = 64, r = 24;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "CrossPromoRounded", hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(r - x - 0.5f, x + 0.5f - (s - r)));
                        float dy = Mathf.Max(0, Mathf.Max(r - y - 0.5f, y + 0.5f - (s - r)));
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                _rounded.hideFlags = HideFlags.HideAndDontSave;
                return _rounded;
            }
        }

        /// <summary>Code-drawn phone outline (portrait) with a home bar, white on transparent.</summary>
        public static Texture2D PhoneIcon
        {
            get
            {
                if (_phone != null) return _phone;
                const int w = 96, h = 168, r = 16, border = 8;
                _phone = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "CrossPromoPhone", hideFlags = HideFlags.HideAndDontSave };
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float outer = RoundedDist(x, y, w, h, r);
                        float inner = RoundedDist(x - border, y - border, w - 2 * border, h - 2 * border, r - border / 2);
                        float a = Mathf.Clamp01(outer + 0.5f) * (1f - Mathf.Clamp01(inner + 0.5f));
                        bool bar = y >= 14 && y <= 18 && x >= w / 2 - 14 && x <= w / 2 + 14;
                        if (bar) a = 1f;
                        px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                _phone.SetPixels32(px);
                _phone.Apply(false, true);
                return _phone;
            }
        }

        /// <summary>Positive inside a rounded rect of size w×h at origin, by distance to the edge.</summary>
        static float RoundedDist(float x, float y, float w, float h, float r)
        {
            float cx = x + 0.5f, cy = y + 0.5f;
            if (cx < 0 || cy < 0 || cx > w || cy > h) return -1;
            float dx = Mathf.Max(0, Mathf.Max(r - cx, cx - (w - r)));
            float dy = Mathf.Max(0, Mathf.Max(r - cy, cy - (h - r)));
            float corner = r - Mathf.Sqrt(dx * dx + dy * dy);
            float edge = Mathf.Min(Mathf.Min(cx, w - cx), Mathf.Min(cy, h - cy));
            return Mathf.Min(corner, edge);
        }

        public static string T(string language, string en, string nl)
            => !string.IsNullOrEmpty(language) && language.StartsWith("nl", StringComparison.OrdinalIgnoreCase) ? nl : en;
    }
}
