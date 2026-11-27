using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Relead.CrossPromo.Tests
{
    public class SlideshowTests
    {
        readonly List<Texture2D> _tex = new List<Texture2D>();

        Texture2D T() { var t = new Texture2D(4, 4); _tex.Add(t); return t; }

        [TearDown] public void TearDown() { foreach (var t in _tex) Object.DestroyImmediate(t); _tex.Clear(); }

        [Test]
        public void InterleavesGamesUpToTwoShotsEach()
        {
            var a = new PromoTarget(); a.Screenshots.AddRange(new[] { T(), null, T(), T() });
            var b = new PromoTarget(); b.Screenshots.Add(T());
            var c = new PromoTarget();
            var slides = SlideshowPromoPlayer.BuildSlides(new List<PromoTarget> { a, b, c }, 2);
            Assert.AreEqual(3, slides.Count);
            Assert.AreEqual(0, slides[0].Key);
            Assert.AreEqual(1, slides[1].Key);
            Assert.AreEqual(0, slides[2].Key);
            Assert.AreSame(a.Screenshots[2], slides[2].Value, "null skipped");
        }

        [Test]
        public void SlideTimingClamped()
        {
            Assert.AreEqual(6f, SlideshowPromoPlayer.SlideSeconds(1));
            Assert.AreEqual(5f, SlideshowPromoPlayer.SlideSeconds(3));
            Assert.AreEqual(3f, SlideshowPromoPlayer.SlideSeconds(10));
        }

        [Test]
        public void SlidesLoop()
        {
            Assert.AreEqual(0, SlideshowPromoPlayer.SlideIndex(0f, 5f, 3));
            Assert.AreEqual(2, SlideshowPromoPlayer.SlideIndex(14.9f, 5f, 3));
            Assert.AreEqual(0, SlideshowPromoPlayer.SlideIndex(15f, 5f, 3), "wraps after the last slide");
            Assert.AreEqual(1, SlideshowPromoPlayer.SlideIndex(21f, 5f, 3));
            Assert.AreEqual(0, SlideshowPromoPlayer.SlideIndex(-1f, 5f, 3));
            Assert.AreEqual(0, SlideshowPromoPlayer.SlideIndex(10f, 5f, 0));
        }
    }
}
