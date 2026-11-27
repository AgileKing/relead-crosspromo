using UnityEditor;
using UnityEngine;

namespace Relead.CrossPromo.Editor
{
    /// <summary>Window > Relead > Cross-Promo Tester: play promo X as a portrait or landscape host (Play Mode).</summary>
    public sealed class CrossPromoTestWindow : EditorWindow
    {
        string _hostGameId = "editor-host";
        DeviceOrientation _device = DeviceOrientation.Portrait;
        string _last = "";
        Vector2 _scroll;

        [MenuItem("Window/Relead/Cross-Promo Tester")]
        static void Open() => GetWindow<CrossPromoTestWindow>("Cross-Promo");

        void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode. The window initialises CrossPromo (store ids not required) if the scene didn't.", MessageType.Info);
                return;
            }
            if (!CrossPromo.IsInitialized)
            {
                _hostGameId = EditorGUILayout.TextField("Host game id", _hostGameId);
                if (GUILayout.Button("Init CrossPromo"))
                    CrossPromo.Init(new CrossPromoConfig
                    {
                        HostGameId = _hostGameId,
                        RequireStoreLink = false,
                        DeviceOrientation = () => _device,
                        VerboseLogging = true,
                    });
                return;
            }

            _device = (DeviceOrientation)EditorGUILayout.EnumPopup(new GUIContent("Simulated device", "Used if the scene's Init didn't set its own provider."), _device);
            EditorGUILayout.LabelField("Ready", CrossPromo.IsReady + " (video: " + CrossPromo.IsVideoReady + ")");
            EditorGUILayout.LabelField("Daily cap", CrossPromo.IsCappedToday ? "reached" : CrossPromo.RemainingToday + " left today");
            EditorGUILayout.LabelField("Last result", _last);
            if (GUILayout.Button("Show (normal selection)")) CrossPromo.Show(Report);
            if (GUILayout.Button("Show offline slideshow")) CrossPromo.ShowFallbackForTesting(Report);
            if (GUILayout.Button("Check catalog now")) CrossPromo.Refresh();

            var cat = CrossPromo.Catalog;
            if (cat == null) { EditorGUILayout.HelpBox("No catalog yet.", MessageType.Warning); return; }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var p in cat.Promos)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(p.Id, EditorStyles.boldLabel);
                foreach (var m in p.Media)
                    EditorGUILayout.LabelField("  " + m.Type + " " + m.Orientation + (CrossPromo.IsCached(m) ? "  ✓ cached" : "  – not cached"));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Download")) CrossPromo.DownloadForTesting(p.Id, ok => { _last = "download " + p.Id + ": " + ok; Repaint(); });
                if (GUILayout.Button("Play as portrait host")) CrossPromo.ShowPromoForTesting(p.Id, PromoOrientation.Portrait, Report);
                if (GUILayout.Button("Play as landscape host")) CrossPromo.ShowPromoForTesting(p.Id, PromoOrientation.Landscape, Report);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        void Report(PromoResult r) { _last = r.ToString(); Repaint(); }

        void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }
    }
}
