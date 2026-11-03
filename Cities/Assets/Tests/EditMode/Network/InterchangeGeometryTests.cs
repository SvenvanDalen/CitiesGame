using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    public class InterchangeGeometryTests
    {
        private static readonly InterchangeKind[] Kinds = { InterchangeKind.Diamond, InterchangeKind.Cloverleaf, InterchangeKind.Turbine };

        [TestCase(InterchangeKind.Diamond, 4)]
        [TestCase(InterchangeKind.Cloverleaf, 12)]
        [TestCase(InterchangeKind.Turbine, 8)]
        public void EachPresetHasTheExpectedNumberOfRamps(InterchangeKind kind, int ramps)
        {
            var plan = InterchangeGeometry.Plan(kind, float3.zero, 0f);

            Assert.That(plan.Roads.Count(r => r.Role == InterchangeRole.Ramp), Is.EqualTo(ramps));
        }

        [Test]
        public void EveryKindHasAPresetWithANameInOrder()
        {
            Assert.That(InterchangeGeometry.Presets.Select(p => p.Kind), Is.EqualTo(Kinds));
            Assert.That(InterchangeGeometry.Presets.All(p => p.Name.Length > 0 && p.Description.Length > 0), Is.True);
        }

        [Test]
        public void AnUnknownKindIsRejected() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => InterchangeGeometry.Plan((InterchangeKind)99, float3.zero, 0f));

        [Test]
        public void RoadsStartAndEndExactlyOnTheirNodesAndNoNodeHasTooManyArms([ValueSource(nameof(Kinds))] InterchangeKind kind)
        {
            var plan = InterchangeGeometry.Plan(kind, new float3(100f, 3f, -50f), 0.7f);

            foreach (var road in plan.Roads)
            {
                Assert.That(math.distance(road.Curve.P0, plan.Nodes[road.Start]), Is.LessThan(1e-4f));
                Assert.That(math.distance(road.Curve.P3, plan.Nodes[road.End]), Is.LessThan(1e-4f));
                Assert.That(road.Start, Is.Not.EqualTo(road.End));
            }

            for (var node = 0; node < plan.Nodes.Count; node++)
            {
                var arms = plan.Roads.Count(r => r.Start == node || r.End == node);
                Assert.That(arms, Is.InRange(1, RoadNetwork.MaxArms), $"knooppunt {node}");
            }
        }

        [Test]
        public void EverythingIsConnectedToTheHighway([ValueSource(nameof(Kinds))] InterchangeKind kind)
        {
            var plan = InterchangeGeometry.Plan(kind, float3.zero, 0f);
            var reached = new HashSet<int> { plan.West };
            var queue = new Queue<int>(reached);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var road in plan.Roads.Where(r => r.Start == node || r.End == node))
                {
                    var other = road.Start == node ? road.End : road.Start;
                    if (reached.Add(other))
                    {
                        queue.Enqueue(other);
                    }
                }
            }

            Assert.That(reached.Count, Is.EqualTo(plan.Nodes.Count));
            Assert.That(reached, Does.Contain(plan.East).And.Contain(plan.South).And.Contain(plan.North));
        }

        [Test]
        public void TheFourEndsLieOnTheAxesAtTheFullLength()
        {
            var plan = InterchangeGeometry.Plan(InterchangeKind.Cloverleaf, new float3(10f, 0f, 20f), 0f);

            Assert.That(plan.Nodes[plan.West], Is.EqualTo(new float3(10f - InterchangeGeometry.HalfLength, 0f, 20f)));
            Assert.That(plan.Nodes[plan.East], Is.EqualTo(new float3(10f + InterchangeGeometry.HalfLength, 0f, 20f)));
            Assert.That(plan.Nodes[plan.South].z, Is.EqualTo(20f - InterchangeGeometry.HalfLength).Within(1e-3f));
            Assert.That(plan.Nodes[plan.North].z, Is.EqualTo(20f + InterchangeGeometry.HalfLength).Within(1e-3f));
        }

        [Test]
        public void RotatingTurnsTheWholeInterchangeAroundTheCenter()
        {
            var center = new float3(0f, 0f, 0f);
            var plan = InterchangeGeometry.Plan(InterchangeKind.Diamond, center, math.PI / 2f);

            Assert.That(math.distance(plan.Nodes[plan.East], center), Is.EqualTo(InterchangeGeometry.HalfLength).Within(1e-2f));
            Assert.That(math.abs(plan.Nodes[plan.East].x), Is.LessThan(1e-2f), "oost wijst na een kwartslag naar de Z-as");
        }

        [Test]
        public void TheCrossingRoadPassesOverTheHighwayOnABridge([ValueSource(nameof(Kinds))] InterchangeKind kind)
        {
            var plan = InterchangeGeometry.Plan(kind, float3.zero, 0f);

            var bridges = plan.Roads.Where(r => r.Elevation == RoadElevation.Bridge).ToList();
            Assert.That(bridges.Count, Is.EqualTo(1));
            Assert.That(plan.Nodes[bridges[0].Start].y, Is.EqualTo(InterchangeGeometry.DeckHeight));
            Assert.That(plan.Nodes[bridges[0].End].y, Is.EqualTo(InterchangeGeometry.DeckHeight));
            Assert.That(plan.Roads.Where(r => r.Role == InterchangeRole.Highway).All(r => r.Curve.P0.y == 0f && r.Curve.P3.y == 0f), Is.True);
        }

        [Test]
        public void NoRoadClimbsSteeperThanSevenPercent([ValueSource(nameof(Kinds))] InterchangeKind kind)
        {
            var plan = InterchangeGeometry.Plan(kind, float3.zero, 0f);

            foreach (var road in plan.Roads)
            {
                for (var i = 0; i <= 24; i++)
                {
                    var d = road.Curve.Derivative(i / 24f);
                    var run = math.length(d.xz);
                    Assert.That(run, Is.GreaterThan(1e-3f));
                    Assert.That(math.abs(d.y) / run, Is.LessThanOrEqualTo(0.07f), $"{kind} {road.Role} bij {i}/24");
                }
            }
        }

        [Test]
        public void RampsJoinTheAxesTangentially([ValueSource(nameof(Kinds))] InterchangeKind kind)
        {
            var plan = InterchangeGeometry.Plan(kind, float3.zero, 0f);

            foreach (var ramp in plan.Roads.Where(r => r.Role == InterchangeRole.Ramp))
            {
                AssertTangential(kind, plan.Nodes[ramp.Start], math.normalize(ramp.Curve.Derivative(0f)));
                AssertTangential(kind, plan.Nodes[ramp.End], math.normalize(ramp.Curve.Derivative(1f)));
            }
        }

        [Test]
        public void TheCrossingRoadRisesLinearlyToTheDeckAndStaysFlatOnIt()
        {
            Assert.That(InterchangeGeometry.CrossingHeight(0f), Is.EqualTo(InterchangeGeometry.DeckHeight));
            Assert.That(InterchangeGeometry.CrossingHeight(-InterchangeGeometry.BridgeHalfSpan), Is.EqualTo(InterchangeGeometry.DeckHeight));
            Assert.That(InterchangeGeometry.CrossingHeight(InterchangeGeometry.ApproachLength), Is.EqualTo(0f));
            Assert.That(InterchangeGeometry.CrossingHeight(500f), Is.EqualTo(0f));
            var middle = (InterchangeGeometry.BridgeHalfSpan + InterchangeGeometry.ApproachLength) / 2f;
            Assert.That(InterchangeGeometry.CrossingHeight(middle), Is.EqualTo(InterchangeGeometry.DeckHeight / 2f).Within(1e-4f));
        }

        /// <summary>Een knooppunt op de snelweg (Z gelijk aan het midden) wordt langs X aangesloten, op de kruisende weg langs Z; lussen-tussenpunten zijn vrij.</summary>
        private static void AssertTangential(InterchangeKind kind, float3 node, float3 direction)
        {
            if (math.abs(node.z) < 0.5f)
            {
                Assert.That(math.abs(direction.x), Is.GreaterThan(0.99f));
            }
            else if (math.abs(node.x) < 0.5f)
            {
                // De ruit sluit met een T-aansluiting dwars op de kruisende weg aan.
                var along = kind == InterchangeKind.Diamond ? math.abs(direction.x) : math.abs(direction.z);
                Assert.That(along, Is.GreaterThan(0.99f));
            }
        }
    }
}
