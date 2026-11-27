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
    /// maxPerDay cap, counted per local calendar day (the player's day, not UTC). Counts completed promos only
    /// (watched to the reward, video or fallback slideshow); skipped, failed or aborted ones don't use up the day
    /// (decision 0011). maxPerDay 0 = nothing today (kill switch from the catalog).
    /// </summary>
    public sealed class DailyCap
    {
        public const string Key = "relead.crosspromo.daily";
        readonly IKeyValueStore _store;

        public DailyCap(IKeyValueStore store) { _store = store; }

        /// <summary>
        /// Count for today. A stored day later than today (device date set back, or a time-zone change west)
        /// keeps counting that later day, so moving the clock back and forth never refills the cap.
        /// </summary>
        public int ShownToday(DateTime nowLocal)
        {
            string day;
            int n = Read(out day);
            return day != null && string.CompareOrdinal(DayKey(nowLocal), day) <= 0 ? n : 0;
        }

        int Read(out string day)
        {
            day = null;
            var raw = _store.Get(Key);
            if (string.IsNullOrEmpty(raw)) return 0;
            int bar = raw.IndexOf('|');
            if (bar != 10) return 0; // yyyy-MM-dd
            int n;
            if (!int.TryParse(raw.Substring(bar + 1), NumberStyles.None, CultureInfo.InvariantCulture, out n)) return 0;
            day = raw.Substring(0, bar);
            return n;
        }

        public bool CanShow(DateTime nowLocal, int maxPerDay) => ShownToday(nowLocal) < maxPerDay;

        public int Remaining(DateTime nowLocal, int maxPerDay) => Math.Max(0, maxPerDay - ShownToday(nowLocal));

        public void RecordShown(DateTime nowLocal)
        {
            string stored;
            Read(out stored);
            var today = DayKey(nowLocal);
            var day = stored != null && string.CompareOrdinal(today, stored) < 0 ? stored : today;
            int n = ShownToday(nowLocal) + 1;
            _store.Set(Key, day + "|" + n.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Counts the promo when its outcome is Completed. Returns true when it was counted.</summary>
        public bool RecordResult(PromoOutcome outcome, DateTime nowLocal)
        {
            if (outcome != PromoOutcome.Completed) return false;
            RecordShown(nowLocal);
            return true;
        }

        static string DayKey(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
