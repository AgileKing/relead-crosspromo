using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace Relead.CrossPromo.Tests
{
    public class CatalogUpdaterTests
    {
        const string A = "https://ads.relead.com";
        const string B = "https://backup.example";
        string _dir;
        DateTime _now;
        FakeHttp _http;

        [SetUp]
        public void SetUp()
        {
            _dir = TestUtil.NewTempDir();
            _now = new DateTime(2026, 12, 1, 8, 0, 0, DateTimeKind.Utc);
            _http = new FakeHttp();
        }

        [TearDown] public void TearDown() { try { Directory.Delete(_dir, true); } catch (Exception) { } }

        CatalogUpdater New(params string[] urls) => new CatalogUpdater(_dir, _http, urls.Length > 0 ? urls : new[] { A, B }, () => _now);

        static string Url(string b) => b + "/catalog/promos.json";

        [Test]
        public void ChecksAtMostOnceADay()
        {
            _http.Text(Url(A), TestUtil.Fixture("promos.json"), 200, new Dictionary<string, string> { { "ETag", "\"v1\"" } });
            var u = New();
            u.LoadFromDisk();
            CatalogSource src = CatalogSource.None;
            u.CheckIfDue(s => src = s);
            Assert.AreEqual(CatalogSource.Network, src);
            Assert.AreEqual(1, _http.Count(Url(A)));

            // New launch, 23 h later: not due, served from disk without a request.
            _now = _now.AddHours(23);
            var u2 = New();
            Assert.IsTrue(u2.LoadFromDisk());
            Assert.IsFalse(u2.IsCheckDue());
            u2.CheckIfDue(s => src = s);
            Assert.AreEqual(CatalogSource.Disk, src);
            Assert.AreEqual(1, _http.Count(Url(A)));
            Assert.NotNull(u2.Catalog.FindPromo("kingofgames-2026-11"));

            // 25 h after the first check: due again, conditional GET, 304 keeps the disk copy.
            _now = _now.AddHours(2);
            _http.Text(Url(A), null, 304);
            var u3 = New();
            u3.LoadFromDisk();
            Assert.IsTrue(u3.IsCheckDue());
            u3.CheckIfDue(s => src = s);
            Assert.AreEqual(CatalogSource.NotModified, src);
            Assert.AreEqual("\"v1\"", _http.Requests[_http.Requests.Count - 1].Headers["If-None-Match"]);
            Assert.NotNull(u3.Catalog);
            var u4 = New();
            u4.LoadFromDisk();
            Assert.IsFalse(u4.IsCheckDue(), "304 counts as a check");
        }

        [Test]
        public void FailedCheckRetriesNextLaunchNotInLoop()
        {
            var u = New();
            u.LoadFromDisk();
            CatalogSource src = CatalogSource.Network;
            u.CheckIfDue(s => src = s);
            Assert.AreEqual(CatalogSource.None, src);
            Assert.AreEqual(1, _http.Count(Url(A)));
            Assert.AreEqual(1, _http.Count(Url(B)), "tries fallback base URL");
            Assert.IsFalse(u.IsCheckDue(), "not again this session");
            u.CheckIfDue(s => src = s);
            Assert.AreEqual(1, _http.Count(Url(A)));

            var next = New();
            next.LoadFromDisk();
            Assert.IsTrue(next.IsCheckDue(), "next launch retries");
        }

        [Test]
        public void FallsBackToSecondBaseUrlAndRemembersIt()
        {
            _http.Text(Url(A), "<html>blocked</html>", 200);
            _http.Text(Url(B), TestUtil.Fixture("promos.json"));
            var u = New();
            u.LoadFromDisk();
            CatalogSource src = CatalogSource.None;
            u.CheckIfDue(s => src = s);
            Assert.AreEqual(CatalogSource.Network, src);
            Assert.AreEqual(B, u.ActiveBaseUrl);

            _now = _now.AddDays(2);
            var u2 = New();
            u2.LoadFromDisk();
            Assert.AreEqual(B, u2.ActiveBaseUrl);
            int before = _http.Requests.Count;
            u2.CheckIfDue(s => src = s);
            Assert.AreEqual(Url(B), _http.Requests[before].Url, "last working host first");
        }

        [Test]
        public void BadNetworkCatalogKeepsLastGood()
        {
            _http.Text(Url(A), TestUtil.Fixture("promos.json"));
            var u = New(A);
            u.LoadFromDisk();
            u.CheckIfDue(null);
            _now = _now.AddDays(2);
            _http.Text(Url(A), "{\"schemaVersion\":9}");
            var u2 = New(A);
            u2.LoadFromDisk();
            CatalogSource src = CatalogSource.None;
            u2.CheckIfDue(s => src = s);
            Assert.AreEqual(CatalogSource.Disk, src);
            Assert.NotNull(u2.Catalog.FindPromo("kingofgames-2026-11"));
            StringAssert.Contains("schemaVersion", u2.LastError);
            Assert.IsTrue(New(A).LoadFromDisk(), "file on disk not overwritten");
        }

        [Test]
        public void ClockMovedBackIsDue()
        {
            _http.Text(Url(A), TestUtil.Fixture("promos.json"));
            var u = New();
            u.LoadFromDisk();
            u.CheckIfDue(null);
            _now = _now.AddDays(-3);
            var u2 = New();
            u2.LoadFromDisk();
            Assert.IsTrue(u2.IsCheckDue());
        }

        [Test]
        public void NoConditionalHeadersWithoutCatalog()
        {
            _http.Text(Url(A), null, 304);
            var u = New(A);
            u.CheckIfDue(null);
            Assert.IsFalse(_http.Requests[0].Headers.ContainsKey("If-None-Match"));
            Assert.IsNull(u.Catalog, "304 without a cached catalog is not success");
        }
    }

    public class MediaCacheTests
    {
        string _dir;
        [SetUp] public void SetUp() { _dir = TestUtil.NewTempDir(); }
        [TearDown] public void TearDown() { try { Directory.Delete(_dir, true); } catch (Exception) { } }

        static string Sha(byte[] b) { var h = SHA256.Create().ComputeHash(b); var sb = new StringBuilder(); foreach (var x in h) sb.Append(x.ToString("x2")); return sb.ToString(); }

        [Test]
        public void CommitVerifiesSizeAndSha()
        {
            var c = new MediaCache(_dir);
            var data = Encoding.UTF8.GetBytes("video-bytes");
            string err;

            var tmp = c.TempPathFor("a/v.mp4");
            File.WriteAllBytes(tmp, data);
            Assert.IsFalse(c.Commit(tmp, "a/v.mp4", data.Length + 1, null, out err));
            StringAssert.StartsWith("size", err);
            Assert.IsFalse(File.Exists(tmp));
            Assert.IsFalse(c.IsCached("a/v.mp4"));

            File.WriteAllBytes(tmp, data);
            Assert.IsFalse(c.Commit(tmp, "a/v.mp4", null, new string('0', 64), out err));
            Assert.AreEqual("sha256 mismatch", err);

            File.WriteAllBytes(tmp, data);
            Assert.IsTrue(c.Commit(tmp, "a/v.mp4", data.Length, Sha(data).ToUpperInvariant(), out err), err);
            Assert.IsTrue(c.IsCached("a/v.mp4"));
            Assert.IsTrue(c.PathFor("a/v.mp4").EndsWith(".mp4"));
            Assert.AreNotEqual(c.PathFor("a/v.mp4"), c.PathFor("b/v.mp4"));
        }

        [Test]
        public void EmptyFileNeverPasses()
        {
            var p = Path.Combine(_dir, "x");
            File.WriteAllBytes(p, new byte[0]);
            string err;
            Assert.IsFalse(MediaCache.Verify(p, null, null, out err));
        }

        [Test]
        public void EvictKeepsOnlyListed()
        {
            var c = new MediaCache(_dir);
            foreach (var u in new[] { "a.mp4", "b.mp4", "c.png" })
            {
                string err;
                File.WriteAllBytes(c.TempPathFor(u), new byte[] { 1, 2, 3 });
                Assert.IsTrue(c.Commit(c.TempPathFor(u), u, null, null, out err));
            }
            File.WriteAllBytes(c.TempPathFor("d.mp4"), new byte[] { 1 });
            Assert.AreEqual(10, c.TotalBytes());
            Assert.AreEqual(7, c.EvictExcept(new[] { "b.mp4" }), "a, c and the stale .part");
            Assert.IsTrue(c.IsCached("b.mp4"));
            Assert.IsFalse(c.IsCached("a.mp4"));
            Assert.IsFalse(c.IsCached("c.png"));
            Assert.AreEqual(3, c.TotalBytes());
        }

        [Test]
        public void UrlJoin()
        {
            Assert.AreEqual("https://x/a/b.mp4", UrlUtil.Join("https://x/", "/a/b.mp4"));
            Assert.AreEqual("https://x/a", UrlUtil.Join("https://x", "a"));
            Assert.AreEqual("https://cdn/z", UrlUtil.Join("https://x", "https://cdn/z"));
        }
    }

    public class PrefetchTests
    {
        const string Base = "https://ads.relead.com";
        string _dir;
        FakeHttp _http;
        MediaCache _cache;
        bool _wifi;
        MemStore _store;
        ShowHistory _history;

        [SetUp]
        public void SetUp()
        {
            _dir = TestUtil.NewTempDir();
            _http = new FakeHttp();
            _cache = new MediaCache(_dir);
            _wifi = true;
            _store = new MemStore();
            _history = new ShowHistory(_store);
        }

        [TearDown] public void TearDown() { try { Directory.Delete(_dir, true); } catch (Exception) { } }

        static Catalog Cat()
        {
            var sb = new StringBuilder(@"{""schemaVersion"":1,""games"":[");
            for (int i = 1; i <= 4; i++) sb.Append(i > 1 ? "," : "").Append(@"{""id"":""g" + i + @""",""urlScheme"":""relead-g0" + i + @""",""icon"":""g" + i + @"/icon.png"",""store"":{""android"":""com.r.g" + i + @"""}}");
            sb.Append(@"],""promos"":[");
            for (int i = 1; i <= 4; i++) sb.Append(i > 1 ? "," : "").Append(@"{""id"":""p" + i + @""",""gameId"":""g" + i + @""",""weight"":" + (10 - i) + @",""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""p" + i + @".mp4""}]}");
            sb.Append("]}");
            var r = CatalogParser.Parse(sb.ToString());
            Assert.IsTrue(r.Ok, r.Error);
            return r.Catalog;
        }

        SelectionContext Ctx() => new SelectionContext { HostGameId = "host", Platform = PromoPlatform.Android, NowUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc) };

        void ServeAll()
        {
            for (int i = 1; i <= 4; i++)
            {
                _http.Bytes(Base + "/p" + i + ".mp4", new byte[] { 1, 2, (byte)i });
                _http.Bytes(Base + "/g" + i + "/icon.png", new byte[] { 9 });
            }
        }

        List<PrefetchItem> Plan(Catalog c) => PrefetchPlanner.Plan(c, Ctx(), _history.LastShownUtc, PrefetchPlanner.DefaultSlots);

        static List<string> Ids(List<PrefetchItem> l) { var r = new List<string>(); foreach (var i in l) r.Add(i.Promo.Id); return r; }

        [Test]
        public void PlanTakesTwoSlotsNeverShownFirstThenWeight()
        {
            var c = Cat();
            CollectionAssert.AreEqual(new[] { "p1", "p2" }, Ids(Plan(c)));
            _history.RecordShown("p1", new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc));
            CollectionAssert.AreEqual(new[] { "p2", "p3" }, Ids(Plan(c)), "shown rotates out");
            _history.RecordShown("p2", new DateTime(2026, 11, 30, 1, 0, 0, DateTimeKind.Utc));
            _history.RecordShown("p3", new DateTime(2026, 11, 30, 2, 0, 0, DateTimeKind.Utc));
            _history.RecordShown("p4", new DateTime(2026, 11, 30, 3, 0, 0, DateTimeKind.Utc));
            CollectionAssert.AreEqual(new[] { "p1", "p2" }, Ids(Plan(c)), "all shown: least recently shown first");
        }

        [Test]
        public void WifiOnlyPrefetchFillsTwoSlotsAndRotates()
        {
            ServeAll();
            var c = Cat();
            var pf = new Prefetcher(_cache, _http, () => _wifi);

            _wifi = false;
            PrefetchOutcome outcome = PrefetchOutcome.Done;
            pf.Run(Plan(c), Base, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.NotOnWifi, outcome);
            Assert.AreEqual(0, _http.Requests.Count, "no download off wifi");

            _wifi = true;
            pf.Run(Plan(c), Base, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.Done, outcome);
            Assert.IsTrue(_cache.IsCached("p1.mp4"));
            Assert.IsTrue(_cache.IsCached("p2.mp4"));
            Assert.IsFalse(_cache.IsCached("p3.mp4"));
            Assert.IsTrue(_cache.IsCached("g1/icon.png"));

            // p1 shown → next pass fetches p3 and evicts p1.
            _history.RecordShown("p1", new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc));
            pf.Run(Plan(c), Base, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.Done, outcome);
            Assert.IsFalse(_cache.IsCached("p1.mp4"));
            Assert.IsFalse(_cache.IsCached("g1/icon.png"));
            Assert.IsTrue(_cache.IsCached("p2.mp4"));
            Assert.IsTrue(_cache.IsCached("p3.mp4"));

            int reqs = _http.Requests.Count;
            pf.Run(Plan(c), Base, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.NothingToDo, outcome);
            Assert.AreEqual(reqs, _http.Requests.Count);
        }

        [Test]
        public void NoEvictionWhenADownloadFails()
        {
            ServeAll();
            var c = Cat();
            var pf = new Prefetcher(_cache, _http, () => _wifi);
            pf.Run(Plan(c), Base, null);
            _history.RecordShown("p1", DateTime.UtcNow);
            _http.Routes.Remove(Base + "/p3.mp4");
            PrefetchOutcome outcome = PrefetchOutcome.Done;
            pf.Run(Plan(c), Base, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.PartlyFailed, outcome);
            Assert.IsTrue(_cache.IsCached("p1.mp4"), "keep something to show");
            StringAssert.Contains("p3.mp4", pf.LastError);
        }

        [Test]
        public void StopsWhenLeavingWifi()
        {
            ServeAll();
            var c = Cat();
            int calls = 0;
            var pf = new Prefetcher(_cache, _http, () => calls++ < 2);
            PrefetchOutcome outcome = PrefetchOutcome.Done;
            pf.Run(Plan(c), Base, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.PartlyFailed, outcome);
            Assert.AreEqual(1, _http.Requests.Count);
        }

        [Test]
        public void BusyWhileRunning()
        {
            ServeAll();
            _http.Defer = true;
            var pf = new Prefetcher(_cache, _http, () => true);
            pf.Run(Plan(Cat()), Base, null);
            PrefetchOutcome outcome = PrefetchOutcome.Done;
            pf.Run(Plan(Cat()), Base, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.Busy, outcome);
            _http.Defer = false;
            _http.Flush();
            Assert.IsFalse(pf.IsBusy);
        }

        [Test]
        public void OptionalIconFailureIsNotAFailure()
        {
            ServeAll();
            _http.Routes.Remove(Base + "/g1/icon.png");
            PrefetchOutcome outcome = PrefetchOutcome.PartlyFailed;
            new Prefetcher(_cache, _http, () => true).Run(Plan(Cat()), Base, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.Done, outcome);
        }
    }
}
