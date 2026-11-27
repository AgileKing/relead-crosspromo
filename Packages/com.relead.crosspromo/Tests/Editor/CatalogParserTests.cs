using System;
using System.Collections.Generic;
using NUnit.Framework;
using Relead.CrossPromo.Json;

namespace Relead.CrossPromo.Tests
{
    public class CatalogParserTests
    {
        [Test]
        public void ParsesSampleFixture()
        {
            var r = CatalogParser.Parse(TestUtil.Fixture("promos.json"));
            Assert.IsTrue(r.Ok, r.Error);
            var c = r.Catalog;
            Assert.AreEqual(1, c.SchemaVersion);
            Assert.AreEqual(new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc), c.Updated);
            Assert.AreEqual(3, c.Rules.MaxPerDay);
            Assert.AreEqual(5f, c.Rules.SkipAfterSec);
            Assert.AreEqual(3f, c.Rules.RotateHintTimeoutSec);

            var g = c.FindGame("kingofgames");
            Assert.NotNull(g);
            Assert.AreEqual("King of Games", g.Name);
            Assert.AreEqual("relead-g01", g.UrlScheme);
            Assert.AreEqual(PromoOrientation.Landscape, g.Orientation);
            Assert.IsNull(g.Store.Android, "TODO placeholder must count as missing");

            var p = c.FindPromo("kingofgames-2026-11");
            Assert.NotNull(p);
            Assert.AreEqual("kingofgames", p.GameId);
            Assert.AreEqual(10, p.Weight);
            Assert.IsNull(p.Start);
            Assert.IsNull(p.End);
            Assert.IsNull(p.Platforms);
            Assert.AreEqual("Gratis spelen", p.Cta.Get("nl-NL"));
            Assert.AreEqual("Play free", p.Cta.Get("de"));
            Assert.AreEqual(1, p.Media.Count);
            Assert.AreEqual(PromoMedia.TypeVideo, p.Media[0].Type);
            Assert.AreEqual(PromoOrientation.Landscape, p.Media[0].Orientation);
            Assert.AreEqual(20, p.Media[0].DurationSec);
        }

        [Test]
        public void IgnoresUnknownFieldsAndKeepsUnknownMediaTypes()
        {
            var json = @"{""schemaVersion"":1,""future"":{""x"":[1,2]},""rules"":{""maxPerDay"":2,""newRule"":true},
                ""games"":[{""id"":""a"",""name"":""A"",""urlScheme"":""relead-g02"",""screenshots"":[""a/1.webp""],""store"":{""ios"":""123"",""tv"":""x""},""extra"":null}],
                ""promos"":[{""id"":""p"",""gameId"":""a"",""weight"":1,""whatever"":[{}],
                  ""media"":[{""type"":""hologram"",""orientation"":""portrait"",""url"":""a/b.holo""},
                             {""type"":""video"",""orientation"":""landscape"",""url"":""a/b.mp4"",""bytes"":1234,""sha256"":""ABCDEF""}]}]}";
            var r = CatalogParser.Parse(json);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(2, r.Catalog.Rules.MaxPerDay);
            Assert.AreEqual("123", r.Catalog.Games[0].Store.Ios);
            var p = r.Catalog.Promos[0];
            Assert.AreEqual(2, p.Media.Count);
            Assert.AreEqual("hologram", p.Media[0].Type);
            Assert.AreEqual(1234, p.Media[1].Bytes);
            Assert.AreEqual("abcdef", p.Media[1].Sha256);
        }

        [Test]
        public void DropsInvalidEntriesWithoutFailing()
        {
            var json = @"{""schemaVersion"":1,""games"":[{""name"":""no id""},5,{""id"":""ok""}],
                ""promos"":[
                  {""id"":""no-media"",""gameId"":""ok"",""weight"":1},
                  {""id"":""bad-media"",""gameId"":""ok"",""weight"":1,""media"":[{""type"":""video"",""orientation"":""diagonal"",""url"":""x""}]},
                  {""id"":""good"",""gameId"":""ok"",""weight"":""heavy"",""media"":[]},
                  {""id"":""dup"",""gameId"":""ok"",""weight"":1,""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""x""}]},
                  {""id"":""dup"",""gameId"":""ok"",""weight"":1,""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""y""}]},
                  ""string""]}";
            var r = CatalogParser.Parse(json);
            Assert.IsTrue(r.Ok, r.Error);
            Assert.AreEqual(1, r.Catalog.Games.Count);
            Assert.AreEqual(1, r.Catalog.Promos.Count);
            Assert.AreEqual("x", r.Catalog.Promos[0].Media[0].Url);
            Assert.That(r.Warnings.Count, Is.GreaterThanOrEqualTo(6));
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[1,2]")]
        [TestCase(@"{""schemaVersion"":2,""promos"":[]}")]
        [TestCase(@"{""promos"":[]}")]
        [TestCase(@"{""schemaVersion"":1}")]
        [TestCase(@"{""schemaVersion"":1,""promos"":[")]
        public void FailsCleanlyOnBadRoot(string json)
        {
            var r = CatalogParser.Parse(json);
            Assert.IsFalse(r.Ok);
            Assert.IsNotEmpty(r.Error);
        }

        [Test]
        public void ParsesPlatformsAndDates()
        {
            var json = @"{""schemaVersion"":1,""promos"":[{""id"":""p"",""gameId"":""g"",""weight"":2.5,
                ""start"":""2026-12-01T00:00:00Z"",""end"":""2026-12-31T12:00:00+01:00"",""minPackageVersion"":""0.3.0"",
                ""platforms"":[""ios"",""webgl"",""android""],
                ""media"":[{""type"":""video"",""orientation"":""portrait"",""url"":""x""}]}]}";
            var p = CatalogParser.Parse(json).Catalog.Promos[0];
            Assert.AreEqual(2.5, p.Weight);
            Assert.AreEqual(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), p.Start);
            Assert.AreEqual(new DateTime(2026, 12, 31, 11, 0, 0, DateTimeKind.Utc), p.End);
            Assert.AreEqual("0.3.0", p.MinPackageVersion);
            CollectionAssert.AreEqual(new[] { PromoPlatform.IOS, PromoPlatform.Android }, p.Platforms);
        }

        [Test]
        public void NegativeRulesKeepDefaults()
        {
            var json = @"{""schemaVersion"":1,""rules"":{""maxPerDay"":-1,""skipAfterSec"":""5"",""endCardAtSec"":-1,""rewardAfterSec"":""x""},""promos"":[]}";
            var c = CatalogParser.Parse(json).Catalog;
            Assert.AreEqual(3, c.Rules.MaxPerDay);
            Assert.AreEqual(0f, c.Rules.SkipAfterSec);
            Assert.AreEqual(25f, c.Rules.EndCardAtSec);
            Assert.AreEqual(30f, c.Rules.RewardAfterSec);
        }

        [Test]
        public void ParsesAdTimelineRules()
        {
            var c = CatalogParser.Parse(@"{""schemaVersion"":1,""rules"":{""skipAfterSec"":0,""endCardAtSec"":20,""rewardAfterSec"":35},""promos"":[]}").Catalog;
            Assert.AreEqual(0f, c.Rules.SkipAfterSec);
            Assert.AreEqual(20f, c.Rules.EndCardAtSec);
            Assert.AreEqual(35f, c.Rules.RewardAfterSec);

            var r = CatalogParser.Parse(@"{""schemaVersion"":1,""rules"":{""endCardAtSec"":25,""rewardAfterSec"":10},""promos"":[]}");
            Assert.AreEqual(25f, r.Catalog.Rules.RewardAfterSec, "reward never before the download panel");
            Assert.IsNotEmpty(r.Warnings);
        }

        [Test]
        public void ParsesMediaLanguageAndSize()
        {
            var json = @"{""schemaVersion"":1,""promos"":[{""id"":""p"",""gameId"":""g"",""weight"":1,""media"":[
                {""type"":""video"",""orientation"":""portrait"",""url"":""a.mp4"",""width"":720,""height"":1560},
                {""type"":""video"",""orientation"":""portrait"",""url"":""b.mp4"",""lang"":""NL "",""width"":960,""height"":1280},
                {""type"":""video"",""orientation"":""portrait"",""url"":""c.mp4"",""lang"":""en"",""width"":0,""height"":""x""}]}]}";
            var m = CatalogParser.Parse(json).Catalog.Promos[0].Media;
            Assert.IsNull(m[0].Lang);
            Assert.AreEqual(720, m[0].Width);
            Assert.AreEqual(1560, m[0].Height);
            Assert.AreEqual(1560f / 720f, m[0].AspectRatio.Value, 1e-4);
            Assert.IsTrue(m[0].IsDefaultLanguage);
            Assert.AreEqual("nl", m[1].Lang);
            Assert.IsFalse(m[1].IsDefaultLanguage);
            Assert.AreEqual(1280f / 960f, m[1].AspectRatio.Value, 1e-4);
            Assert.IsTrue(m[2].IsDefaultLanguage, "en counts as default");
            Assert.IsNull(m[2].Width, "0 = unknown");
            Assert.IsNull(m[2].Height);
            Assert.IsNull(m[2].AspectRatio);
        }

        [Test]
        public void LocalizedFallbacks()
        {
            var t = new LocalizedText();
            Assert.AreEqual("", t.Get("en"));
            t.Values["fr"] = "Bonjour";
            Assert.AreEqual("Bonjour", t.Get("nl"));
            t.Values["en"] = "Hello";
            Assert.AreEqual("Hello", t.Get("nl"));
            Assert.AreEqual("Hello", t.Get(null));
            t.Values["nl"] = "Hallo";
            Assert.AreEqual("Hallo", t.Get("nl_BE"));
        }
    }

    public class MiniJsonTests
    {
        [Test]
        public void RoundTrip()
        {
            var obj = new Dictionary<string, object>
            {
                { "s", "a\"b\\c\n\u0001é" }, { "i", 42L }, { "d", 1.5 }, { "b", true }, { "n", null },
                { "l", new List<object> { 1L, "x" } },
            };
            var json = MiniJson.Serialize(obj);
            object back;
            string err;
            Assert.IsTrue(MiniJson.TryParse(json, out back, out err), err);
            var d = (Dictionary<string, object>)back;
            Assert.AreEqual("a\"b\\c\n\u0001é", d["s"]);
            Assert.AreEqual(42L, d["i"]);
            Assert.AreEqual(1.5, d["d"]);
            Assert.AreEqual(true, d["b"]);
            Assert.IsNull(d["n"]);
            Assert.AreEqual(2, ((List<object>)d["l"]).Count);
        }

        [Test]
        public void SerializesFloatsInvariant()
        {
            var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("nl-NL");
                Assert.AreEqual("[1.25]", MiniJson.Serialize(new List<object> { 1.25f }));
            }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = prev; }
        }

        [TestCase("{\"a\":}")]
        [TestCase("[1,]")]
        [TestCase("\"abc")]
        [TestCase("tru")]
        [TestCase("{} x")]
        [TestCase("\"\\u12\"")]
        public void RejectsMalformed(string json)
        {
            object v;
            string err;
            Assert.IsFalse(MiniJson.TryParse(json, out v, out err));
        }
    }
}
