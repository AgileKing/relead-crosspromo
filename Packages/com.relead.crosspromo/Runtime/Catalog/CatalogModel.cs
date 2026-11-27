using System;
using System.Collections.Generic;

namespace Relead.CrossPromo
{
    public enum PromoOrientation { Portrait, Landscape }

    public enum PromoPlatform { Android, IOS, Windows }

    /// <summary>Parsed promos.json (schemaVersion 1). Contract: crosspromo-content/catalog/promos.schema.json.</summary>
    public sealed class Catalog
    {
        public const int SupportedSchemaVersion = 1;

        public int SchemaVersion;
        public DateTime? Updated;
        public CatalogRules Rules = new CatalogRules();
        public List<GameInfo> Games = new List<GameInfo>();
        public List<Promo> Promos = new List<Promo>();

        public GameInfo FindGame(string id)
        {
            if (id == null) return null;
            foreach (var g in Games) if (g.Id == id) return g;
            return null;
        }

        public Promo FindPromo(string id)
        {
            if (id == null) return null;
            foreach (var p in Promos) if (p.Id == id) return p;
            return null;
        }
    }

    public sealed class CatalogRules
    {
        public int MaxPerDay = 3;
        /// <summary>Top-left Skip button appears (0 = from the start). Skip before RewardAfterSec = no reward.</summary>
        public float SkipAfterSec = 0f;
        /// <summary>Download panel over the still-playing video.</summary>
        public float EndCardAtSec = 25f;
        /// <summary>"Earned! Close" appears; any close from here on = Completed. Never below EndCardAtSec.</summary>
        public float RewardAfterSec = 30f;
        public float RotateHintTimeoutSec = 3f;
    }

    public sealed class GameInfo
    {
        public string Id;
        public string Name;
        public string UrlScheme;
        public PromoOrientation? Orientation;
        public string Icon;
        public StoreIds Store = new StoreIds();
    }

    public sealed class StoreIds
    {
        /// <summary>Package name. "TODO"/empty in the catalog = null.</summary>
        public string Android;
        /// <summary>Numeric App Store id.</summary>
        public string Ios;
        /// <summary>Microsoft Store product id.</summary>
        public string Windows;

        public string For(PromoPlatform p)
        {
            switch (p)
            {
                case PromoPlatform.Android: return Android;
                case PromoPlatform.IOS: return Ios;
                case PromoPlatform.Windows: return Windows;
                default: return null;
            }
        }
    }

    public sealed class Promo
    {
        public string Id;
        public string GameId;
        public double Weight;
        public DateTime? Start;
        public DateTime? End;
        public string MinPackageVersion = "0.1.0";
        /// <summary>Null = all platforms.</summary>
        public List<PromoPlatform> Platforms;
        public LocalizedText Headline = new LocalizedText();
        public LocalizedText Cta = new LocalizedText();
        public string Poster;
        public List<PromoMedia> Media = new List<PromoMedia>();
    }

    public sealed class PromoMedia
    {
        public const string TypeVideo = "video";
        public const string TypePlayable = "playable";

        /// <summary>Raw type string; unknown types are kept and skipped by selection.</summary>
        public string Type;
        public PromoOrientation Orientation;
        /// <summary>Relative to the base URL. Immutable on the server: new content = new path.</summary>
        public string Url;
        public double? DurationSec;
        /// <summary>ISO 639-1 language of the content, lower case. Null = default for every language.</summary>
        public string Lang;
        /// <summary>Pixel size (display), or null when the catalog doesn't say.</summary>
        public int? Width;
        public int? Height;
        public long? Bytes;
        /// <summary>Lower-case hex, or null.</summary>
        public string Sha256;

        /// <summary>Long side / short side, or null without a size.</summary>
        public float? AspectRatio
        {
            get
            {
                if (Width == null || Height == null || Width <= 0 || Height <= 0) return null;
                return (float)Math.Max(Width.Value, Height.Value) / Math.Min(Width.Value, Height.Value);
            }
        }

        /// <summary>No language, or English: the variant for players whose language has no own cut.</summary>
        public bool IsDefaultLanguage => string.IsNullOrEmpty(Lang) || string.Equals(Lang, "en", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Language code → text, fallback "en".</summary>
    public sealed class LocalizedText
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsEmpty => Values.Count == 0;

        /// <summary>"nl-NL" → "nl-NL", then "nl", then "en", then any value, then "".</summary>
        public string Get(string language)
        {
            string v;
            if (!string.IsNullOrEmpty(language))
            {
                if (Values.TryGetValue(language, out v) && !string.IsNullOrEmpty(v)) return v;
                int dash = language.IndexOfAny(new[] { '-', '_' });
                if (dash > 0 && Values.TryGetValue(language.Substring(0, dash), out v) && !string.IsNullOrEmpty(v)) return v;
            }
            if (Values.TryGetValue("en", out v) && !string.IsNullOrEmpty(v)) return v;
            foreach (var kv in Values) if (!string.IsNullOrEmpty(kv.Value)) return kv.Value;
            return "";
        }
    }
}
