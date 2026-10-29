using System;
using System.Collections.Generic;
using Cities.Core.Spatial;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Spatial
{
    public class GeometryTests
    {
        private static readonly GridGeometry Grid = new GridGeometry(new float2(0f, 0f), 10f, new int2(8, 6));

        [Test]
        public void AabbNormalisesCorners()
        {
            var box = new Aabb2(new float2(5f, 1f), new float2(1f, 3f));

            Assert.That(box.Min, Is.EqualTo(new float2(1f, 1f)));
            Assert.That(box.Max, Is.EqualTo(new float2(5f, 3f)));
            Assert.That(box.Center, Is.EqualTo(new float2(3f, 2f)));
            Assert.That(box.Size, Is.EqualTo(new float2(4f, 2f)));
        }

        [Test]
        public void AabbContainmentAndOverlap()
        {
            var box = Aabb2.FromCenter(new float2(0f, 0f), new float2(2f, 1f));

            Assert.That(box.Contains(new float2(2f, 1f)), Is.True);
            Assert.That(box.Contains(new float2(2.1f, 0f)), Is.False);
            Assert.That(box.Overlaps(new Aabb2(new float2(2f, -5f), new float2(3f, 5f))), Is.True, "Rakende randen tellen als overlap");
            Assert.That(box.Overlaps(new Aabb2(new float2(2.5f, 0f), new float2(3f, 1f))), Is.False);
            Assert.That(box.Encapsulate(Aabb2.FromCircle(new float2(5f, 0f), 1f)).Max, Is.EqualTo(new float2(6f, 1f)));
        }

        [Test]
        public void AabbDistanceAndCircle()
        {
            var box = new Aabb2(new float2(0f, 0f), new float2(2f, 2f));

            Assert.That(box.DistanceSq(new float2(1f, 1f)), Is.EqualTo(0f));
            Assert.That(box.DistanceSq(new float2(5f, 6f)), Is.EqualTo(25f));
            Assert.That(box.OverlapsCircle(new float2(5f, 6f), 5f), Is.True);
            Assert.That(box.OverlapsCircle(new float2(5f, 6f), 4.9f), Is.False);
        }

        [Test]
        public void AabbRaycastHitsMissesAndStartsInside()
        {
            var box = new Aabb2(new float2(4f, -1f), new float2(6f, 1f));

            Assert.That(box.Raycast(new float2(0f, 0f), new float2(1f, 0f), 100f, out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(4f).Within(1e-5f));
            Assert.That(box.Raycast(new float2(0f, 0f), new float2(-1f, 0f), 100f, out _), Is.False, "Achter de oorsprong");
            Assert.That(box.Raycast(new float2(0f, 5f), new float2(1f, 0f), 100f, out _), Is.False, "Parallel ernaast");
            Assert.That(box.Raycast(new float2(0f, 0f), new float2(1f, 0f), 3f, out _), Is.False, "Verder dan maximum");
            Assert.That(box.Raycast(new float2(5f, 0f), new float2(0f, 1f), 10f, out distance), Is.True);
            Assert.That(distance, Is.EqualTo(0f));
        }

        [Test]
        public void AabbRaycastDiagonal()
        {
            var box = new Aabb2(new float2(3f, 3f), new float2(4f, 4f));
            var direction = math.normalize(new float2(1f, 1f));

            Assert.That(box.Raycast(float2.zero, direction, 100f, out var distance), Is.True);
            Assert.That(distance, Is.EqualTo(math.sqrt(18f)).Within(1e-4f));
        }

        [Test]
        public void AabbEquality()
        {
            var a = new Aabb2(new float2(0f, 0f), new float2(1f, 1f));

            Assert.That(a == new Aabb2(new float2(1f, 1f), new float2(0f, 0f)), Is.True);
            Assert.That(a != new Aabb2(new float2(0f, 0f), new float2(1f, 2f)), Is.True);
            Assert.That(a.Equals((object)new Aabb2(new float2(0f, 0f), new float2(1f, 1f))), Is.True);
            Assert.That(a.Equals(1), Is.False);
            Assert.That(a.GetHashCode(), Is.EqualTo(new Aabb2(new float2(0f, 0f), new float2(1f, 1f)).GetHashCode()));
            Assert.That(a.ToString(), Does.Contain("–"));
        }

        [Test]
        public void GeometryMapsPointsAndClampsOutside()
        {
            Assert.That(Grid.CellOf(new float2(15f, 59f)), Is.EqualTo(new int2(1, 5)));
            Assert.That(Grid.CellOf(new float2(-20f, 500f)), Is.EqualTo(new int2(0, 5)));
            Assert.That(Grid.IndexOf(new float2(15f, 25f)), Is.EqualTo((2 * 8) + 1));
            Assert.That(Grid.Length, Is.EqualTo(48));
            Assert.That(Grid.Bounds, Is.EqualTo(new Aabb2(float2.zero, new float2(80f, 60f))));
            Assert.That(Grid.CellBounds(new int2(2, 3)), Is.EqualTo(new Aabb2(new float2(20f, 30f), new float2(30f, 40f))));
        }

        [Test]
        public void GeometryCellRange()
        {
            Grid.CellRange(new Aabb2(new float2(5f, 5f), new float2(25f, 12f)), out var min, out var max);

            Assert.That(min, Is.EqualTo(new int2(0, 0)));
            Assert.That(max, Is.EqualTo(new int2(2, 1)));
        }

        [Test]
        public void GeometryValidation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new GridGeometry(float2.zero, 0f, new int2(1, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new GridGeometry(float2.zero, 1f, new int2(0, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new GridGeometry(float2.zero, 1f, new int2(100_000, 100_000)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Grid.Index(new int2(8, 0)));
        }

        [Test]
        public void CoveringRoundsUp()
        {
            var grid = GridGeometry.Covering(new float2(-10f, 0f), new float2(95f, 0f), 10f);

            Assert.That(grid.CellCount, Is.EqualTo(new int2(10, 1)));
            Assert.That(grid.Origin, Is.EqualTo(new float2(-10f, 0f)));
        }

        [Test]
        public void GeometryEquality()
        {
            var same = new GridGeometry(new float2(0f, 0f), 10f, new int2(8, 6));

            Assert.That(Grid == same, Is.True);
            Assert.That(Grid != new GridGeometry(new float2(0f, 0f), 5f, new int2(8, 6)), Is.True);
            Assert.That(Grid.Equals((object)same), Is.True);
            Assert.That(Grid.Equals("grid"), Is.False);
            Assert.That(Grid.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(Grid.ToString(), Does.StartWith("8×6"));
        }

        [Test]
        public void RayWalkerVisitsCellsInOrder()
        {
            var cells = Walk(new float2(5f, 5f), new float2(1f, 0f), 30f);

            Assert.That(cells, Is.EqualTo(new[] { new int2(0, 0), new int2(1, 0), new int2(2, 0), new int2(3, 0) }));
        }

        [Test]
        public void RayWalkerDiagonalStaysConnected()
        {
            var cells = Walk(new float2(1f, 2f), math.normalize(new float2(3f, 2f)), 100f);

            Assert.That(cells[0], Is.EqualTo(new int2(0, 0)));
            for (var i = 1; i < cells.Count; i++)
            {
                var step = math.abs(cells[i] - cells[i - 1]);
                Assert.That(step.x + step.y, Is.EqualTo(1), $"Stap {i} slaat een cel over");
            }

            var last = cells[cells.Count - 1];
            Assert.That(last.x == 7 || last.y == 5, Is.True, "Loopt door tot de rand");
        }

        [Test]
        public void RayWalkerEntersFromOutside()
        {
            var cells = Walk(new float2(-15f, 15f), new float2(1f, 0f), 30f);

            Assert.That(cells[0], Is.EqualTo(new int2(0, 1)));
            Assert.That(cells.Count, Is.EqualTo(2), "Van -15 tot 15: cellen 0 en 1");
        }

        [Test]
        public void RayWalkerMissingGridVisitsNothing()
        {
            Assert.That(Walk(new float2(-15f, 15f), new float2(-1f, 0f), 100f), Is.Empty);
        }

        [Test]
        public void RayWalkerNegativeDirection()
        {
            var cells = Walk(new float2(35f, 55f), new float2(0f, -1f), 100f);

            Assert.That(cells.Count, Is.EqualTo(6));
            Assert.That(cells[5], Is.EqualTo(new int2(3, 0)));
        }

        private static List<int2> Walk(float2 origin, float2 direction, float maxDistance)
        {
            var cells = new List<int2>();
            var walker = new GridRayWalker(Grid, origin, direction, maxDistance);
            while (walker.MoveNext())
            {
                cells.Add(walker.Cell);
            }

            return cells;
        }
    }
}
