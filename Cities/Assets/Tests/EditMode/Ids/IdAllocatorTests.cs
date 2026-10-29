using System;
using System.Collections.Generic;
using Cities.Core.Ids;
using NUnit.Framework;
using Unity.Collections;

namespace Cities.Tests.EditMode.Ids
{
    /// <summary>Gebruikt Collections-containers en draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class IdAllocatorTests
    {
        private IdAllocator<CitizenKind> _allocator;

        [SetUp]
        public void SetUp() => _allocator = new IdAllocator<CitizenKind>(4, Allocator.Persistent);

        [TearDown]
        public void TearDown() => _allocator.Dispose();

        [Test]
        public void AllocatesSequentialIndicesWithGenerationOne()
        {
            var a = _allocator.Allocate();
            var b = _allocator.Allocate();

            Assert.That(a, Is.EqualTo(new EntityId<CitizenKind>(0, 1)));
            Assert.That(b, Is.EqualTo(new EntityId<CitizenKind>(1, 1)));
            Assert.That(_allocator.AliveCount, Is.EqualTo(2));
            Assert.That(_allocator.SlotCount, Is.EqualTo(2));
        }

        [Test]
        public void ReleasedIdBecomesStaleAndSlotIsReusedWithNewGeneration()
        {
            var first = _allocator.Allocate();

            Assert.That(_allocator.Release(first), Is.True);
            Assert.That(_allocator.IsAlive(first), Is.False);

            var reused = _allocator.Allocate();
            Assert.That(reused.Index, Is.EqualTo(first.Index));
            Assert.That(reused.Generation, Is.GreaterThan(first.Generation));
            Assert.That(_allocator.IsAlive(reused), Is.True);
            Assert.That(_allocator.IsAlive(first), Is.False, "Oude verwijzing blijft ongeldig na hergebruik");
        }

        [Test]
        public void FreeSlotsAreReusedInReleaseOrder()
        {
            var ids = new List<EntityId<CitizenKind>>();
            for (var i = 0; i < 4; i++)
            {
                ids.Add(_allocator.Allocate());
            }

            _allocator.Release(ids[2]);
            _allocator.Release(ids[0]);

            Assert.That(_allocator.Allocate().Index, Is.EqualTo(2));
            Assert.That(_allocator.Allocate().Index, Is.EqualTo(0));
            Assert.That(_allocator.Allocate().Index, Is.EqualTo(4));
        }

        [Test]
        public void ReleasingTwiceOrInvalidIdsDoesNothing()
        {
            var id = _allocator.Allocate();
            _allocator.Release(id);

            Assert.That(_allocator.Release(id), Is.False);
            Assert.That(_allocator.Release(EntityId<CitizenKind>.None), Is.False);
            Assert.That(_allocator.Release(new EntityId<CitizenKind>(99, 1)), Is.False);
            Assert.That(_allocator.Release(new EntityId<CitizenKind>(0, 2)), Is.False, "Even generatie is nooit een levend ID");
            Assert.That(_allocator.AliveCount, Is.EqualTo(0));
        }

        [Test]
        public void GetAliveAtReturnsCurrentIdOrNone()
        {
            var a = _allocator.Allocate();
            var b = _allocator.Allocate();
            _allocator.Release(a);

            Assert.That(_allocator.GetAliveAt(0), Is.EqualTo(EntityId<CitizenKind>.None));
            Assert.That(_allocator.GetAliveAt(1), Is.EqualTo(b));
            Assert.That(_allocator.GetAliveAt(-1), Is.EqualTo(EntityId<CitizenKind>.None));
            Assert.That(_allocator.GetAliveAt(2), Is.EqualTo(EntityId<CitizenKind>.None));
        }

        [Test]
        public void ManyCyclesKeepCountsConsistent()
        {
            var alive = new List<EntityId<CitizenKind>>();
            var random = new Random(42);
            for (var step = 0; step < 10_000; step++)
            {
                if (alive.Count > 0 && random.Next(3) == 0)
                {
                    var victim = random.Next(alive.Count);
                    Assert.That(_allocator.Release(alive[victim]), Is.True);
                    alive.RemoveAt(victim);
                }
                else
                {
                    alive.Add(_allocator.Allocate());
                }
            }

            Assert.That(_allocator.AliveCount, Is.EqualTo(alive.Count));
            Assert.That(alive.TrueForAll(_allocator.IsAlive), Is.True);
            Assert.That(new HashSet<int>(alive.ConvertAll(id => id.Index)).Count, Is.EqualTo(alive.Count), "Geen twee levende ID's op dezelfde plek");
            Assert.That(_allocator.RetiredCount, Is.EqualTo(0));
        }

        [Test]
        public void NegativeCapacityIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new IdAllocator<CitizenKind>(-1, Allocator.Temp));
        }

        [Test]
        public void DisposeReleasesMemory()
        {
            var allocator = new IdAllocator<VehicleKind>(1, Allocator.Persistent);
            Assert.That(allocator.IsCreated, Is.True);

            allocator.Dispose();
            allocator.Dispose();

            Assert.That(allocator.IsCreated, Is.False);
        }
    }
}
