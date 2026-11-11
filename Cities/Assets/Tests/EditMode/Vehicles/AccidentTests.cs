using System;
using Cities.Core.Persistence;
using Cities.Simulation.Services;
using Cities.Simulation.Vehicles;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Vehicles
{
    public class AccidentTests
    {
        private static AccidentModel Busy(ServiceDispatcher dispatcher = null, ulong seed = 7UL)
        {
            var model = new AccidentModel(seed, dispatcher) { Enabled = true };
            for (ulong step = 0; step < 20_000 && model.Active.Count == 0; step++)
            {
                model.Check(1UL, new float2(5f, 5f), 1f, step);
            }

            return model;
        }

        [Test]
        public void NothingHappensWhenDisabledOrQuiet()
        {
            var off = new AccidentModel(7UL);
            var quiet = new AccidentModel(7UL) { Enabled = true };
            for (ulong step = 0; step < 5_000; step++)
            {
                Assert.That(off.Check(1UL, float2.zero, 1f, step), Is.Null);
                Assert.That(quiet.Check(1UL, float2.zero, 0.1f, step), Is.Null);
            }
        }

        [Test]
        public void BusyRoadEventuallyBlocksAndClearsAgain()
        {
            var model = Busy();

            Assert.That(model.Active.Count, Is.EqualTo(1));
            Assert.That(model.IsBlocked(1UL), Is.True);
            Assert.That(model.SpeedFactor(1UL), Is.EqualTo(0f));
            Assert.That(model.SpeedFactor(2UL), Is.EqualTo(1f));
            Assert.That(model.Check(1UL, float2.zero, 1f, 99_999UL), Is.Null);
            Assert.That(model.Tick(AccidentModel.ClearSeconds + 1f).Count, Is.EqualTo(1));
            Assert.That(model.IsBlocked(1UL), Is.False);
        }

        [Test]
        public void OutcomeIsDeterministic()
        {
            Assert.That(Busy(null, 3UL).Active[0].Remaining, Is.EqualTo(Busy(null, 3UL).Active[0].Remaining));
            Assert.That(Busy(null, 3UL).NextId, Is.EqualTo(Busy(null, 3UL).NextId));
        }

        [Test]
        public void EmergencyServicesSpeedUpTheCleanup()
        {
            var dispatcher = new ServiceDispatcher(10f);
            dispatcher.Add(new ServiceStation(ServiceKind.Police, new float2(5f, 5f), 100f, 1, 0));
            var model = Busy(dispatcher);

            Assert.That(model.Active[0].Responder, Is.Not.Null);
            Assert.That(model.Active[0].Responder.Handler, Is.Not.Null);
            model.Tick(AccidentModel.ClearSeconds / 2f + 1f);
            Assert.That(model.Active.Count, Is.EqualTo(0));
        }

        [Test]
        public void SectionRoundTrips()
        {
            var model = Busy();
            var bytes = SaveBytes.Write(new SaveMetadata(), new AccidentsSection(model));

            var loaded = new AccidentModel(0UL);
            SaveBytes.Read(bytes, new AccidentsSection(loaded));

            Assert.That(loaded.Enabled, Is.True);
            Assert.That(loaded.Seed, Is.EqualTo(7UL));
            Assert.That(loaded.IsBlocked(1UL), Is.True);
            Assert.That(SaveBytes.Write(new SaveMetadata(), new AccidentsSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new AccidentsSection(loaded).Id, Is.EqualTo("ACCI"));
            Assert.Throws<ArgumentNullException>(() => new AccidentsSection(null));
        }
    }
}
