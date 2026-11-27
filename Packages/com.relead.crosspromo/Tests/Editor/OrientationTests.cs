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

        [Test]
        public void MatchingMediaPlaysUpright()
        {
            Assert.AreEqual(StageMode.Upright, OrientationLogic.Next(StageMode.RotateHint, L, L, DeviceOrientation.Portrait, 0, 3));
        }

        [Test]
        public void HintThenRotateWhenDeviceTurns()
        {
            var m = OrientationLogic.Next(StageMode.RotateHint, P, L, DeviceOrientation.Portrait, 0f, 3f);
            Assert.AreEqual(StageMode.RotateHint, m);
            m = OrientationLogic.Next(m, P, L, DeviceOrientation.Portrait, 2.9f, 3f);
            Assert.AreEqual(StageMode.RotateHint, m);
            m = OrientationLogic.Next(m, P, L, DeviceOrientation.LandscapeRight, 2.95f, 3f);
            Assert.AreEqual(StageMode.Rotated, m);
            m = OrientationLogic.Next(m, P, L, DeviceOrientation.Portrait, 10f, 3f);
            Assert.AreEqual(StageMode.Rotated, m, "sticks, no flapping back");
        }

        [Test]
        public void AlreadyHeldRightSkipsHint()
        {
            Assert.AreEqual(StageMode.Rotated, OrientationLogic.Next(StageMode.RotateHint, L, P, DeviceOrientation.Portrait, 0f, 3f));
        }

        [Test]
        public void TimeoutLetterboxesAndSticks()
        {
            var m = OrientationLogic.Next(StageMode.RotateHint, P, L, DeviceOrientation.FaceUp, 3f, 3f);
            Assert.AreEqual(StageMode.Letterbox, m);
            Assert.AreEqual(StageMode.Letterbox, OrientationLogic.Next(m, P, L, DeviceOrientation.LandscapeLeft, 4f, 3f));
        }

        [Test]
        public void ZeroTimeoutLetterboxesImmediately()
        {
            Assert.AreEqual(StageMode.Letterbox, OrientationLogic.Next(StageMode.RotateHint, P, L, DeviceOrientation.Portrait, 0f, 0f));
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
