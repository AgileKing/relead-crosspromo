using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Relead.CrossPromo.Tests
{
    /// <summary>Review 2026-11-27 (docs/reviews): download trust, limits, playback failure, lifecycle, safe area.</summary>
    public class UrlSafetyTests
    {
        [Test]
        public void OnlyPlainRelativePathsAreSafe()
        {
            foreach (var ok in new[] { "games/kog/p1/portrait.mp4", "/a/b.mp4", "a-b_c.1.png", "catalog/promos.json" })
                Assert.IsTrue(UrlUtil.IsSafeRelative(ok), ok);
            foreach (var bad in new[] { null, "", "http://x/a.mp4", "https://evil/a.mp4", "//evil/a.mp4", "../a", "a/../b", "a/./b", "a//b",
                         "a\\b", "a%2e%2e/b", "a:b", "javascript:alert(1)", "a b.mp4", "a?x=1", "a#f", new string('a', 600) })
                Assert.IsFalse(UrlUtil.IsSafeRelative(bad), bad ?? "null");
        }

        [Test]
        public void ParserDropsAbsoluteMediaAndIconUrls()
        {
            var r = CatalogParser.Parse(@"{""schemaVersion"":1,
                ""games"":[{""id"":""g"",""icon"":""https://evil/icon.png""},{""id"":""h"",""icon"":""h/icon.png""}],
                ""promos"":[{""id"":""p"",""gameId"":""g"",""weight"":1,""media"":[
                    {""type"":""video"",""orientation"":""portrait"",""url"":""https://evil/x.mp4""},
                    {""type"":""video"",""orientation"":""portrait"",""url"":""../x.mp4""},
                    {""type"":""video"",""orientation"":""portrait"",""url"":""g/ok.mp4""}]}]}");
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsNull(r.Catalog.FindGame("g").Icon);
            Assert.AreEqual("h/icon.png", r.Catalog.FindGame("h").Icon);
            var media = r.Catalog.FindPromo("p").Media;
            Assert.AreEqual(1, media.Count);
            Assert.AreEqual("g/ok.mp4", media[0].Url);
        }

        [Test]
        public void LiveCatalogMeetsTheDownloadRules()
        {
            var r = CatalogParser.Parse(TestUtil.Fixture("promos.live.json"));
            Assert.IsTrue(r.Ok, r.Error);
            Assert.IsEmpty(r.Warnings, "no media dropped");
            foreach (var p in r.Catalog.Promos)
                foreach (var m in p.Media)
                    Assert.IsTrue(Prefetcher.CanDownload(m), p.Id + " " + m.Url);
        }

        [Test]
        public void HttpsOnlyBaseUrls()
        {
            Assert.IsTrue(UrlUtil.IsHttps("https://ads.relead.com"));
            Assert.IsFalse(UrlUtil.IsHttps("http://ads.relead.com"));
            Assert.IsFalse(UrlUtil.IsHttps("file:///sdcard"));
            Assert.IsFalse(UrlUtil.IsHttps(null));
        }
    }

    public class DownloadLimitTests
    {
        const string A = "https://ads.relead.com";
        string _dir;
        FakeHttp _http;

        [SetUp] public void SetUp() { _dir = TestUtil.NewTempDir(); _http = new FakeHttp(); }
        [TearDown] public void TearDown() { try { Directory.Delete(_dir, true); } catch (Exception) { } }

        static string CatUrl(string b) => b + "/catalog/promos.json";

        [Test]
        public void PlainHttpBaseUrlIsNeverContacted()
        {
            _http.Text(CatUrl(A), TestUtil.Fixture("promos.live.json"));
            var u = new CatalogUpdater(_dir, _http, new[] { "http://ads.relead.com", A }, () => DateTime.UtcNow);
            CatalogSource src = CatalogSource.None;
            u.CheckIfDue(s => src = s);
            Assert.AreEqual(CatalogSource.Network, src);
            Assert.AreEqual(0, _http.Count(CatUrl("http://ads.relead.com")));
        }

        [Test]
        public void OversizedCatalogIsRejected()
        {
            var big = TestUtil.Fixture("promos.live.json").Replace("{", "{" + new string(' ', CatalogUpdater.MaxCatalogBytes / 10));
            Assert.Greater(Encoding.UTF8.GetByteCount(big), CatalogUpdater.MaxCatalogBytes);
            _http.Text(CatUrl(A), big);
            var u = new CatalogUpdater(_dir, _http, new[] { A }, () => DateTime.UtcNow);
            u.CheckIfDue(null);
            Assert.AreEqual(CatalogUpdater.MaxCatalogBytes, _http.Requests[0].MaxBytes);
            Assert.IsNull(u.Catalog);
            StringAssert.Contains(HttpResponse.TooLargeError, u.LastError);
        }

        static Catalog Cat(string media1, string media2)
        {
            var r = CatalogParser.Parse(@"{""schemaVersion"":1,
                ""games"":[{""id"":""g1"",""icon"":""g1/icon.png"",""store"":{""android"":""com.r.g1""}},{""id"":""g2"",""store"":{""android"":""com.r.g2""}}],
                ""promos"":[{""id"":""p1"",""gameId"":""g1"",""weight"":9,""media"":[" + media1 + @"]},
                           {""id"":""p2"",""gameId"":""g2"",""weight"":1,""media"":[" + media2 + @"]}]}");
            Assert.IsTrue(r.Ok, r.Error);
            return r.Catalog;
        }

        static string Media(string url, long? bytes, string sha)
            => @"{""type"":""video"",""orientation"":""portrait"",""url"":""" + url + @""""
               + (bytes.HasValue ? @",""bytes"":" + bytes.Value : "") + (sha != null ? @",""sha256"":""" + sha + @"""" : "") + "}";

        static SelectionContext Ctx() => new SelectionContext { HostGameId = "host", Platform = PromoPlatform.Android, NowUtc = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc) };

        [Test]
        public void MediaWithoutSizeAndHashIsNeverPlanned()
        {
            var sha = MediaCacheTests.Sha(new byte[] { 1 });
            Assert.AreEqual(1, PrefetchPlanner.Plan(Cat(Media("a.mp4", null, sha), Media("b.mp4", 1, sha)), Ctx(), null, 2).Count, "no bytes");
            Assert.AreEqual(1, PrefetchPlanner.Plan(Cat(Media("a.mp4", 1, null), Media("b.mp4", 1, sha)), Ctx(), null, 2).Count, "no sha256");
            var plan = PrefetchPlanner.Plan(Cat(Media("a.mp4", Prefetcher.MaxMediaBytes + 1, sha), Media("b.mp4", 1, sha)), Ctx(), null, 2);
            Assert.AreEqual(1, plan.Count, "too big");
            Assert.AreEqual("p2", plan[0].Promo.Id);
        }

        [Test]
        public void DownloadsAreCappedAtTheExpectedSize()
        {
            var video = new byte[] { 1, 2, 3 };
            var cache = new MediaCache(_dir);
            var c = Cat(Media("a.mp4", 3, MediaCacheTests.Sha(video)), Media("b.mp4", 2, MediaCacheTests.Sha(new byte[] { 1, 2 })));
            _http.Bytes(A + "/a.mp4", video);
            _http.Bytes(A + "/b.mp4", video); // server sends more than the catalog says
            _http.Bytes(A + "/g1/icon.png", new byte[Prefetcher.MaxIconBytes + 1]);
            PrefetchOutcome outcome = PrefetchOutcome.Done;
            new Prefetcher(cache, _http, () => true).Run(PrefetchPlanner.Plan(c, Ctx(), null, 2), A, o => outcome = o);
            Assert.AreEqual(PrefetchOutcome.PartlyFailed, outcome);
            Assert.IsTrue(cache.IsCached(c.FindPromo("p1").Media[0]));
            Assert.IsFalse(cache.IsCached(c.FindPromo("p2").Media[0]));
            Assert.IsFalse(cache.IsCached("g1/icon.png"), "icon over the cap");
            foreach (var r in _http.Requests)
                Assert.AreEqual(r.Url.EndsWith("icon.png") ? Prefetcher.MaxIconBytes : (r.Url.EndsWith("a.mp4") ? 3 : 2), r.MaxBytes, r.Url);
        }

        [Test]
        public void TruncatedOrDeletedCacheFileIsNotCached()
        {
            var cache = new MediaCache(_dir);
            var m = new PromoMedia { Url = "a.mp4", Bytes = 3 };
            string err;
            File.WriteAllBytes(cache.TempPathFor("a.mp4"), new byte[] { 1, 2, 3 });
            Assert.IsTrue(cache.Commit(cache.TempPathFor("a.mp4"), "a.mp4", 3, null, out err), err);
            Assert.IsTrue(cache.IsCached(m));
            File.WriteAllBytes(cache.PathFor("a.mp4"), new byte[] { 1, 2 });
            Assert.IsFalse(cache.IsCached(m), "size no longer matches");
            cache.Delete("a.mp4");
            Assert.IsFalse(File.Exists(cache.PathFor("a.mp4")));
            cache.Delete("never-there.mp4");
        }

        [Test]
        public void AtomicWriteReplacesAndLeavesNoTemp()
        {
            var p = Path.Combine(_dir, "state.json");
            AtomicFile.WriteAllText(p, "one");
            AtomicFile.WriteAllText(p, "two");
            Assert.AreEqual("two", File.ReadAllText(p));
            Assert.AreEqual(1, Directory.GetFiles(_dir).Length);
        }
    }

    public class ImageGuardTests
    {
        [Test]
        public void ReadsPngAndJpegSizeWithoutDecoding()
        {
            var t = new Texture2D(3, 5, TextureFormat.RGBA32, false);
            try
            {
                int w, h;
                Assert.IsTrue(ImageGuard.TryGetSize(t.EncodeToPNG(), out w, out h));
                Assert.AreEqual(3, w); Assert.AreEqual(5, h);
                Assert.IsTrue(ImageGuard.TryGetSize(t.EncodeToJPG(), out w, out h));
                Assert.AreEqual(3, w); Assert.AreEqual(5, h);
                Assert.IsFalse(ImageGuard.TryGetSize(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, out w, out h));
                Assert.IsFalse(ImageGuard.TryGetSize(null, out w, out h));
                Assert.IsTrue(ImageGuard.IsAcceptableIcon(t.EncodeToPNG()));
            }
            finally { UnityEngine.Object.DestroyImmediate(t); }
        }

        [Test]
        public void RejectsHugeDimensions()
        {
            // PNG header claiming 20000 x 20000 (decompression bomb): rejected before LoadImage.
            var b = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0x4E, 0x20, 0, 0, 0x4E, 0x20, 8, 6, 0, 0, 0 };
            int w, h;
            Assert.IsTrue(ImageGuard.TryGetSize(b, out w, out h));
            Assert.AreEqual(20000, w);
            Assert.IsFalse(ImageGuard.IsAcceptableIcon(b));
        }
    }

    public class LifecycleTests
    {
        [Test]
        public void CapIgnoresClockMovedBack()
        {
            var cap = new DailyCap(new MemStore());
            var day2 = new DateTime(2026, 12, 2, 10, 0, 0, DateTimeKind.Local);
            var day1 = day2.AddDays(-1);
            cap.RecordResult(PromoOutcome.Completed, day2);
            Assert.AreEqual(1, cap.ShownToday(day1), "date set back: keeps the later day's count");
            cap.RecordResult(PromoOutcome.Completed, day1);
            Assert.AreEqual(2, cap.ShownToday(day2));
            Assert.AreEqual(0, cap.ShownToday(day2.AddDays(1)), "a real new day still resets");
        }

        [Test]
        public void StallWatchFiresOnlyWhenFramesStop()
        {
            var s = new StallWatch(5f);
            Assert.IsFalse(s.Tick(1f, -1));
            Assert.IsFalse(s.Tick(4f, -1), "4 s waiting for the first frame");
            Assert.IsFalse(s.Tick(0.5f, 0), "first frame resets");
            for (int f = 1; f < 100; f++) Assert.IsFalse(s.Tick(0.1f, f));
            Assert.IsFalse(s.Tick(0.1f, 0), "loop back to frame 0 is progress");
            Assert.IsFalse(s.Tick(4.8f, 0));
            Assert.IsTrue(s.Tick(0.3f, 0), "5 s on one frame");
            s.Reset();
            Assert.IsFalse(s.Tick(4.9f, 0), "reset after resume");
        }

        [Test]
        public void GatePausesOnFocusLossOrPause()
        {
            var g = new PlaybackGate();
            Assert.IsTrue(g.Active);
            Assert.AreEqual(PlaybackGate.Change.Paused, g.SetFocus(false));
            Assert.AreEqual(PlaybackGate.Change.None, g.SetPaused(true));
            Assert.AreEqual(PlaybackGate.Change.None, g.SetFocus(true), "still paused");
            Assert.IsFalse(g.Active);
            Assert.AreEqual(PlaybackGate.Change.Resumed, g.SetPaused(false));
            Assert.IsTrue(g.Active);
            Assert.AreEqual(PlaybackGate.Change.None, g.SetFocus(true));
        }

        [Test]
        public void FailedVideoFallsBackToSlideshow()
        {
            Assert.IsTrue(CrossPromo.FallBackAfter(new PromoResult { Outcome = PromoOutcome.Failed, FailReason = "video: decode" }, false));
            Assert.IsTrue(CrossPromo.FallBackAfter(new PromoResult { Outcome = PromoOutcome.Failed, FailReason = "stalled" }, false));
            Assert.IsFalse(CrossPromo.FallBackAfter(new PromoResult { Outcome = PromoOutcome.Failed, FailReason = "destroyed" }, false), "scene change");
            Assert.IsFalse(CrossPromo.FallBackAfter(new PromoResult { Outcome = PromoOutcome.Failed, FailReason = "x" }, true), "slideshow itself failed");
            Assert.IsFalse(CrossPromo.FallBackAfter(new PromoResult { Outcome = PromoOutcome.Skipped }, false));
            Assert.IsFalse(CrossPromo.FallBackAfter(new PromoResult { Outcome = PromoOutcome.Completed }, false));
        }

        [Test]
        public void OnceRunsOnceEvenWhenTheCallbackThrows()
        {
            int n = 0;
            var once = CrossPromo.Once<int>(x => { n += x; throw new InvalidOperationException("host bug"); });
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                once(1);
                once(1);
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
            Assert.AreEqual(1, n);
        }
    }

    public class SafeAreaTests
    {
        // Screen insets left, right, top, bottom (canvas units): notch left (landscape phone), home bar bottom.
        static readonly Vector4 Screen = new Vector4(100, 20, 0, 40);

        [Test]
        public void UprightKeepsScreenInsets()
        {
            Assert.AreEqual(Screen, OrientationLogic.SafeInsets(Screen, 0f));
            Assert.AreEqual(Screen, OrientationLogic.SafeInsets(Screen, 360f));
        }

        [Test]
        public void RotatedStageMapsInsetsToItsOwnSides()
        {
            // z = +90 (counter-clockwise): stage top faces screen left, stage left faces screen bottom.
            Assert.AreEqual(new Vector4(40, 0, 100, 20), OrientationLogic.SafeInsets(Screen, 90f));
            // z = -90: stage top faces screen right, stage left faces screen top.
            Assert.AreEqual(new Vector4(0, 40, 20, 100), OrientationLogic.SafeInsets(Screen, -90f));
            Assert.AreEqual(new Vector4(0, 40, 20, 100), OrientationLogic.SafeInsets(Screen, 270f));
            Assert.AreEqual(new Vector4(20, 100, 40, 0), OrientationLogic.SafeInsets(Screen, 180f));
        }

        [Test]
        public void HostShapeChangeMidAdReevaluatesTheStage()
        {
            Assert.AreEqual(StageMode.Upright, OrientationLogic.AfterHostChange(PromoOrientation.Landscape, PromoOrientation.Landscape));
            Assert.AreEqual(StageMode.Letterbox, OrientationLogic.AfterHostChange(PromoOrientation.Portrait, PromoOrientation.Landscape), "no second hint");
            // From there the normal rules apply: device already turned → rotated.
            Assert.AreEqual(StageMode.Rotated, OrientationLogic.Next(StageMode.Letterbox, PromoOrientation.Portrait, PromoOrientation.Landscape,
                DeviceOrientation.LandscapeLeft, 5f, 3f, OrientationLogic.MatchStableSec));
        }
    }
}
