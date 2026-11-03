using System.Linq;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    /// <summary>Het wegennet staat in NativeLists; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class InterchangeTests
    {
        private RoadNetwork _network;

        [SetUp]
        public void SetUp() => _network = new RoadNetwork(64);

        [TearDown]
        public void TearDown() => _network.Dispose();

        [TestCase(InterchangeKind.Diamond)]
        [TestCase(InterchangeKind.Cloverleaf)]
        [TestCase(InterchangeKind.Turbine)]
        public void BuildPutsThePlanInTheNetwork(InterchangeKind kind)
        {
            var plan = InterchangeGeometry.Plan(kind, float3.zero, 0f);

            var result = Interchange.Build(_network, kind, float3.zero);

            Assert.That(_network.NodeCount, Is.EqualTo(plan.Nodes.Count));
            Assert.That(_network.SegmentCount, Is.EqualTo(plan.Roads.Count));
            Assert.That(_network.GetNode(result.West).Position.x, Is.EqualTo(-InterchangeGeometry.HalfLength).Within(1e-3f));
            Assert.That(result.Segments.Count(s => _network.GetSegment(s).Elevation == RoadElevation.Bridge), Is.EqualTo(1));
            Assert.That(result.Segments.Count(s => _network.IsOneWay(_network.GetSegment(s))), Is.EqualTo(plan.Roads.Count(r => r.Role == InterchangeRole.Ramp)));
        }

        [Test]
        public void CanBuildOnlyOnOpenGround()
        {
            Assert.That(Interchange.CanBuild(_network, float3.zero), Is.Null);

            Interchange.Build(_network, InterchangeKind.Diamond, float3.zero);

            Assert.That(Interchange.CanBuild(_network, new float3(100f, 0f, 100f)), Is.Not.Null);
            Assert.That(Interchange.CanBuild(_network, new float3(2000f, 0f, 0f)), Is.Null);
        }

        [Test]
        public void SpeedAndProfileFollowTheRole()
        {
            Assert.That(Interchange.SpeedLimit(InterchangeRole.Highway), Is.GreaterThan(Interchange.SpeedLimit(InterchangeRole.Ramp)));
            Assert.That(Interchange.Profile(InterchangeRole.Highway).Count, Is.GreaterThan(Interchange.Profile(InterchangeRole.Ramp).Count));
        }
    }
}
