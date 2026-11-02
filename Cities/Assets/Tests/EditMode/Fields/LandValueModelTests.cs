using Cities.Simulation.Fields;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Fields
{
    public class LandValueModelTests
    {
        [Test]
        public void EverythingPresentAndCleanIsWorthOne()
        {
            Assert.That(LandValueModel.Value(new LandValueFactors(1f, 1f, 1f, 1f, 0f)), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(LandValueModel.Value(new LandValueFactors(0f, 0f, 0f, 0f, 0f)), Is.Zero);
        }

        [Test]
        public void ServicesWeighMoreThanParks()
        {
            var services = LandValueModel.Value(new LandValueFactors(1f, 0f, 0f, 0f, 0f));
            var parks = LandValueModel.Value(new LandValueFactors(0f, 1f, 0f, 0f, 0f));

            Assert.That(services, Is.GreaterThan(parks));
        }

        [Test]
        public void PollutionLowersTheValueButNotToZero()
        {
            var clean = LandValueModel.Value(new LandValueFactors(1f, 1f, 1f, 1f, 0f));
            var dirty = LandValueModel.Value(new LandValueFactors(1f, 1f, 1f, 1f, 1f));

            Assert.That(dirty, Is.EqualTo(clean * 0.25f).Within(1e-5f));
        }

        [Test]
        public void FactorsAreClamped()
        {
            Assert.That(LandValueModel.Value(new LandValueFactors(5f, 5f, 5f, 5f, -3f)), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void RentRisesWithValueAndIsClamped()
        {
            Assert.That(LandValueModel.Rent(0f), Is.EqualTo(10f).Within(1e-4f));
            Assert.That(LandValueModel.Rent(1f), Is.EqualTo(100f).Within(1e-4f));
            Assert.That(LandValueModel.Rent(0.5f), Is.GreaterThan(LandValueModel.Rent(0.2f)));
            Assert.That(LandValueModel.Rent(9f), Is.EqualTo(100f).Within(1e-4f));
            Assert.That(LandValueModel.Rent(-1f), Is.EqualTo(10f).Within(1e-4f));
        }
    }
}
