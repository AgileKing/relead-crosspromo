using System;
using System.Globalization;

namespace Relead.CrossPromo
{
    /// <summary>Minimal semver compare on major.minor.patch. Pre-release/build suffixes are ignored. Unparseable = 0.0.0.</summary>
    public static class SemVer
    {
        public static int Compare(string a, string b)
        {
            var x = Parse(a);
            var y = Parse(b);
            for (int i = 0; i < 3; i++)
            {
                int c = x[i].CompareTo(y[i]);
                if (c != 0) return c;
            }
            return 0;
        }

        /// <summary>True if <paramref name="have"/> &gt;= <paramref name="need"/>. Empty need = always true.</summary>
        public static bool Satisfies(string have, string need)
            => string.IsNullOrEmpty(need) || Compare(have, need) >= 0;

        static int[] Parse(string v)
        {
            var r = new int[3];
            if (string.IsNullOrEmpty(v)) return r;
            v = v.Trim();
            if (v.StartsWith("v", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
            int cut = v.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) v = v.Substring(0, cut);
            var parts = v.Split('.');
            for (int i = 0; i < 3 && i < parts.Length; i++)
            {
                int n;
                if (int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out n)) r[i] = n;
            }
            return r;
        }
    }
}
