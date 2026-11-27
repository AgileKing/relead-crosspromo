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
    }

    public sealed class HttpResponse
    {
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
        public static string Join(string baseUrl, string relative)
        {
            if (string.IsNullOrEmpty(relative)) return baseUrl;
            if (relative.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || relative.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return relative;
            if (string.IsNullOrEmpty(baseUrl)) return relative;
            return baseUrl.TrimEnd('/') + "/" + relative.TrimStart('/');
        }
    }
}
