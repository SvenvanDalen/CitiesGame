using System;
using Cities.Gameplay.Visuals;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Visuals
{
    public sealed class ParticlePoolTests
    {
        [Test]
        public void EmitUsesASlotAndExpiresAfterItsLifetime()
        {
            var pool = new ParticlePool();
            var slot = pool.Emit(ParticleKind.Sparks, new float3(1f, 2f, 3f));

            Assert.That(slot, Is.EqualTo(0));
            Assert.That(pool.IsAlive(ParticleKind.Sparks, slot), Is.True);
            Assert.That(pool.PositionOf(ParticleKind.Sparks, slot), Is.EqualTo(new float3(1f, 2f, 3f)));
            Assert.That(pool.ActiveCount(ParticleKind.Sparks), Is.EqualTo(1));

            pool.Update(ParticlePool.Lifetime(ParticleKind.Sparks) / 2f);
            Assert.That(pool.Progress(ParticleKind.Sparks, slot), Is.EqualTo(0.5f).Within(0.001f));

            pool.Update(ParticlePool.Lifetime(ParticleKind.Sparks));
            Assert.That(pool.IsAlive(ParticleKind.Sparks, slot), Is.False);
            Assert.That(pool.TotalActive(), Is.EqualTo(0));
        }

        [Test]
        public void ExpiredSlotsAreReused()
        {
            var pool = new ParticlePool();
            var first = pool.Emit(ParticleKind.Smoke, float3.zero);
            pool.Release(ParticleKind.Smoke, first);

            Assert.That(pool.Emit(ParticleKind.Smoke, float3.zero), Is.EqualTo(first));
        }

        [Test]
        public void FullPoolRefusesOrOverwritesTheOldest()
        {
            var pool = new ParticlePool { Density = 0.05f };
            var capacity = pool.Capacity(ParticleKind.Dust);
            Assert.That(capacity, Is.EqualTo(5));

            for (var i = 0; i < capacity; i++)
            {
                Assert.That(pool.Emit(ParticleKind.Dust, new float3(i, 0f, 0f)), Is.GreaterThanOrEqualTo(0));
                pool.Update(0.1f);
            }

            Assert.That(pool.Emit(ParticleKind.Dust, float3.zero), Is.EqualTo(-1));
            var slot = pool.Emit(ParticleKind.Dust, new float3(99f, 0f, 0f), true);
            Assert.That(slot, Is.EqualTo(0));
            Assert.That(pool.PositionOf(ParticleKind.Dust, slot).x, Is.EqualTo(99f));
            Assert.That(pool.ActiveCount(ParticleKind.Dust), Is.EqualTo(capacity));
        }

        [Test]
        public void ZeroDensityAllowsNothingEvenWhenOverwriting()
        {
            var pool = new ParticlePool { Density = 0f };

            Assert.That(pool.Emit(ParticleKind.Fire, float3.zero), Is.EqualTo(-1));
            Assert.That(pool.Emit(ParticleKind.Fire, float3.zero, true), Is.EqualTo(-1));
        }

        [Test]
        public void DensityIsClamped()
        {
            var pool = new ParticlePool { Density = 4f };
            Assert.That(pool.Density, Is.EqualTo(1f));
            pool.Density = -1f;
            Assert.That(pool.Density, Is.EqualTo(0f));
        }

        [Test]
        public void InactiveSlotsAndBadArgumentsAreRejectedOrIgnored()
        {
            var pool = new ParticlePool();

            Assert.That(pool.IsAlive(ParticleKind.Steam, -1), Is.False);
            Assert.That(pool.IsAlive(ParticleKind.Steam, 10_000), Is.False);
            Assert.Throws<ArgumentException>(() => pool.PositionOf(ParticleKind.Steam, 0));
            Assert.Throws<ArgumentException>(() => pool.Progress(ParticleKind.Steam, 0));
            Assert.DoesNotThrow(() => pool.Release(ParticleKind.Steam, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Update(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Emit((ParticleKind)9, float3.zero));
        }

        [Test]
        public void EveryKindHasALifetime()
        {
            for (var k = 0; k < 5; k++)
            {
                Assert.That(ParticlePool.Lifetime((ParticleKind)k), Is.GreaterThan(0f));
            }
        }
    }

    public sealed class PropScatterTests
    {
        private static float Full(float2 p) => 1f;

        [Test]
        public void PlacementIsDeterministicAndInsideTheArea()
        {
            var a = PropScatter.Fill(3, new float2(0f, 0f), new float2(100f, 100f), Full);
            var b = PropScatter.Fill(3, new float2(0f, 0f), new float2(100f, 100f), Full);

            Assert.That(a.Count, Is.EqualTo(b.Count));
            Assert.That(a.Count, Is.GreaterThan(300));
            for (var i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].Position, Is.EqualTo(b[i].Position));
                Assert.That(a[i].Position.x, Is.InRange(0f, 100f));
                Assert.That(a[i].Position.y, Is.InRange(0f, 100f));
                Assert.That(a[i].Scale, Is.InRange(0.8f, 1.3f));
                Assert.That(a[i].Rotation, Is.InRange(0f, 2f * math.PI));
            }
        }

        [Test]
        public void TilesFillTheSameAsOneBigArea()
        {
            var whole = PropScatter.Fill(9, new float2(0f, 0f), new float2(80f, 40f), Full);
            var left = PropScatter.Fill(9, new float2(0f, 0f), new float2(40f, 40f), Full);
            var right = PropScatter.Fill(9, new float2(40f, 0f), new float2(80f, 40f), Full);

            Assert.That(left.Count + right.Count, Is.EqualTo(whole.Count));
        }

        [Test]
        public void DensityMapSteersWhereThingsGrow()
        {
            var props = PropScatter.Fill(1, new float2(0f, 0f), new float2(200f, 100f), p => p.x < 100f ? 1f : 0f);

            Assert.That(props.Count, Is.GreaterThan(0));
            foreach (var prop in props)
            {
                Assert.That(prop.Position.x, Is.LessThan(100f));
            }
        }

        [Test]
        public void LowQualityRemovesAStableSubset()
        {
            var high = PropScatter.Fill(5, new float2(0f, 0f), new float2(100f, 100f), Full, 1f);
            var low = PropScatter.Fill(5, new float2(0f, 0f), new float2(100f, 100f), Full, 0.3f);
            var none = PropScatter.Fill(5, new float2(0f, 0f), new float2(100f, 100f), Full, 0f);
            var positions = new System.Collections.Generic.HashSet<float2>();
            foreach (var prop in high)
            {
                positions.Add(prop.Position);
            }

            Assert.That(low.Count, Is.LessThan(high.Count));
            Assert.That(low.Count, Is.GreaterThan(0));
            Assert.That(none.Count, Is.EqualTo(0));
            foreach (var prop in low)
            {
                Assert.That(positions, Does.Contain(prop.Position));
            }
        }

        [Test]
        public void AllKindsAppearAndGrassIsMostCommon()
        {
            var props = PropScatter.Fill(2, new float2(0f, 0f), new float2(400f, 400f), Full);
            var counts = new int[4];
            foreach (var prop in props)
            {
                counts[(int)prop.Kind]++;
            }

            Assert.That(counts, Has.All.GreaterThan(0));
            Assert.That(counts[(int)PropKind.Grass], Is.GreaterThan(counts[(int)PropKind.Tree]));
        }

        [Test]
        public void NegativeCoordinatesWorkAndBadInputIsRejected()
        {
            Assert.That(PropScatter.Fill(1, new float2(-50f, -50f), new float2(0f, 0f), Full).Count, Is.GreaterThan(50));
            Assert.Throws<ArgumentNullException>(() => PropScatter.Fill(1, float2.zero, new float2(1f, 1f), null));
            Assert.Throws<ArgumentException>(() => PropScatter.Fill(1, new float2(10f, 10f), new float2(0f, 0f), Full));
        }
    }

    public sealed class InstanceBatcherTests
    {
        [Test]
        public void GroupsByKeyAndSplitsAtTheInstancingLimit()
        {
            var batcher = new InstanceBatcher();
            for (var i = 0; i < 2_500; i++)
            {
                batcher.Add(1, float4x4.Translate(new float3(i, 0f, 0f)));
            }

            for (var i = 0; i < 10; i++)
            {
                batcher.Add(2, float4x4.identity);
            }

            var batches = batcher.Build();

            Assert.That(batcher.InstanceCount, Is.EqualTo(2_510));
            Assert.That(batcher.DrawCalls(), Is.EqualTo(4));
            Assert.That(batches.Count, Is.EqualTo(4));
            var total = 0;
            foreach (var batch in batches)
            {
                Assert.That(batch.Count, Is.InRange(1, InstanceBatcher.MaxPerBatch));
                total += batch.Count;
            }

            Assert.That(total, Is.EqualTo(2_510));
            Assert.That(batches[0].Matrices[0].c3.x, Is.EqualTo(0f));
            Assert.That(batches[1].Matrices[0].c3.x, Is.EqualTo(1023f));
        }

        [Test]
        public void ClearAndRebuildReusesBuffers()
        {
            var batcher = new InstanceBatcher();
            batcher.Add(7, float4x4.identity);
            var first = batcher.Build()[0].Matrices;

            batcher.Clear();
            Assert.That(batcher.InstanceCount, Is.EqualTo(0));
            Assert.That(batcher.DrawCalls(), Is.EqualTo(0));
            Assert.That(batcher.Build().Count, Is.EqualTo(0));

            batcher.Add(7, float4x4.identity);
            Assert.That(batcher.Build()[0].Matrices, Is.SameAs(first));
        }

        [Test]
        public void ExactlyTheLimitIsOneBatch()
        {
            var batcher = new InstanceBatcher();
            for (var i = 0; i < InstanceBatcher.MaxPerBatch; i++)
            {
                batcher.Add(3, float4x4.identity);
            }

            Assert.That(batcher.DrawCalls(), Is.EqualTo(1));
            batcher.Add(3, float4x4.identity);
            Assert.That(batcher.DrawCalls(), Is.EqualTo(2));
        }
    }
}
