using System;
using Cities.Gameplay.Overlays;
using Cities.Simulation.Transit;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Overlays
{
    public class UndergroundViewTests
    {
        [Test]
        public void TheViewStartsAboveGroundAndOnlyUndergroundThingsWaitForTheSwitch()
        {
            var view = new UndergroundView();

            Assert.That(view.Enabled, Is.False);
            Assert.That(view.GroundOpacity, Is.EqualTo(1f));
            Assert.That(view.IsSettled, Is.True);
            Assert.That(view.IsVisible(0f), Is.True);
            Assert.That(view.IsVisible(12f), Is.False);

            view.Toggle();

            Assert.That(view.Enabled, Is.True);
            Assert.That(view.IsVisible(12f), Is.True);
            Assert.That(view.IsSettled, Is.False, "de dekking moet nog wegfaden");
        }

        [Test]
        public void TheGroundFadesSmoothlyAndStopsAtTheTarget()
        {
            var view = new UndergroundView();
            view.SetEnabled(true);

            view.Step(0.1f);
            Assert.That(view.GroundOpacity, Is.EqualTo(1f - UndergroundView.FadeSpeed * 0.1f).Within(1e-5f));

            view.Step(10f);
            Assert.That(view.GroundOpacity, Is.EqualTo(UndergroundView.UndergroundGroundOpacity));
            Assert.That(view.IsSettled, Is.True);

            view.SetEnabled(false);
            view.Step(10f);
            Assert.That(view.GroundOpacity, Is.EqualTo(1f));
            Assert.That(view.IsSettled, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => view.Step(-1f));
        }

        [Test]
        public void ChangingTheStateRaisesTheEventOnlyOnAChange()
        {
            var view = new UndergroundView();
            var calls = 0;
            var last = false;
            view.Changed += enabled =>
            {
                calls++;
                last = enabled;
            };

            view.SetEnabled(false);
            view.SetEnabled(true);
            view.SetEnabled(true);

            Assert.That(calls, Is.EqualTo(1));
            Assert.That(last, Is.True);
        }

        [Test]
        public void DeeperLayersDrawDarker()
        {
            Assert.That(UndergroundView.DepthTint(0f), Is.EqualTo(1f));
            Assert.That(UndergroundView.DepthTint(Metro.MaxDepth), Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(UndergroundView.DepthTint(10f), Is.GreaterThan(UndergroundView.DepthTint(30f)));
            Assert.That(UndergroundView.DepthTint(500f), Is.EqualTo(0.4f).Within(1e-5f), "dieper dan het maximum blijft op de donkerste tint");
        }
    }
}
