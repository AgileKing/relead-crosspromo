using System;
using System.Collections.Generic;

namespace Relead.CrossPromo
{
    /// <summary>Everything selection needs to know about the running game. Pure data, no Unity calls.</summary>
    public sealed class SelectionContext
    {
        public string HostGameId;
        public PromoPlatform Platform;
        public string PackageVersion = CrossPromoInfo.PackageVersion;
        public DateTime NowUtc = DateTime.UtcNow;
        public PromoOrientation HostOrientation = PromoOrientation.Portrait;
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
            if (ctx.RequireStoreLink && string.IsNullOrEmpty(game.Store.For(ctx.Platform))) return "no store id";
            if (ctx.IsInstalled != null && ctx.IsInstalled(game)) return "installed";
            if (ChooseMedia(p, ctx, false) == null) return "no playable media";
            return null;
        }

        /// <summary>
        /// Best media of a promo: playable type, cached (if <paramref name="requireCached"/> and a cache predicate is set),
        /// host orientation preferred, otherwise the other orientation. Within an orientation the first in catalog order wins.
        /// </summary>
        public static PromoMedia ChooseMedia(Promo p, SelectionContext ctx, bool requireCached)
        {
            PromoMedia other = null;
            foreach (var m in p.Media)
            {
                if (!IsPlayable(m, ctx)) continue;
                if (requireCached && ctx.IsMediaCached != null && !ctx.IsMediaCached(m)) continue;
                if (m.Orientation == ctx.HostOrientation) return m;
                if (other == null) other = m;
            }
            return other;
        }

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
