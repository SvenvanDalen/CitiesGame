using System;
using System.Collections.Generic;
using System.IO;
using Cities.Core.Persistence;
using Cities.World.Grid;
using Cities.World.Maps;
using Cities.World.Terrain;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Maps
{
    public class MapDocumentTests
    {
        // 2×2 tegels van 16 cellen van 10 m: 320 m.
        private static readonly WorldDimensions Dimensions = new WorldDimensions(2, 2, 16, 10f);

        private static MapDocument NewMap(float baseHeight = 5f) => new MapDocument(Dimensions, "Testkaart", baseHeight);

        [Test]
        public void NewMapHasTheBaseHeightEverywhere()
        {
            var map = NewMap(7f);

            Assert.That(map.CornersX, Is.EqualTo(33));
            Assert.That(map.HeightAt(0, 0), Is.EqualTo(7f));
            Assert.That(map.HeightAt(32, 32), Is.EqualTo(7f));
            Assert.That(map.Heights.Length, Is.EqualTo(33 * 33));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.HeightAt(33, 0));
        }

        [Test]
        public void RaiseAndLowerChangeTheCenterMostAndNotOutsideTheRadius()
        {
            var map = NewMap(0f);

            map.Paint(TerraformMode.Raise, 160f, 160f, 50f, 10f);

            Assert.That(map.HeightAt(16, 16), Is.EqualTo(10f).Within(0.001f));
            Assert.That(map.HeightAt(18, 16), Is.GreaterThan(0f).And.LessThan(10f));
            Assert.That(map.HeightAt(24, 16), Is.EqualTo(0f));

            map.Paint(TerraformMode.Lower, 160f, 160f, 50f, 10f);
            Assert.That(map.HeightAt(16, 16), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void FlattenMovesTowardsTheTargetWithoutOvershoot()
        {
            var map = NewMap(0f);
            map.Paint(TerraformMode.Raise, 160f, 160f, 50f, 20f);

            map.Paint(TerraformMode.Flatten, 160f, 160f, 50f, 100f, 3f);

            Assert.That(map.HeightAt(16, 16), Is.EqualTo(3f));
        }

        [Test]
        public void SmoothReducesASpike()
        {
            var map = NewMap(0f);
            map.SetHeight(16, 16, 20f);

            map.Paint(TerraformMode.Smooth, 160f, 160f, 30f, 100f);

            Assert.That(map.HeightAt(16, 16), Is.LessThan(20f));
            Assert.That(map.HeightAt(15, 16), Is.GreaterThan(0f));
        }

        [Test]
        public void HeightsAreClampedToTheTerrainLimits()
        {
            var map = NewMap(0f);

            map.SetHeight(1, 1, 5000f);
            map.SetHeight(2, 2, -5000f);

            Assert.That(map.HeightAt(1, 1), Is.EqualTo(TerrainBrush.MaxHeight));
            Assert.That(map.HeightAt(2, 2), Is.EqualTo(TerrainBrush.MinHeight));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.Paint(TerraformMode.Raise, 0f, 0f, 0f, 1f));
        }

        [Test]
        public void WaterFillsOnlyBelowTheLevelAndCanBeCleared()
        {
            var map = NewMap(2f);
            map.SetHeight(16, 16, -3f);

            map.FillWater(160f, 160f, 40f, 0f);

            Assert.That(map.WaterDepthAt(16, 16), Is.EqualTo(3f));
            Assert.That(map.WaterDepthAt(17, 16), Is.EqualTo(0f), "Terrein boven het peil blijft droog");

            map.ClearWater(160f, 160f, 40f);
            Assert.That(map.WaterDepthAt(16, 16), Is.EqualTo(0f));
        }

        [Test]
        public void ResourcesAreValidatedAndRemovedByCell()
        {
            var map = NewMap();

            Assert.That(map.PlaceResource(new MapResource(ResourceKind.Oil, 10, 10, 4, 0.8f), out var reason), Is.True, reason);
            Assert.That(map.PlaceResource(new MapResource(ResourceKind.Ore, -1, 10, 4, 0.8f), out reason), Is.False);
            Assert.That(reason, Does.Contain("kaart"));
            Assert.That(map.PlaceResource(new MapResource(ResourceKind.Ore, 5, 5, 0, 0.8f), out reason), Is.False);
            Assert.That(map.PlaceResource(new MapResource(ResourceKind.Ore, 5, 5, 99, 0.8f), out reason), Is.False);
            Assert.That(map.PlaceResource(new MapResource(ResourceKind.Ore, 5, 5, 3, 0f), out reason), Is.False);
            Assert.That(map.PlaceResource(new MapResource(ResourceKind.Ore, 5, 5, 3, 1.5f), out reason), Is.False);

            Assert.That(map.RemoveResourcesAt(12, 11), Is.EqualTo(1));
            Assert.That(map.Resources, Is.Empty);
        }

        [Test]
        public void ConnectionsMustBeOnTheEdgeAndKeepTheirDistance()
        {
            var map = NewMap();

            Assert.That(map.AddConnection(new MapConnection(MapEdge.South, 5, ConnectionKind.Road), out _), Is.True);
            Assert.That(map.AddConnection(new MapConnection(MapEdge.South, 7, ConnectionKind.Rail), out var reason), Is.False);
            Assert.That(reason, Does.Contain("dicht"));
            Assert.That(map.AddConnection(new MapConnection(MapEdge.East, 7, ConnectionKind.Rail), out _), Is.True);
            Assert.That(map.AddConnection(new MapConnection(MapEdge.North, 99, ConnectionKind.Road), out reason), Is.False);
            Assert.That(map.AddConnection(new MapConnection(MapEdge.West, -1, ConnectionKind.Road), out _), Is.False);

            Assert.That(map.RemoveConnection(new MapConnection(MapEdge.East, 7, ConnectionKind.Rail)), Is.True);
            Assert.That(map.Connections, Has.Count.EqualTo(1));
        }

        [Test]
        public void ConnectionPositionsSitOnTheMapBorder()
        {
            var map = NewMap();

            Assert.That(map.PositionOf(new MapConnection(MapEdge.South, 2, ConnectionKind.Road)), Is.EqualTo((25f, 0f)));
            Assert.That(map.PositionOf(new MapConnection(MapEdge.North, 2, ConnectionKind.Road)), Is.EqualTo((25f, 320f)));
            Assert.That(map.PositionOf(new MapConnection(MapEdge.West, 2, ConnectionKind.Road)), Is.EqualTo((0f, 25f)));
            Assert.That(map.PositionOf(new MapConnection(MapEdge.East, 2, ConnectionKind.Road)), Is.EqualTo((320f, 25f)));
        }

        [Test]
        public void ValidationNeedsNameRoadAndSensibleWaterConnections()
        {
            var map = new MapDocument(Dimensions, " ");
            var errors = new List<string>();
            Assert.That(map.Validate(errors), Is.False);
            Assert.That(errors, Has.Some.Contains("naam"));
            Assert.That(errors, Has.Some.Contains("weg"));

            map.Name = "Eiland";
            map.AddConnection(new MapConnection(MapEdge.South, 5, ConnectionKind.Road), out _);
            map.AddConnection(new MapConnection(MapEdge.North, 5, ConnectionKind.Waterway), out _);
            errors.Clear();
            Assert.That(map.Validate(errors), Is.False, "De waterverbinding ligt op droog land");
            Assert.That(errors, Has.Some.Contains("op het droge"));

            map.SetHeight(5, 32, -5f);
            map.SetHeight(6, 32, -5f);
            map.FillWater(55f, 320f, 15f, 0f);
            errors.Clear();
            Assert.That(map.Validate(errors), Is.True, string.Join("; ", errors));
        }

        [Test]
        public void RoadInTheWaterIsReported()
        {
            var map = NewMap(-5f);
            map.FillWater(55f, 0f, 20f, 0f);
            map.AddConnection(new MapConnection(MapEdge.South, 5, ConnectionKind.Highway), out _);
            var errors = new List<string>();

            Assert.That(map.Validate(errors), Is.False);
            Assert.That(errors, Has.Some.Contains("in het water"));
        }

        [Test]
        public void SaveAndLoadRoundTripEverything()
        {
            var map = NewMap(1f);
            map.Paint(TerraformMode.Raise, 100f, 200f, 60f, 15f);
            map.FillWater(10f, 10f, 40f, 3f);
            map.PlaceResource(new MapResource(ResourceKind.FertileGround, 8, 9, 5, 0.6f), out _);
            map.AddConnection(new MapConnection(MapEdge.West, 12, ConnectionKind.Highway), out _);
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                map.Save(stream, new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));
                bytes = stream.ToArray();
            }

            MapDocument loaded;
            using (var stream = new MemoryStream(bytes))
            {
                loaded = MapDocument.Load(stream);
            }

            Assert.That(loaded.Name, Is.EqualTo("Testkaart"));
            Assert.That(loaded.Dimensions, Is.EqualTo(Dimensions));
            Assert.That(loaded.Heights.ToArray(), Is.EqualTo(map.Heights.ToArray()));
            Assert.That(loaded.WaterDepths.ToArray(), Is.EqualTo(map.WaterDepths.ToArray()));
            Assert.That(loaded.Resources, Is.EqualTo(map.Resources));
            Assert.That(loaded.Connections, Is.EqualTo(map.Connections));
        }

        [Test]
        public void BrokenFilesAreRejected()
        {
            var map = NewMap();
            byte[] bytes;
            using (var stream = new MemoryStream())
            {
                map.Save(stream, DateTime.UtcNow);
                bytes = stream.ToArray();
            }

            bytes[bytes.Length - 3] ^= 0xFF;
            Assert.Throws<SaveFormatException>(() => MapDocument.Load(new MemoryStream(bytes)));
            Assert.Throws<SaveFormatException>(() => MapDocument.Load(new MemoryStream(new byte[] { 1, 2, 3 })));
        }

        [Test]
        public void WaterDepthCannotBeNegative()
        {
            var map = NewMap();

            map.SetWaterDepth(1, 1, -4f);
            map.SetWaterDepth(2, 2, 3f);

            Assert.That(map.WaterDepthAt(1, 1), Is.EqualTo(0f));
            Assert.That(map.WaterDepthAt(2, 2), Is.EqualTo(3f));
        }

        [Test]
        public void StructsCompareByValue()
        {
            Assert.That(new MapConnection(MapEdge.South, 1, ConnectionKind.Air).Equals((object)new MapConnection(MapEdge.South, 1, ConnectionKind.Air)), Is.True);
            Assert.That(new MapResource(ResourceKind.Oil, 1, 2, 3, 0.5f).GetHashCode(), Is.EqualTo(new MapResource(ResourceKind.Oil, 1, 2, 3, 0.5f).GetHashCode()));
            Assert.That(new MapResource(ResourceKind.Oil, 1, 2, 3, 0.5f).Equals((object)"x"), Is.False);
            Assert.That(new MapConnection(MapEdge.South, 1, ConnectionKind.Air).GetHashCode(), Is.EqualTo(new MapConnection(MapEdge.South, 1, ConnectionKind.Air).GetHashCode()));
        }
    }
}
