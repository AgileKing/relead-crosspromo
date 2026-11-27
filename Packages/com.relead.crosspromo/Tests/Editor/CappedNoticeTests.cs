using NUnit.Framework;
using UnityEngine;

namespace Relead.CrossPromo.Tests
{
    public class CappedNoticeTests
    {
        CappedNotice _notice;

        [TearDown]
        public void TearDown()
        {
            if (_notice != null) Object.DestroyImmediate(_notice.gameObject);
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
                if (es.name == "CrossPromo EventSystem") Object.DestroyImmediate(es.gameObject);
        }

        [Test]
        public void MessagePerLanguage()
        {
            Assert.AreEqual("Maxed out ads for today", CappedNotice.Message("en"));
            Assert.AreEqual("Max aantal ads voor vandaag bereikt", CappedNotice.Message("nl"));
            Assert.AreEqual("Maxed out ads for today", CappedNotice.Message(null));
        }

        [Test]
        public void TapClosesAtOnceAndReportsOnce()
        {
            int calls = 0;
            _notice = CappedNotice.Show("en", 30000, () => calls++);
            Assert.AreEqual(0, calls);
            _notice.Close();
            Assert.AreEqual(1, calls, "no timer before dismiss");
            _notice.Close();
            Assert.AreEqual(1, calls, "only once");
        }

        [Test]
        public void AutoClosesAfterAFewSeconds()
        {
            int calls = 0;
            _notice = CappedNotice.Show("nl", 30000, () => calls++);
            _notice.Tick(CappedNotice.AutoCloseSec - 0.1f);
            Assert.AreEqual(0, calls);
            _notice.Tick(-5f);
            Assert.AreEqual(0, calls, "negative dt ignored");
            _notice.Tick(0.1f);
            Assert.AreEqual(1, calls);
            Assert.LessOrEqual(CappedNotice.AutoCloseSec, 5f, "a few seconds, not a wait");
        }

        [Test]
        public void DestroyedFromOutsideStillReports()
        {
            int calls = 0;
            _notice = CappedNotice.Show("en", 30000, () => calls++);
            Object.DestroyImmediate(_notice.gameObject);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void HasDismissButtonCoveringTheScreen()
        {
            _notice = CappedNotice.Show("en", 30000, null);
            var buttons = _notice.GetComponentsInChildren<UnityEngine.UI.Button>();
            Assert.AreEqual(2, buttons.Length, "tap-anywhere + ×");
            var dismiss = (RectTransform)buttons[0].transform;
            Assert.AreEqual(Vector2.zero, dismiss.anchorMin);
            Assert.AreEqual(Vector2.one, dismiss.anchorMax);
        }

        [Test]
        public void CappedResultIsDistinctAndNeverRewarded()
        {
            var r = CrossPromo.CappedResult();
            Assert.AreEqual(PromoOutcome.Capped, r.Outcome);
            Assert.AreNotEqual(PromoOutcome.Completed, r.Outcome);
            Assert.AreEqual("daily cap", r.FailReason);
        }

        [Test]
        public void CapQueriesBeforeInit()
        {
            if (CrossPromo.IsInitialized) Assert.Ignore("already initialised in this domain");
            Assert.IsFalse(CrossPromo.IsCappedToday);
            Assert.AreEqual(0, CrossPromo.RemainingToday);
        }
    }
}
