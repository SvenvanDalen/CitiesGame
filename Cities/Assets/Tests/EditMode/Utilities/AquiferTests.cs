using System;
using Cities.Simulation.Utilities;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Utilities
{
    public class AquiferTests
    {
        [Test]
        public void ThePumpOnlyGetsWhatIsThere()
        {
            var aquifer = new Aquifer(1000f, 100f);

            Assert.That(aquifer.Extract(60f), Is.EqualTo(60f));
            Assert.That(aquifer.Extract(60f), Is.EqualTo(40f), "de laag loopt leeg");
            Assert.That(aquifer.Level, Is.Zero);
        }

        [Test]
        public void RainRefillsTheLayerSlowlyAndNotBeyondCapacity()
        {
            var aquifer = new Aquifer(1000f, 500f);

            aquifer.Step();
            Assert.That(aquifer.Level, Is.EqualTo(502f).Within(1e-3f));
            for (var i = 0; i < 1000; i++)
            {
                aquifer.Step();
            }

            Assert.That(aquifer.Level, Is.EqualTo(1000f));
        }

        [Test]
        public void IndustrialSeepageMixesIntoTheLayerByVolume()
        {
            var aquifer = new Aquifer(1000f, 100f);

            aquifer.Seep(100f, 1f);

            Assert.That(aquifer.Pollution, Is.EqualTo(0.5f).Within(1e-5f), "helft van het water is vuil");
            Assert.That(aquifer.Level, Is.EqualTo(200f));
        }

        [Test]
        public void ASeepageInAnEmptyLayerSetsThePollutionAndIsClampedToTheCapacity()
        {
            var empty = new Aquifer(100f, 0f);
            empty.Seep(50f, 0.4f);
            Assert.That(empty.Pollution, Is.EqualTo(0.4f).Within(1e-5f));

            var full = new Aquifer(100f, 100f);
            full.Seep(50f, 1f);
            Assert.That(full.Level, Is.EqualTo(100f));
            Assert.That(full.Pollution, Is.Zero, "er past niets bij");

            var none = new Aquifer(100f, 0f);
            none.Seep(0f, 1f);
            Assert.That(none.Pollution, Is.Zero);
        }

        [Test]
        public void PollutionRecoversButVerySlowly()
        {
            var aquifer = new Aquifer(1000f, 500f);
            aquifer.Seep(500f, 1f);
            var before = aquifer.Pollution;

            aquifer.Step();

            Assert.That(aquifer.Pollution, Is.LessThan(before));
            Assert.That(aquifer.Pollution, Is.GreaterThan(before * 0.99f));
        }

        [Test]
        public void PollutedGroundwaterGivesWorseDrinkingWater()
        {
            var aquifer = new Aquifer(1000f, 100f);
            aquifer.Seep(100f, 1f);

            Assert.That(WaterQuality.Intake(WaterSource.Groundwater, aquifer.Pollution), Is.LessThan(1f));
        }

        [Test]
        public void BadInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Aquifer(0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Aquifer(10f, 5f).Extract(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Aquifer(10f, 5f).Seep(-1f, 0f));
            Assert.That(new Aquifer(10f, 50f).Level, Is.EqualTo(10f), "startniveau begrensd");
            Assert.That(new Aquifer(10f, 5f).Fill, Is.EqualTo(0.5f));
        }
    }
}
