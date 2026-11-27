using System;
using System.Collections.Generic;
using System.Globalization;
using Relead.CrossPromo.Json;

namespace Relead.CrossPromo
{
    /// <summary>
    /// Forgiving parser: unknown fields and unknown media types are ignored, an invalid game/promo/media entry
    /// is dropped (with a warning) instead of failing the whole catalog. Only invalid JSON, a non-object root,
    /// a wrong schemaVersion or a missing promos array fails.
    /// </summary>
    public static class CatalogParser
    {
        public sealed class Result
        {
            public Catalog Catalog;
            public string Error;
            public readonly List<string> Warnings = new List<string>();
            public bool Ok => Catalog != null;
        }

        public static Result Parse(string json)
        {
            var r = new Result();
            object rootObj;
            string err;
            if (!MiniJson.TryParse(json, out rootObj, out err)) { r.Error = "Invalid JSON: " + err; return r; }
            var root = rootObj as Dictionary<string, object>;
            if (root == null) { r.Error = "Root is not an object"; return r; }

            var version = JsonRead.Long(root, "schemaVersion");
            if (version != Catalog.SupportedSchemaVersion)
            {
                r.Error = "Unsupported schemaVersion " + (version.HasValue ? version.Value.ToString(CultureInfo.InvariantCulture) : "(missing)");
                return r;
            }

            var promos = JsonRead.Arr(root, "promos");
            if (promos == null) { r.Error = "Missing promos array"; return r; }

            var c = new Catalog { SchemaVersion = (int)version.Value, Updated = ParseDate(JsonRead.Str(root, "updated")) };

            var rules = JsonRead.Obj(root, "rules");
            if (rules != null)
            {
                var maxPerDay = JsonRead.Long(rules, "maxPerDay");
                if (maxPerDay != null && maxPerDay >= 0) c.Rules.MaxPerDay = (int)Math.Min(maxPerDay.Value, int.MaxValue);
                var skip = JsonRead.Num(rules, "skipAfterSec");
                if (skip != null && skip >= 0) c.Rules.SkipAfterSec = (float)skip.Value;
                var endCard = JsonRead.Num(rules, "endCardAtSec");
                if (endCard != null && endCard >= 0) c.Rules.EndCardAtSec = (float)endCard.Value;
                var reward = JsonRead.Num(rules, "rewardAfterSec");
                if (reward != null && reward >= 0) c.Rules.RewardAfterSec = (float)reward.Value;
                if (c.Rules.RewardAfterSec < c.Rules.EndCardAtSec) { r.Warnings.Add("rules.rewardAfterSec < endCardAtSec, raised"); c.Rules.RewardAfterSec = c.Rules.EndCardAtSec; }
                var hint = JsonRead.Num(rules, "rotateHintTimeoutSec");
                if (hint != null && hint >= 0) c.Rules.RotateHintTimeoutSec = (float)hint.Value;
            }

            var games = JsonRead.Arr(root, "games");
            if (games != null)
            {
                for (int i = 0; i < games.Count; i++)
                {
                    var g = ParseGame(games[i] as Dictionary<string, object>);
                    if (g == null) { r.Warnings.Add("games[" + i + "] invalid, skipped"); continue; }
                    if (c.FindGame(g.Id) != null) { r.Warnings.Add("games[" + i + "] duplicate id " + g.Id + ", skipped"); continue; }
                    c.Games.Add(g);
                }
            }

            for (int i = 0; i < promos.Count; i++)
            {
                var p = ParsePromo(promos[i] as Dictionary<string, object>, i, r.Warnings);
                if (p == null) { r.Warnings.Add("promos[" + i + "] invalid, skipped"); continue; }
                if (c.FindPromo(p.Id) != null) { r.Warnings.Add("promos[" + i + "] duplicate id " + p.Id + ", skipped"); continue; }
                c.Promos.Add(p);
            }

            r.Catalog = c;
            return r;
        }

        static GameInfo ParseGame(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var id = JsonRead.Str(o, "id");
            if (string.IsNullOrEmpty(id)) return null;
            var g = new GameInfo
            {
                Id = id,
                Name = JsonRead.Str(o, "name") ?? id,
                UrlScheme = JsonRead.Str(o, "urlScheme"),
                Orientation = ParseOrientation(JsonRead.Str(o, "orientation")),
                Icon = SafePath(JsonRead.Str(o, "icon")),
            };
            var store = JsonRead.Obj(o, "store");
            if (store != null)
            {
                g.Store.Android = Clean(JsonRead.Str(store, "android"));
                g.Store.Ios = Clean(JsonRead.Str(store, "ios"));
                g.Store.Windows = Clean(JsonRead.Str(store, "windows"));
            }
            return g;
        }

        /// <summary>Catalog file paths must be plain relative paths (no other host, no ".."); anything else counts as missing.</summary>
        static string SafePath(string s) => UrlUtil.IsSafeRelative(s) ? s : null;

        /// <summary>"TODO" / empty placeholders count as missing.</summary>
        static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (string.Equals(s, "TODO", StringComparison.OrdinalIgnoreCase)) return null;
            return s;
        }

        static Promo ParsePromo(Dictionary<string, object> o, int index, List<string> warnings)
        {
            if (o == null) return null;
            var id = JsonRead.Str(o, "id");
            var gameId = JsonRead.Str(o, "gameId");
            var weight = JsonRead.Num(o, "weight");
            var media = JsonRead.Arr(o, "media");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(gameId) || weight == null || media == null) return null;

            var p = new Promo
            {
                Id = id,
                GameId = gameId,
                Weight = Math.Max(0, weight.Value),
                Start = ParseDate(JsonRead.Str(o, "start")),
                End = ParseDate(JsonRead.Str(o, "end")),
                Headline = ParseLocalized(JsonRead.Obj(o, "headline")),
                Cta = ParseLocalized(JsonRead.Obj(o, "cta")),
                Poster = SafePath(JsonRead.Str(o, "poster")),
            };
            var mpv = JsonRead.Str(o, "minPackageVersion");
            if (!string.IsNullOrEmpty(mpv)) p.MinPackageVersion = mpv;

            var platforms = JsonRead.Arr(o, "platforms");
            if (platforms != null)
            {
                p.Platforms = new List<PromoPlatform>();
                foreach (var item in platforms)
                {
                    var pl = ParsePlatform(item as string);
                    if (pl != null) p.Platforms.Add(pl.Value);
                }
            }

            for (int m = 0; m < media.Count; m++)
            {
                var md = ParseMedia(media[m] as Dictionary<string, object>);
                if (md == null) { warnings.Add("promos[" + index + "].media[" + m + "] invalid, skipped"); continue; }
                p.Media.Add(md);
            }
            if (p.Media.Count == 0) return null;
            return p;
        }

        static PromoMedia ParseMedia(Dictionary<string, object> o)
        {
            if (o == null) return null;
            var type = JsonRead.Str(o, "type");
            var orientation = ParseOrientation(JsonRead.Str(o, "orientation"));
            var url = JsonRead.Str(o, "url");
            if (string.IsNullOrEmpty(type) || orientation == null || !UrlUtil.IsSafeRelative(url)) return null; // relative to the base URL only
            var sha = JsonRead.Str(o, "sha256");
            var lang = JsonRead.Str(o, "lang");
            var w = JsonRead.Long(o, "width");
            var h = JsonRead.Long(o, "height");
            return new PromoMedia
            {
                Type = type,
                Orientation = orientation.Value,
                Url = url,
                DurationSec = JsonRead.Num(o, "durationSec"),
                Lang = string.IsNullOrWhiteSpace(lang) ? null : lang.Trim().ToLowerInvariant(),
                Width = w != null && w > 0 && w <= int.MaxValue ? (int)w.Value : (int?)null,
                Height = h != null && h > 0 && h <= int.MaxValue ? (int)h.Value : (int?)null,
                Bytes = JsonRead.Long(o, "bytes"),
                Sha256 = string.IsNullOrEmpty(sha) ? null : sha.ToLowerInvariant(),
            };
        }

        static LocalizedText ParseLocalized(Dictionary<string, object> o)
        {
            var t = new LocalizedText();
            if (o == null) return t;
            foreach (var kv in o)
            {
                var s = kv.Value as string;
                if (s != null) t.Values[kv.Key] = s;
            }
            return t;
        }

        internal static PromoOrientation? ParseOrientation(string s)
        {
            if (s == "portrait") return PromoOrientation.Portrait;
            if (s == "landscape") return PromoOrientation.Landscape;
            return null;
        }

        internal static PromoPlatform? ParsePlatform(string s)
        {
            switch (s)
            {
                case "android": return PromoPlatform.Android;
                case "ios": return PromoPlatform.IOS;
                case "windows": return PromoPlatform.Windows;
                default: return null;
            }
        }

        internal static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            DateTime d;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out d))
                return DateTime.SpecifyKind(d, DateTimeKind.Utc);
            return null;
        }
    }
}
