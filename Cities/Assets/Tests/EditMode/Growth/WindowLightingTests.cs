using System.Linq;
using Cities.Simulation.Growth;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Growth
{
    public class WindowLightingTests
    {
        private const float Evening = 20f / 24f;
        private const float Noon = 12f / 24f;
        private const float Night = 3f / 24f;

        [Test]
        public void EveningHasMoreLightThanNightAndNoon()
        {
            Assert.That(WindowLighting.LitFraction(Evening, 1f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(WindowLighting.LitFraction(Night, 1f), Is.LessThan(0.1f));
            Assert.That(WindowLighting.LitFraction(Noon, 1f), Is.LessThan(0.1f));
            Assert.That(WindowLighting.LitFraction(Evening, 1f), Is.GreaterThan(WindowLighting.LitFraction(Night, 1f)));
        }

        [Test]
        public void OccupancyScalesTheFractionAndIsClamped()
        {
            Assert.That(WindowLighting.LitFraction(Evening, 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(WindowLighting.LitFraction(Evening, 0f), Is.Zero, "leeg gebouw: donker");
            Assert.That(WindowLighting.LitFraction(Evening, 5f), Is.EqualTo(WindowLighting.LitFraction(Evening, 1f)));
            Assert.That(WindowLighting.LitFraction(Evening, -1f), Is.Zero);
        }

        [Test]
        public void TheCurveIsSmoothAndWrapsAroundMidnight()
        {
            Assert.That(WindowLighting.LitFraction(18.5f / 24f, 1f), Is.EqualTo((0.70f + 0.95f) / 2f).Within(1e-4f), "tussen 18:00 en 19:00 gemengd");
            Assert.That(WindowLighting.LitFraction(23.5f / 24f, 1f), Is.EqualTo((0.25f + 0.05f) / 2f).Within(1e-4f), "23:30 mengt met middernacht");
            Assert.That(WindowLighting.LitFraction(1.25f, 1f), Is.EqualTo(WindowLighting.LitFraction(0.25f, 1f)).Within(1e-5f), "dag 2 is dag 1");
            Assert.That(WindowLighting.LitFraction(-0.1f, 1f), Is.EqualTo(WindowLighting.LitFraction(0.9f, 1f)).Within(1e-5f));
        }

        [Test]
        public void EachWindowHasAStableThresholdBetweenZeroAndOne()
        {
            var thresholds = Enumerable.Range(0, 500).Select(i => WindowLighting.Threshold(i, 42UL)).ToArray();

            Assert.That(thresholds, Has.All.InRange(0f, 0.99999f));
            Assert.That(thresholds, Is.EqualTo(Enumerable.Range(0, 500).Select(i => WindowLighting.Threshold(i, 42UL)).ToArray()));
            Assert.That(thresholds.Distinct().Count(), Is.GreaterThan(400), "ramen verschillen");
            Assert.That(thresholds.Average(), Is.EqualTo(0.5f).Within(0.08f));
            Assert.That(WindowLighting.Threshold(3, 1UL), Is.Not.EqualTo(WindowLighting.Threshold(3, 2UL)), "elk gebouw een eigen patroon");
        }

        [Test]
        public void TheShareOfLitWindowsFollowsTheFractionAndOnlyGrows()
        {
            const int windows = 1000;
            int Lit(float time, float occupancy) => Enumerable.Range(0, windows).Count(i => WindowLighting.IsLit(i, 7UL, time, occupancy));

            Assert.That(Lit(Evening, 1f), Is.GreaterThan(900));
            Assert.That(Lit(Evening, 0.5f), Is.InRange(400, 600));
            Assert.That(Lit(Noon, 1f), Is.LessThan(100));
            Assert.That(Lit(Evening, 0f), Is.Zero);
            var dim = Enumerable.Range(0, windows).Where(i => WindowLighting.IsLit(i, 7UL, Evening, 0.3f)).ToArray();
            Assert.That(dim.All(i => WindowLighting.IsLit(i, 7UL, Evening, 0.8f)), Is.True, "een raam dat brandt blijft branden als het aandeel stijgt");
        }
    }
}
