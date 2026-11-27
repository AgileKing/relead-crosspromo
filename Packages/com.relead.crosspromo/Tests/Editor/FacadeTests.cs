using NUnit.Framework;
using UnityEngine;

namespace Relead.CrossPromo.Tests
{
    public class FacadeTests
    {
        [Test]
        public void LanguageCodes()
        {
            Assert.AreEqual("nl", CrossPromo.LanguageCode(SystemLanguage.Dutch));
            Assert.AreEqual("en", CrossPromo.LanguageCode(SystemLanguage.Japanese));
        }

        [Test]
        public void PlatformNamesMatchCatalog()
        {
            foreach (PromoPlatform p in System.Enum.GetValues(typeof(PromoPlatform)))
                Assert.AreEqual(p, CatalogParser.ParsePlatform(CrossPromo.PlatformName(p)));
        }

        [Test]
        public void InitRequiresHostGameId()
        {
            Assert.Throws<System.ArgumentException>(() => CrossPromo.Init(new CrossPromoConfig()));
            Assert.Throws<System.ArgumentException>(() => CrossPromo.Init(null));
        }

        [Test]
        public void ShowBeforeInitFailsCleanly()
        {
            if (CrossPromo.IsInitialized) Assert.Ignore("already initialised in this domain");
            PromoResult r = null;
            CrossPromo.Show(x => r = x);
            Assert.AreEqual(PromoOutcome.Failed, r.Outcome);
            Assert.IsFalse(CrossPromo.IsReady);
        }

        [Test]
        public void DefaultBaseUrlsStartWithAdsRelead()
        {
            var c = new CrossPromoConfig();
            Assert.AreEqual("https://ads.relead.com", c.BaseUrls[0]);
            Assert.That(c.BaseUrls.Length, Is.GreaterThan(1), "fallback host for DNS ad-blockers");
        }
    }
}
