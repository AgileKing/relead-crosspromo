using System;
using System.Collections.Generic;
using System.IO;
using Relead.CrossPromo.Json;
using UnityEditor;
using UnityEngine;

namespace Relead.CrossPromo.Editor
{
    /// <summary>
    /// Builds Runtime/Resources/ReleadCrossPromoFallback.asset from Runtime/FallbackScreenshots/fallback-games.json
    /// and the JPGs next to it. Run after changing screenshots; commit the asset and the .meta files.
    /// </summary>
    public static class FallbackSetBuilder
    {
        public const string PackageRoot = "Packages/com.relead.crosspromo";
        public const string ShotsDir = PackageRoot + "/Runtime/FallbackScreenshots";
        public const string ManifestPath = ShotsDir + "/fallback-games.json";
        public const string AssetPath = PackageRoot + "/Runtime/Resources/" + FallbackGameSet.ResourceName + ".asset";

        [MenuItem("Relead/Cross-Promo/Rebuild Fallback Set")]
        public static void Rebuild()
        {
            var games = ReadManifest(File.ReadAllText(Path.GetFullPath(ManifestPath)));
            var list = new List<FallbackGame>();
            foreach (var g in games)
            {
                var dir = ShotsDir + "/" + g.id;
                var shots = new List<Texture2D>();
                if (Directory.Exists(Path.GetFullPath(dir)))
                {
                    var files = Directory.GetFiles(Path.GetFullPath(dir), "*.jpg");
                    Array.Sort(files, StringComparer.Ordinal);
                    foreach (var f in files)
                    {
                        var assetPath = dir + "/" + Path.GetFileName(f);
                        ConfigureImporter(assetPath);
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                        if (tex != null) shots.Add(tex);
                    }
                }
                if (shots.Count == 0) Debug.LogWarning("[CrossPromo] fallback game " + g.id + " has no screenshots in " + dir);
                g.screenshots = shots.ToArray();
                list.Add(g);
            }

            Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(AssetPath)));
            var set = AssetDatabase.LoadAssetAtPath<FallbackGameSet>(AssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<FallbackGameSet>();
                AssetDatabase.CreateAsset(set, AssetPath);
            }
            set.games = list.ToArray();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log("[CrossPromo] fallback set rebuilt: " + list.Count + " games");
        }

        /// <summary>Parses fallback-games.json. Pure, tested.</summary>
        public static List<FallbackGame> ReadManifest(string json)
        {
            object o;
            string err;
            if (!MiniJson.TryParse(json, out o, out err)) throw new FormatException("fallback-games.json: " + err);
            var arr = JsonRead.Arr(o as Dictionary<string, object>, "games") ?? new List<object>();
            var result = new List<FallbackGame>();
            foreach (var item in arr)
            {
                var d = item as Dictionary<string, object>;
                var id = JsonRead.Str(d, "id");
                if (string.IsNullOrEmpty(id)) continue;
                result.Add(new FallbackGame
                {
                    id = id,
                    name = JsonRead.Str(d, "name") ?? id,
                    urlScheme = JsonRead.Str(d, "urlScheme") ?? "",
                    androidPackage = JsonRead.Str(d, "android") ?? "",
                    iosAppId = JsonRead.Str(d, "ios") ?? "",
                    windowsProductId = JsonRead.Str(d, "windows") ?? "",
                });
            }
            return result;
        }

        static void ConfigureImporter(string assetPath)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null) { AssetDatabase.ImportAsset(assetPath); imp = AssetImporter.GetAtPath(assetPath) as TextureImporter; }
            if (imp == null) return;
            bool changed = imp.textureType != TextureImporterType.Default || imp.mipmapEnabled || imp.npotScale != TextureImporterNPOTScale.None
                || imp.maxTextureSize != 1024 * 2 || imp.wrapMode != TextureWrapMode.Clamp || imp.alphaSource != TextureImporterAlphaSource.None;
            if (!changed) return;
            imp.textureType = TextureImporterType.Default;
            imp.mipmapEnabled = false;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.maxTextureSize = 2048;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
        }
    }
}
