using System;
using System.Collections.Generic;

namespace Relead.CrossPromo
{
    /// <summary>Everything selection needs to know about the running game. Pure data, no Unity calls.</summary>
    public sealed class SelectionContext
    {
        public string HostGameId;
        /// <summary>Application.identifier. A game whose Android package equals it is the host too (guards against a wrong HostGameId).</summary>
        public string HostAppIdentifier;
        public PromoPlatform Platform;
        public string PackageVersion = CrossPromoInfo.PackageVersion;
        public DateTime NowUtc = DateTime.UtcNow;
        public PromoOrientation HostOrientation = PromoOrientation.Portrait;
        /// <summary>Player language ("nl", "nl-NL"); picks media with that lang, else the default-language media.</summary>
        public string Language;
        /// <summary>Screen long side / short side (iPad ~1.33, phones ~2.0-2.2). Null = unknown: catalog order decides.</summary>
        public float? ScreenAspect;
        /// <summary>Media types this build can play. Default: video only.</summary>
        public ICollection<string> PlayableTypes = new[] { PromoMedia.TypeVideo };
        /// <summary>Null = nothing counts as installed.</summary>
        public Func<GameInfo, bool> IsInstalled;
        /// <summary>Null = don't require cached media (used for prefetch planning).</summary>
        public Func<PromoMedia, bool> IsMediaCached;
        /// <summary>Skip promos whose target game has no store id for this platform (no point showing them). Off in editor tests.</summary>
        public bool RequireStoreLink = true;
        /// <summary>Avoid showing the same promo twice in a row when there is another choice.</summary>
        public string LastShownPromoId;
    }

    public sealed class PromoChoice
    {
        public Promo Promo;
        public PromoMedia Media;
        public GameInfo Game;
        /// <summary>True when the media orientation differs from the host (rotate hint / letterbox).</summary>
        public bool NeedsRotation;
    }

    public static class PromoSelector
    {
        /// <summary>Catalog-level filters: host, installed, dates, platform, package version, weight, store link, playable media.</summary>
        public static List<Promo> Eligible(Catalog catalog, SelectionContext ctx)
        {
            var list = new List<Promo>();
            if (catalog == null) return list;
            foreach (var p in catalog.Promos)
                if (Reject(catalog, p, ctx) == null) list.Add(p);
            return list;
        }

        /// <summary>Why a promo is filtered out, or null if it is eligible (ignoring cache state). For logs and the editor window.</summary>
        public static string Reject(Catalog catalog, Promo p, SelectionContext ctx)
        {
            if (p.GameId == ctx.HostGameId) return "self";
            if (p.Weight <= 0) return "weight 0";
            if (p.Start.HasValue && ctx.NowUtc < p.Start.Value) return "not started";
            if (p.End.HasValue && ctx.NowUtc >= p.End.Value) return "ended";
            if (p.Platforms != null && !p.Platforms.Contains(ctx.Platform)) return "platform";
            if (!SemVer.Satisfies(ctx.PackageVersion, p.MinPackageVersion)) return "needs package " + p.MinPackageVersion;
            var game = catalog.FindGame(p.GameId);
            if (game == null) return "unknown game";
            if (IsHostApp(game, ctx.HostAppIdentifier)) return "self";
            if (ctx.RequireStoreLink && string.IsNullOrEmpty(game.Store.For(ctx.Platform))) return "no store id";
            if (ctx.IsInstalled != null && ctx.IsInstalled(game)) return "installed";
            if (ChooseMedia(p, ctx, false) == null) return "no playable media";
            return null;
        }

        /// <summary>
        /// Best media of a promo among the playable (and, if <paramref name="requireCached"/> and a cache predicate is set,
        /// cached) items, ranked by: 1) host orientation; 2) language: ctx.Language, then default (no lang / en),
        /// then any other; 3) aspect ratio closest to ctx.ScreenAspect (items without a size rank after sized ones);
        /// 4) catalog order.
        /// </summary>
        public static PromoMedia ChooseMedia(Promo p, SelectionContext ctx, bool requireCached)
        {
            PromoMedia best = null;
            int bestOrient = 0, bestLang = 0;
            float bestAspect = 0;
            foreach (var m in p.Media)
            {
                if (!IsPlayable(m, ctx)) continue;
                if (requireCached && ctx.IsMediaCached != null && !ctx.IsMediaCached(m)) continue;
                int orient = m.Orientation == ctx.HostOrientation ? 0 : 1;
                int lang = LanguageRank(m, ctx.Language);
                float aspect = AspectDistance(m, ctx.ScreenAspect);
                if (best == null || orient < bestOrient || (orient == bestOrient && (lang < bestLang || (lang == bestLang && aspect < bestAspect))))
                {
                    best = m; bestOrient = orient; bestLang = lang; bestAspect = aspect;
                }
            }
            return best;
        }

        /// <summary>0 = the player's language, 1 = default (no lang / en), 2 = another language.</summary>
        public static int LanguageRank(PromoMedia m, string language)
        {
            if (!string.IsNullOrEmpty(m.Lang) && !string.IsNullOrEmpty(language))
            {
                int dash = language.IndexOfAny(new[] { '-', '_' });
                var primary = dash > 0 ? language.Substring(0, dash) : language;
                if (string.Equals(m.Lang, primary, StringComparison.OrdinalIgnoreCase)) return 0;
            }
            return m.IsDefaultLanguage ? 1 : 2;
        }

        /// <summary>|ln(media / screen)|: symmetric, 0 = same shape. Unknown on either side = 1000 (catalog order decides).</summary>
        public static float AspectDistance(PromoMedia m, float? screenAspect)
        {
            var a = m.AspectRatio;
            if (a == null || screenAspect == null || screenAspect <= 0) return 1000f;
            return Math.Abs((float)Math.Log(a.Value / screenAspect.Value));
        }

        /// <summary>Long side / short side of a screen size, or null for a zero size.</summary>
        public static float? Aspect(int width, int height)
            => width <= 0 || height <= 0 ? (float?)null : (float)Math.Max(width, height) / Math.Min(width, height);

        /// <summary>Bundle id and Android package are the same for Relead games, so the package identifies the host on every platform.</summary>
        public static bool IsHostApp(GameInfo game, string hostAppIdentifier)
            => game != null && !string.IsNullOrEmpty(hostAppIdentifier) && !string.IsNullOrEmpty(game.Store.Android)
               && string.Equals(game.Store.Android, hostAppIdentifier, StringComparison.OrdinalIgnoreCase);

        public static bool IsPlayable(PromoMedia m, SelectionContext ctx)
        {
            if (m == null || ctx.PlayableTypes == null) return false;
            foreach (var t in ctx.PlayableTypes) if (t == m.Type) return true;
            return false;
        }

        /// <summary>Weighted pick. <paramref name="random01"/> in [0, 1). Returns null for an empty list or zero total weight.</summary>
        public static Promo PickWeighted(IList<Promo> promos, double random01)
        {
            double total = 0;
            foreach (var p in promos) total += Math.Max(0, p.Weight);
            if (total <= 0) return null;
            double target = Math.Max(0, Math.Min(random01, 0.999999999)) * total;
            double acc = 0;
            foreach (var p in promos)
            {
                double w = Math.Max(0, p.Weight);
                if (w <= 0) continue;
                acc += w;
                if (target < acc) return p;
            }
            for (int i = promos.Count - 1; i >= 0; i--) if (promos[i].Weight > 0) return promos[i];
            return null;
        }

        /// <summary>Full selection: eligible promos with cached playable media, last-shown avoided, weighted pick.</summary>
        public static PromoChoice Select(Catalog catalog, SelectionContext ctx, double random01)
        {
            var ready = new List<Promo>();
            foreach (var p in Eligible(catalog, ctx))
                if (ChooseMedia(p, ctx, true) != null) ready.Add(p);
            if (ready.Count > 1 && ctx.LastShownPromoId != null)
                ready.RemoveAll(p => p.Id == ctx.LastShownPromoId);
            var promo = PickWeighted(ready, random01);
            if (promo == null) return null;
            var media = ChooseMedia(promo, ctx, true);
            return new PromoChoice
            {
                Promo = promo,
                Media = media,
                Game = catalog.FindGame(promo.GameId),
                NeedsRotation = media.Orientation != ctx.HostOrientation,
            };
        }
    }
}
