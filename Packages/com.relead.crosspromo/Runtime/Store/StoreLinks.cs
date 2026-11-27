using System;

namespace Relead.CrossPromo
{
    public sealed class StoreLink
    {
        /// <summary>Opens the store app directly.</summary>
        public string Primary;
        /// <summary>Web URL, for logs / if the store app is missing.</summary>
        public string Web;
    }

    /// <summary>Store URLs with campaign parameters. Pure.</summary>
    public static class StoreLinks
    {
        public const string Campaign = "crosspromo";

        /// <summary>Null when the game has no store id for this platform.</summary>
        public static StoreLink Build(PromoPlatform platform, GameInfo game, string hostGameId, string promoId, string iosProviderToken = null)
        {
            if (game == null) return null;
            var id = game.Store.For(platform);
            if (string.IsNullOrEmpty(id)) return null;
            switch (platform)
            {
                case PromoPlatform.Android:
                {
                    // Read by game B on first launch via the Play Install Referrer API.
                    var referrer = Uri.EscapeDataString("utm_source=" + hostGameId + "&utm_campaign=" + Campaign + "&utm_content=" + promoId);
                    var q = "?id=" + Uri.EscapeDataString(id) + "&referrer=" + referrer;
                    return new StoreLink { Primary = "market://details" + q, Web = "https://play.google.com/store/apps/details" + q };
                }
                case PromoPlatform.IOS:
                {
                    var q = "?mt=8&ct=" + Uri.EscapeDataString(IosCampaignToken(hostGameId, promoId));
                    if (!string.IsNullOrEmpty(iosProviderToken)) q += "&pt=" + Uri.EscapeDataString(iosProviderToken);
                    var url = "https://apps.apple.com/app/id" + Uri.EscapeDataString(id) + q;
                    return new StoreLink { Primary = url, Web = url };
                }
                case PromoPlatform.Windows:
                {
                    var cid = Uri.EscapeDataString(Campaign + "_" + promoId);
                    return new StoreLink
                    {
                        Primary = "ms-windows-store://pdp/?productid=" + Uri.EscapeDataString(id) + "&cid=" + cid,
                        Web = "https://apps.microsoft.com/detail/" + Uri.EscapeDataString(id) + "?cid=" + cid,
                    };
                }
                default: return null;
            }
        }

        /// <summary>App Store Connect campaign token: max 40 characters.</summary>
        public static string IosCampaignToken(string hostGameId, string promoId)
        {
            var t = "cp-" + hostGameId + "-" + promoId;
            return t.Length <= 40 ? t : t.Substring(0, 40);
        }
    }
}
