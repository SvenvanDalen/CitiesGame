using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Transit;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Transit
{
    public class WaterTrafficTests
    {
        private sealed class FakeWater : IWaterDepthSource
        {
            /// <summary>Zee (10 m) voor y kleiner dan nul; een ondiepe bank (1 m) tussen x = 2000 en 2100; verder land.</summary>
            public float DepthAt(float2 position)
            {
                if (position.y > 0f)
                {
                    return 0f;
                }

                return position.x >= 2000f && position.x <= 2100f ? 1f : 10f;
            }
        }

        private TransitNetwork _network;
        private WaterNetwork _water;

        [SetUp]
        public void SetUp()
        {
            _network = new TransitNetwork();
            _water = new WaterNetwork(_network, new FakeWater());
        }

        [Test]
        public void AShipNeedsWaterDeeperThanItsDraughtPlusClearance()
        {
            Assert.That(WaterTraffic.RequiredDepth(VesselKind.Ferry), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(WaterTraffic.IsDepthOk(VesselKind.Ferry, 3f), Is.True);
            Assert.That(WaterTraffic.IsDepthOk(VesselKind.Ferry, 2.9f), Is.False);
            Assert.That(WaterTraffic.IsDepthOk(VesselKind.CargoShip, 8f), Is.False);
            Assert.That(WaterTraffic.IsDepthOk(VesselKind.CargoShip, 8.5f), Is.True);
        }

        [Test]
        public void VesselPropertiesDifferPerKind()
        {
            Assert.That(WaterTraffic.Beam(VesselKind.CargoShip), Is.GreaterThan(WaterTraffic.Beam(VesselKind.Ferry)));
            Assert.That(WaterTraffic.Speed(VesselKind.Ferry), Is.GreaterThan(WaterTraffic.Speed(VesselKind.CargoShip)));
            Assert.That(WaterTraffic.Capacity(VesselKind.CargoShip), Is.GreaterThan(WaterTraffic.Capacity(VesselKind.Ferry)));
            Assert.That(WaterTraffic.DwellTime(VesselKind.CargoShip), Is.GreaterThan(WaterTraffic.DwellTime(VesselKind.Ferry)));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.Draught((VesselKind)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.Beam((VesselKind)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.Speed((VesselKind)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.Capacity((VesselKind)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.DwellTime((VesselKind)9));
        }

        [Test]
        public void ACanalMustBeDeepAndWideEnoughForTheVessel()
        {
            Assert.That(WaterTraffic.CanalAdmits(VesselKind.Ferry, 30f, 3f), Is.True);
            Assert.That(WaterTraffic.CanalAdmits(VesselKind.Ferry, 29f, 3f), Is.False, "te smal om te passeren");
            Assert.That(WaterTraffic.CanalAdmits(VesselKind.Ferry, 30f, 2f), Is.False, "te ondiep");
            Assert.That(WaterTraffic.CanalAdmits(VesselKind.CargoShip, 30f, 9f), Is.False, "vrachtschip is 30 m breed");
            Assert.That(WaterTraffic.CanalAdmits(VesselKind.CargoShip, 90f, 9f), Is.True);
            Assert.That(WaterTraffic.IsCanalWidthOk(WaterTraffic.MinCanalWidth - 1f), Is.False);
            Assert.That(WaterTraffic.IsCanalWidthOk(WaterTraffic.MaxCanalWidth + 1f), Is.False);
            Assert.That(WaterTraffic.IsCanalDepthOk(0f), Is.False);
            Assert.That(WaterTraffic.IsCanalDepthOk(WaterTraffic.MaxCanalDepth + 1f), Is.False);
        }

        [Test]
        public void SailingIsFasterThanTheCanalAndHasADetour()
        {
            Assert.That(WaterTraffic.SailTime(VesselKind.Ferry, 800f), Is.EqualTo(800f * WaterTraffic.RouteDetour / 8f).Within(1e-3f));
            Assert.That(WaterTraffic.CanalTime(300f), Is.EqualTo(100f).Within(1e-3f));
            Assert.That(WaterTraffic.SailTime(VesselKind.Ferry, 0f), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.SailTime(VesselKind.Ferry, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.CanalTime(float.PositiveInfinity));
        }

        [Test]
        public void HandlingTakesAtLeastTheFixedDwellTime()
        {
            Assert.That(WaterTraffic.HandlingTime(0f), Is.EqualTo(WaterTraffic.DwellTime(VesselKind.CargoShip)));
            Assert.That(WaterTraffic.HandlingTime(5000f), Is.EqualTo(5000f / WaterTraffic.CraneRate).Within(1e-2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.HandlingTime(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.HandlingTime(5001f));
        }

        [Test]
        public void DailyCapacityGrowsWithShipsAndShrinksWithRoundTripTime()
        {
            var one = WaterTraffic.DailyCapacity(VesselKind.Ferry, 1, 3600f);
            Assert.That(one, Is.EqualTo(250f * 24f).Within(1e-2f));
            Assert.That(WaterTraffic.DailyCapacity(VesselKind.Ferry, 3, 3600f), Is.EqualTo(3f * one).Within(1e-2f));
            Assert.That(WaterTraffic.DailyCapacity(VesselKind.Ferry, 1, 7200f), Is.EqualTo(one / 2f).Within(1e-2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.DailyCapacity(VesselKind.Ferry, 0, 3600f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WaterTraffic.DailyCapacity(VesselKind.Ferry, 1, 0f));
        }

        [Test]
        public void APortNeedsDeepWaterAndRoom()
        {
            var a = _water.AddPort(new float2(0f, -50f), VesselKind.Ferry);

            Assert.That(_water.IsPort(a), Is.True);
            Assert.That(_water.GetPort(a).Depth, Is.EqualTo(10f));
            Assert.That(_water.GetPort(a).Kind, Is.EqualTo(VesselKind.Ferry));
            Assert.That(_network.HasStop(a), Is.True);
            Assert.Throws<ArgumentException>(() => _water.AddPort(new float2(500f, 50f), VesselKind.Ferry), "land");
            Assert.Throws<ArgumentException>(() => _water.AddPort(new float2(2050f, -10f), VesselKind.Ferry), "ondiepte");
            Assert.Throws<ArgumentException>(() => _water.AddPort(new float2(50f, -50f), VesselKind.Ferry), "te dichtbij");
            Assert.That(_network.Stops.Count(), Is.EqualTo(1), "een mislukte haven laat geen halte achter");
            Assert.Throws<ArgumentException>(() => _water.GetPort(99));
            Assert.That(_water.Network, Is.SameAs(_network));
            Assert.Throws<ArgumentNullException>(() => new WaterNetwork(null, new FakeWater()));
            Assert.Throws<ArgumentNullException>(() => new WaterNetwork(_network, null));
            Assert.Throws<ArgumentNullException>(() => new WaterSection(null));
        }

        [Test]
        public void AFerryLineSailsOverOpenWater()
        {
            var a = _water.AddPort(new float2(0f, -50f), VesselKind.Ferry);
            var b = _water.AddPort(new float2(1000f, -50f), VesselKind.Ferry);

            var line = _water.AddLine("F1", new[] { a, b }, false, 2, 600f);

            Assert.That(_water.KindOf(line), Is.EqualTo(VesselKind.Ferry));
            Assert.That(_water.LegTime(a, b, VesselKind.Ferry), Is.EqualTo(WaterTraffic.SailTime(VesselKind.Ferry, 1000f)).Within(1e-3f));
            var sailing = 2f * WaterTraffic.SailTime(VesselKind.Ferry, 1000f);
            Assert.That(_water.RoundTripTime(line), Is.EqualTo(sailing + 2f * WaterTraffic.DwellTime(VesselKind.Ferry)).Within(1e-2f));
            Assert.That(_water.DailyCapacity(line), Is.EqualTo(WaterTraffic.DailyCapacity(VesselKind.Ferry, 2, _water.RoundTripTime(line))).Within(1e-2f));
        }

        [Test]
        public void ALineCannotCrossAShoalOrMixKinds()
        {
            var a = _water.AddPort(new float2(0f, -50f), VesselKind.Ferry);
            var far = _water.AddPort(new float2(4000f, -50f), VesselKind.Ferry);
            var cargo = _water.AddPort(new float2(0f, -500f), VesselKind.CargoShip);

            Assert.That(_water.IsNavigable(a, far, VesselKind.Ferry), Is.False, "de bank ligt ertussen");
            Assert.Throws<ArgumentException>(() => _water.AddLine("F", new[] { a, far }, false, 1, 60f));
            Assert.Throws<ArgumentException>(() => _water.LegTime(a, far, VesselKind.Ferry));
            Assert.Throws<ArgumentException>(() => _water.AddLine("mix", new[] { a, cargo }, false, 1, 60f), "veer en vracht door elkaar");
            Assert.Throws<ArgumentNullException>(() => _water.CheckLine(null, false));
            Assert.DoesNotThrow(() => _water.CheckLine(Array.Empty<int>(), false));
        }

        [Test]
        public void ACanalLetsAShipPassWhereOpenWaterIsTooShallow()
        {
            var a = _water.AddPort(new float2(0f, -50f), VesselKind.CargoShip);
            var b = _water.AddPort(new float2(4000f, -50f), VesselKind.CargoShip);
            Assert.That(_water.IsNavigable(a, b, VesselKind.CargoShip), Is.False);

            var narrow = _water.AddCanal(a, b, 40f, 9f);
            Assert.That(_water.IsNavigable(a, b, VesselKind.CargoShip), Is.False, "40 m is te smal om te passeren");
            Assert.That(_water.RemoveCanal(narrow), Is.True);
            Assert.That(_water.RemoveCanal(narrow), Is.False);

            var canal = _water.AddCanal(b, a, 100f, 9f);
            Assert.That(_water.IsNavigable(a, b, VesselKind.CargoShip), Is.True);
            Assert.That(_water.LegTime(a, b, VesselKind.CargoShip), Is.EqualTo(WaterTraffic.CanalTime(4000f)).Within(1e-2f));
            Assert.That(_water.GetCanal(canal).Connects(a, b), Is.True);
            Assert.That(_water.TryRemovePort(a), Is.False, "nog een kanaal");
            Assert.Throws<ArgumentException>(() => _water.GetCanal(99));
        }

        [Test]
        public void ACanalNeedsTwoPortsAndSaneDimensions()
        {
            var a = _water.AddPort(new float2(0f, -50f), VesselKind.Ferry);
            var b = _water.AddPort(new float2(500f, -50f), VesselKind.Ferry);

            Assert.Throws<ArgumentException>(() => _water.AddCanal(a, 99, 50f, 5f));
            Assert.Throws<ArgumentException>(() => _water.AddCanal(a, a, 50f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _water.AddCanal(a, b, 5f, 5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _water.AddCanal(a, b, 50f, 0f));
            _water.AddCanal(a, b, 50f, 5f);
            Assert.Throws<ArgumentException>(() => _water.AddCanal(b, a, 50f, 5f), "dubbel kanaal");
        }

        [Test]
        public void ACargoLoopIncludesTheWayBackAndTheCranes()
        {
            var a = _water.AddPort(new float2(0f, -100f), VesselKind.CargoShip);
            var b = _water.AddPort(new float2(1000f, -100f), VesselKind.CargoShip);
            var c = _water.AddPort(new float2(500f, -1000f), VesselKind.CargoShip);

            var loop = _water.AddLine("ring", new[] { a, b, c }, true, 2, 1200f);

            var legs = _water.LegTime(a, b, VesselKind.CargoShip) + _water.LegTime(b, c, VesselKind.CargoShip) + _water.LegTime(c, a, VesselKind.CargoShip);
            Assert.That(_water.RoundTripTime(loop, 5000f), Is.EqualTo(legs + 3f * WaterTraffic.HandlingTime(5000f)).Within(1e-1f));
            Assert.That(_water.RoundTripTime(loop, 5000f), Is.GreaterThan(_water.RoundTripTime(loop, 0f)));
        }

        [Test]
        public void APortInUseCannotBeRemoved()
        {
            var a = _water.AddPort(new float2(0f, -50f), VesselKind.Ferry);
            var b = _water.AddPort(new float2(1000f, -50f), VesselKind.Ferry);
            _water.AddLine("F1", new[] { a, b }, false, 1, 60f);

            Assert.That(_water.TryRemovePort(a), Is.False);
            Assert.That(_water.TryRemovePort(99), Is.False);
            _network.RemoveLine(1);
            Assert.That(_water.TryRemovePort(a), Is.True);
            Assert.That(_water.IsPort(a), Is.False);
            Assert.That(_network.HasStop(a), Is.False);
        }

        [Test]
        public void TheSectionRoundTripsPortsAndCanals()
        {
            var a = _water.AddPort(new float2(0f, -50f), VesselKind.CargoShip);
            var b = _water.AddPort(new float2(1000f, -50f), VesselKind.Ferry);
            _water.AddCanal(a, b, 60f, 12f);
            var section = new WaterSection(_water);
            var bytes = SaveBytes.Write(new SaveMetadata(), section);

            var restored = new WaterNetwork(new TransitNetwork(), new FakeWater());
            restored.Restore(new[] { new Port(77, VesselKind.Ferry, 5f) }, new List<Canal>(), 9);
            SaveBytes.Read(bytes, new WaterSection(restored));

            Assert.That(restored.Ports.Select(p => (p.Stop, p.Kind, p.Depth)), Is.EqualTo(_water.Ports.Select(p => (p.Stop, p.Kind, p.Depth))));
            Assert.That(restored.Canals.Single().Width, Is.EqualTo(60f));
            Assert.That(restored.NextCanalId, Is.EqualTo(_water.NextCanalId));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new WaterSection(restored)), Is.EqualTo(bytes));
            Assert.That(section.Id, Is.EqualTo("WATR"));
            Assert.That(section.Version, Is.EqualTo(1));
        }

        [Test]
        public void TheSectionRejectsImpossibleData()
        {
            var section = new WaterSection(_water);

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1), "negatief aantal havens");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(1); w.Write(1); w.Write(7); w.Write(10f); }), 1), "onbekend soort");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(1); w.Write(1); w.Write(0); w.Write(1f); }), 1), "te ondiep");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(2); w.Write(1); w.Write(0); w.Write(10f); w.Write(1); w.Write(0); w.Write(10f); }), 1), "dubbele haven");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(0); w.Write(-1); }), 1), "negatief aantal kanalen");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { w.Write(0); w.Write(1); w.Write(1); w.Write(1); w.Write(2); w.Write(50f); w.Write(5f); }), 1),
                "kanaal zonder havens");
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(0); w.Write(0); w.Write(0); }), 1), "volgende ID te laag");
            Assert.That(_water.Ports, Is.Empty);
        }

        [Test]
        public void TheSectionRejectsBadCanals()
        {
            var section = new WaterSection(_water);
            void Ports(SaveWriter w)
            {
                w.Write(2);
                w.Write(1); w.Write(0); w.Write(10f);
                w.Write(2); w.Write(0); w.Write(10f);
            }

            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w => { Ports(w); w.Write(1); w.Write(1); w.Write(1); w.Write(2); w.Write(5f); w.Write(5f); w.Write(2); }), 1),
                "te smal");
            Assert.Throws<SaveFormatException>(
                () => section.Read(SaveBytes.ReaderFor(w =>
                {
                    Ports(w);
                    w.Write(2);
                    w.Write(1); w.Write(1); w.Write(2); w.Write(50f); w.Write(5f);
                    w.Write(1); w.Write(2); w.Write(1); w.Write(50f); w.Write(5f);
                    w.Write(2);
                }), 1),
                "dubbel kanaal");
        }
    }
}
