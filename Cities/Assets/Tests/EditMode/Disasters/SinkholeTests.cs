using System;
using Cities.Core.Persistence;
using Cities.Simulation.Disasters;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class SinkholeTests
    {
        private static readonly SinkholeBuilding[] Buildings =
        {
            new SinkholeBuilding(1, new float2(5f, 0f), 6f, 100_000),
            new SinkholeBuilding(2, new float2(40f, 0f), 6f, 200_000),
            new SinkholeBuilding(3, new float2(0f, 100f), 6f, 300_000),
        };

        private static readonly SinkholeSegment[] Segments =
        {
            new SinkholeSegment(10, new float2(-50f, 20f), new float2(50f, 20f)),
            new SinkholeSegment(11, new float2(-50f, 80f), new float2(50f, 80f)),
            new SinkholeSegment(12, new float2(3f, 3f), new float2(3f, 3f)),
        };

        [Test]
        public void TheHoleGrowsToItsMaximumAndStaysThere()
        {
            var hole = new Sinkhole(1, float2.zero, 30f, 10f);

            Assert.That(hole.Radius, Is.Zero);
            hole.Advance(Buildings, Segments);
            Assert.That(hole.Radius, Is.EqualTo(10f));
            Assert.That(hole.IsStable, Is.False);
            hole.Advance(Buildings, Segments);
            hole.Advance(Buildings, Segments);
            hole.Advance(Buildings, Segments);
            Assert.That(hole.Radius, Is.EqualTo(30f));
            Assert.That(hole.IsStable, Is.True);
        }

        [Test]
        public void BuildingsAndRoadsDisappearAsTheHoleReachesThem()
        {
            var hole = new Sinkhole(1, float2.zero, 50f, 10f);

            var first = hole.Advance(Buildings, Segments);
            CollectionAssert.AreEqual(new[] { 1 }, first.Buildings);
            CollectionAssert.AreEqual(new[] { 12 }, first.Segments);
            Assert.That(first.ValueLost, Is.EqualTo(100_000));

            var second = hole.Advance(Buildings, Segments);
            Assert.That(second.Buildings, Is.Empty);
            CollectionAssert.AreEqual(new[] { 10 }, second.Segments);
            Assert.That(second.RoadLengthLost, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(Sinkhole.RepairCost(second), Is.EqualTo(50_000));

            hole.Advance(Buildings, Segments);
            var fourth = hole.Advance(Buildings, Segments);
            Assert.That(fourth.Radius, Is.EqualTo(40f));
            CollectionAssert.AreEqual(new[] { 2 }, fourth.Buildings);
            CollectionAssert.AreEqual(new[] { 1, 2 }, hole.LostBuildings);
            Assert.That(hole.LostSegments, Has.No.Member(11));
        }

        [Test]
        public void WhatIsGoneIsNotLostTwice()
        {
            var hole = new Sinkhole(1, float2.zero, 50f, 50f);
            var first = hole.Advance(Buildings, Segments);
            var again = hole.Advance(Buildings, Segments);

            Assert.That(first.Buildings, Has.Count.EqualTo(2));
            Assert.That(again.Buildings, Is.Empty);
            Assert.That(again.Segments, Is.Empty);
            Assert.That(again.ValueLost, Is.Zero);
            Assert.That(hole.LostBuildings, Has.Count.EqualTo(2));
        }

        [Test]
        public void ContainsFollowsTheRadius()
        {
            var hole = new Sinkhole(1, new float2(10f, 10f), 20f, 20f);
            hole.Advance(Array.Empty<SinkholeBuilding>(), Array.Empty<SinkholeSegment>());

            Assert.That(hole.Contains(new float2(10f, 29f)), Is.True);
            Assert.That(hole.Contains(new float2(10f, 31f)), Is.False);
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Sinkhole(1, float2.zero, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Sinkhole(1, float2.zero, 1f, 0f));
            var hole = new Sinkhole(1, float2.zero, 5f, 1f);
            Assert.Throws<ArgumentNullException>(() => hole.Advance(null, Segments));
            Assert.Throws<ArgumentNullException>(() => hole.Advance(Buildings, null));
            Assert.Throws<ArgumentNullException>(() => Sinkhole.RepairCost(null));
        }

        [Test]
        public void ItIsAnnouncedAndCanBeRegisteredAsADisaster()
        {
            var manager = new DisasterManager();
            manager.Register(Sinkhole.Spec);

            var disaster = manager.Trigger("Zinkgat", new float2(1f, 2f));

            Assert.That(disaster.Phase, Is.EqualTo(DisasterPhase.Warning));
            Assert.That(Sinkhole.Spec.DurationDays, Is.EqualTo(3));
            manager.SetEnabled("Zinkgat", false);
            Assert.That(manager.IsEnabled("Zinkgat"), Is.False);
        }

        [Test]
        public void TheFieldStepsAllHolesAndIsLimited()
        {
            var field = new SinkholeField();
            field.Open(float2.zero, 50f, 10f);
            field.Open(new float2(0f, 100f), 20f, 20f);

            var outcomes = field.Step(Buildings, Segments);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 3 }, outcomes[1].Buildings);
            Assert.That(field.NextId, Is.EqualTo(3));

            var full = new SinkholeField();
            for (var i = 0; i < SinkholeField.MaxHoles; i++)
            {
                full.Open(float2.zero, 1f, 1f);
            }

            Assert.Throws<InvalidOperationException>(() => full.Open(float2.zero, 1f, 1f));
        }

        [Test]
        public void TheSectionRoundTripsDeterministically()
        {
            var field = new SinkholeField();
            field.Open(float2.zero, 50f, 10f);
            field.Open(new float2(0f, 100f), 20f, 20f);
            field.Step(Buildings, Segments);
            field.Step(Buildings, Segments);
            var bytes = SaveBytes.Write(new SaveMetadata(), new SinkholeSection(field));

            var loaded = new SinkholeField();
            SaveBytes.Read(bytes, new SinkholeSection(loaded));

            Assert.That(loaded.Holes, Has.Count.EqualTo(2));
            Assert.That(loaded.NextId, Is.EqualTo(3));
            Assert.That(loaded.Holes[0].AgeDays, Is.EqualTo(2));
            CollectionAssert.AreEqual(field.Holes[0].LostBuildings, loaded.Holes[0].LostBuildings);
            CollectionAssert.AreEqual(field.Holes[0].LostSegments, loaded.Holes[0].LostSegments);
            Assert.That(loaded.Holes[1].Center, Is.EqualTo(new float2(0f, 100f)));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new SinkholeSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new SinkholeSection(loaded).Id, Is.EqualTo("SINK"));
            Assert.That(loaded.Step(Buildings, Segments)[0].Buildings, Is.Empty);
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new SinkholeSection(null));
            var section = new SinkholeSection(new SinkholeField());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, -1, 1, 10f, 1f, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 9, 10f, 1f, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 1, 0f, 1f, 0)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 5, 1, 1, 10f, 1f, -2)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Corrupt(w, 0, 0, 1, 10f, 1f, 0)), 1));
        }

        private static void Corrupt(SaveWriter w, int nextId, int count, int id, float maxRadius, float growth, int lost)
        {
            w.Write(nextId);
            w.Write(count);
            if (count > 0)
            {
                w.Write(id);
                w.Write(0f);
                w.Write(0f);
                w.Write(maxRadius);
                w.Write(growth);
                w.Write(0);
                w.Write(lost);
            }
        }
    }
}
