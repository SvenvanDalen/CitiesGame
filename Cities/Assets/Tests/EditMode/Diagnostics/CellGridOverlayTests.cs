using System;
using System.Linq;
using Cities.Core.Diagnostics;
using Cities.World.Grid;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class CellGridOverlayTests
    {
        private static readonly WorldDimensions World = WorldDimensions.Default;

        [Test]
        public void CloseByEveryCellIsDrawnOnTheTerrain()
        {
            var geometry = Collect(altitude: 150f, focus: new float2(9216f, 9216f), new FlatTerrain(25f));

            Assert.That(geometry.Lines.Any(l => l.Color == DebugColor.Neutral), Is.True, "Cellen van 8 m");
            Assert.That(geometry.Lines.Any(l => l.Color == DebugColor.Info), Is.True, "Blokken van 32 m");
            Assert.That(geometry.Lines.All(l => Math.Abs(l.From.y - (25f + CellGridOverlay.Lift)) < 1e-4f), Is.True);
            var cellX = geometry.Lines.Where(l => l.Color == DebugColor.Neutral && l.From.x == l.To.x).Select(l => l.From.x).Distinct().ToList();
            Assert.That(cellX.All(x => Math.Abs(x % World.CellSize) < 1e-3f), Is.True, "Lijnen op celgrenzen");
            Assert.That(geometry.DroppedLines, Is.Zero);
        }

        [Test]
        public void HigherUpOnlyBlocksAndThenOnlyTiles()
        {
            var blocks = Collect(altitude: 1500f, focus: new float2(9216f, 9216f), FlatTerrain.Zero);
            var tiles = Collect(altitude: 8000f, focus: new float2(9216f, 9216f), FlatTerrain.Zero);

            Assert.That(blocks.Lines.Any(l => l.Color == DebugColor.Neutral), Is.False);
            Assert.That(blocks.Lines.Any(l => l.Color == DebugColor.Info), Is.True);
            Assert.That(tiles.Lines.All(l => l.Color == DebugColor.Accent), Is.True);

            // 10 tegelranden per richting over 18.432 m, in stukken van 64 m.
            var expected = 2 * (World.TilesX + 1) * (int)Math.Ceiling(World.SizeX / 64f);
            Assert.That(tiles.Lines.Count, Is.EqualTo(expected));
        }

        [Test]
        public void LinesFollowTheTerrain()
        {
            var slope = new SlopeTerrain();

            var geometry = Collect(altitude: 100f, focus: new float2(400f, 400f), slope);

            Assert.That(geometry.Lines.All(l => Math.Abs(l.From.y - (slope.SurfaceHeight(l.From.xz) + CellGridOverlay.Lift)) < 1e-3f), Is.True);
            Assert.That(geometry.Lines.Select(l => l.From.y).Distinct().Count(), Is.GreaterThan(10));
        }

        [Test]
        public void GridStaysInsideTheMapAtTheEdge()
        {
            var geometry = Collect(altitude: 200f, focus: new float2(5f, 5f), FlatTerrain.Zero);

            Assert.That(geometry.Lines.All(l => l.From.x >= 0f && l.From.z >= 0f && l.To.x <= World.SizeX && l.To.z <= World.SizeZ), Is.True);
        }

        [Test]
        public void WorstCaseFitsTheLineBudget()
        {
            var geometry = Collect(altitude: 599f, focus: new float2(9216f, 9216f), FlatTerrain.Zero, radius: 4000f);

            Assert.That(geometry.DroppedLines, Is.Zero);
            Assert.That(geometry.Lines.Count, Is.LessThan(geometry.MaxLines));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => new CellGridOverlay(World, null));
            var overlay = new CellGridOverlay(World, FlatTerrain.Zero);
            Assert.That(overlay.Name, Is.EqualTo(CellGridOverlay.OverlayName));
            Assert.Throws<ArgumentNullException>(() => overlay.Collect(null, default));
        }

        private static DebugGeometry Collect(float altitude, float2 focus, ITerrainHeight terrain, float radius = 800f)
        {
            var geometry = new DebugGeometry();
            var ground = terrain.SurfaceHeight(focus);
            var view = new DebugView(new float3(focus.x, ground + altitude, focus.y - altitude), new float3(focus.x, ground, focus.y), radius);
            new CellGridOverlay(World, terrain).Collect(geometry, view);
            return geometry;
        }

        private sealed class SlopeTerrain : ITerrainHeight
        {
            public float SurfaceHeight(float2 position) => 10f + (position.x * 0.05f) + (position.y * 0.02f);
        }
    }
}
