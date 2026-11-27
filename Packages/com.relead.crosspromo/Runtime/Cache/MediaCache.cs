using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Relead.CrossPromo
{
    /// <summary>
    /// Files under persistentDataPath/relead-crosspromo/media, named by a hash of the relative URL
    /// (catalog URLs are immutable). A file only appears there after its size/sha256 check passed,
    /// so "exists" = "cached and valid".
    /// </summary>
    public sealed class MediaCache
    {
        readonly string _dir;

        public MediaCache(string dir) { _dir = dir; }

        public string Directory => _dir;

        public string PathFor(string relativeUrl)
        {
            var ext = Path.GetExtension(StripQuery(relativeUrl));
            if (ext.Length > 8) ext = "";
            return Path.Combine(_dir, Hash(relativeUrl).Substring(0, 24) + ext);
        }

        public bool IsCached(string relativeUrl) => !string.IsNullOrEmpty(relativeUrl) && File.Exists(PathFor(relativeUrl));
        /// <summary>Cached and, when the catalog gives a size, still that size (a truncated or replaced file is not cached).</summary>
        public bool IsCached(PromoMedia m)
        {
            if (m == null || !IsCached(m.Url)) return false;
            if (!m.Bytes.HasValue) return true;
            try { return new FileInfo(PathFor(m.Url)).Length == m.Bytes.Value; }
            catch (Exception) { return false; }
        }

        /// <summary>Removes a cached file (e.g. after it failed to play), so it is downloaded again.</summary>
        public void Delete(string relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return;
            TryDelete(PathFor(relativeUrl));
        }

        /// <summary>Size and sha256 check. Missing expectations pass; an empty file never does.</summary>
        public static bool Verify(string path, long? bytes, string sha256, out string error)
        {
            error = null;
            if (!File.Exists(path)) { error = "missing"; return false; }
            long len = new FileInfo(path).Length;
            if (len == 0) { error = "empty"; return false; }
            if (bytes.HasValue && len != bytes.Value) { error = "size " + len + " != " + bytes.Value; return false; }
            if (!string.IsNullOrEmpty(sha256))
            {
                string actual;
                using (var s = File.OpenRead(path)) actual = Hex(SHA256.Create().ComputeHash(s));
                if (!string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase)) { error = "sha256 mismatch"; return false; }
            }
            return true;
        }

        /// <summary>Moves a verified temp file into the cache. Deletes the temp file on failure.</summary>
        public bool Commit(string tempPath, string relativeUrl, long? bytes, string sha256, out string error)
        {
            try
            {
                if (!Verify(tempPath, bytes, sha256, out error)) { TryDelete(tempPath); return false; }
                System.IO.Directory.CreateDirectory(_dir);
                var final = PathFor(relativeUrl);
                AtomicFile.Replace(tempPath, final);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                TryDelete(tempPath);
                return false;
            }
        }

        public string TempPathFor(string relativeUrl) => PathFor(relativeUrl) + ".part";

        /// <summary>Deletes every cached file not in <paramref name="keepRelativeUrls"/> (and stale .part files). Returns bytes freed.</summary>
        public long EvictExcept(ICollection<string> keepRelativeUrls)
        {
            if (!System.IO.Directory.Exists(_dir)) return 0;
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in keepRelativeUrls) keep.Add(Path.GetFileName(PathFor(u)));
            long freed = 0;
            foreach (var f in System.IO.Directory.GetFiles(_dir))
            {
                if (keep.Contains(Path.GetFileName(f))) continue;
                try { freed += new FileInfo(f).Length; File.Delete(f); } catch (Exception) { }
            }
            return freed;
        }

        public long TotalBytes()
        {
            if (!System.IO.Directory.Exists(_dir)) return 0;
            long n = 0;
            foreach (var f in System.IO.Directory.GetFiles(_dir)) n += new FileInfo(f).Length;
            return n;
        }

        static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch (Exception) { } }

        static string StripQuery(string u)
        {
            int q = u.IndexOfAny(new[] { '?', '#' });
            return q >= 0 ? u.Substring(0, q) : u;
        }

        internal static string Hash(string s) => Hex(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(s ?? "")));

        internal static string Hex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (var x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }
}
