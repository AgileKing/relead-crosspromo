using Relead.CrossPromo;
using UnityEngine;
using UnityEngine.UI;

namespace Relead.CrossPromo.Samples
{
    /// <summary>
    /// Demo host: add to an empty GameObject in an empty scene and press Play.
    /// Builds a "Watch for 10 coins" button, enables it when CrossPromo.IsReady, grants coins on Completed.
    /// </summary>
    public sealed class DemoHost : MonoBehaviour
    {
        public string hostGameId = "demo-host";
        public string ingestKey = "";
        int _coins;
        Button _button;
        Text _label;

        void Start()
        {
            CrossPromo.Init(new CrossPromoConfig { HostGameId = hostGameId, IngestKey = ingestKey, RequireStoreLink = false, VerboseLogging = true });
            CrossPromo.Opened += () => AudioListener.volume = 0f;
            CrossPromo.Closed += r => AudioListener.volume = 1f;

            var canvasGo = new GameObject("DemoCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1080);
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            var btnGo = new GameObject("WatchButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)btnGo.transform;
            rt.sizeDelta = new Vector2(600, 160);
            _button = btnGo.GetComponent<Button>();
            _button.onClick.AddListener(OnWatch);
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(btnGo.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            _label = textGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 44;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.black;
        }

        void Update()
        {
            // Capped: Show still works and shows "Maxed out ads for today" (outcome Capped, no reward).
            _button.interactable = !CrossPromo.IsShowing && (CrossPromo.IsReady || CrossPromo.IsCappedToday);
            _label.text = "Watch for 10 coins (" + _coins + ")";
        }

        void OnWatch()
        {
            CrossPromo.Show(result =>
            {
                Debug.Log("[Demo] " + result);
                if (result.Outcome == PromoOutcome.Completed) _coins += 10; // reward for watching only
            });
        }
    }
}
