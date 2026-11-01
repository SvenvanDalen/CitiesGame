using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Network
{
    public class RoutePlannerTests
    {
        /// <summary>Kleine graaf uit een lijst (van, naar, kosten); de schatting is altijd 0.</summary>
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

        [Test]
        public void TheCheapestRouteWinsOverTheFewestSteps()
        {
            var graph = new FakeGraph((1, 2, 10f), (2, 4, 10f), (1, 3, 1f), (3, 5, 1f), (5, 4, 1f));

            var result = RoutePlanner.Find(graph, 1, 4);

            Assert.That(result.Found, Is.True);
            Assert.That(result.Path, Is.EqualTo(new ulong[] { 1, 3, 5, 4 }));
            Assert.That(result.Cost, Is.EqualTo(3f));
        }

        [Test]
        public void ARouteToTheStartIsJustTheStart()
        {
            var result = RoutePlanner.Find(new FakeGraph((1, 2, 1f)), 1, 1);

            Assert.That(result.Path, Is.EqualTo(new ulong[] { 1 }));
            Assert.That(result.Cost, Is.Zero);
        }

        [Test]
        public void NoRouteGivesAnEmptyPathAndCycleDoesNotLoopForever()
        {
            var graph = new FakeGraph((1, 2, 1f), (2, 1, 1f), (3, 4, 1f));

            var result = RoutePlanner.Find(graph, 1, 4);

            Assert.That(result.Found, Is.False);
            Assert.That(result.Path, Is.Empty);
        }

        [Test]
        public void EqualRoutesAreResolvedByTheLowestKey()
        {
            var graph = new FakeGraph((1, 3, 1f), (1, 2, 1f), (2, 4, 1f), (3, 4, 1f));

            var result = RoutePlanner.Find(graph, 1, 4);

            Assert.That(result.Path, Is.EqualTo(new ulong[] { 1, 2, 4 }));
        }

        [Test]
        public void ACheaperRouteFoundLaterReplacesAnEarlierOne()
        {
            var graph = new FakeGraph((1, 4, 10f), (1, 2, 1f), (2, 3, 1f), (3, 4, 1f));

            var result = RoutePlanner.Find(graph, 1, 4);

            Assert.That(result.Path, Is.EqualTo(new ulong[] { 1, 2, 3, 4 }));
            Assert.That(result.Cost, Is.EqualTo(3f));
        }

        [Test]
        public void ANullGraphIsRejected() =>
            Assert.Throws<ArgumentNullException>(() => RoutePlanner.Find(null, 1, 2));
    }
}
