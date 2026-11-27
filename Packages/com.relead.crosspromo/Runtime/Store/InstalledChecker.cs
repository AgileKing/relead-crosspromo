using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Relead.CrossPromo
{
    public interface IInstalledChecker
    {
        bool IsInstalled(GameInfo game);
    }

    /// <summary>Always "not installed" (Windows, editor, unknown platforms). Editor can force ids for testing.</summary>
    public sealed class NoInstalledChecker : IInstalledChecker
    {
        public readonly HashSet<string> ForcedInstalled = new HashSet<string>();
        public bool IsInstalled(GameInfo game) => game != null && ForcedInstalled.Contains(game.Id);
    }

    /// <summary>Caches answers per game for the session (the native calls are cheap but not free).</summary>
    public sealed class CachingInstalledChecker : IInstalledChecker
    {
        readonly IInstalledChecker _inner;
        readonly Dictionary<string, bool> _cache = new Dictionary<string, bool>();

        public CachingInstalledChecker(IInstalledChecker inner) { _inner = inner; }

        public bool IsInstalled(GameInfo game)
        {
            if (game == null || game.Id == null) return false;
            bool v;
            if (_cache.TryGetValue(game.Id, out v)) return v;
            try { v = _inner.IsInstalled(game); }
            catch (Exception e) { Debug.LogWarning("[CrossPromo] installed check failed for " + game.Id + ": " + e.Message); v = false; }
            _cache[game.Id] = v;
            return v;
        }

        /// <summary>Call on resume: the player may have installed a game from the store.</summary>
        public void Clear() => _cache.Clear();
    }

    /// <summary>iOS: canOpenURL("relead-gNN://"). Needs the schemes in LSApplicationQueriesSchemes (added by the build post-process).</summary>
    public sealed class IosInstalledChecker : IInstalledChecker
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern bool _ReleadCP_CanOpenUrl(string url);
#else
        static bool _ReleadCP_CanOpenUrl(string url) => false;
#endif
        public bool IsInstalled(GameInfo game)
        {
            if (game == null || string.IsNullOrEmpty(game.UrlScheme)) return false;
            return _ReleadCP_CanOpenUrl(game.UrlScheme + "://");
        }
    }

    /// <summary>
    /// Android: package lookup (needs &lt;queries&gt;&lt;package&gt; on Android 11+), then the relead-gNN scheme
    /// (needs &lt;queries&gt;&lt;intent&gt; for the scheme; every game registers its own). Both added by the build post-process.
    /// Plain JNI from C#, no Java plugin needed.
    /// </summary>
    public sealed class AndroidInstalledChecker : IInstalledChecker
    {
        public bool IsInstalled(GameInfo game)
        {
            if (game == null) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var pm = activity.Call<AndroidJavaObject>("getPackageManager"))
            {
                if (!string.IsNullOrEmpty(game.Store.Android))
                {
                    try
                    {
                        using (pm.Call<AndroidJavaObject>("getPackageInfo", game.Store.Android, 0)) return true;
                    }
                    catch (AndroidJavaException) { /* NameNotFoundException = not installed or not visible */ }
                }
                if (!string.IsNullOrEmpty(game.UrlScheme))
                {
                    using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                    using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", game.UrlScheme + "://"))
                    using (var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", uri))
                    using (var info = pm.Call<AndroidJavaObject>("resolveActivity", intent, 0))
                    {
                        if (info != null) return true;
                    }
                }
            }
#endif
            return false;
        }
    }

    /// <summary>iOS identifierForVendor (same for all our apps on a device). Null elsewhere.</summary>
    public static class VendorId
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern string _ReleadCP_VendorId();
        public static string Get() { try { return _ReleadCP_VendorId(); } catch (Exception) { return null; } }
#else
        public static string Get() => null;
#endif
    }
}
