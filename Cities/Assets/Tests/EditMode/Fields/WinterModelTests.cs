using System;
using Cities.Simulation.Fields;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Fields
{
    public class WinterModelTests
    {
        [Test]
        public void SnowBuildsUpAndMeltsAndIsCapped()
        {
            var winter = new WinterModel();
            winter.AdvanceHour(-3f, 4f);
            winter.AdvanceHour(-3f, 4f);
            Assert.That(winter.SnowDepth, Is.EqualTo(8f));

            winter.AdvanceHour(5f, 0f);
            Assert.That(winter.SnowDepth, Is.EqualTo(6f).Within(0.001f));

            winter.AdvanceHour(-1f, 500f);
            Assert.That(winter.SnowDepth, Is.EqualTo(WinterModel.MaxSnowDepth));
            winter.AdvanceHour(30f, 0f);
            winter.AdvanceHour(30f, 0f);
            Assert.That(winter.SnowDepth, Is.EqualTo(6f).Within(0.01f));
        }

        [Test]
        public void SnowAndFrostLowerGripButNeverBelowTheFloor()
        {
            var winter = new WinterModel();
            Assert.That(winter.RoadGrip, Is.EqualTo(1f));

            winter.AdvanceHour(-10f, 100f);
            Assert.That(winter.RoadGrip, Is.EqualTo(1f - WinterModel.MaxGripLoss).Within(0.001f));
            winter.AdvanceHour(-2f, 0f);
            Assert.That(winter.RoadGrip, Is.GreaterThanOrEqualTo(0.6f));
        }

        [Test]
        public void PlowsClearTheRoadAndHeatingFollowsTheCold()
        {
            var winter = new WinterModel { Plows = 2 };
            winter.AdvanceHour(-5f, 10f);
            Assert.That(winter.SnowDepth, Is.EqualTo(0f));
            Assert.That(winter.HeatingDemand, Is.EqualTo(1f + (0.04f * 20f)).Within(0.001f));
            winter.AdvanceHour(20f, 0f);
            Assert.That(winter.HeatingDemand, Is.EqualTo(1f));
        }

        [Test]
        public void RejectsBadInput()
        {
            var winter = new WinterModel();
            Assert.Throws<ArgumentOutOfRangeException>(() => winter.AdvanceHour(0f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => winter.AdvanceHour(float.NaN, 0f));
        }
    }
}
