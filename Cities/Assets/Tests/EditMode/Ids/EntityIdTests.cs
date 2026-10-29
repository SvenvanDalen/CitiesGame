using System;
using Cities.Core.Ids;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Ids
{
    public class EntityIdTests
    {
        [Test]
        public void DefaultIsInvalid()
        {
            Assert.That(default(EntityId<BuildingKind>).IsValid, Is.False);
            Assert.That(EntityId<BuildingKind>.None, Is.EqualTo(default(EntityId<BuildingKind>)));
            Assert.That(EntityId<BuildingKind>.None.ToString(), Is.EqualTo("None"));
        }

        [Test]
        public void PackedRoundTrips()
        {
            var id = new EntityId<CitizenKind>(123456, 789);

            var restored = EntityId<CitizenKind>.FromPacked(id.Packed);

            Assert.That(restored, Is.EqualTo(id));
            Assert.That(id.Packed, Is.EqualTo((789UL << 32) | 123456UL));
        }

        [Test]
        public void PackedWithNegativeIndexIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => EntityId<CitizenKind>.FromPacked(0xFFFFFFFFUL));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new EntityId<CitizenKind>(-1, 1));
        }

        [Test]
        public void GenerationDistinguishesReusedSlots()
        {
            var old = new EntityId<VehicleKind>(5, 1);
            var reused = new EntityId<VehicleKind>(5, 3);

            Assert.That(old == reused, Is.False);
            Assert.That(old != reused, Is.True);
            Assert.That(old.GetHashCode(), Is.Not.EqualTo(reused.GetHashCode()));
        }

        [Test]
        public void OrderingIsByIndexThenGeneration()
        {
            var a = new EntityId<NodeKind>(1, 9);
            var b = new EntityId<NodeKind>(2, 1);
            var c = new EntityId<NodeKind>(2, 3);

            Assert.That(a < b && b < c, Is.True);
            Assert.That(c > a && c >= b && a <= b, Is.True);
            Assert.That(b.CompareTo(new EntityId<NodeKind>(2, 1)), Is.EqualTo(0));
        }

        [Test]
        public void EqualityAndText()
        {
            var id = new EntityId<SegmentKind>(4, 7);

            Assert.That(id.Equals((object)new EntityId<SegmentKind>(4, 7)), Is.True);
            Assert.That(id.Equals("Segment#4v7"), Is.False);
            Assert.That(id.ToString(), Is.EqualTo("Segment#4v7"));
            Assert.That(id.IsValid, Is.True);
        }
    }
}
