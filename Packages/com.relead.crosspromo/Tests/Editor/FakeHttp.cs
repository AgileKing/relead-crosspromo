using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Relead.CrossPromo.Tests
{
    /// <summary>Synchronous IHttp. Routes by full URL; unknown URL = network error.</summary>
    sealed class FakeHttp : IHttp
    {
        public sealed class Route
        {
            public long Status = 200;
            public byte[] Body;
            public Dictionary<string, string> Headers = new Dictionary<string, string>();
            public Func<HttpRequest, Route> Dynamic;
        }

        public readonly Dictionary<string, Route> Routes = new Dictionary<string, Route>();
        public readonly List<HttpRequest> Requests = new List<HttpRequest>();
        /// <summary>When set, callbacks are queued instead of run, to simulate in-flight requests.</summary>
        public bool Defer;
        public readonly List<Action> Pending = new List<Action>();

        public void Text(string url, string body, long status = 200, Dictionary<string, string> headers = null)
        {
            Routes[url] = new Route { Status = status, Body = body == null ? null : Encoding.UTF8.GetBytes(body), Headers = headers ?? new Dictionary<string, string>() };
        }

        public void Bytes(string url, byte[] body) { Routes[url] = new Route { Body = body }; }

        public int Count(string url) { int n = 0; foreach (var r in Requests) if (r.Url == url) n++; return n; }

        public void Send(HttpRequest request, Action<HttpResponse> done)
        {
            Requests.Add(request);
            var resp = new HttpResponse();
            Route route;
            if (Routes.TryGetValue(request.Url, out route))
            {
                if (route.Dynamic != null) route = route.Dynamic(request);
                resp.Status = route.Status;
                foreach (var h in route.Headers) resp.Headers[h.Key] = h.Value;
                if (!string.IsNullOrEmpty(request.DownloadToPath))
                {
                    if (route.Status == 200 && route.Body != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(request.DownloadToPath));
                        File.WriteAllBytes(request.DownloadToPath, route.Body);
                    }
                }
                else resp.Data = route.Body;
            }
            else resp.Error = "Cannot resolve host";
            if (Defer) Pending.Add(() => done(resp));
            else done(resp);
        }

        public void Flush()
        {
            while (Pending.Count > 0)
            {
                var a = Pending[0];
                Pending.RemoveAt(0);
                a();
            }
        }
    }

    sealed class MemStore : IKeyValueStore
    {
        public readonly Dictionary<string, string> D = new Dictionary<string, string>();
        public string Get(string k) { string v; return D.TryGetValue(k, out v) ? v : null; }
        public void Set(string k, string v) { D[k] = v; }
    }
}
