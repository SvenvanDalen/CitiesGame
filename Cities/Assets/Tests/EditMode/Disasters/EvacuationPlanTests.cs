using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Disasters;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class EvacuationPlanTests
    {
        private static EvacuationPlan TwoShelters()
        {
            var plan = new EvacuationPlan { Buses = 1 };
            plan.AddShelter(new float2(0f, 0f), 2);
            plan.AddShelter(new float2(100f, 0f), 10);
            return plan;
        }

        [Test]
        public void PeopleGoToTheNearestShelterWithRoom()
        {
            var plan = TwoShelters();
            var people = new[]
            {
                new Evacuee(3, new float2(30f, 0f), false),
                new Evacuee(1, new float2(10f, 0f), true),
                new Evacuee(2, new float2(20f, 0f), false),
            };

            var result = plan.Evacuate(people);

            Assert.That(result.Evacuated, Is.EqualTo(3));
            Assert.That(result.Routes.Select(r => (r.EvacueeId, r.ShelterId)), Is.EqualTo(new[] { (1, 1), (2, 1), (3, 2) }), "op id, derde moet naar de volgende");
            Assert.That(result.Routes[2].Distance, Is.EqualTo(70f));
            Assert.That(result.Routes.Select(r => r.ByBus), Is.EqualTo(new[] { false, true, true }));
            Assert.That(plan.Shelters[0].Occupants, Is.EqualTo(2));
            Assert.That(plan.Shelters[1].Occupants, Is.EqualTo(1));
            Assert.That(plan.FreePlaces, Is.EqualTo(9));
        }

        [Test]
        public void PeopleWithoutAShelterWithRoomAreReported()
        {
            var plan = new EvacuationPlan();
            plan.AddShelter(float2.zero, 1);

            var result = plan.Evacuate(new[] { new Evacuee(1, float2.zero, true), new Evacuee(2, float2.zero, true) });

            Assert.That(result.Unplaced, Is.EqualTo(new[] { 2 }));
            Assert.That(result.Evacuated, Is.EqualTo(1));
            Assert.That(new EvacuationPlan().Evacuate(new[] { new Evacuee(1, float2.zero, true) }).Unplaced, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void TiesGoToTheLowestShelterId()
        {
            var plan = new EvacuationPlan();
            plan.AddShelter(new float2(-10f, 0f), 5);
            plan.AddShelter(new float2(10f, 0f), 5);

            var result = plan.Evacuate(new[] { new Evacuee(1, float2.zero, true) });

            Assert.That(result.Routes[0].ShelterId, Is.EqualTo(1));
        }

        [Test]
        public void BusesSetHowManyDaysTheEvacuationTakes()
        {
            var plan = new EvacuationPlan { Buses = 1 };

            Assert.That(plan.BusLiftPerDay, Is.EqualTo(EvacuationPlan.BusCapacity * EvacuationPlan.TripsPerBusPerDay));
            Assert.That(plan.BusDaysFor(0), Is.Zero);
            Assert.That(plan.BusDaysFor(240), Is.EqualTo(1));
            Assert.That(plan.BusDaysFor(241), Is.EqualTo(2));
            plan.Buses = 0;
            Assert.That(plan.BusDaysFor(1), Is.EqualTo(int.MaxValue), "zonder bussen komt niemand weg");
            Assert.Throws<ArgumentOutOfRangeException>(() => plan.Buses = -1);
        }

        [Test]
        public void TheResultReportsTheBusDaysForPeopleWithoutATransportOfTheirOwn()
        {
            var plan = new EvacuationPlan { Buses = 1 };
            plan.AddShelter(float2.zero, 1000);
            var people = Enumerable.Range(1, 241).Select(i => new Evacuee(i, float2.zero, false)).ToList();

            Assert.That(plan.Evacuate(people).BusDays, Is.EqualTo(2));
        }

        [Test]
        public void SheltersEmptyAgainAndCanBeRemoved()
        {
            var plan = TwoShelters();
            plan.Evacuate(new[] { new Evacuee(1, float2.zero, true) });

            plan.ClearOccupants();

            Assert.That(plan.FreePlaces, Is.EqualTo(plan.TotalCapacity));
            Assert.That(plan.RemoveShelter(1), Is.True);
            Assert.That(plan.RemoveShelter(1), Is.False);
            Assert.That(plan.Shelters, Has.Count.EqualTo(1));
        }

        [Test]
        public void BadInputIsRejected()
        {
            var plan = new EvacuationPlan();

            Assert.Throws<ArgumentOutOfRangeException>(() => plan.AddShelter(float2.zero, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Shelter(1, float2.zero, 5, 6));
            Assert.Throws<ArgumentNullException>(() => plan.Evacuate(null));
        }

        [Test]
        public void TheSectionRoundTripsDeterministically()
        {
            var plan = TwoShelters();
            plan.Buses = 3;
            plan.Evacuate(new List<Evacuee> { new Evacuee(1, float2.zero, true) });
            var bytes = SaveBytes.Write(new SaveMetadata(), new EvacuationSection(plan));

            var loaded = new EvacuationPlan();
            SaveBytes.Read(bytes, new EvacuationSection(loaded));

            Assert.That(loaded.Buses, Is.EqualTo(3));
            Assert.That(loaded.NextId, Is.EqualTo(3));
            Assert.That(loaded.Shelters[0].Occupants, Is.EqualTo(1));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new EvacuationSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new EvacuationSection(loaded).Id, Is.EqualTo("EVAC"));
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new EvacuationSection(null));
            var section = new EvacuationSection(new EvacuationPlan());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 0, 0, 1, 5, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, -1, 1, 5, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 0, 9, 5, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 0, 1, 0, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 0, 1, 5, 6)), 1));
        }

        private static void Corrupt(SaveWriter w, int nextId, int buses, int id, int capacity, int occupants)
        {
            w.Write(nextId);
            w.Write(buses);
            w.Write(1);
            w.Write(id);
            w.Write(0f);
            w.Write(0f);
            w.Write(capacity);
            w.Write(occupants);
        }
    }
}
