using System;
using System.Globalization;

namespace Relead.CrossPromo
{
    /// <summary>Tiny persistent string store. PlayerPrefs in the game, a dictionary in tests.</summary>
    public interface IKeyValueStore
    {
        string Get(string key);
        void Set(string key, string value);
    }

    /// <summary>
    /// maxPerDay cap, counted per local calendar day (the player's day, not UTC). Counts every shown promo,
    /// video or fallback slideshow. maxPerDay 0 = nothing today (kill switch from the catalog).
    /// </summary>
    public sealed class DailyCap
    {
        public const string Key = "relead.crosspromo.daily";
        readonly IKeyValueStore _store;

        public DailyCap(IKeyValueStore store) { _store = store; }

        public int ShownToday(DateTime nowLocal)
        {
            var raw = _store.Get(Key);
            if (string.IsNullOrEmpty(raw)) return 0;
            int bar = raw.IndexOf('|');
            if (bar <= 0) return 0;
            if (raw.Substring(0, bar) != DayKey(nowLocal)) return 0;
            int n;
            return int.TryParse(raw.Substring(bar + 1), NumberStyles.None, CultureInfo.InvariantCulture, out n) ? n : 0;
        }

        public bool CanShow(DateTime nowLocal, int maxPerDay) => ShownToday(nowLocal) < maxPerDay;

        public int Remaining(DateTime nowLocal, int maxPerDay) => Math.Max(0, maxPerDay - ShownToday(nowLocal));

        public void RecordShown(DateTime nowLocal)
        {
            int n = ShownToday(nowLocal) + 1;
            _store.Set(Key, DayKey(nowLocal) + "|" + n.ToString(CultureInfo.InvariantCulture));
        }

        static string DayKey(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
