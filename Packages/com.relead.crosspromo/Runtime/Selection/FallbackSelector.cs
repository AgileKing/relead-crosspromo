using System;
using System.Collections.Generic;
using UnityEngine;

namespace Relead.CrossPromo
{
    /// <summary>One game in the bundled offline set (see FallbackGameSet). Screenshots ship in the package only.</summary>
    [Serializable]
    public sealed class FallbackGame
    {
        public string id;
        public string name;
        public string urlScheme;
        public string androidPackage;
        public string iosAppId;
        public string windowsProductId;
        public Texture2D[] screenshots = new Texture2D[0];

        public int ScreenshotCount
        {
            get
            {
                int n = 0;
                if (screenshots != null) foreach (var s in screenshots) if (s != null) n++;
                return n;
            }
        }

        public GameInfo ToGameInfo()
        {
            var g = new GameInfo { Id = id, Name = string.IsNullOrEmpty(name) ? id : name, UrlScheme = urlScheme };
            g.Store.Android = Nullify(androidPackage);
            g.Store.Ios = Nullify(iosAppId);
            g.Store.Windows = Nullify(windowsProductId);
            return g;
        }

        static string Nullify(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary>
    /// Picks games for the offline slideshow: never the host, never a game without screenshots.
    /// Prefers games that are not installed and have a store id for this platform (shuffled);
    /// if none are left, falls back to installed ones so the rewarded slot still works offline.
    /// </summary>
    public static class FallbackSelector
    {
        public static List<FallbackGame> Select(IList<FallbackGame> games, string hostGameId, PromoPlatform platform,
            Func<GameInfo, bool> isInstalled, int maxGames, System.Random random, string hostAppIdentifier = null)
        {
            var preferred = new List<FallbackGame>();
            var lastResort = new List<FallbackGame>();
            if (games != null)
            {
                foreach (var g in games)
                {
                    if (g == null || string.IsNullOrEmpty(g.id) || g.id == hostGameId || g.ScreenshotCount == 0) continue;
                    var info = g.ToGameInfo();
                    if (PromoSelector.IsHostApp(info, hostAppIdentifier)) continue;
                    bool installed = isInstalled != null && isInstalled(info);
                    bool hasStore = !string.IsNullOrEmpty(info.Store.For(platform));
                    if (!installed && hasStore) preferred.Add(g);
                    else lastResort.Add(g);
                }
            }
            var pool = preferred.Count > 0 ? preferred : lastResort;
            Shuffle(pool, random ?? new System.Random());
            if (maxGames > 0 && pool.Count > maxGames) pool.RemoveRange(maxGames, pool.Count - maxGames);
            return pool;
        }

        static void Shuffle<T>(IList<T> list, System.Random r)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }
    }
}
