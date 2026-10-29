using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Spatial;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Cities.Tests.EditMode.Spatial
{
    /// <summary>Gebruikt Collections en Burst en draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class SpatialGridTests
    {
        private static readonly GridGeometry Geometry = GridGeometry.Covering(float2.zero, new float2(1000f, 1000f), 50f);
        private static readonly int[] OnlyOne = { 1 };

        private SpatialGrid<int> _grid;
        private NativeList<int> _results;

        [SetUp]
        public void SetUp()
        {
            _grid = new SpatialGrid<int>(Geometry, 64, Allocator.Persistent);
            _results = new NativeList<int>(256, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            _grid.Dispose();
            _results.Dispose();
        }

        [Test]
        public void ObjectSpanningCellsIsReportedOnce()
        {
            _grid.Insert(1, new Aabb2(new float2(40f, 40f), new float2(160f, 60f)));

            _grid.QueryRect(new Aabb2(float2.zero, new float2(1000f, 1000f)), _results);

            Assert.That(_results.AsArray().ToArray(), Is.EqualTo(OnlyOne));
        }

        [Test]
        public void RemoveAndUpdateAreReflectedInQueries()
        {
            var a = _grid.Insert(1, Aabb2.FromCircle(new float2(100f, 100f), 5f));
            var b = _grid.Insert(2, Aabb2.FromCircle(new float2(110f, 100f), 5f));

            _grid.Remove(a);
            _grid.Update(b, Aabb2.FromCircle(new float2(800f, 800f), 5f));
            _grid.QueryRadius(new float2(100f, 100f), 50f, _results);

            Assert.That(_results.Length, Is.EqualTo(0));
            Assert.That(_grid.Count, Is.EqualTo(1));
            Assert.That(_grid.GetBounds(b).Center, Is.EqualTo(new float2(800f, 800f)));
            Assert.That(_grid.GetValue(b), Is.EqualTo(2));
        }

        [Test]
        public void HandlesAreReusedAndStaleHandlesRejected()
        {
            var a = _grid.Insert(1, Aabb2.FromCircle(new float2(10f, 10f), 1f));
            _grid.Remove(a);

            Assert.Throws<ArgumentException>(() => _grid.Remove(a));
            Assert.Throws<ArgumentException>(() => _grid.GetValue(-1));
            Assert.That(_grid.Insert(5, Aabb2.FromCircle(new float2(10f, 10f), 1f)), Is.EqualTo(a));
        }

        [Test]
        public void QueriesMatchBruteForce()
        {
            var random = new Unity.Mathematics.Random(7);
            var boxes = new Dictionary<int, Aabb2>();
            var handles = new Dictionary<int, int>();
            for (var i = 0; i < 2000; i++)
            {
                var box = Aabb2.FromCenter(random.NextFloat2(new float2(-50f), new float2(1050f)), random.NextFloat2(new float2(1f), new float2(80f)));
                boxes[i] = box;
                handles[i] = _grid.Insert(i, box);
            }

            for (var i = 0; i < 500; i++)
            {
                var id = random.NextInt(2000);
                if (!boxes.ContainsKey(id))
                {
                    continue;
                }

                if (random.NextBool())
                {
                    _grid.Remove(handles[id]);
                    boxes.Remove(id);
                }
                else
                {
                    var moved = Aabb2.FromCenter(random.NextFloat2(new float2(0f), new float2(1000f)), random.NextFloat2(new float2(1f), new float2(40f)));
                    _grid.Update(handles[id], moved);
                    boxes[id] = moved;
                }
            }

            for (var q = 0; q < 200; q++)
            {
                var area = Aabb2.FromCenter(random.NextFloat2(new float2(0f), new float2(1000f)), random.NextFloat2(new float2(5f), new float2(200f)));
                _results.Clear();
                _grid.QueryRect(area, _results);
                var expected = boxes.Where(p => p.Value.Overlaps(area)).Select(p => p.Key).OrderBy(k => k);
                Assert.That(_results.AsArray().ToArray().OrderBy(k => k), Is.EqualTo(expected), $"Rechthoek {area}");

                var center = random.NextFloat2(new float2(0f), new float2(1000f));
                var radius = random.NextFloat(5f, 150f);
                _results.Clear();
                _grid.QueryRadius(center, radius, _results);
                expected = boxes.Where(p => p.Value.OverlapsCircle(center, radius)).Select(p => p.Key).OrderBy(k => k);
                Assert.That(_results.AsArray().ToArray().OrderBy(k => k), Is.EqualTo(expected), $"Cirkel {center} r={radius}");
            }
        }

        [Test]
        public void RaycastReturnsNearestHit()
        {
            _grid.Insert(1, new Aabb2(new float2(300f, 90f), new float2(320f, 110f)));
            _grid.Insert(2, new Aabb2(new float2(200f, 95f), new float2(210f, 105f)));
            _grid.Insert(3, new Aabb2(new float2(100f, 300f), new float2(110f, 310f)));

            Assert.That(_grid.Raycast(new float2(0f, 100f), new float2(1f, 0f), 1000f, out var hit, out var distance), Is.True);
            Assert.That(hit, Is.EqualTo(2));
            Assert.That(distance, Is.EqualTo(200f).Within(1e-3f));
            Assert.That(_grid.Raycast(new float2(0f, 100f), new float2(-1f, 0f), 1000f, out _, out _), Is.False);
        }

        [Test]
        public void RaycastMatchesBruteForce()
        {
            var random = new Unity.Mathematics.Random(11);
            var boxes = new List<Aabb2>();
            for (var i = 0; i < 1000; i++)
            {
                var box = Aabb2.FromCenter(random.NextFloat2(new float2(0f), new float2(1000f)), random.NextFloat2(new float2(2f), new float2(20f)));
                boxes.Add(box);
                _grid.Insert(i, box);
            }

            for (var r = 0; r < 200; r++)
            {
                var origin = random.NextFloat2(new float2(-100f), new float2(1100f));
                var direction = math.normalize(random.NextFloat2Direction());
                var expectedDistance = float.PositiveInfinity;
                for (var i = 0; i < boxes.Count; i++)
                {
                    if (boxes[i].Raycast(origin, direction, 800f, out var t) && t < expectedDistance)
                    {
                        expectedDistance = t;
                    }
                }

                var found = _grid.Raycast(origin, direction, 800f, out _, out var distance);
                Assert.That(found, Is.EqualTo(!float.IsPositiveInfinity(expectedDistance)), $"Straal {r}");
                if (found)
                {
                    Assert.That(distance, Is.EqualTo(expectedDistance).Within(1e-3f), $"Straal {r}");
                }
            }
        }

        [Test]
        public void QueriesDoNotAllocate()
        {
            for (var i = 0; i < 100; i++)
            {
                _grid.Insert(i, Aabb2.FromCircle(new float2(i * 9f, 500f), 3f));
            }

            var area = new Aabb2(new float2(0f, 450f), new float2(900f, 550f));
            void RunQueries()
            {
                _results.Clear();
                _grid.QueryRect(area, _results);
                _grid.QueryRadius(new float2(450f, 500f), 100f, _results);
                _grid.Raycast(new float2(0f, 500f), new float2(1f, 0f), 900f, out _, out _);
            }

            RunQueries(); // eerste aanroep JIT-compileert de generieke methoden
            Assert.That(RunQueries, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void RaycastFindsObjectsOverhangingGridEdge()
        {
            // Steekt 15 m onder het raster uit; een straal die buiten het raster loopt moet het toch raken.
            _grid.Insert(1, new Aabb2(new float2(400f, -15f), new float2(420f, 10f)));

            Assert.That(_grid.Raycast(new float2(0f, -10f), new float2(1f, 0f), 1000f, out var hit, out var distance), Is.True);
            Assert.That(hit, Is.EqualTo(1));
            Assert.That(distance, Is.EqualTo(400f).Within(1e-3f));
        }

        [Test]
        public void ClearEmptiesGrid()
        {
            _grid.Insert(1, Aabb2.FromCircle(new float2(10f, 10f), 1f));

            _grid.Clear();
            _grid.QueryRect(Geometry.Bounds, _results);

            Assert.That(_grid.Count, Is.EqualTo(0));
            Assert.That(_results.Length, Is.EqualTo(0));
        }
    }
}
