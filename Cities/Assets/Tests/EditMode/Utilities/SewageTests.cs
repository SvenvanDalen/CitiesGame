using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class SewageTests
    {
        [Test]
        public void TreatmentCoversWhatItCanAndTheRestRunsOffUntreated()
        {
            Assert.That(Sewage.Untreated(100f, 100f, 1f), Is.Zero, "volledige zuivering");
            Assert.That(Sewage.Untreated(100f, 60f, 1f), Is.EqualTo(40f), "te weinig capaciteit");
            Assert.That(Sewage.Untreated(100f, 100f, 0.9f), Is.EqualTo(10f).Within(1e-4f), "niet alles wordt schoon");
            Assert.That(Sewage.Untreated(100f, 0f, 1f), Is.EqualTo(100f), "geen zuivering");
            Assert.That(Sewage.Untreated(100f, 100f, 5f), Is.Zero, "rendement begrensd");
        }

        [Test]
        public void PollutionBuildsUpDownstreamAndRecoversWithoutNewDischarge()
        {
            var pollution = Sewage.Downstream(new[] { 0f, 20f, 0f, 0f }, 100f);

            Assert.That(pollution[0], Is.Zero, "stroomopwaarts is schoon");
            Assert.That(pollution[1], Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(pollution[2], Is.EqualTo(0.18f).Within(1e-5f));
            Assert.That(pollution[3], Is.LessThan(pollution[2]));
        }

        [Test]
        public void SecondOutletAddsToTheFirstAndPollutionIsCapped()
        {
            var pollution = Sewage.Downstream(new[] { 30f, 30f, 500f, -10f }, 100f);

            Assert.That(pollution[1], Is.EqualTo((0.3f * 0.9f) + 0.3f).Within(1e-5f));
            Assert.That(pollution[2], Is.EqualTo(1f));
            Assert.That(pollution[3], Is.EqualTo(0.9f).Within(1e-5f), "negatieve lozing telt als nul");
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Sewage.Untreated(-1f, 0f, 1f));
            Assert.Throws<ArgumentNullException>(() => Sewage.Downstream(null, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Sewage.Downstream(Array.Empty<float>(), 0f));
        }
    }
}
