using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Relead.CrossPromo.Editor
{
    /// <summary>Per-game build settings. Create via Assets > Create > Relead > Cross-Promo Build Settings (one per project).</summary>
    [CreateAssetMenu(menuName = "Relead/Cross-Promo Build Settings", fileName = "CrossPromoBuildSettings")]
    public sealed class CrossPromoBuildSettings : ScriptableObject
    {
        [Tooltip("This game's own scheme from the catalog games[].urlScheme, e.g. relead-g01.")]
        public string hostUrlScheme;
        [Tooltip("Android package names of other Relead games to declare in <queries> (reserve ahead where known).")]
        public string[] androidPackages = new string[0];

        public static CrossPromoBuildSettings Find()
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(CrossPromoBuildSettings).Name);
            if (guids.Length == 0) return null;
            if (guids.Length > 1) Debug.LogWarning("[CrossPromo] more than one CrossPromoBuildSettings asset; using the first.");
            return AssetDatabase.LoadAssetAtPath<CrossPromoBuildSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    /// <summary>What the native projects must contain. Pure, tested.</summary>
    public static class NativeSetup
    {
        public const int ReservedSchemeCount = 50;
        /// <summary>iOS allows at most 50 LSApplicationQueriesSchemes entries.</summary>
        public const int IosQuerySchemeLimit = 50;
        const string AndroidNs = "http://schemas.android.com/apk/res/android";

        public static readonly Regex SchemePattern = new Regex("^relead-g[0-9]{2}$");

        public static List<string> ReservedSchemes()
        {
            var l = new List<string>();
            for (int i = 1; i <= ReservedSchemeCount; i++) l.Add("relead-g" + i.ToString("00", CultureInfo.InvariantCulture));
            return l;
        }

        /// <summary>Existing entries first, then ours, de-duplicated. Warning if over the iOS limit.</summary>
        public static List<string> MergeQuerySchemes(IEnumerable<string> existing, out string warning)
        {
            var result = new List<string>();
            if (existing != null) foreach (var s in existing) if (!string.IsNullOrEmpty(s) && !result.Contains(s)) result.Add(s);
            foreach (var s in ReservedSchemes()) if (!result.Contains(s)) result.Add(s);
            warning = result.Count > IosQuerySchemeLimit
                ? "LSApplicationQueriesSchemes has " + result.Count + " entries; iOS only honours " + IosQuerySchemeLimit + ". Remove other schemes or some relead-gNN won't be detectable."
                : null;
            return result;
        }

        /// <summary>
        /// Adds to an AndroidManifest.xml: &lt;queries&gt; with an intent per reserved scheme and the given packages,
        /// and a VIEW/BROWSABLE intent-filter for the host's own scheme on the launcher activity. Idempotent.
        /// </summary>
        public static string PatchAndroidManifest(string xml, string hostScheme, IEnumerable<string> packages)
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var manifest = doc.DocumentElement;
            if (manifest == null || manifest.Name != "manifest") throw new ArgumentException("not an AndroidManifest");

            XmlElement queries = null;
            foreach (XmlNode n in manifest.ChildNodes) if (n.Name == "queries") { queries = (XmlElement)n; break; }
            if (queries == null)
            {
                queries = doc.CreateElement("queries");
                manifest.InsertBefore(queries, FirstChild(manifest, "application"));
            }

            var havePackages = new HashSet<string>();
            var haveSchemes = new HashSet<string>();
            foreach (XmlNode n in queries.ChildNodes)
            {
                if (n.Name == "package") havePackages.Add(((XmlElement)n).GetAttribute("name", AndroidNs));
                if (n.Name == "intent")
                    foreach (XmlNode d in n.ChildNodes)
                        if (d.Name == "data") haveSchemes.Add(((XmlElement)d).GetAttribute("scheme", AndroidNs));
            }
            if (packages != null)
                foreach (var p in packages)
                {
                    if (string.IsNullOrWhiteSpace(p) || havePackages.Contains(p.Trim())) continue;
                    var e = doc.CreateElement("package");
                    e.SetAttribute("name", AndroidNs, p.Trim());
                    queries.AppendChild(e);
                    havePackages.Add(p.Trim());
                }
            foreach (var s in ReservedSchemes())
            {
                if (haveSchemes.Contains(s)) continue;
                var intent = doc.CreateElement("intent");
                var action = doc.CreateElement("action");
                action.SetAttribute("name", AndroidNs, "android.intent.action.VIEW");
                var data = doc.CreateElement("data");
                data.SetAttribute("scheme", AndroidNs, s);
                intent.AppendChild(action);
                intent.AppendChild(data);
                queries.AppendChild(intent);
            }

            if (!string.IsNullOrEmpty(hostScheme))
            {
                var activity = FindLauncherActivity(manifest);
                if (activity == null) throw new InvalidOperationException("no launcher activity in manifest");
                bool present = false;
                foreach (XmlNode f in activity.ChildNodes)
                    if (f.Name == "intent-filter")
                        foreach (XmlNode d in f.ChildNodes)
                            if (d.Name == "data" && ((XmlElement)d).GetAttribute("scheme", AndroidNs) == hostScheme) present = true;
                if (!present)
                {
                    var filter = doc.CreateElement("intent-filter");
                    filter.AppendChild(Named(doc, "action", "android.intent.action.VIEW"));
                    filter.AppendChild(Named(doc, "category", "android.intent.category.DEFAULT"));
                    filter.AppendChild(Named(doc, "category", "android.intent.category.BROWSABLE"));
                    var data = doc.CreateElement("data");
                    data.SetAttribute("scheme", AndroidNs, hostScheme);
                    filter.AppendChild(data);
                    activity.AppendChild(filter);
                }
            }

            using (var sw = new StringWriter(CultureInfo.InvariantCulture))
            using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }))
            {
                doc.Save(xw);
                xw.Flush();
                return sw.ToString().Replace("encoding=\"utf-16\"", "encoding=\"utf-8\"");
            }
        }

        static XmlElement Named(XmlDocument doc, string tag, string name)
        {
            var e = doc.CreateElement(tag);
            e.SetAttribute("name", AndroidNs, name);
            return e;
        }

        static XmlNode FirstChild(XmlElement parent, string name)
        {
            foreach (XmlNode n in parent.ChildNodes) if (n.Name == name) return n;
            return null;
        }

        static XmlElement FindLauncherActivity(XmlElement manifest)
        {
            var app = FirstChild(manifest, "application");
            if (app == null) return null;
            foreach (XmlNode a in app.ChildNodes)
            {
                if (a.Name != "activity" && a.Name != "activity-alias") continue;
                foreach (XmlNode f in a.ChildNodes)
                {
                    if (f.Name != "intent-filter") continue;
                    foreach (XmlNode c in f.ChildNodes)
                        if (c.Name == "category" && ((XmlElement)c).GetAttribute("name", AndroidNs) == "android.intent.category.LAUNCHER")
                            return (XmlElement)a;
                }
            }
            return null;
        }

        internal static string ValidatedHostScheme(CrossPromoBuildSettings s)
        {
            if (s == null)
            {
                Debug.LogWarning("[CrossPromo] No CrossPromoBuildSettings asset: this game's own url scheme is not registered, so other games can't see it is installed. Create one via Assets > Create > Relead > Cross-Promo Build Settings.");
                return null;
            }
            var scheme = (s.hostUrlScheme ?? "").Trim();
            if (!SchemePattern.IsMatch(scheme))
            {
                Debug.LogWarning("[CrossPromo] hostUrlScheme '" + scheme + "' is not relead-gNN; not registered.");
                return null;
            }
            return scheme;
        }
    }

    /// <summary>Android: patch unityLibrary's manifest after Gradle project generation.</summary>
    sealed class AndroidManifestPostProcess : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var file = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(file)) { Debug.LogWarning("[CrossPromo] manifest not found at " + file); return; }
            var settings = CrossPromoBuildSettings.Find();
            var scheme = NativeSetup.ValidatedHostScheme(settings);
            File.WriteAllText(file, NativeSetup.PatchAndroidManifest(File.ReadAllText(file), scheme, settings != null ? settings.androidPackages : null));
        }
    }

    /// <summary>iOS: LSApplicationQueriesSchemes relead-g01..g50 and CFBundleURLTypes for the host's own scheme.</summary>
    sealed class IosPlistPostProcess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
#if UNITY_IOS
            if (report.summary.platform != BuildTarget.iOS) return;
            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new UnityEditor.iOS.Xcode.PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;

            var existing = new List<string>();
            var arr = root["LSApplicationQueriesSchemes"] as UnityEditor.iOS.Xcode.PlistElementArray;
            if (arr != null) foreach (var v in arr.values) existing.Add(v.AsString());
            string warning;
            var merged = NativeSetup.MergeQuerySchemes(existing, out warning);
            if (warning != null) Debug.LogWarning("[CrossPromo] " + warning);
            arr = root.CreateArray("LSApplicationQueriesSchemes");
            foreach (var s in merged) arr.AddString(s);

            var scheme = NativeSetup.ValidatedHostScheme(CrossPromoBuildSettings.Find());
            if (scheme != null)
            {
                var types = root["CFBundleURLTypes"] as UnityEditor.iOS.Xcode.PlistElementArray ?? root.CreateArray("CFBundleURLTypes");
                bool present = false;
                foreach (var t in types.values)
                {
                    var schemes = t.AsDict()["CFBundleURLSchemes"] as UnityEditor.iOS.Xcode.PlistElementArray;
                    if (schemes != null) foreach (var s in schemes.values) if (s.AsString() == scheme) present = true;
                }
                if (!present)
                {
                    var dict = types.AddDict();
                    dict.SetString("CFBundleURLName", "com.relead.crosspromo");
                    dict.CreateArray("CFBundleURLSchemes").AddString(scheme);
                }
            }
            plist.WriteToFile(plistPath);
#endif
        }
    }
}
