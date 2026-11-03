using System;
using System.Collections.Generic;
using Cities.Simulation.Vehicles;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Vehicles
{
    public sealed class VehicleLodTests
    {
        private static VehiclePlacement At(float x) => new VehiclePlacement(new float2(x, 0));

        private static VehicleLod Lod(int maxFull = 100) => new VehicleLod(50f, 200f, 10f, maxFull);

        [Test]
        public void DetailFollowsDistance()
        {
            var lod = Lod();
            lod.Refresh(float2.zero, new[] { At(10), At(120), At(500) });

            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Full));
            Assert.That(lod.DetailOf(1), Is.EqualTo(VehicleDetail.Point));
            Assert.That(lod.DetailOf(2), Is.EqualTo(VehicleDetail.Hidden));
            Assert.That((lod.FullCount, lod.PointCount, lod.HiddenCount), Is.EqualTo((1, 1, 1)));
        }

        [Test]
        public void TheBorderDoesNotFlicker()
        {
            var lod = Lod();
            lod.Refresh(float2.zero, new[] { At(45) });
            lod.Refresh(float2.zero, new[] { At(55) });
            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Full));

            lod.Refresh(float2.zero, new[] { At(65) });
            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Point));

            lod.Refresh(float2.zero, new[] { At(55) });
            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Point), "terug naar volledig pas binnen de straal");
        }

        [Test]
        public void PointToHiddenHasHysteresisToo()
        {
            var lod = Lod();
            lod.Refresh(float2.zero, new[] { At(190) });
            lod.Refresh(float2.zero, new[] { At(205) });
            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Point));

            lod.Refresh(float2.zero, new[] { At(215) });
            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Hidden));

            lod.Refresh(float2.zero, new[] { At(205) });
            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Hidden));
        }

        [Test]
        public void TheNearestWinWhenFullModelsRunOut()
        {
            var lod = Lod(maxFull: 2);
            lod.Refresh(float2.zero, new[] { At(40), At(10), At(30), At(20) });

            Assert.That(lod.DetailOf(1), Is.EqualTo(VehicleDetail.Full));
            Assert.That(lod.DetailOf(3), Is.EqualTo(VehicleDetail.Full));
            Assert.That(lod.DetailOf(2), Is.EqualTo(VehicleDetail.Point));
            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Point));
            Assert.That(lod.FullCount, Is.EqualTo(2));
            Assert.That(lod.PointCount, Is.EqualTo(2));
        }

        [Test]
        public void EqualDistanceFallsBackOnTheLowestIndex()
        {
            var lod = Lod(maxFull: 1);
            lod.Refresh(float2.zero, new[] { At(20), At(-20) });

            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Full));
            Assert.That(lod.DetailOf(1), Is.EqualTo(VehicleDetail.Point));
        }

        [Test]
        public void InvalidSlotsAreIgnoredAndOutOfRangeIsHidden()
        {
            var lod = Lod();
            lod.Refresh(float2.zero, new[] { new VehiclePlacement(float2.zero, false), At(5) });

            Assert.That(lod.DetailOf(0), Is.EqualTo(VehicleDetail.Hidden));
            Assert.That(lod.DetailOf(1), Is.EqualTo(VehicleDetail.Full));
            Assert.That(lod.DetailOf(-1), Is.EqualTo(VehicleDetail.Hidden));
            Assert.That(lod.DetailOf(9), Is.EqualTo(VehicleDetail.Hidden));
            Assert.That(lod.HiddenCount, Is.EqualTo(0));
            Assert.That(lod.Count, Is.EqualTo(2));
        }

        [Test]
        public void RadiiAreNormalised()
        {
            var lod = new VehicleLod(100f, 10f, -5f, -1);

            Assert.That(lod.PointRadius, Is.EqualTo(100f));
            Assert.That(lod.FullRadius, Is.EqualTo(100f));
            Assert.That(lod.Margin, Is.EqualTo(0f));
            Assert.That(lod.MaxFull, Is.EqualTo(0));
        }

        [Test]
        public void ANullListIsRejected() =>
            Assert.Throws<ArgumentNullException>(() => Lod().Refresh(float2.zero, null));

        [Test]
        public void RefreshDoesNotAllocateOnceWarm()
        {
            var lod = Lod();
            var cars = new List<VehiclePlacement>();
            for (var i = 0; i < 500; i++)
            {
                cars.Add(At(i));
            }

            lod.Refresh(float2.zero, cars);
            lod.Refresh(new float2(1, 0), cars);
            var before = GC.GetAllocatedBytesForCurrentThread();
            lod.Refresh(new float2(3, 0), cars);
            var after = GC.GetAllocatedBytesForCurrentThread();

            Assert.That(after - before, Is.LessThanOrEqualTo(0));
        }

        [Test]
        public void ThePoolReusesReleasedViews()
        {
            var pool = new VehicleViewPool<object>(() => new object(), 2);
            var a = pool.Acquire();
            var b = pool.Acquire();
            Assert.That(pool.Created, Is.EqualTo(2));

            Assert.That(pool.Release(a), Is.True);
            Assert.That(pool.Release(b), Is.True);
            Assert.That(pool.Idle, Is.EqualTo(2));
            Assert.That(pool.Acquire(), Is.SameAs(b));
            Assert.That(pool.Created, Is.EqualTo(2));
        }

        [Test]
        public void AFullPoolDropsTheSurplus()
        {
            var pool = new VehicleViewPool<object>(() => new object(), 1);
            Assert.That(pool.Release(new object()), Is.True);
            Assert.That(pool.Release(new object()), Is.False);
            Assert.That(pool.Idle, Is.EqualTo(1));
        }

        [Test]
        public void ThePoolValidatesArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new VehicleViewPool<object>(null, 1));
            Assert.Throws<ArgumentNullException>(() => new VehicleViewPool<object>(() => new object(), 1).Release(null));
        }
    }
}
