using System.Collections.Generic;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Network;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Zoning
{
    /// <summary>Wegennet en kaart staan in native geheugen; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class ZoneServiceTests
    {
        private static readonly WorldDimensions World = new WorldDimensions(2, 2, 16, 8f);

        private WorldMap _map;
        private RoadNetwork _network;
        private ZoneService _zones;
        private Cities.Core.Ids.EntityId<Cities.Core.Ids.SegmentKind> _road;

        [SetUp]
        public void SetUp()
        {
            _map = new WorldMap(World);
            _network = new RoadNetwork(8);
            var a = _network.AddNode(new float3(128f, 0f, 0f));
            var b = _network.AddNode(new float3(128f, 0f, 255f));
            _road = _network.AddSegment(a, b, Bezier3.Straight(new float3(128f, 0f, 0f), new float3(128f, 0f, 255f)), LaneProfiles.Road(1, bikeLanes: false, sidewalks: true), 13.9f);
            _zones = new ZoneService(_map, _network);
        }

        [TearDown]
        public void TearDown()
        {
            _network.Dispose();
            _map.Dispose();
        }

        [Test]
        public void OnlyCellsAlongTheRoadAndNotOnItCanBeZoned()
        {
            Assert.That(_zones.Reason(new CellCoord(16, 10)), Is.Not.Null, "op het wegdek");
            Assert.That(_zones.Reason(new CellCoord(18, 10)), Is.Null, "langs de weg");
            Assert.That(_zones.Reason(new CellCoord(28, 10)), Does.Contain("Te ver"));
            Assert.That(_zones.Reason(new CellCoord(-1, 0)), Is.EqualTo("Buiten de kaart"));
        }

        [Test]
        public void PaintingZonesOnlyTheAllowedCellsAndReportsTheChange()
        {
            RectInt changed = default;
            _zones.ZonesChanged += r => changed = r;
            var area = new RectInt(10, 5, 20, 4);
            var allowed = 0;
            for (var z = 5; z < 9; z++)
            {
                for (var x = 10; x < 30; x++)
                {
                    allowed += _zones.Reason(new CellCoord(x, z)) == null ? 1 : 0;
                }
            }

            var count = _zones.Paint(area, ZoneType.Residential, ZoneDensity.High);

            Assert.That(count, Is.EqualTo(allowed));
            Assert.That(allowed, Is.GreaterThan(0).And.LessThan(80));
            Assert.That(_map.Zones.GetForCell(new CellCoord(18, 6)), Is.EqualTo(new ZoneCell(ZoneType.Residential, ZoneDensity.High)));
            Assert.That(_map.Zones.GetForCell(new CellCoord(16, 6)).IsZoned, Is.False, "op het wegdek blijft leeg");
            Assert.That(changed.width, Is.GreaterThan(0));
            Assert.That(_zones.Paint(area, ZoneType.Residential, ZoneDensity.High), Is.EqualTo(0), "niets te doen de tweede keer");
        }

        [Test]
        public void ClearRemovesTheZone()
        {
            _zones.Paint(new RectInt(18, 5, 2, 2), ZoneType.Commercial, ZoneDensity.Low);

            Assert.That(_zones.Clear(new RectInt(18, 5, 2, 2)), Is.EqualTo(4));
            Assert.That(_map.Zones.GetForCell(new CellCoord(18, 5)).IsZoned, Is.False);
        }

        [Test]
        public void RevalidateClearsZonesWhoseRoadIsGone()
        {
            var area = new RectInt(10, 5, 20, 4);
            var painted = _zones.Paint(area, ZoneType.Industrial, ZoneDensity.Low);
            _network.RemoveSegment(_road);

            Assert.That(_zones.Revalidate(area), Is.EqualTo(painted));
            Assert.That(_map.Zones.GetForCell(new CellCoord(18, 6)).IsZoned, Is.False);
        }

        [Test]
        public void ZonesCanBePaintedOverWithAnotherType()
        {
            _zones.Paint(new RectInt(18, 5, 1, 1), ZoneType.Residential, ZoneDensity.Low);
            _zones.Paint(new RectInt(18, 5, 1, 1), ZoneType.Office, ZoneDensity.High);

            Assert.That(_map.Zones.GetForCell(new CellCoord(18, 5)).Type, Is.EqualTo(ZoneType.Office));
        }
    }
}
