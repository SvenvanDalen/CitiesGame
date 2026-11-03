using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Network
{
    public sealed class RouteCacheTests
    {
        private sealed class FakeGraph : IRouteGraph
        {
            private readonly List<(ulong From, ulong To, float Cost)> _edges;

            public FakeGraph(params (ulong From, ulong To, float Cost)[] edges) => _edges = edges.ToList();

            public int Expansions { get; private set; }

            public void Expand(ulong node, List<RouteStep> steps)
            {
                Expansions++;
                steps.AddRange(_edges.Where(e => e.From == node).Select(e => new RouteStep(e.To, e.Cost)));
            }

            public float Estimate(ulong from, ulong goal) => 0f;
        }

        private static FakeGraph Line() => new FakeGraph((1, 2, 1f), (2, 3, 2f), (3, 4, 4f));

        [Test]
        public void SecondIdenticalQueryIsServedFromTheCache()
        {
            var cache = new RouteCache();
            var graph = Line();
            var first = cache.Find(graph, 1, 4);
            var expansions = graph.Expansions;
            var second = cache.Find(graph, 1, 4);

            Assert.That(second.Path, Is.EqualTo(first.Path));
            Assert.That(graph.Expansions, Is.EqualTo(expansions), "geen nieuwe berekening");
            Assert.That(cache.Hits, Is.EqualTo(1));
            Assert.That(cache.Misses, Is.EqualTo(1));
        }

        [Test]
        public void AStartOnACachedRouteGetsTheTail()
        {
            var cache = new RouteCache();
            var graph = Line();
            cache.Find(graph, 1, 4);
            var tail = cache.Find(graph, 2, 4);

            Assert.That(tail.Path, Is.EqualTo(new ulong[] { 2, 3, 4 }));
            Assert.That(tail.Cost, Is.EqualTo(6f).Within(0.001f));
            Assert.That(cache.Misses, Is.EqualTo(1));
        }

        [Test]
        public void UnreachableGoalIsNotCached()
        {
            var cache = new RouteCache();
            var result = cache.Find(Line(), 4, 1);
            Assert.That(result.Found, Is.False);
            Assert.That(cache.Count, Is.Zero);
        }

        [Test]
        public void InvalidateDropsOnlyRoutesThroughTheNode()
        {
            var cache = new RouteCache();
            var graph = new FakeGraph((1, 2, 1f), (2, 3, 1f), (7, 8, 1f));
            cache.Find(graph, 1, 3);
            cache.Find(graph, 7, 8);

            Assert.That(cache.Invalidate(2), Is.EqualTo(1));
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.Invalidate(2), Is.Zero);
            cache.Find(graph, 7, 8);
            Assert.That(cache.Hits, Is.EqualTo(1));
        }

        [Test]
        public void InvalidatedRouteIsRecomputedWithTheNewGraph()
        {
            var cache = new RouteCache();
            cache.Find(new FakeGraph((1, 2, 1f), (2, 3, 1f)), 1, 3);
            cache.Invalidate(2);
            var detour = cache.Find(new FakeGraph((1, 4, 1f), (4, 3, 1f)), 1, 3);
            Assert.That(detour.Path, Is.EqualTo(new ulong[] { 1, 4, 3 }));
        }

        [Test]
        public void OldRoutesAreRecomputed()
        {
            var cache = new RouteCache(10, 5);
            var graph = Line();
            cache.Find(graph, 1, 4, 0);
            cache.Find(graph, 1, 4, 4);
            Assert.That(cache.Misses, Is.EqualTo(1));
            cache.Find(graph, 1, 4, 5);
            Assert.That(cache.Misses, Is.EqualTo(2));
            cache.Find(graph, 2, 4, 20);
            Assert.That(cache.Misses, Is.EqualTo(3), "ook een staartstuk van een verlopen route telt niet");
        }

        [Test]
        public void CapacityEvictsTheOldestRoute()
        {
            var cache = new RouteCache(2);
            var graph = new FakeGraph((1, 2, 1f), (3, 4, 1f), (5, 6, 1f));
            cache.Find(graph, 1, 2);
            cache.Find(graph, 3, 4);
            cache.Find(graph, 5, 6);

            Assert.That(cache.Count, Is.EqualTo(2));
            cache.Find(graph, 1, 2);
            Assert.That(cache.Misses, Is.EqualTo(4), "de oudste route was weg");
        }

        [Test]
        public void ClearEmptiesTheCache()
        {
            var cache = new RouteCache();
            cache.Find(Line(), 1, 4);
            cache.Clear();
            Assert.That(cache.Count, Is.Zero);
            Assert.That(cache.Invalidate(2), Is.Zero);
        }

        [Test]
        public void RejectsBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RouteCache(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RouteCache(1, 0));
            Assert.Throws<ArgumentNullException>(() => new RouteCache().Find(null, 1, 2));
        }
    }
}
