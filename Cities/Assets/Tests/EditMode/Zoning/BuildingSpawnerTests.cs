using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.Gameplay.Zoning;
using Cities.Simulation.Network;
using Cities.World.Buildings;
using Cities.World.Grid;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Cities.Tests.EditMode.Zoning
{
    /// <summary>Wegennet en kaart staan in native geheugen; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class BuildingSpawnerTests
    {
        private static readonly WorldDimensions World = new WorldDimensions(2, 2, 16, 8f);

        private WorldMap _map;
        private RoadNetwork _network;
        private ZoneService _zones;
        private FootprintOccupancy _occupancy;
        private BuildingDefinition _house;
        private GameObject _prefab;
        private List<(Footprint Footprint, float3 Position)> _placed;

        [SetUp]
        public void SetUp()
        {
            _map = new WorldMap(World);
            _network = new RoadNetwork(8);
            var a = _network.AddNode(new float3(128f, 0f, 0f));
            var b = _network.AddNode(new float3(128f, 0f, 255f));
            _network.AddSegment(a, b, Bezier3.Straight(new float3(128f, 0f, 0f), new float3(128f, 0f, 255f)), LaneProfiles.Road(1, bikeLanes: false, sidewalks: true), 13.9f);
            _zones = new ZoneService(_map, _network);
            _occupancy = new FootprintOccupancy();
            _placed = new List<(Footprint, float3)>();
            _prefab = new GameObject("house");
            _house = ScriptableObject.CreateInstance<BuildingDefinition>();
            var spec = new BuildingSpec { FootprintWidth = 2, FootprintDepth = 2, Zone = ZoneType.Residential, Density = ZoneDensity.Low, Level = 1, Households = 1 };
            Set("_spec", spec);
            Set("_prefab", _prefab);
        }

        private void Set(string field, object value) =>
            typeof(BuildingDefinition).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_house, value);

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_prefab);
            Object.DestroyImmediate(_house);
            _network.Dispose();
            _map.Dispose();
        }

        [Test]
        public void BuildingsGrowOnZonedCellsFaceTheRoadAndNeverOverlap()
        {
            _zones.Paint(new RectInt(17, 10, 4, 12), ZoneType.Residential, ZoneDensity.Low);
            var spawner = Create(_ => 1f);

            var grown = spawner.Grow(400);

            Assert.That(grown, Is.GreaterThan(3));
            Assert.That(_placed, Has.Count.EqualTo(grown));
            foreach (var (footprint, _) in _placed)
            {
                Assert.That(footprint.QuarterTurns, Is.EqualTo(1), "de weg ligt ten westen: de voorkant wijst naar -X");
                foreach (var other in _placed)
                {
                    Assert.That(ReferenceEquals(other.Footprint, footprint) || other.Footprint == footprint || !other.Footprint.Overlaps(footprint), Is.True);
                }
            }

            Assert.That(_placed.Count, Is.EqualTo(_occupancy.Placed.Count));
        }

        [Test]
        public void WithoutDemandOrZonesNothingGrows()
        {
            var spawner = Create(_ => 1f);
            Assert.That(spawner.Grow(50), Is.Zero, "geen zones");

            _zones.Paint(new RectInt(17, 10, 4, 4), ZoneType.Residential, ZoneDensity.Low);
            Assert.That(Create(_ => 0f).Grow(50), Is.Zero, "geen vraag");
            Assert.That(Create(_ => -1f).Grow(50), Is.Zero, "negatieve vraag");
        }

        [Test]
        public void OnlyMatchingZoneTypeAndDensityGetABuilding()
        {
            _zones.Paint(new RectInt(17, 10, 4, 4), ZoneType.Commercial, ZoneDensity.Low);
            _zones.Paint(new RectInt(17, 20, 4, 4), ZoneType.Residential, ZoneDensity.High);

            Assert.That(Create(_ => 1f).Grow(200), Is.Zero);
        }

        [Test]
        public void ErasedZonesAreForgotten()
        {
            _zones.Paint(new RectInt(17, 10, 4, 4), ZoneType.Residential, ZoneDensity.Low);
            var spawner = Create(_ => 1f);
            var before = spawner.ZonedCells;
            _zones.Clear(new RectInt(17, 10, 4, 4));

            Assert.That(before, Is.GreaterThan(0));
            Assert.That(spawner.ZonedCells, Is.Zero);
            Assert.That(spawner.Grow(50), Is.Zero);
        }

        private BuildingSpawner Create(System.Func<ZoneType, float> demand) =>
            new BuildingSpawner(_map, _zones, _occupancy, new[] { _house }, demand, Place, seed: 7);

        private void Place(BuildingDefinition definition, Footprint footprint, float3 position)
        {
            _occupancy.Add(footprint);
            _placed.Add((footprint, position));
        }
    }
}
