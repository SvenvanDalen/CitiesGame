using System;
using Cities.Core.Randomness;
using Cities.Simulation.Citizens;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Citizens
{
    public class CitizenHealthModelTests
    {
        private static readonly HealthEnvironment Clean = new HealthEnvironment(0f, 0f, 0f, true, 0f);

        private static LifeRules Rules(float death = 0f, float oldAge = 0f) => new LifeRules(18, 67, 45, 0f, death, oldAge);

        [Test]
        public void PollutionAndAgeLowerTheTargetAndCareRaisesIt()
        {
            Assert.That(CitizenHealthModel.TargetHealth(30, Clean), Is.EqualTo(100));
            Assert.That(CitizenHealthModel.TargetHealth(30, new HealthEnvironment(1f, 1f, 1f, true, 0f)), Is.EqualTo(55));
            Assert.That(CitizenHealthModel.TargetHealth(120, Clean), Is.EqualTo(85));
            Assert.That(CitizenHealthModel.TargetHealth(30, new HealthEnvironment(0.5f, 0f, 0f, false, 0f)), Is.EqualTo(85));
            Assert.That(CitizenHealthModel.TargetHealth(120, new HealthEnvironment(1f, 1f, 1f, false, 0f)), Is.EqualTo(20));
        }

        [Test]
        public void TheEnvironmentIsClamped()
        {
            var env = new HealthEnvironment(5f, -1f, 2f, false, -3f);

            Assert.That(env.Air, Is.EqualTo(1f));
            Assert.That(env.Water, Is.EqualTo(0f));
            Assert.That(env.Soil, Is.EqualTo(1f));
            Assert.That(env.BedLoad, Is.EqualTo(0f));
            Assert.That(env.HasCare, Is.False);
        }

        [Test]
        public void HealthMovesAtMostOneStepPerYearTowardsTheTarget()
        {
            var env = new HealthEnvironment(1f, 1f, 1f, false, 0f);
            var citizen = new Citizen { Age = 30, Health = 100 };

            CitizenHealthModel.UpdateHealth(ref citizen, env);
            Assert.That(citizen.Health, Is.EqualTo(90));

            citizen.Health = 52;
            CitizenHealthModel.UpdateHealth(ref citizen, env);
            Assert.That(citizen.Health, Is.EqualTo(45), "doel 45 is binnen de stap bereikt");

            citizen.Health = 10;
            CitizenHealthModel.UpdateHealth(ref citizen, Clean);
            Assert.That(citizen.Health, Is.EqualTo(20), "herstel is ook begrensd");
        }

        [Test]
        public void MissingCareOrFullBedsRaiseTheDeathChance()
        {
            var citizen = new Citizen { Age = 80, Health = 100 };
            var rules = Rules(0.01f);
            var baseline = rules.DeathChance(80, 100);

            Assert.That(CitizenHealthModel.DeathChance(rules, citizen, Clean), Is.EqualTo(baseline).Within(1e-6f));
            Assert.That(CitizenHealthModel.DeathChance(rules, citizen, new HealthEnvironment(0, 0, 0, false, 0f)), Is.EqualTo(baseline * 2f).Within(1e-6f));
            Assert.That(CitizenHealthModel.DeathChance(rules, citizen, new HealthEnvironment(0, 0, 0, true, 1.5f)), Is.EqualTo(baseline * 1.5f).Within(1e-6f));
            Assert.That(CitizenHealthModel.CareFactor(new HealthEnvironment(0, 0, 0, true, 9f)), Is.EqualTo(CitizenHealthModel.MaxCareFactor));
            Assert.That(CitizenHealthModel.CareFactor(new HealthEnvironment(0, 0, 0, true, float.PositiveInfinity)), Is.EqualTo(CitizenHealthModel.MaxCareFactor));
            Assert.Throws<ArgumentNullException>(() => CitizenHealthModel.DeathChance(null, citizen, Clean));
        }

        [Test]
        public void WithoutCareMoreCitizensDieThanWithCare()
        {
            var rules = Rules(0.1f);
            var noCare = new HealthEnvironment(0, 0, 0, false, 0f);
            var randomA = new DeterministicRandom(7);
            var randomB = new DeterministicRandom(7);
            int diedA = 0, diedB = 0;
            for (var i = 0; i < 2000; i++)
            {
                var a = new Citizen { Age = 40, Health = 100 };
                var b = a;
                diedA += CitizenHealthModel.Yearly(ref a, rules, noCare, ref randomA) == LifeEvent.Died ? 1 : 0;
                diedB += CitizenHealthModel.Yearly(ref b, rules, Clean, ref randomB) == LifeEvent.Died ? 1 : 0;
            }

            Assert.That(diedA, Is.GreaterThan(diedB));
            Assert.That(diedB, Is.GreaterThan(0));
        }

        [Test]
        public void YearlyFollowsTheNormalLifeCycleWhenSafe()
        {
            var random = new DeterministicRandom(1);
            var citizen = new Citizen { Age = 17, Health = 100 };

            Assert.That(CitizenHealthModel.Yearly(ref citizen, Rules(), Clean, ref random), Is.EqualTo(LifeEvent.GrewUp));
            Assert.That(citizen.Age, Is.EqualTo(18));

            var doomed = new Citizen { Age = Citizen.MaxAge, Health = 100 };
            Assert.That(CitizenHealthModel.Yearly(ref doomed, Rules(), Clean, ref random), Is.EqualTo(LifeEvent.Died));
        }

        [Test]
        public void PatientsFillTheBedsOfHealthServices()
        {
            var dispatcher = new ServiceDispatcher(10f);
            Assert.That(CitizenHealthModel.Beds(dispatcher), Is.EqualTo(0));
            dispatcher.Add(HealthModel.Clinic(float2.zero));
            dispatcher.Add(HealthModel.Hospital(float2.zero));
            dispatcher.Add(new ServiceStation(ServiceKind.Fire, float2.zero, 100f, 3, 0));

            var beds = CitizenHealthModel.Beds(dispatcher);
            Assert.That(beds, Is.EqualTo(5 * CitizenHealthModel.BedsPerVehicle), "de brandweer telt niet mee");

            var citizens = new[] { new Citizen { Health = 10 }, new Citizen { Health = 39 }, new Citizen { Health = 40 }, new Citizen { Health = 90 } };
            var patients = CitizenHealthModel.Patients(citizens);
            Assert.That(patients, Is.EqualTo(2));
            Assert.That(CitizenHealthModel.BedLoad(patients, beds), Is.EqualTo(2f / beds).Within(1e-6f));
            Assert.That(CitizenHealthModel.BedLoad(0, 0), Is.EqualTo(0f));
            Assert.That(CitizenHealthModel.BedLoad(1, 0), Is.EqualTo(float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => CitizenHealthModel.Patients(null));
            Assert.Throws<ArgumentNullException>(() => CitizenHealthModel.Beds(null));
        }
    }
}
