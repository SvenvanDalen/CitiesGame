using System;
using System.Linq;
using Cities.Core.Randomness;
using Cities.Simulation.Services;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Services
{
    public class HealthModelTests
    {
        private static ServiceDispatcher WithClinic()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(HealthModel.Clinic(float2.zero));
            return dispatcher;
        }

        [Test]
        public void SicknessGrowsWithAge()
        {
            Assert.That(HealthModel.SickChance(0), Is.EqualTo(HealthModel.BaseSickPerDay));
            Assert.That(HealthModel.SickChance(60), Is.EqualTo(HealthModel.BaseSickPerDay * 3f).Within(1e-7f));
            Assert.That(HealthModel.SickChance(-5), Is.EqualTo(HealthModel.BaseSickPerDay));
        }

        [Test]
        public void HospitalsReachFurtherAndHaveMoreAmbulancesThanClinics()
        {
            var clinic = HealthModel.Clinic(float2.zero);
            var hospital = HealthModel.Hospital(float2.zero);

            Assert.That(hospital.Radius, Is.GreaterThan(clinic.Radius));
            Assert.That(hospital.Vehicles, Is.GreaterThan(clinic.Vehicles));
            Assert.That(hospital.UpkeepPerMonth, Is.GreaterThan(clinic.UpkeepPerMonth));
            Assert.That(hospital.Kind, Is.EqualTo(ServiceKind.Health));
        }

        [Test]
        public void ASickCitizenIsTreatedWhenTheAmbulanceArrives()
        {
            var dispatcher = WithClinic();
            var model = new HealthModel(dispatcher);
            model.Add(1, new float2(100f, 0f), 40);
            var random = new DeterministicRandom(1);

            Assert.That(model.Infect(1), Is.True);
            Assert.That(model.Infect(1), Is.False, "al ziek");
            Assert.That(model.StateOf(1), Is.EqualTo(PatientState.Sick));
            dispatcher.Tick(1000f);
            var died = model.Step(ref random);

            Assert.That(died, Is.Empty);
            Assert.That(model.StateOf(1), Is.EqualTo(PatientState.Healthy));
        }

        [Test]
        public void AnUntreatedCitizenDiesAndFreesTheReport()
        {
            var dispatcher = new ServiceDispatcher(10f);
            var model = new HealthModel(dispatcher);
            model.Add(1, new float2(100f, 0f), 80);
            var random = new DeterministicRandom(1);
            model.Infect(1);

            var died = Array.Empty<int>();
            for (var day = 0; day < HealthModel.UntreatedDays; day++)
            {
                Assert.That(model.StateOf(1), Is.EqualTo(PatientState.Sick));
                died = model.Step(ref random).ToArray();
            }

            Assert.That(died, Is.EqualTo(new[] { 1 }));
            Assert.That(model.StateOf(1), Is.EqualTo(PatientState.Dead));
            Assert.That(dispatcher.Open, Is.Empty);
            Assert.That(model.Infect(1), Is.False, "overleden");
        }

        [Test]
        public void HealthyCitizensFallSickByChanceAndNeverTheSameWayTwice()
        {
            var dispatcher = WithClinic();
            var model = new HealthModel(dispatcher);
            for (var i = 0; i < 2000; i++)
            {
                model.Add(i, new float2(10f, 0f), 90);
            }

            var random = new DeterministicRandom(5);
            model.Step(ref random);

            var sick = dispatcher.Open.Count;
            Assert.That(sick, Is.InRange(1, 100), "ongeveer 2000 x 0,008");
            var again = new HealthModel(WithClinic());
            for (var i = 0; i < 2000; i++)
            {
                again.Add(i, new float2(10f, 0f), 90);
            }

            var replay = new DeterministicRandom(5);
            again.Step(ref replay);
            for (var i = 0; i < 2000; i++)
            {
                Assert.That(again.StateOf(i), Is.EqualTo(model.StateOf(i)), "zelfde seed, zelfde zieken");
            }
        }

        [Test]
        public void BadInputIsRejected()
        {
            var model = new HealthModel(WithClinic());
            model.Add(1, float2.zero, 30);

            Assert.Throws<ArgumentNullException>(() => new HealthModel(null));
            Assert.Throws<ArgumentException>(() => model.Add(1, float2.zero, 30));
            Assert.Throws<ArgumentException>(() => model.StateOf(9));
        }
    }
}
