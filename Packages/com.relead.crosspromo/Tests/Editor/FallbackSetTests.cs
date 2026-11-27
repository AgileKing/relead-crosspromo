using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Relead.CrossPromo.Editor;
using UnityEditor;
using UnityEngine;

namespace Relead.CrossPromo.Tests
{
    public class FallbackSetTests
    {
        static FallbackGameSet Bundled() => AssetDatabase.LoadAssetAtPath<FallbackGameSet>(FallbackSetBuilder.AssetPath);

        [Test]
        public void BundledSetExistsAndIsLoadableFromResources()
        {
            Assert.NotNull(Bundled(), "run Relead > Cross-Promo > Rebuild Fallback Set");
            Assert.NotNull(Resources.Load<FallbackGameSet>(FallbackGameSet.ResourceName));
        }

        [Test]
        public void BundledSetMatchesManifest()
        {
            var manifest = FallbackSetBuilder.ReadManifest(File.ReadAllText(Path.GetFullPath(FallbackSetBuilder.ManifestPath)));
            var set = Bundled();
            Assert.AreEqual(manifest.Count, set.games.Length, "asset out of date: rebuild it");
            var ids = new HashSet<string>();
            for (int i = 0; i < manifest.Count; i++)
            {
                var g = set.games[i];
                Assert.AreEqual(manifest[i].id, g.id);
                Assert.IsTrue(ids.Add(g.id), "duplicate id " + g.id);
                Assert.AreEqual(manifest[i].androidPackage, g.androidPackage);
                Assert.AreEqual(manifest[i].iosAppId, g.iosAppId);
                Assert.That(g.ScreenshotCount, Is.InRange(1, 4), g.id);
                Assert.IsFalse(string.IsNullOrEmpty(g.androidPackage), g.id + " needs an Android package");
                foreach (var t in g.screenshots)
                {
                    Assert.LessOrEqual(Mathf.Max(t.width, t.height), 1080, g.id + " screenshot too big");
                    StringAssert.EndsWith(".jpg", AssetDatabase.GetAssetPath(t));
                }
            }
        }

        [Test]
        public void LiveCatalogParsesAndOfflineSetMatchesIt()
        {
            var r = CatalogParser.Parse(TestUtil.Fixture("promos.live.json"));
            Assert.IsTrue(r.Ok, r.Error);
            CollectionAssert.IsEmpty(r.Warnings);
            var c = r.Catalog;
            Assert.AreEqual(6, c.Games.Count);
            Assert.IsNull(c.FindGame("gpking").Store.Ios, "missing key = no store link");
            var kp = c.FindPromo("kingofgames-kp001");
            Assert.NotNull(kp);
            var ctx = new SelectionContext { HostGameId = "puzzleking", Platform = PromoPlatform.Android, HostOrientation = PromoOrientation.Landscape };
            ctx.Language = "nl"; ctx.ScreenAspect = 4f / 3f;
            var ipadNl = PromoSelector.ChooseMedia(kp, ctx, false);
            Assert.AreEqual("nl", ipadNl.Lang);
            Assert.AreEqual(960, ipadNl.Width, "live iPad cut");
            ctx.Language = "en"; ctx.ScreenAspect = 2.17f;
            var phoneEn = PromoSelector.ChooseMedia(kp, ctx, false);
            Assert.IsTrue(phoneEn.IsDefaultLanguage);
            Assert.AreEqual(1560, phoneEn.Width, "live wide-phone cut");
            Assert.AreSame(kp.Media[0], phoneEn, "first media = default-language phone cut (what 0.1.1 builds take)");

            foreach (var g in Bundled().games)
            {
                var cg = c.FindGame(g.id);
                Assert.NotNull(cg, g.id + " not in catalog");
                Assert.AreEqual(cg.UrlScheme, g.urlScheme, g.id);
                Assert.AreEqual(cg.Name, g.name, g.id);
                Assert.AreEqual(cg.Store.Android ?? "", g.androidPackage, g.id);
                Assert.AreEqual(cg.Store.Ios ?? "", g.iosAppId, g.id);
                Assert.AreEqual(cg.Store.Windows ?? "", g.windowsProductId, g.id);
            }
        }

        [Test]
        public void EachKnownGameNeverSeesItself()
        {
            var games = new List<FallbackGame>(Bundled().games);
            foreach (var host in games)
            {
                for (int seed = 0; seed < 5; seed++)
                {
                    var pick = FallbackSelector.Select(games, host.id, PromoPlatform.Android, null, 0, new System.Random(seed));
                    foreach (var g in pick) Assert.AreNotEqual(host.id, g.id);
                    Assert.AreEqual(games.Count - 1, pick.Count);
                }
            }
        }

        [Test]
        public void WrongHostIdStillExcludedByAppIdentifier()
        {
            var games = new List<FallbackGame>(Bundled().games);
            var pick = FallbackSelector.Select(games, "puzzle-typo", PromoPlatform.Android, null, 0, new System.Random(1), "com.relead.puzzle");
            foreach (var g in pick) Assert.AreNotEqual("puzzleking", g.id);
            Assert.AreEqual(games.Count - 1, pick.Count);
        }

        [Test]
        public void ManifestParserSkipsEntriesWithoutId()
        {
            var l = FallbackSetBuilder.ReadManifest("{\"games\":[{\"name\":\"x\"},{\"id\":\"a\",\"android\":\"com.a\"}]}");
            Assert.AreEqual(1, l.Count);
            Assert.AreEqual("com.a", l[0].androidPackage);
            Assert.AreEqual("a", l[0].name);
        }
    }

    public class HostAppIdentifierTests
    {
        [Test]
        public void CatalogPromoForOwnPackageIsSelf()
        {
            var json = @"{""schemaVersion"":1,""games"":[{""id"":""puzzleking"",""urlScheme"":""relead-g02"",""store"":{""android"":""com.relead.puzzle""}}],
              ""promos"":[{""id"":""p"",""gameId"":""puzzleking"",""weight"":1,""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""x.mp4""}]}]}";
            var c = CatalogParser.Parse(json).Catalog;
            var ctx = new SelectionContext { HostGameId = "wrong-id", HostAppIdentifier = "com.relead.puzzle", Platform = PromoPlatform.Android };
            Assert.AreEqual("self", PromoSelector.Reject(c, c.Promos[0], ctx));
            ctx.HostAppIdentifier = "com.relead.gp";
            Assert.IsNull(PromoSelector.Reject(c, c.Promos[0], ctx));
            ctx.HostAppIdentifier = null;
            Assert.IsNull(PromoSelector.Reject(c, c.Promos[0], ctx));
        }
    }
}
