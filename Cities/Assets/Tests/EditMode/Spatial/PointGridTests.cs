using System.Linq;
using Cities.Core.Spatial;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Cities.Tests.EditMode.Spatial
{
    /// <summary>Gebruikt Collections en Burst-jobs en draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class PointGridTests
    {
        private static readonly GridGeometry Geometry = GridGeometry.Covering(float2.zero, new float2(2000f, 2000f), 32f);

        private PointGrid _grid;
        private NativeArray<float2> _positions;
        private NativeList<int> _results;

        [SetUp]
        public void SetUp()
        {
            _grid = new PointGrid(Geometry, 10_000, Allocator.Persistent);
            _positions = new NativeArray<float2>(10_000, Allocator.Persistent);
            var random = new Random(3);
            for (var i = 0; i < _positions.Length; i++)
            {
                _positions[i] = random.NextFloat2(new float2(-20f), new float2(2020f));
            }

            _results = new NativeList<int>(1024, Allocator.Persistent);
            _grid.Build(_positions);
        }

        [TearDown]
        public void TearDown()
        {
            _grid.Dispose();
            _positions.Dispose();
            _results.Dispose();
        }

        [Test]
        public void EveryPointIsInItsCell()
        {
            Assert.That(_grid.CellCount.Sum(), Is.EqualTo(_positions.Length));
            var sorted = _grid.SortedIndices;
            Assert.That(sorted.Distinct().Count(), Is.EqualTo(_positions.Length));
            for (var cell = 0; cell < Geometry.Length; cell++)
            {
                for (var i = _grid.CellStart[cell]; i < _grid.CellStart[cell] + _grid.CellCount[cell]; i++)
                {
                    Assert.That(Geometry.IndexOf(_positions[sorted[i]]), Is.EqualTo(cell));
                }
            }
        }

        [Test]
        public void RadiusQueryMatchesBruteForce()
        {
            var random = new Random(9);
            for (var q = 0; q < 100; q++)
            {
                var center = random.NextFloat2(new float2(0f), new float2(2000f));
                var radius = random.NextFloat(1f, 120f);
                _results.Clear();
                _grid.QueryRadius(_positions, center, radius, _results);

                var expected = Enumerable.Range(0, _positions.Length).Where(i => math.distancesq(_positions[i], center) <= radius * radius);
                Assert.That(_results.AsArray().ToArray().OrderBy(i => i), Is.EqualTo(expected), $"Query {q}");
            }
        }

        [Test]
        public void FindNearestMatchesBruteForce()
        {
            var random = new Random(5);
            for (var q = 0; q < 200; q++)
            {
                var center = random.NextFloat2(new float2(-50f), new float2(2050f));
                var maxRadius = random.NextFloat(5f, 300f);

                var found = _grid.FindNearest(_positions, center, maxRadius);

                var best = -1;
                var bestSq = maxRadius * maxRadius;
                for (var i = 0; i < _positions.Length; i++)
                {
                    var d = math.distancesq(_positions[i], center);
                    if (d < bestSq || (d <= bestSq && best < 0))
                    {
                        best = i;
                        bestSq = d;
                    }
                }

                if (best < 0)
                {
                    Assert.That(found, Is.EqualTo(-1), $"Query {q}");
                }
                else
                {
                    Assert.That(math.distancesq(_positions[found], center), Is.EqualTo(bestSq).Within(1e-3f), $"Query {q}");
                }
            }
        }

        [Test]
        public void FindNearestSkipsExcludedPoint()
        {
            var self = 42;

            var other = _grid.FindNearest(_positions, _positions[self], 500f, self);

            Assert.That(other, Is.Not.EqualTo(self));
            Assert.That(other, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void RebuildAndQueriesDoNotAllocate()
        {
            Assert.That(() =>
            {
                _grid.Build(_positions);
                _results.Clear();
                _grid.QueryRadius(_positions, new float2(1000f, 1000f), 50f, _results);
                _grid.FindNearest(_positions, new float2(500f, 500f), 100f);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void RebuildWithFewerPointsShrinks()
        {
            using var few = new NativeArray<float2>(new[] { new float2(10f, 10f), new float2(1990f, 1990f) }, Allocator.TempJob);

            _grid.Build(few);

            Assert.That(_grid.SortedIndices.Length, Is.EqualTo(2));
            Assert.That(_grid.FindNearest(few, new float2(0f, 0f), 50f), Is.EqualTo(0));
        }
    }
}
