using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Relead.CrossPromo.Tests
{
    public class SelectionTests
    {
        static readonly DateTime Now = new DateTime(2026, 12, 10, 12, 0, 0, DateTimeKind.Utc);

        static Catalog Build()
        {
            var json = @"{""schemaVersion"":1,
              ""games"":[
                {""id"":""host"",""name"":""Host"",""urlScheme"":""relead-g01"",""store"":{""android"":""com.r.host"",""ios"":""1""}},
                {""id"":""a"",""name"":""A"",""urlScheme"":""relead-g02"",""store"":{""android"":""com.r.a"",""ios"":""2""}},
                {""id"":""b"",""name"":""B"",""urlScheme"":""relead-g03"",""store"":{""android"":""com.r.b""}},
                {""id"":""c"",""name"":""C"",""urlScheme"":""relead-g04"",""store"":{""android"":""com.r.c"",""ios"":""4""}}],
              ""promos"":[
                {""id"":""self"",""gameId"":""host"",""weight"":5,""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""s.mp4""}]},
                {""id"":""a1"",""gameId"":""a"",""weight"":1,""media"":[{""type"":""video"",""orientation"":""landscape"",""url"":""a1l.mp4""},{""type"":""video"",""orientation"":""portrait"",""url"":""a1p.mp4""}]},
                {""id"":""b1"",""gameId"":""b"",""weight"":3,""media"":[{""type"":""video"",""orientation"":""landscape"",""url"":""b1l.mp4""}]},
                {""id"":""c-future"",""gameId"":""c"",""weight"":1,""start"":""2027-01-01T00:00:00Z"",""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""cf.mp4""}]},
                {""id"":""c-past"",""gameId"":""c"",""weight"":1,""end"":""2026-12-10T12:00:00Z"",""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""cp.mp4""}]},
                {""id"":""c-newpkg"",""gameId"":""c"",""weight"":1,""minPackageVersion"":""0.2.0"",""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""cn.mp4""}]},
                {""id"":""c-playable"",""gameId"":""c"",""weight"":1,""media"":[{""type"":""playable"",""orientation"":""portrait"",""url"":""cpl/index.html""}]},
                {""id"":""c-ios"",""gameId"":""c"",""weight"":1,""platforms"":[""ios""],""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""ci.mp4""}]},
                {""id"":""c-zero"",""gameId"":""c"",""weight"":0,""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""cz.mp4""}]},
                {""id"":""ghost"",""gameId"":""nope"",""weight"":1,""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""g.mp4""}]}]}";
            var r = CatalogParser.Parse(json);
            Assert.IsTrue(r.Ok, r.Error);
            return r.Catalog;
        }

        static SelectionContext Ctx(PromoPlatform platform = PromoPlatform.Android) => new SelectionContext
        {
            HostGameId = "host",
            Platform = platform,
            PackageVersion = "0.1.0",
            NowUtc = Now,
            HostOrientation = PromoOrientation.Portrait,
        };

        static List<string> Ids(IEnumerable<Promo> ps) { var l = new List<string>(); foreach (var p in ps) l.Add(p.Id); return l; }

        [Test]
        public void FiltersSelfDatesVersionTypesPlatformsWeightUnknownGame()
        {
            CollectionAssert.AreEquivalent(new[] { "a1", "b1" }, Ids(PromoSelector.Eligible(Build(), Ctx())));
        }

        [Test]
        public void RejectReasons()
        {
            var c = Build();
            var ctx = Ctx();
            Assert.AreEqual("self", PromoSelector.Reject(c, c.FindPromo("self"), ctx));
            Assert.AreEqual("not started", PromoSelector.Reject(c, c.FindPromo("c-future"), ctx));
            Assert.AreEqual("ended", PromoSelector.Reject(c, c.FindPromo("c-past"), ctx), "end is exclusive");
            StringAssert.StartsWith("needs package", PromoSelector.Reject(c, c.FindPromo("c-newpkg"), ctx));
            Assert.AreEqual("no playable media", PromoSelector.Reject(c, c.FindPromo("c-playable"), ctx));
            Assert.AreEqual("platform", PromoSelector.Reject(c, c.FindPromo("c-ios"), ctx));
            Assert.AreEqual("weight 0", PromoSelector.Reject(c, c.FindPromo("c-zero"), ctx));
            Assert.AreEqual("unknown game", PromoSelector.Reject(c, c.FindPromo("ghost"), ctx));
        }

        [Test]
        public void NewerPackageAndPlayableSupportUnlockPromos()
        {
            var ctx = Ctx();
            ctx.PackageVersion = "0.2.0";
            ctx.PlayableTypes = new[] { "video", "playable" };
            CollectionAssert.AreEquivalent(new[] { "a1", "b1", "c-newpkg", "c-playable" }, Ids(PromoSelector.Eligible(Build(), ctx)));
        }

        [Test]
        public void IosPlatformNeedsStoreId()
        {
            var c = Build();
            var ctx = Ctx(PromoPlatform.IOS);
            Assert.AreEqual("no store id", PromoSelector.Reject(c, c.FindPromo("b1"), ctx));
            CollectionAssert.AreEquivalent(new[] { "a1", "c-ios" }, Ids(PromoSelector.Eligible(c, ctx)));
            ctx.RequireStoreLink = false;
            CollectionAssert.Contains(Ids(PromoSelector.Eligible(c, ctx)), "b1");
        }

        [Test]
        public void InstalledGamesAreFiltered()
        {
            var ctx = Ctx();
            ctx.IsInstalled = g => g.Id == "b";
            CollectionAssert.AreEquivalent(new[] { "a1" }, Ids(PromoSelector.Eligible(Build(), ctx)));
        }

        [Test]
        public void OrientationPrefersHostThenOther()
        {
            var c = Build();
            var ctx = Ctx();
            Assert.AreEqual("a1p.mp4", PromoSelector.ChooseMedia(c.FindPromo("a1"), ctx, false).Url);
            ctx.HostOrientation = PromoOrientation.Landscape;
            Assert.AreEqual("a1l.mp4", PromoSelector.ChooseMedia(c.FindPromo("a1"), ctx, false).Url);
            ctx.HostOrientation = PromoOrientation.Portrait;
            Assert.AreEqual("b1l.mp4", PromoSelector.ChooseMedia(c.FindPromo("b1"), ctx, false).Url, "other orientation if no match");
        }

        [Test]
        public void CachedOtherOrientationBeatsUncachedMatch()
        {
            var c = Build();
            var ctx = Ctx();
            ctx.IsMediaCached = m => m.Url == "a1l.mp4";
            var choice = PromoSelector.Select(c, ctx, 0.0);
            Assert.AreEqual("a1", choice.Promo.Id);
            Assert.AreEqual("a1l.mp4", choice.Media.Url);
            Assert.IsTrue(choice.NeedsRotation);
            Assert.AreEqual("a", choice.Game.Id);
        }

        [Test]
        public void SelectNeedsCachedMedia()
        {
            var ctx = Ctx();
            ctx.IsMediaCached = m => false;
            Assert.IsNull(PromoSelector.Select(Build(), ctx, 0.5));
        }

        [Test]
        public void WeightedPickBoundaries()
        {
            var ps = new List<Promo> { new Promo { Id = "x", Weight = 1 }, new Promo { Id = "z", Weight = 0 }, new Promo { Id = "y", Weight = 3 } };
            Assert.AreEqual("x", PromoSelector.PickWeighted(ps, 0.0).Id);
            Assert.AreEqual("x", PromoSelector.PickWeighted(ps, 0.2499).Id);
            Assert.AreEqual("y", PromoSelector.PickWeighted(ps, 0.25).Id);
            Assert.AreEqual("y", PromoSelector.PickWeighted(ps, 0.9999999).Id);
            Assert.AreEqual("y", PromoSelector.PickWeighted(ps, 1.0).Id);
            Assert.IsNull(PromoSelector.PickWeighted(new List<Promo>(), 0.5));
            Assert.IsNull(PromoSelector.PickWeighted(new List<Promo> { new Promo { Weight = 0 } }, 0.5));
        }

        [Test]
        public void WeightedPickDistribution()
        {
            var ps = new List<Promo> { new Promo { Id = "x", Weight = 1 }, new Promo { Id = "y", Weight = 3 } };
            var r = new System.Random(7);
            int y = 0;
            for (int i = 0; i < 4000; i++) if (PromoSelector.PickWeighted(ps, r.NextDouble()).Id == "y") y++;
            Assert.That(y / 4000.0, Is.InRange(0.72, 0.78));
        }

        [Test]
        public void AvoidsLastShownWhenThereIsAChoice()
        {
            var ctx = Ctx();
            ctx.LastShownPromoId = "b1";
            for (int i = 0; i < 10; i++) Assert.AreEqual("a1", PromoSelector.Select(Build(), ctx, i / 10.0).Promo.Id);
            ctx.IsInstalled = g => g.Id == "a";
            Assert.AreEqual("b1", PromoSelector.Select(Build(), ctx, 0.5).Promo.Id, "only choice may repeat");
        }

        [TestCase("0.1.0", "0.1.0", 0)]
        [TestCase("0.1.10", "0.1.9", 1)]
        [TestCase("1.0.0", "0.99.99", 1)]
        [TestCase("v0.2", "0.2.0", 0)]
        [TestCase("0.2.0-beta", "0.2.0", 0)]
        [TestCase("garbage", "0.0.0", 0)]
        public void SemVerCompare(string a, string b, int sign)
        {
            Assert.AreEqual(sign, Math.Sign(SemVer.Compare(a, b)));
        }

        [Test]
        public void PackageVersionConstantMatchesPackageJson()
        {
            var json = File.ReadAllText(Path.GetFullPath("Packages/com.relead.crosspromo/package.json"));
            StringAssert.Contains("\"version\": \"" + CrossPromoInfo.PackageVersion + "\"", json);
        }
    }

    public class DailyCapTests
    {
        [Test]
        public void CountsPerLocalDayAndResets()
        {
            var store = new MemStore();
            var cap = new DailyCap(store);
            var day1 = new DateTime(2026, 12, 1, 23, 50, 0, DateTimeKind.Local);
            Assert.IsTrue(cap.CanShow(day1, 2));
            cap.RecordShown(day1);
            cap.RecordShown(day1);
            Assert.IsFalse(cap.CanShow(day1, 2));
            Assert.AreEqual(0, cap.Remaining(day1, 2));
            Assert.IsTrue(cap.CanShow(day1, 3));
            var day2 = day1.AddMinutes(15);
            Assert.AreEqual(0, cap.ShownToday(day2));
            Assert.IsTrue(cap.CanShow(day2, 2));
            Assert.AreEqual(2, new DailyCap(store).ShownToday(day1), "persists");
        }

        [Test]
        public void ZeroMeansOff()
        {
            Assert.IsFalse(new DailyCap(new MemStore()).CanShow(DateTime.Now, 0));
        }

        [Test]
        public void CorruptValueCountsAsZero()
        {
            var s = new MemStore();
            s.Set(DailyCap.Key, "garbage");
            Assert.AreEqual(0, new DailyCap(s).ShownToday(DateTime.Now));
        }
    }

    public class FallbackSelectorTests
    {
        static Texture2D _tex;

        [OneTimeSetUp] public void Setup() { _tex = new Texture2D(2, 2); }
        [OneTimeTearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_tex); }

        FallbackGame G(string id, bool shots = true, string android = "com.r.x") => new FallbackGame
        {
            id = id, name = id.ToUpperInvariant(), androidPackage = android,
            screenshots = shots ? new[] { _tex } : new Texture2D[0],
        };

        static List<string> Ids(List<FallbackGame> l) { var r = new List<string>(); foreach (var g in l) r.Add(g.id); return r; }

        [Test]
        public void NothingCachedFallbackSkipsHostInstalledAndNoScreenshots()
        {
            var games = new List<FallbackGame> { G("host"), G("a"), G("b", shots: false), G("c"), G("d", android: "") };
            var pick = FallbackSelector.Select(games, "host", PromoPlatform.Android, g => g.Id == "c", 0, new System.Random(1));
            CollectionAssert.AreEquivalent(new[] { "a" }, Ids(pick));
        }

        [Test]
        public void FallsBackToInstalledRatherThanNothing()
        {
            var games = new List<FallbackGame> { G("host"), G("a"), G("c") };
            var pick = FallbackSelector.Select(games, "host", PromoPlatform.Android, g => true, 0, new System.Random(1));
            CollectionAssert.AreEquivalent(new[] { "a", "c" }, Ids(pick));
        }

        [Test]
        public void EmptyWhenOnlyHostOrNoScreenshots()
        {
            var games = new List<FallbackGame> { G("host"), G("b", shots: false), null };
            Assert.IsEmpty(FallbackSelector.Select(games, "host", PromoPlatform.Android, null, 3, new System.Random(1)));
            Assert.IsEmpty(FallbackSelector.Select(null, "host", PromoPlatform.Android, null, 3, null));
        }

        [Test]
        public void CapsAndShuffles()
        {
            var games = new List<FallbackGame>();
            for (int i = 0; i < 8; i++) games.Add(G("g" + i));
            Assert.AreEqual(3, FallbackSelector.Select(games, "host", PromoPlatform.Android, null, 3, new System.Random(1)).Count);
            var seen = new HashSet<string>();
            for (int s = 0; s < 20; s++) seen.Add(FallbackSelector.Select(games, "host", PromoPlatform.Android, null, 1, new System.Random(s))[0].id);
            Assert.That(seen.Count, Is.GreaterThan(2));
        }
    }
}
