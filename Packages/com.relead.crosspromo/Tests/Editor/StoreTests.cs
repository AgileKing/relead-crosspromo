using System.Collections.Generic;
using NUnit.Framework;
using Relead.CrossPromo.Editor;

namespace Relead.CrossPromo.Tests
{
    public class StoreLinkTests
    {
        static GameInfo Game()
        {
            var g = new GameInfo { Id = "kingofgames", UrlScheme = "relead-g01" };
            g.Store.Android = "com.relead.kingofgames";
            g.Store.Ios = "1234567890";
            g.Store.Windows = "9NABCDEFGH12";
            return g;
        }

        [Test]
        public void Android()
        {
            var l = StoreLinks.Build(PromoPlatform.Android, Game(), "gpking", "kingofgames-2026-11");
            Assert.AreEqual("market://details?id=com.relead.kingofgames&referrer=utm_source%3Dgpking%26utm_campaign%3Dcrosspromo%26utm_content%3Dkingofgames-2026-11", l.Primary);
            StringAssert.StartsWith("https://play.google.com/store/apps/details?id=com.relead.kingofgames&referrer=utm_source%3Dgpking", l.Web);
        }

        [Test]
        public void Ios()
        {
            var l = StoreLinks.Build(PromoPlatform.IOS, Game(), "gpking", "kingofgames-2026-11");
            Assert.AreEqual("https://apps.apple.com/app/id1234567890?mt=8&ct=cp-gpking-kingofgames-2026-11", l.Primary);
            var withPt = StoreLinks.Build(PromoPlatform.IOS, Game(), "gpking", "p", "118000");
            StringAssert.EndsWith("&pt=118000", withPt.Primary);
        }

        [Test]
        public void IosCampaignTokenMax40()
        {
            var t = StoreLinks.IosCampaignToken("averyveryverylonghostgameid", "averyveryverylongpromoid-2026-11");
            Assert.AreEqual(40, t.Length);
        }

        [Test]
        public void Windows()
        {
            var l = StoreLinks.Build(PromoPlatform.Windows, Game(), "gpking", "kog-1");
            Assert.AreEqual("ms-windows-store://pdp/?productid=9NABCDEFGH12&cid=crosspromo_kog-1", l.Primary);
            Assert.AreEqual("https://apps.microsoft.com/detail/9NABCDEFGH12?cid=crosspromo_kog-1", l.Web);
        }

        [Test]
        public void MissingIdGivesNull()
        {
            var g = Game();
            g.Store.Ios = null;
            Assert.IsNull(StoreLinks.Build(PromoPlatform.IOS, g, "h", "p"));
            Assert.IsNull(StoreLinks.Build(PromoPlatform.Android, null, "h", "p"));
        }

        [Test]
        public void CachingCheckerAsksOncePerGame()
        {
            int calls = 0;
            var inner = new CountingChecker(() => calls++);
            var c = new CachingInstalledChecker(inner);
            var g = Game();
            Assert.IsTrue(c.IsInstalled(g));
            Assert.IsTrue(c.IsInstalled(g));
            Assert.AreEqual(1, calls);
            c.Clear();
            c.IsInstalled(g);
            Assert.AreEqual(2, calls);
            Assert.IsFalse(c.IsInstalled(null));
        }

        [Test]
        public void CheckerExceptionMeansNotInstalled()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var c = new CachingInstalledChecker(new CountingChecker(() => { throw new System.Exception("boom"); }));
            Assert.IsFalse(c.IsInstalled(Game()));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        sealed class CountingChecker : IInstalledChecker
        {
            readonly System.Action _a;
            public CountingChecker(System.Action a) { _a = a; }
            public bool IsInstalled(GameInfo g) { _a(); return true; }
        }
    }

    public class NativeSetupTests
    {
        const string Manifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<manifest xmlns:android=""http://schemas.android.com/apk/res/android"" xmlns:tools=""http://schemas.android.com/tools"">
  <application>
    <activity android:name=""com.unity3d.player.UnityPlayerGameActivity"" android:exported=""true"">
      <intent-filter>
        <category android:name=""android.intent.category.LAUNCHER"" />
        <action android:name=""android.intent.action.MAIN"" />
      </intent-filter>
    </activity>
  </application>
</manifest>";

        [Test]
        public void ReservedSchemes()
        {
            var s = NativeSetup.ReservedSchemes();
            Assert.AreEqual(50, s.Count);
            Assert.AreEqual("relead-g01", s[0]);
            Assert.AreEqual("relead-g50", s[49]);
            foreach (var x in s) Assert.IsTrue(NativeSetup.SchemePattern.IsMatch(x));
        }

        [Test]
        public void IosMergeKeepsExistingAndWarnsOverLimit()
        {
            string warning;
            var m = NativeSetup.MergeQuerySchemes(new[] { "relead-g03" }, out warning);
            Assert.AreEqual(50, m.Count);
            Assert.IsNull(warning);
            m = NativeSetup.MergeQuerySchemes(new[] { "fb", "whatsapp" }, out warning);
            Assert.AreEqual(52, m.Count);
            Assert.AreEqual("fb", m[0]);
            Assert.IsNotNull(warning);
        }

        [Test]
        public void AndroidManifestPatch()
        {
            var x = NativeSetup.PatchAndroidManifest(Manifest, "relead-g02", new[] { "com.relead.kingofgames", " ", "com.relead.gpking" });
            StringAssert.Contains("<queries>", x);
            StringAssert.Contains("android:name=\"com.relead.kingofgames\"", x);
            StringAssert.Contains("android:scheme=\"relead-g01\"", x);
            StringAssert.Contains("android:scheme=\"relead-g50\"", x);
            StringAssert.Contains("android.intent.category.BROWSABLE", x);
            StringAssert.Contains("encoding=\"utf-8\"", x);
            Assert.Less(x.IndexOf("<queries>"), x.IndexOf("<application>"));

            var again = NativeSetup.PatchAndroidManifest(x, "relead-g02", new[] { "com.relead.kingofgames" });
            Assert.AreEqual(Count(x, "<intent>"), Count(again, "<intent>"), "idempotent");
            Assert.AreEqual(1, Count(again, "android:scheme=\"relead-g02\"") - 1, "own scheme: one query + one filter");
            Assert.AreEqual(1, Count(again, "com.relead.kingofgames"));
        }

        [Test]
        public void AndroidPatchWithoutHostScheme()
        {
            var x = NativeSetup.PatchAndroidManifest(Manifest, null, null);
            StringAssert.DoesNotContain("BROWSABLE", x);
            Assert.AreEqual(50, Count(x, "<intent>"));
        }

        static int Count(string s, string sub)
        {
            int n = 0, i = 0;
            while ((i = s.IndexOf(sub, i, System.StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; }
            return n;
        }
    }
}
