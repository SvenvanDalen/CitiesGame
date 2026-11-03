using System;
using Cities.Gameplay.Overlays;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Overlays
{
    public class OverlayScaleTests
    {
        private static readonly OverlayColor Black = new OverlayColor(0, 0, 0);
        private static readonly OverlayColor White = new OverlayColor(255, 255, 255);

        [Test]
        public void LerpBlendsAndClamps()
        {
            Assert.That(OverlayColor.Lerp(Black, White, 0.5f), Is.EqualTo(new OverlayColor(128, 128, 128)));
            Assert.That(OverlayColor.Lerp(Black, White, -3f), Is.EqualTo(Black));
            Assert.That(OverlayColor.Lerp(Black, White, 9f), Is.EqualTo(White));
        }

        [Test]
        public void PackPutsRedInTheLowestByte()
        {
            var color = new OverlayColor(1, 2, 3, 4);

            Assert.That(color.Pack(), Is.EqualTo(0x04030201u));
            Assert.That(color.Equals((object)new OverlayColor(1, 2, 3, 4)), Is.True);
            Assert.That(color.GetHashCode(), Is.EqualTo(new OverlayColor(1, 2, 3, 4).GetHashCode()));
            Assert.That(color.ToString(), Is.EqualTo("#01020304"));
        }

        [Test]
        public void ScaleInterpolatesBetweenStopsAndClampsOutside()
        {
            var scale = new OverlayColorScale(new[] { Black, White, Black }, new OverlayColor(9, 9, 9, 0));

            Assert.That(scale.StopCount, Is.EqualTo(3));
            Assert.That(scale.Stop(1), Is.EqualTo(White));
            Assert.That(scale.Evaluate(0f), Is.EqualTo(Black));
            Assert.That(scale.Evaluate(0.25f), Is.EqualTo(new OverlayColor(128, 128, 128)));
            Assert.That(scale.Evaluate(0.5f), Is.EqualTo(White));
            Assert.That(scale.Evaluate(-1f), Is.EqualTo(Black));
            Assert.That(scale.Evaluate(2f), Is.EqualTo(Black));
        }

        [Test]
        public void NotANumberGivesTheMissingColor()
        {
            var missing = new OverlayColor(9, 9, 9, 0);

            Assert.That(new OverlayColorScale(new[] { Black, White }, missing).Evaluate(float.NaN), Is.EqualTo(missing));
        }

        [Test]
        public void ScaleNeedsTwoStops()
        {
            Assert.Throws<ArgumentException>(() => new OverlayColorScale(new[] { Black }, Black));
            Assert.Throws<ArgumentNullException>(() => new OverlayColorScale(null, Black));
        }

        [Test]
        public void PresetsRunInTheirDirection()
        {
            Assert.That(OverlayColorScale.Hot.Evaluate(0f).G, Is.GreaterThan(OverlayColorScale.Hot.Evaluate(0f).R), "laag is groen");
            Assert.That(OverlayColorScale.Hot.Evaluate(1f).R, Is.GreaterThan(OverlayColorScale.Hot.Evaluate(1f).G), "hoog is rood");
            Assert.That(OverlayColorScale.Good.Evaluate(1f), Is.EqualTo(OverlayColorScale.Hot.Evaluate(0f)));
            Assert.That(OverlayColorScale.Terrain.StopCount, Is.EqualTo(5));
        }

        [Test]
        public void LegendNormalizesAndFormats()
        {
            var legend = new OverlayLegend("Landwaarde", "per m²", 100f, 300f, OverlayColorScale.Good, "0.0");

            Assert.That(legend.Normalize(200f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(float.IsNaN(legend.Normalize(float.NaN)), Is.True);
            Assert.That(legend.ValueAt(0.25f), Is.EqualTo(150f).Within(1e-4f));
            Assert.That(legend.ValueAt(5f), Is.EqualTo(300f));
            Assert.That(legend.ColorOf(300f), Is.EqualTo(OverlayColorScale.Good.Evaluate(1f)));
            Assert.That(legend.Describe(12.5f), Is.EqualTo("12,5 per m²"));
            Assert.That(legend.Describe(float.NaN), Is.EqualTo("geen gegevens"));
            Assert.That(new OverlayLegend("x", null, 0f, 1f, OverlayColorScale.Hot).Describe(1f), Is.EqualTo("1"));
        }

        [Test]
        public void LegendRejectsAnInvalidRange()
        {
            Assert.Throws<ArgumentException>(() => new OverlayLegend("x", "", 5f, 5f, OverlayColorScale.Hot));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OverlayLegend("x", "", float.NaN, 5f, OverlayColorScale.Hot));
            Assert.Throws<ArgumentNullException>(() => new OverlayLegend(null, "", 0f, 5f, OverlayColorScale.Hot));
            Assert.Throws<ArgumentNullException>(() => new OverlayLegend("x", "", 0f, 5f, null));
        }
    }
}
