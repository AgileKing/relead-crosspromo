using System;
using System.Collections.Generic;

namespace Relead.CrossPromo
{
    public sealed class HttpRequest
    {
        /// <summary>Generous: the host may be slow shared hosting whose first request takes seconds.</summary>
        public const int DefaultTimeoutSec = 15;

        public string Url;
        public string Method = "GET";
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();
        public byte[] Body;
        /// <summary>If set, the body is streamed to this file instead of memory.</summary>
        public string DownloadToPath;
        public int TimeoutSec = DefaultTimeoutSec;
        /// <summary>Abort when the body (Content-Length or bytes received) exceeds this. 0 = no limit.</summary>
        public long MaxBytes;
    }

    public sealed class HttpResponse
    {
        /// <summary>Error text when HttpRequest.MaxBytes was exceeded (Status 0, nothing kept).</summary>
        public const string TooLargeError = "too large";

        /// <summary>0 = no HTTP answer (network error, timeout, DNS block).</summary>
        public long Status;
        public string Error;
        public byte[] Data;
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsNetworkError => Status == 0;
        public bool IsSuccess => Status >= 200 && Status < 300;
        public string Text => Data == null ? null : System.Text.Encoding.UTF8.GetString(Data);

        public string Header(string name)
        {
            string v;
            return Headers.TryGetValue(name, out v) ? v : null;
        }
    }

    /// <summary>Async HTTP. The callback is always invoked exactly once (on the main thread in the Unity implementation).</summary>
    public interface IHttp
    {
        void Send(HttpRequest request, Action<HttpResponse> done);
    }

    public static class UrlUtil
    {
        /// <summary>
        /// Base URL + catalog-relative path. Null when the path is not a plain relative path (absolute URL,
        /// "//host", "..", odd characters): catalog content can never point downloads at another host.
        /// </summary>
        public static string Join(string baseUrl, string relative)
        {
            if (string.IsNullOrEmpty(relative)) return baseUrl;
            if (!IsSafeRelative(relative)) return null;
            if (string.IsNullOrEmpty(baseUrl)) return relative;
            return baseUrl.TrimEnd('/') + "/" + relative.TrimStart('/');
        }

        public const int MaxPathLength = 512;

        /// <summary>Letters, digits, '.', '_', '-' and '/' separated segments; no empty, "." or ".." segment. One leading '/' allowed.</summary>
        public static bool IsSafeRelative(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > MaxPathLength) return false;
            var p = path[0] == '/' ? path.Substring(1) : path;
            if (p.Length == 0) return false;
            foreach (var ch in p)
            {
                bool ok = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-' || ch == '/';
                if (!ok) return false;
            }
            foreach (var seg in p.Split('/'))
                if (seg.Length == 0 || seg == "." || seg == "..") return false;
            return true;
        }

        public static bool IsHttps(string url) => !string.IsNullOrEmpty(url) && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
