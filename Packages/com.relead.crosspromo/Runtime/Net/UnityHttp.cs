using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Relead.CrossPromo
{
    /// <summary>IHttp over UnityWebRequest, driven by coroutines on the CrossPromo runner object. Never blocks.</summary>
    public sealed class UnityHttp : IHttp
    {
        readonly MonoBehaviour _runner;

        public UnityHttp(MonoBehaviour runner) { _runner = runner; }

        public void Send(HttpRequest request, Action<HttpResponse> done)
        {
            _runner.StartCoroutine(Run(request, done));
        }

        static IEnumerator Run(HttpRequest r, Action<HttpResponse> done)
        {
            var resp = new HttpResponse();
            UnityWebRequest req = null;
            try
            {
                req = new UnityWebRequest(r.Url, r.Method);
                if (r.Body != null) req.uploadHandler = new UploadHandlerRaw(r.Body);
                if (!string.IsNullOrEmpty(r.DownloadToPath))
                {
                    var dir = Path.GetDirectoryName(r.DownloadToPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    req.downloadHandler = new DownloadHandlerFile(r.DownloadToPath) { removeFileOnAbort = true };
                }
                else req.downloadHandler = new DownloadHandlerBuffer();
                foreach (var h in r.Headers) req.SetRequestHeader(h.Key, h.Value);
                req.timeout = r.TimeoutSec;
            }
            catch (Exception e)
            {
                if (req != null) req.Dispose();
                resp.Error = e.Message;
                done(resp);
                yield break;
            }

            var op = req.SendWebRequest();
            bool tooLarge = false;
            while (!op.isDone)
            {
                if (r.MaxBytes > 0 && TooLarge(req, r.MaxBytes))
                {
                    tooLarge = true;
                    req.Abort(); // DownloadHandlerFile.removeFileOnAbort: the partial file is deleted
                    break;
                }
                yield return null;
            }
            if (!tooLarge && r.MaxBytes > 0 && TooLarge(req, r.MaxBytes)) tooLarge = true;

            if (tooLarge)
            {
                req.Dispose();
                if (!string.IsNullOrEmpty(r.DownloadToPath)) { try { File.Delete(r.DownloadToPath); } catch (Exception) { } }
                resp.Status = 0;
                resp.Error = HttpResponse.TooLargeError;
                done(resp);
                yield break;
            }

            try
            {
                resp.Status = req.result == UnityWebRequest.Result.ConnectionError ? 0 : req.responseCode;
                resp.Error = req.error;
                var headers = req.GetResponseHeaders();
                if (headers != null) foreach (var kv in headers) resp.Headers[kv.Key] = kv.Value;
                if (string.IsNullOrEmpty(r.DownloadToPath)) resp.Data = req.downloadHandler.data;
            }
            catch (Exception e)
            {
                resp.Status = 0;
                resp.Error = e.Message;
            }
            finally { req.Dispose(); }
            done(resp);
        }

        /// <summary>Content-Length announced over the cap, or more bytes received than the cap.</summary>
        static bool TooLarge(UnityWebRequest req, long max)
        {
            if ((long)req.downloadedBytes > max) return true;
            var cl = req.GetResponseHeader("Content-Length");
            long n;
            return cl != null && long.TryParse(cl, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out n) && n > max;
        }
    }
}
