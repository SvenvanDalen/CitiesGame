using System;
using Cities.Simulation.Fields;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Fields
{
    public class WaterPollutionTests
    {
        [Test]
        public void ADischargePollutesItsReachAndThenFlowsDownstreamDiluted()
        {
            var river = new WaterPollution(4);
            river.Discharge(1, WaterDischarge.Industry);

            river.Step();
            var atOutlet = river.Level(1);
            Assert.That(river.Level(0), Is.Zero, "bovenstrooms blijft schoon");
            Assert.That(atOutlet, Is.GreaterThan(0f));
            Assert.That(river.Level(2), Is.Zero);

            river.Step();
            Assert.That(river.Level(1), Is.Zero, "de lozing was eenmalig");
            Assert.That(river.Level(2), Is.GreaterThan(0f).And.LessThan(atOutlet), "een stuk doorgestroomd en verdund");
        }

        [Test]
        public void DownstreamIntakeAndFishingSufferFromPollutionUpstream()
        {
            var river = new WaterPollution(3);
            for (var i = 0; i < 5; i++)
            {
                river.Discharge(0, WaterDischarge.Sewage);
                river.Step();
            }

            Assert.That(river.IntakeQuality(2), Is.LessThan(1f));
            Assert.That(river.FishingYield(2), Is.LessThan(1f));
            Assert.That(river.FishingYield(0), Is.LessThan(river.FishingYield(2)), "bij de uitlaat is het vuilst");
        }

        [Test]
        public void CleanWaterGivesFullQualityAndYield()
        {
            var river = new WaterPollution(2);

            Assert.That(river.IntakeQuality(1), Is.EqualTo(1f));
            Assert.That(river.FishingYield(1), Is.EqualTo(1f));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var river = new WaterPollution(2);

            Assert.Throws<ArgumentOutOfRangeException>(() => new WaterPollution(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => river.Discharge(2, WaterDischarge.Sewage));
            Assert.Throws<ArgumentOutOfRangeException>(() => river.Level(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterPollution.Emission((WaterDischarge)9));
            Assert.That(WaterPollution.Emission(WaterDischarge.Industry), Is.GreaterThan(WaterPollution.Emission(WaterDischarge.Sewage)));
        }
    }
}
