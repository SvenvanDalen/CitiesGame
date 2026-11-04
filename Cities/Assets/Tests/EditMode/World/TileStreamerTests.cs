using System;
using System.Linq;
using Cities.World.Grid;
using Cities.World.Tiles;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.World
{
    public class TileStreamerTests
    {
        // 9×9 tegels van 100 m (10 cellen van 10 m).
        private static readonly WorldDimensions Map = new WorldDimensions(9, 9, 10, 10f);

        private static Vector2 CenterOf(int x, int z) => new Vector2((x + 0.5f) * 100f, (z + 0.5f) * 100f);

        [Test]
        public void BudgetDeterminesMaxResident()
        {
            Assert.That(new TileStreamer(Map, 1000, 5500, 1000f).MaxResident, Is.EqualTo(5));
            Assert.That(new TileStreamer(Map, 1, 1_000_000, 1000f).MaxResident, Is.EqualTo(81));
        }

        [Test]
        public void LoadsNearestTilesFirstWithinBudget()
        {
            var streamer = new TileStreamer(Map, 1000, 5000, 1000f);

            streamer.Update(CenterOf(4, 4));

            Assert.That(streamer.ResidentCount, Is.EqualTo(5));
            Assert.That(streamer.ToLoad[0], Is.EqualTo(new TileCoord(4, 4)));
            Assert.That(streamer.ResidentBytes, Is.EqualTo(5000));
            Assert.That(streamer.IsResident(new TileCoord(4, 4)), Is.True);
        }

        [Test]
        public void OnlyTilesInsideTheRadiusLoad()
        {
            var streamer = new TileStreamer(Map, 1, 1000, 10f);

            streamer.Update(CenterOf(4, 4));

            Assert.That(streamer.ToLoad, Is.EqualTo(new[] { new TileCoord(4, 4) }));
        }

        [Test]
        public void MovingTheFocusUnloadsFarTilesAndLoadsNewOnes()
        {
            var streamer = new TileStreamer(Map, 1, 1000, 120f, 0f);
            streamer.Update(CenterOf(1, 1));
            Assert.That(streamer.IsResident(new TileCoord(1, 1)), Is.True);

            streamer.Update(CenterOf(7, 7));

            Assert.That(streamer.IsResident(new TileCoord(1, 1)), Is.False);
            Assert.That(streamer.ToUnload, Does.Contain(new TileCoord(1, 1)));
            Assert.That(streamer.IsResident(new TileCoord(7, 7)), Is.True);
        }

        [Test]
        public void UnloadMarginKeepsTilesOnTheEdge()
        {
            var streamer = new TileStreamer(Map, 1, 1000, 60f, 200f);
            streamer.Update(CenterOf(4, 4));
            var loaded = streamer.ResidentCount;

            streamer.Update(CenterOf(4, 4) + new Vector2(120f, 0f));

            Assert.That(streamer.ToUnload, Is.Empty);
            Assert.That(streamer.ResidentCount, Is.GreaterThanOrEqualTo(loaded));
        }

        [Test]
        public void OverfullBudgetDropsTheFarthestTilesFirst()
        {
            var streamer = new TileStreamer(Map, 1, 3, 2000f, 2000f);
            streamer.Update(CenterOf(0, 0));
            Assert.That(streamer.ResidentCount, Is.EqualTo(3));

            streamer.Update(CenterOf(8, 8));

            Assert.That(streamer.ResidentCount, Is.EqualTo(3));
            Assert.That(streamer.IsResident(new TileCoord(8, 8)), Is.True);
            Assert.That(streamer.IsResident(new TileCoord(0, 0)), Is.False);
        }

        [Test]
        public void MaxLoadsSpreadsTheWorkOverUpdates()
        {
            var streamer = new TileStreamer(Map, 1, 1000, 1000f);

            streamer.Update(CenterOf(4, 4), maxLoads: 4);
            Assert.That(streamer.ToLoad.Count, Is.EqualTo(4));

            streamer.Update(CenterOf(4, 4), maxLoads: 4);
            Assert.That(streamer.ResidentCount, Is.EqualTo(8));
            Assert.That(streamer.ToLoad.Intersect(new[] { new TileCoord(4, 4) }), Is.Empty);
        }

        [Test]
        public void StableFocusChangesNothing()
        {
            var streamer = new TileStreamer(Map, 1, 1000, 250f);
            streamer.Update(CenterOf(4, 4));

            streamer.Update(CenterOf(4, 4));

            Assert.That(streamer.ToLoad, Is.Empty);
            Assert.That(streamer.ToUnload, Is.Empty);
        }

        [Test]
        public void FocusOutsideTheMapStillLoadsTheEdge()
        {
            var streamer = new TileStreamer(Map, 1, 1000, 150f);

            streamer.Update(new Vector2(-100f, 50f));

            Assert.That(streamer.IsResident(new TileCoord(0, 0)), Is.True);
            Assert.That(streamer.ToLoad.All(tile => Map.Contains(tile)), Is.True);
        }

        [Test]
        public void DistanceIsZeroInsideAndMeasuredToTheEdgeOutside()
        {
            var streamer = new TileStreamer(Map, 1, 1000, 100f);

            Assert.That(streamer.DistanceTo(new TileCoord(2, 2), CenterOf(2, 2)), Is.EqualTo(0f));
            Assert.That(streamer.DistanceTo(new TileCoord(2, 2), new Vector2(350f, 250f)), Is.EqualTo(50f).Within(0.001f));
            Assert.That(streamer.DistanceTo(new TileCoord(0, 0), new Vector2(130f, 140f)), Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileStreamer(Map, 0, 10, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileStreamer(Map, 10, 5, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileStreamer(Map, 1, 10, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileStreamer(Map, 1, 10, 10f, -1f));
        }
    }
}
