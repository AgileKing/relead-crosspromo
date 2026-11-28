using NUnit.Framework;
using UnityEngine;

namespace Relead.CrossPromo.Tests
{
    public class OrientationTests
    {
        const PromoOrientation P = PromoOrientation.Portrait;
        const PromoOrientation L = PromoOrientation.Landscape;

        [Test]
        public void ShapeFromSize()
        {
            Assert.AreEqual(L, OrientationLogic.FromSize(1920, 1080));
            Assert.AreEqual(P, OrientationLogic.FromSize(1080, 1920));
            Assert.AreEqual(P, OrientationLogic.FromSize(1000, 1000));
        }

        [TestCase(DeviceOrientation.Portrait, P, true)]
        [TestCase(DeviceOrientation.PortraitUpsideDown, P, true)]
        [TestCase(DeviceOrientation.LandscapeLeft, L, true)]
        [TestCase(DeviceOrientation.LandscapeRight, L, true)]
        [TestCase(DeviceOrientation.LandscapeLeft, P, false)]
        [TestCase(DeviceOrientation.FaceUp, L, false)]
        [TestCase(DeviceOrientation.FaceDown, P, false)]
        [TestCase(DeviceOrientation.Unknown, P, false)]
        public void DeviceMatches(DeviceOrientation d, PromoOrientation media, bool expected)
        {
            Assert.AreEqual(expected, OrientationLogic.DeviceMatches(d, media));
        }

        [TestCase(ScreenOrientation.Portrait, DeviceOrientation.LandscapeLeft, -90f)]
        [TestCase(ScreenOrientation.Portrait, DeviceOrientation.LandscapeRight, 90f)]
        [TestCase(ScreenOrientation.LandscapeLeft, DeviceOrientation.Portrait, 90f)]
        [TestCase(ScreenOrientation.LandscapeRight, DeviceOrientation.Portrait, -90f)]
        [TestCase(ScreenOrientation.LandscapeLeft, DeviceOrientation.PortraitUpsideDown, -90f)]
        [TestCase(ScreenOrientation.Portrait, DeviceOrientation.Portrait, 0f)]
        [TestCase(ScreenOrientation.Portrait, DeviceOrientation.PortraitUpsideDown, 180f)]
        public void StageRotation(ScreenOrientation screen, DeviceOrientation device, float z)
        {
            int s = OrientationLogic.ScreenAngle(screen, P);
            Assert.AreEqual(z, OrientationLogic.StageRotation(s, OrientationLogic.DeviceAngle(device).Value));
        }

        [Test]
        public void ScreenAngleFallsBackToShape()
        {
            Assert.AreEqual(90, OrientationLogic.ScreenAngle(ScreenOrientation.AutoRotation, L));
            Assert.AreEqual(0, OrientationLogic.ScreenAngle(ScreenOrientation.AutoRotation, P));
            Assert.IsNull(OrientationLogic.DeviceAngle(DeviceOrientation.FaceUp));
        }

        const float H = OrientationLogic.MatchStableSec;

        [Test]
        public void MatchingMediaPlaysUpright()
        {
            Assert.AreEqual(StageMode.Upright, OrientationLogic.Next(StageMode.RotateHint, L, L, DeviceOrientation.Portrait, 0, 3, 0));
        }

        [Test]
        public void HintThenRotateWhenDeviceTurnsAndHolds()
        {
            var m = OrientationLogic.Next(StageMode.RotateHint, P, L, DeviceOrientation.Portrait, 0f, 3f, 0f);
            Assert.AreEqual(StageMode.RotateHint, m);
            m = OrientationLogic.Next(m, P, L, DeviceOrientation.LandscapeRight, 1f, 3f, 0.1f);
            Assert.AreEqual(StageMode.RotateHint, m, "match not stable yet");
            m = OrientationLogic.Next(m, P, L, DeviceOrientation.LandscapeRight, 1.5f, 3f, H);
            Assert.AreEqual(StageMode.Rotated, m);
            m = OrientationLogic.Next(m, P, L, DeviceOrientation.Portrait, 10f, 3f, 0f);
            Assert.AreEqual(StageMode.Rotated, m, "sticks, no flapping back");
        }

        [Test]
        public void AlreadyHeldRightAtStartRotatesAtOnce()
        {
            Assert.AreEqual(StageMode.Rotated, OrientationLogic.Next(StageMode.RotateHint, L, P, DeviceOrientation.Portrait, 0f, 3f, 0f));
        }

        [Test]
        public void TimeoutLetterboxesThenSwitchesToRotatedOnStableMatch()
        {
            var m = OrientationLogic.Next(StageMode.RotateHint, P, L, DeviceOrientation.FaceUp, 3f, 3f, 0f);
            Assert.AreEqual(StageMode.Letterbox, m);
            Assert.AreEqual(StageMode.Letterbox, OrientationLogic.Next(m, P, L, DeviceOrientation.LandscapeLeft, 4f, 3f, 0.2f), "not stable yet");
            m = OrientationLogic.Next(m, P, L, DeviceOrientation.LandscapeLeft, 20f, 3f, H);
            Assert.AreEqual(StageMode.Rotated, m, "turning later still goes full screen");
            Assert.AreEqual(StageMode.Rotated, OrientationLogic.Next(m, P, L, DeviceOrientation.Portrait, 21f, 3f, 0f));
        }

        [TestCase(DeviceOrientation.FaceUp)]
        [TestCase(DeviceOrientation.FaceDown)]
        [TestCase(DeviceOrientation.Unknown)]
        public void FlatOrUnknownNeverSwitches(DeviceOrientation d)
        {
            Assert.AreEqual(StageMode.Letterbox, OrientationLogic.Next(StageMode.Letterbox, P, L, d, 60f, 3f, 60f));
            Assert.AreEqual(StageMode.RotateHint, OrientationLogic.Next(StageMode.RotateHint, P, L, d, 0f, 3f, 60f));
        }

        [Test]
        public void ZeroTimeoutLetterboxesImmediatelyButStillRotates()
        {
            Assert.AreEqual(StageMode.Letterbox, OrientationLogic.Next(StageMode.RotateHint, P, L, DeviceOrientation.Portrait, 0f, 0f, 0f));
            Assert.AreEqual(StageMode.Rotated, OrientationLogic.Next(StageMode.Letterbox, P, L, DeviceOrientation.LandscapeLeft, 5f, 0f, H));
        }

        [Test]
        public void LetterboxBarHoldsTheRotateHint()
        {
            // Landscape video on a portrait phone (≈ 730×1300 canvas units): bars above and below.
            Assert.AreEqual((1300f - 730f * 9f / 16f) / 2f, OrientationLogic.LetterboxBar(730, 1300, L), 0.01f);
            // Portrait video on a landscape phone: bars left and right.
            Assert.AreEqual((1300f - 730f * 9f / 16f) / 2f, OrientationLogic.LetterboxBar(1300, 730, P), 0.01f);
            Assert.AreEqual(0f, OrientationLogic.LetterboxBar(1920, 1080, L), "16:9 fills the screen");
            Assert.AreEqual(0f, OrientationLogic.LetterboxBar(2400, 1080, L), "wider screen: bars are on the sides, none on top");
            Assert.AreEqual(0f, OrientationLogic.LetterboxBar(0, 0, L));
        }

        [Test]
        public void GlowCentredOnItsElement()
        {
            float g = PromoUI.GlowSpread;
            Assert.AreEqual(new Vector2(g, g), PromoUI.GlowOffset(Vector2.one), "top-right corner element");
            Assert.AreEqual(new Vector2(-g, g), PromoUI.GlowOffset(new Vector2(0, 1)), "top-left corner element");
            Assert.AreEqual(Vector2.zero, PromoUI.GlowOffset(new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void GlowSpriteFadesOut()
        {
            Assert.Greater(PromoUI.GlowAlpha(32, 32), 0.99f, "opaque centre");
            Assert.AreEqual(0f, PromoUI.GlowAlpha(0, 0), "transparent corner");
            Assert.Less(PromoUI.GlowAlpha(0, 32), 0.01f, "transparent edge");
            Assert.Greater(PromoUI.GlowAlpha(16, 32), PromoUI.GlowAlpha(8, 32), "fades outwards");
            var sprite = PromoUI.GlowSprite;
            Assert.AreEqual(PromoUI.GlowSize, sprite.texture.width);
            Assert.Greater(sprite.border.x, 0, "sliced, so it stretches without blurring the centre");
        }

        [Test]
        public void WatchClockAdTimeline()
        {
            var c = new WatchClock(0f, 25f, 30f);
            Assert.IsTrue(c.CanSkip, "Skip from the start");
            Assert.IsFalse(c.ShowEndCard);
            Assert.IsFalse(c.Earned);
            Assert.AreEqual(30, c.SecondsUntilReward);
            c.Tick(-3f);
            Assert.AreEqual(0f, c.WatchedSec, "negative dt ignored");
            c.Tick(24.9f);
            Assert.IsFalse(c.ShowEndCard);
            c.Tick(0.1f);
            Assert.IsTrue(c.ShowEndCard, "download panel at 25 s");
            Assert.IsFalse(c.Earned);
            Assert.AreEqual(5, c.SecondsUntilReward);
            c.Tick(4.5f);
            Assert.AreEqual(1, c.SecondsUntilReward);
            Assert.IsFalse(c.Earned);
            c.Tick(0.5f);
            Assert.IsTrue(c.Earned, "reward at 30 s");
            Assert.AreEqual(0, c.SecondsUntilReward);
            Assert.AreEqual(1f, c.RewardProgress);
        }

        [Test]
        public void WatchClockAutoClosesTenSecondsAfterTheReward()
        {
            var c = new WatchClock(0f, 25f, 30f);
            Assert.AreEqual(10f, WatchClock.DefaultAutoCloseAfterEarnedSec);
            c.Tick(30f);
            Assert.IsTrue(c.Earned);
            Assert.IsFalse(c.AutoClose);
            c.Tick(9.9f);
            Assert.IsFalse(c.AutoClose);
            c.Tick(0.1f);
            Assert.IsTrue(c.AutoClose, "40 s watched = 30 s reward + 10 s");

            var off = new WatchClock(0f, 25f, 30f, 0f);
            off.Tick(1000f);
            Assert.IsFalse(off.AutoClose, "0 = never");
        }

        [Test]
        public void WatchClockSkipDelayAndRewardNeverBeforeEndCard()
        {
            var c = new WatchClock(5f, 25f, 10f);
            Assert.IsFalse(c.CanSkip);
            c.Tick(5f);
            Assert.IsTrue(c.CanSkip);
            c.Tick(10f);
            Assert.IsFalse(c.Earned, "reward raised to endCardAtSec");
            c.Tick(10f);
            Assert.IsTrue(c.Earned);
            var zero = new WatchClock(0, 0, 0);
            Assert.IsTrue(zero.Earned, "zero timeline = earned at once");
            Assert.AreEqual(1f, zero.RewardProgress);
        }
    }
}
