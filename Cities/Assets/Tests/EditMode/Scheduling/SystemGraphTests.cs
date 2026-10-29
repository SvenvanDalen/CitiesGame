using System;
using Cities.Simulation.Scheduling;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Scheduling
{
    public class SystemGraphTests
    {
        private sealed class Node
        {
            private readonly string _name;

            public Node(string name) => _name = name;

            public override string ToString() => _name;
        }

        private readonly Node _a = new Node("A");
        private readonly Node _b = new Node("B");
        private readonly Node _c = new Node("C");
        private readonly Node _d = new Node("D");

        [Test]
        public void WithoutDependenciesKeepsRegistrationOrder()
        {
            var graph = new SystemGraph<Node>();
            graph.Add(_c, null);
            graph.Add(_a, null);
            graph.Add(_b, null);

            Assert.That(graph.Sort(), Is.EqualTo(new[] { _c, _a, _b }));
        }

        [Test]
        public void DependenciesComeFirstEvenWhenRegisteredLater()
        {
            var graph = new SystemGraph<Node>();
            graph.Add(_a, new[] { _b });
            graph.Add(_c, null);
            graph.Add(_b, new[] { _d });
            graph.Add(_d, null);

            var order = graph.Sort();

            Assert.That(order.IndexOf(_d), Is.LessThan(order.IndexOf(_b)));
            Assert.That(order.IndexOf(_b), Is.LessThan(order.IndexOf(_a)));
            Assert.That(order, Is.EqualTo(new[] { _c, _d, _b, _a }), "Bij keuze wint de vroegste registratie");
        }

        [Test]
        public void SortIsStableAcrossCalls()
        {
            var graph = new SystemGraph<Node>();
            graph.Add(_a, null);
            graph.Add(_b, new[] { _a });
            graph.Add(_c, new[] { _a });

            Assert.That(graph.Sort(), Is.EqualTo(graph.Sort()));
            Assert.That(graph.PredecessorsOf(_c), Is.EqualTo(new[] { _a }));
            Assert.That(graph.Count, Is.EqualTo(3));
        }

        [Test]
        public void CycleIsReportedWithMembers()
        {
            var graph = new SystemGraph<Node>();
            graph.Add(_a, new[] { _c });
            graph.Add(_b, new[] { _a });
            graph.Add(_c, new[] { _b });
            graph.Add(_d, null);

            var error = Assert.Throws<InvalidOperationException>(() => graph.Sort());

            Assert.That(error.Message, Does.Contain("A").And.Contain("B").And.Contain("C"));
            Assert.That(error.Message, Does.Not.Contain("D"));
        }

        [Test]
        public void UnknownDependencyIsReported()
        {
            var graph = new SystemGraph<Node>();
            graph.Add(_a, new[] { _b });

            var error = Assert.Throws<InvalidOperationException>(() => graph.Sort());

            Assert.That(error.Message, Does.Contain("B"));
        }

        [Test]
        public void InvalidRegistrationsAreRejected()
        {
            var graph = new SystemGraph<Node>();
            graph.Add(_a, null);

            Assert.Throws<ArgumentNullException>(() => graph.Add(null, null));
            Assert.Throws<InvalidOperationException>(() => graph.Add(_a, null));
            Assert.Throws<ArgumentException>(() => graph.Add(_b, new[] { _b }));
            Assert.Throws<ArgumentException>(() => graph.Add(_c, new Node[] { null }));
            Assert.Throws<ArgumentException>(() => graph.PredecessorsOf(_d));
        }

        [Test]
        public void MessagesUseDescription()
        {
            var graph = new SystemGraph<Node>(node => $"systeem {node}");
            graph.Add(_a, new[] { _b });

            var error = Assert.Throws<InvalidOperationException>(() => graph.Sort());

            Assert.That(error.Message, Does.Contain("systeem A").And.Contain("systeem B"));
            Assert.That(graph.Nodes, Is.EqualTo(new[] { _a }));
            Assert.That(Assert.Throws<ArgumentException>(() => graph.PredecessorsOf(null)).Message, Does.Contain("null"));
        }

        [Test]
        public void DuplicateDependenciesAreMerged()
        {
            var graph = new SystemGraph<Node>();
            graph.Add(_a, null);
            graph.Add(_b, new[] { _a, _a });

            Assert.That(graph.PredecessorsOf(_b), Is.EqualTo(new[] { _a }));
            Assert.That(graph.Sort(), Is.EqualTo(new[] { _a, _b }));
        }
    }
}
