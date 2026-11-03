using System.Collections.Generic;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Policies;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Zoning
{
    public class ZoneSpecializationTests
    {
        private static PolicyBook Book() => new PolicyBook(new Dictionary<string, PolicySpec>
        {
            ["policy.eco"] = new PolicySpec
            {
                Scope = PolicyScope.District,
                Effects = new[] { new PolicyEffect { Key = ZoneSpecialization.EffectKey("eco"), Multiplier = 3f } },
            },
            ["policy.it"] = new PolicySpec
            {
                Scope = PolicyScope.City,
                Effects = new[] { new PolicyEffect { Key = ZoneSpecialization.EffectKey("it"), Multiplier = 0.5f } },
            },
        });

        [Test]
        public void OrdinaryBuildingsAlwaysHaveWeightOne()
        {
            Assert.That(ZoneSpecialization.Weight(null, null, 0), Is.EqualTo(1f));
            Assert.That(ZoneSpecialization.Weight(Book(), "", 2), Is.EqualTo(1f));
        }

        [Test]
        public void SpecializationIsOffWithoutPolicy()
        {
            Assert.That(ZoneSpecialization.Weight(null, "eco", 0), Is.Zero);
            Assert.That(ZoneSpecialization.Weight(Book(), "eco", 1), Is.Zero);
        }

        [Test]
        public void DistrictPolicyEnablesSpecializationOnlyThere()
        {
            var book = Book();
            book.Activate("policy.eco", 1);

            Assert.That(ZoneSpecialization.Weight(book, "eco", 1), Is.EqualTo(3f));
            Assert.That(ZoneSpecialization.Weight(book, "eco", 2), Is.Zero);
            Assert.That(ZoneSpecialization.Weight(book, "it", 1), Is.Zero);
        }

        [Test]
        public void FactorBelowOneDoesNotEnable()
        {
            var book = Book();
            book.Activate("policy.it");

            Assert.That(ZoneSpecialization.Weight(book, "it", 1), Is.Zero);
        }
    }
}
