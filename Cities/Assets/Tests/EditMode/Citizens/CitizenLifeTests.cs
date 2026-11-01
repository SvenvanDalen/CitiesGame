using System;
using Cities.Core.Ids;
using Cities.Core.Randomness;
using Cities.Simulation.Citizens;
using Cities.World.Buildings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Citizens
{
    public class CitizenLifeTests
    {
        private static readonly EntityId<BuildingKind> Building = new EntityId<BuildingKind>(3, 1);

        // Sterfte 0 of 1 en geboorte 0 of 1 maken de uitkomst onafhankelijk van de random.
        private static LifeRules Rules(float birth = 0f, float death = 0f, float oldAge = 0f) => new LifeRules(18, 67, 45, birth, death, oldAge);

        [Test]
        public void ACitizenGrowsUpAndRetiresAtTheConfiguredAges()
        {
            var random = new DeterministicRandom(1);
            var child = new Citizen { Age = 17, Health = 100 };
            var worker = new Citizen { Age = 66, Health = 100, Work = Building };

            Assert.That(CitizenLife.AgeOneYear(ref child, Rules(), ref random), Is.EqualTo(LifeEvent.GrewUp));
            Assert.That(child.Group, Is.EqualTo(AgeGroup.Adult));
            Assert.That(CitizenLife.AgeOneYear(ref worker, Rules(), ref random), Is.EqualTo(LifeEvent.Retired));
            Assert.That(worker.HasWork, Is.False, "pensioen beëindigt het werk");
            Assert.That(CitizenLife.AgeOneYear(ref worker, Rules(), ref random), Is.EqualTo(LifeEvent.None));
            Assert.That(worker.Age, Is.EqualTo(68));
        }

        [Test]
        public void TheLimitsCanBeChanged()
        {
            var rules = new LifeRules(16, 60, 40, 0f, 0f, 0f);
            var random = new DeterministicRandom(1);
            var teen = new Citizen { Age = 15, Health = 100 };

            Assert.That(CitizenLife.AgeOneYear(ref teen, rules, ref random), Is.EqualTo(LifeEvent.GrewUp));
            Assert.That(rules.GroupOf(59), Is.EqualTo(AgeGroup.Adult));
            Assert.That(rules.GroupOf(60), Is.EqualTo(AgeGroup.Retired));
            Assert.That(rules.GroupOf(5), Is.EqualTo(AgeGroup.Child));
        }

        [Test]
        public void DeathChanceRisesWithAgeAndPoorHealth()
        {
            var rules = LifeRules.Default;

            Assert.That(rules.DeathChance(80, 100), Is.GreaterThan(rules.DeathChance(30, 100)));
            Assert.That(rules.DeathChance(30, 0), Is.EqualTo(rules.DeathChance(30, 100) * 2f).Within(1e-6f));
            Assert.That(rules.DeathChance(Citizen.MaxAge, 100), Is.EqualTo(1f));
            Assert.That(rules.DeathChance(40, 500), Is.EqualTo(rules.DeathChance(40, 100)), "gezondheid boven 100 telt niet extra");
        }

        [Test]
        public void ACitizenWithCertainDeathDiesAndStaysUnchanged()
        {
            var random = new DeterministicRandom(1);
            var citizen = new Citizen { Age = 50, Health = 100, Work = Building };

            Assert.That(CitizenLife.AgeOneYear(ref citizen, Rules(death: 1f), ref random), Is.EqualTo(LifeEvent.Died));
            Assert.That(citizen.Age, Is.EqualTo(50));
        }

        [Test]
        public void OnlyFertileAdultsWithAHomeHaveBabies()
        {
            var random = new DeterministicRandom(1);
            var parent = new Citizen { Age = 30, Home = Building, Household = 9 };

            Assert.That(CitizenLife.TryBirth(parent, Rules(birth: 1f), ref random, out var baby), Is.True);
            Assert.That(baby.Age, Is.Zero);
            Assert.That(baby.Home, Is.EqualTo(Building));
            Assert.That(baby.Household, Is.EqualTo(9u));
            Assert.That(baby.Health, Is.EqualTo(100));

            var old = parent;
            old.Age = 46;
            var child = parent;
            child.Age = 17;
            var homeless = parent;
            homeless.Home = default;
            Assert.That(CitizenLife.TryBirth(old, Rules(birth: 1f), ref random, out _), Is.False);
            Assert.That(CitizenLife.TryBirth(child, Rules(birth: 1f), ref random, out _), Is.False);
            Assert.That(CitizenLife.TryBirth(homeless, Rules(birth: 1f), ref random, out _), Is.False);
            Assert.That(CitizenLife.TryBirth(parent, Rules(birth: 0f), ref random, out _), Is.False);
        }

        [Test]
        public void ALifetimeIsDeterministicAndEndsBeforeTheMaximumAge()
        {
            static int Lifespan(ulong seed)
            {
                var random = new DeterministicRandom(seed);
                var citizen = new Citizen { Health = 80 };
                var ev = LifeEvent.None;
                while (ev != LifeEvent.Died)
                {
                    ev = CitizenLife.AgeOneYear(ref citizen, LifeRules.Default, ref random);
                }

                return citizen.Age;
            }

            Assert.That(Lifespan(7), Is.EqualTo(Lifespan(7)));
            Assert.That(Lifespan(7), Is.LessThanOrEqualTo(Citizen.MaxAge));
        }

        [Test]
        public void BadRulesAreRejected()
        {
            var citizen = new Citizen();

            Assert.Throws<ArgumentOutOfRangeException>(() => new LifeRules(0, 67, 45, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LifeRules(18, 18, 18, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LifeRules(18, 200, 45, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LifeRules(18, 67, 10, 0f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LifeRules(18, 67, 45, 1.5f, 0f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LifeRules(18, 67, 45, 0f, float.NaN, 0f));
            Assert.Throws<ArgumentNullException>(() =>
            {
                var local = new DeterministicRandom(1);
                CitizenLife.AgeOneYear(ref citizen, null, ref local);
            });
            Assert.Throws<ArgumentNullException>(() =>
            {
                var local = new DeterministicRandom(1);
                CitizenLife.TryBirth(citizen, null, ref local, out _);
            });
        }
    }
}
