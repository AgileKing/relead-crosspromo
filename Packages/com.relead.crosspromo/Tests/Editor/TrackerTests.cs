using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Relead.CrossPromo.Json;

namespace Relead.CrossPromo.Tests
{
    public class TrackerTests
    {
        const string Endpoint = "https://api.relead.com";
        const string Url = "https://api.relead.com/v1/games/gpking/events";
        string _dir;
        DateTime _now;
        FakeHttp _http;

        [SetUp]
        public void SetUp()
        {
            _dir = TestUtil.NewTempDir();
            _now = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);
            _http = new FakeHttp();
        }

        [TearDown] public void TearDown() { try { Directory.Delete(_dir, true); } catch (Exception) { } }

        ReleadTracker New() => new ReleadTracker(_http, Endpoint, "ingest-key", new TrackerEnvelope
        {
            GameId = "gpking", InstallId = "inst-1", AppVersion = "1.2", Platform = "Android", OsVersion = "14", Locale = "nl-NL",
        }, Path.Combine(_dir, "events.jsonl"), () => _now, new Dictionary<string, object> { { "hostGameId", "gpking" }, { "packageVersion", "0.1.0" } });

        static Dictionary<string, object> Body(HttpRequest r)
        {
            object o;
            string err;
            Assert.IsTrue(MiniJson.TryParse(Encoding.UTF8.GetString(r.Body), out o, out err), err);
            return (Dictionary<string, object>)o;
        }

        [Test]
        public void SendsContractShapedBatch()
        {
            _http.Text(Url, "{\"accepted\":1,\"duplicate\":false}", 202);
            var t = New();
            t.Track("promo_shown", new Dictionary<string, object> { { "promoId", "p1" }, { "targetGameId", "kog" } });
            t.Flush();
            Assert.AreEqual(1, _http.Requests.Count);
            var r = _http.Requests[0];
            Assert.AreEqual("POST", r.Method);
            Assert.AreEqual("Bearer ingest-key", r.Headers["Authorization"]);
            Assert.AreEqual("application/json", r.Headers["Content-Type"]);
            var b = Body(r);
            Assert.IsTrue(Guid.TryParse((string)b["batch_id"], out _));
            var env = (Dictionary<string, object>)b["envelope"];
            Assert.AreEqual("gpking", env["game_id"]);
            Assert.AreEqual("inst-1", env["install_id"]);
            Assert.AreEqual("2026-12-01T10:00:00Z", env["queued_at"]);
            Assert.IsFalse(env.ContainsKey("build"));
            Assert.IsFalse(env.ContainsKey("device_model"), "no device model (decision 0016)");
            var ev = (Dictionary<string, object>)((List<object>)b["events"])[0];
            Assert.AreEqual("promo_shown", ev["name"]);
            Assert.AreEqual("2026-12-01T10:00:00.000Z", ev["ts"]);
            var props = (Dictionary<string, object>)ev["props"];
            Assert.AreEqual("p1", props["promoId"]);
            Assert.AreEqual("gpking", props["hostGameId"]);
            Assert.AreEqual(0, t.Queued);
        }

        [Test]
        public void OfflineQueueSurvivesRestartAndRetriesSameBatchId()
        {
            var t = New();
            t.Track("a", null);
            t.Track("b", null);
            t.Flush();
            Assert.AreEqual(1, _http.Requests.Count, "network error");
            Assert.AreEqual(2, t.Queued);
            var id1 = (string)Body(_http.Requests[0])["batch_id"];

            t.Flush();
            Assert.AreEqual(1, _http.Requests.Count, "backoff holds");

            // Restart, online now.
            _http.Text(Url, "{}", 202);
            var t2 = New();
            Assert.AreEqual(2, t2.Queued);
            t2.Track("c", null);
            t2.Flush();
            Assert.AreEqual(id1, (string)Body(_http.Requests[1])["batch_id"], "same batch id on retry");
            Assert.AreEqual(2, ((List<object>)Body(_http.Requests[1])["events"]).Count, "pending batch unchanged");
            Assert.AreEqual(3, _http.Requests.Count, "then the rest");
            Assert.AreEqual(0, t2.Queued);
        }

        [Test]
        public void ClientErrorDropsBatchServerErrorKeeps()
        {
            _http.Text(Url, "bad", 400);
            var t = New();
            t.Track("a", null);
            t.Flush();
            Assert.AreEqual(0, t.Queued);
            StringAssert.Contains("400", t.LastError);

            _http.Text(Url, "oops", 503);
            t.Track("b", null);
            t.Flush();
            Assert.AreEqual(1, t.Queued);

            _http.Text(Url, "slow down", 429);
            _now = _now.AddMinutes(1);
            t.Flush();
            Assert.AreEqual(1, t.Queued, "429 is retried, not dropped");
        }

        [Test]
        public void BackoffGrowsAndCaps()
        {
            Assert.AreEqual(30, ReleadTracker.Backoff(1).TotalSeconds);
            Assert.AreEqual(60, ReleadTracker.Backoff(2).TotalSeconds);
            Assert.AreEqual(3600, ReleadTracker.Backoff(20).TotalSeconds);
        }

        [Test]
        public void AutoFlushAndBatchLimit()
        {
            _http.Text(Url, "{}", 202);
            var t = New();
            for (int i = 0; i < ReleadTracker.AutoFlushAt - 1; i++) t.Track("e" + i, null);
            Assert.AreEqual(0, _http.Requests.Count);
            t.Track("last", null);
            Assert.AreEqual(1, _http.Requests.Count);

            _http.Defer = true;
            for (int i = 0; i < 250; i++) t.Track("x", null);
            _http.Defer = false;
            _http.Flush();
            foreach (var r in _http.Requests)
                Assert.LessOrEqual(((List<object>)Body(r)["events"]).Count, ReleadTracker.MaxBatchEvents);
        }

        [Test]
        public void NoKeyNoSend()
        {
            var t = new ReleadTracker(_http, Endpoint, "", new TrackerEnvelope { GameId = "g" }, Path.Combine(_dir, "q.jsonl"), () => _now, null);
            t.Track("a", null);
            t.Flush();
            Assert.AreEqual(0, _http.Requests.Count);
            Assert.AreEqual(1, t.Queued, "kept for when a key is configured");
        }

        [Test]
        public void QueueCapDropsOldest()
        {
            var q = new EventQueue(Path.Combine(_dir, "cap.jsonl"));
            for (int i = 0; i < EventQueue.MaxEvents + 5; i++) q.Add("{\"n\":" + i + "}");
            Assert.AreEqual(EventQueue.MaxEvents, q.Count);
            string id;
            Assert.AreEqual("{\"n\":5}", q.TakeBatch(1, 1000, out id)[0]);
        }

        [Test]
        public void CorruptQueueFileStartsEmpty()
        {
            var f = Path.Combine(_dir, "bad.jsonl");
            File.WriteAllText(f, "#batch x notanumber\n{\"a\":1}\n");
            var q = new EventQueue(f);
            Assert.AreEqual(1, q.Count);
            string id;
            Assert.AreEqual(1, q.TakeBatch(10, 1000, out id).Count);
        }
    }
}
