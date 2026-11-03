using System;
using System.Linq;
using Cities.Core.Ids;
using Cities.Core.Persistence;
using Cities.Simulation.Network;
using Cities.Simulation.Vehicles;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    public class TrafficRulesTests
    {
        private static readonly EntityId<SegmentKind> SegmentA = new EntityId<SegmentKind>(1, 1);
        private static readonly EntityId<SegmentKind> SegmentB = new EntityId<SegmentKind>(2, 1);
        private static readonly EntityId<NodeKind> Node = new EntityId<NodeKind>(5, 1);
        private static readonly EntityId<NodeKind> Other = new EntityId<NodeKind>(6, 1);

        [Test]
        public void AnArmWithoutSettingsFollowsTheNetwork()
        {
            var rules = new TrafficRules();

            Assert.That(rules.Override(SegmentA, Node), Is.Null);
            Assert.That(rules.IsNoEntry(SegmentA, Node), Is.False);
            Assert.That(rules.Count, Is.EqualTo(0));
        }

        [Test]
        public void RulesAreKeptPerSegmentAndNode()
        {
            var rules = new TrafficRules();

            rules.SetRightOfWay(SegmentA, Node, RightOfWay.Stop);
            rules.SetNoEntry(SegmentB, Node, true);

            Assert.That(rules.Override(SegmentA, Node), Is.EqualTo(RightOfWay.Stop));
            Assert.That(rules.Override(SegmentA, Other), Is.Null);
            Assert.That(rules.IsNoEntry(SegmentB, Node), Is.True);
            Assert.That(rules.IsNoEntry(SegmentB, Other), Is.False);
            Assert.That(rules.Count, Is.EqualTo(2));
        }

        [Test]
        public void ResettingBothSettingsRemovesTheArm()
        {
            var rules = new TrafficRules();
            rules.SetRightOfWay(SegmentA, Node, RightOfWay.Yield);
            rules.SetNoEntry(SegmentA, Node, true);

            rules.SetRightOfWay(SegmentA, Node, null);
            Assert.That(rules.Count, Is.EqualTo(1), "het verbod blijft");
            rules.SetNoEntry(SegmentA, Node, false);

            Assert.That(rules.Count, Is.EqualTo(0));
        }

        [Test]
        public void AnUnknownRuleIsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TrafficRules().SetRightOfWay(SegmentA, Node, (RightOfWay)99));
        }

        [Test]
        public void ClearAndPruneRemoveRulesOfGoneSegments()
        {
            var rules = new TrafficRules();
            rules.SetNoEntry(SegmentA, Node, true);
            rules.SetNoEntry(SegmentA, Other, true);
            rules.SetNoEntry(SegmentB, Node, true);

            rules.Clear(SegmentA);
            Assert.That(rules.Count, Is.EqualTo(1));

            rules.Prune(s => s != SegmentB);
            Assert.That(rules.Count, Is.EqualTo(0));
        }

        [Test]
        public void RulesSurviveSavingAndLoading()
        {
            var rules = new TrafficRules();
            rules.SetRightOfWay(SegmentB, Other, RightOfWay.Roundabout);
            rules.SetRightOfWay(SegmentA, Node, RightOfWay.Stop);
            rules.SetNoEntry(SegmentA, Node, true);
            var bytes = SaveBytes.Write(new SaveMetadata(), new TrafficRulesSection(rules));

            var loaded = new TrafficRules();
            SaveBytes.Read(bytes, new TrafficRulesSection(loaded));

            Assert.That(loaded.Override(SegmentA, Node), Is.EqualTo(RightOfWay.Stop));
            Assert.That(loaded.IsNoEntry(SegmentA, Node), Is.True);
            Assert.That(loaded.Override(SegmentB, Other), Is.EqualTo(RightOfWay.Roundabout));
            Assert.That(loaded.IsNoEntry(SegmentB, Other), Is.False);
            Assert.That(SaveBytes.Write(new SaveMetadata(), new TrafficRulesSection(loaded)), Is.EqualTo(bytes), "deterministisch opgeslagen");
        }

        [Test]
        public void SavingLeavesOutRulesOfSegmentsThatAreGone()
        {
            var rules = new TrafficRules();
            rules.SetNoEntry(SegmentA, Node, true);
            rules.SetNoEntry(SegmentB, Node, true);

            var bytes = SaveBytes.Write(new SaveMetadata(), new TrafficRulesSection(rules, s => s == SegmentB));
            var loaded = new TrafficRules();
            SaveBytes.Read(bytes, new TrafficRulesSection(loaded));

            Assert.That(loaded.IsNoEntry(SegmentA, Node), Is.False);
            Assert.That(loaded.IsNoEntry(SegmentB, Node), Is.True);
        }

        [Test]
        public void ACorruptCountOrRuleIsRefused()
        {
            var section = new TrafficRulesSection(new TrafficRules());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(
                SaveBytes.ReaderFor(w =>
                {
                    w.Write(1);
                    w.Write(1L);
                    w.Write(1L);
                    w.Write((byte)42);
                    w.Write(false);
                }),
                1));
        }

        [Test]
        public void ReadDoesNotChangeTheRulesUntilTheResultIsApplied()
        {
            var rules = new TrafficRules();
            var source = new TrafficRules();
            source.SetNoEntry(SegmentA, Node, true);
            var reader = SaveBytes.ReaderFor(w => new TrafficRulesSection(source).Write(w));

            var apply = new TrafficRulesSection(rules).Read(reader, 1);
            Assert.That(rules.Count, Is.EqualTo(0));
            apply();

            Assert.That(rules.Count, Is.EqualTo(1));
        }

        [Test]
        public void ASectionNeedsRules()
        {
            Assert.Throws<ArgumentNullException>(() => new TrafficRulesSection(null));
            Assert.That(new TrafficRulesSection(new TrafficRules()).Id, Is.EqualTo("TRFC"));
        }
    }

    /// <summary>Het wegennet staat in NativeLists; draait daarom alleen in Unity.</summary>
    [Category("UnityRuntime")]
    public class TrafficRulesNetworkTests
    {
        private RoadNetwork _network;

        [SetUp]
        public void SetUp() => _network = new RoadNetwork(16);

        [TearDown]
        public void TearDown() => _network.Dispose();

        private (EntityId<NodeKind> Center, EntityId<SegmentKind> Main, EntityId<SegmentKind> Side, EntityId<SegmentKind> Far) Tee()
        {
            var profile = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);
            var center = _network.AddNode(float3.zero);
            var west = _network.AddNode(new float3(-50f, 0f, 0f));
            var east = _network.AddNode(new float3(50f, 0f, 0f));
            var south = _network.AddNode(new float3(0f, 0f, -50f));
            var a = _network.AddSegment(west, center, Bezier3.Straight(new float3(-50f, 0f, 0f), float3.zero), profile, 13.9f);
            var b = _network.AddSegment(center, east, Bezier3.Straight(float3.zero, new float3(50f, 0f, 0f)), profile, 13.9f);
            var c = _network.AddSegment(south, center, Bezier3.Straight(new float3(0f, 0f, -50f), float3.zero), profile, 8.3f);
            return (center, a, c, b);
        }

        [Test]
        public void TheSpeedLimitOfASegmentCanBeChanged()
        {
            var (_, main, _, _) = Tee();
            var lanes = _network.GetSegment(main).Lanes.Length;

            _network.SetSpeedLimit(main, 22.2f);

            Assert.That(_network.GetSegment(main).SpeedLimit, Is.EqualTo(22.2f));
            Assert.That(_network.GetSegment(main).Lanes.Length, Is.EqualTo(lanes));
            Assert.Throws<ArgumentOutOfRangeException>(() => _network.SetSpeedLimit(main, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _network.SetSpeedLimit(main, float.NaN));
        }

        [Test]
        public void TheFasterRoadHasPriorityAndTheOtherGivesWay()
        {
            var (center, main, side, far) = Tee();
            var rules = new TrafficRules();

            Assert.That(RightOfWayResolver.Resolve(_network, rules, main, center), Is.EqualTo(RightOfWay.Priority));
            Assert.That(RightOfWayResolver.Resolve(_network, rules, far, center), Is.EqualTo(RightOfWay.Priority));
            Assert.That(RightOfWayResolver.Resolve(_network, rules, side, center), Is.EqualTo(RightOfWay.Yield));
        }

        [Test]
        public void EqualRoadsGiveRightBeforeLeftAndASetRuleWins()
        {
            var (center, main, side, _) = Tee();
            var rules = new TrafficRules();
            _network.SetSpeedLimit(side, 13.9f);

            Assert.That(RightOfWayResolver.Resolve(_network, rules, side, center), Is.EqualTo(RightOfWay.RightBeforeLeft));

            rules.SetRightOfWay(side, center, RightOfWay.Stop);
            Assert.That(RightOfWayResolver.Resolve(_network, rules, side, center), Is.EqualTo(RightOfWay.Stop));
            Assert.That(RightOfWayResolver.Resolve(_network, rules, main, center), Is.EqualTo(RightOfWay.RightBeforeLeft));
        }

        [Test]
        public void ANodeWithTwoArmsNeedsNoRule()
        {
            var profile = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);
            var a = _network.AddNode(float3.zero);
            var b = _network.AddNode(new float3(50f, 0f, 0f));
            var segment = _network.AddSegment(a, b, Bezier3.Straight(float3.zero, new float3(50f, 0f, 0f)), profile, 13.9f);

            Assert.That(RightOfWayResolver.Resolve(_network, new TrafficRules(), segment, a), Is.EqualTo(RightOfWay.Priority));
        }

        [Test]
        public void ANoEntrySignBlocksTheRouteThroughThatArm()
        {
            var (center, main, _, far) = Tee();
            var inbound = Lane(main, LaneDirection.Forward);
            var outbound = Lane(far, LaneDirection.Forward);
            var rules = new TrafficRules();

            Assert.That(new LanePathfinder(_network, null, rules).FindRoute(inbound, outbound).Lanes, Is.Not.Empty);

            rules.SetNoEntry(far, center, true);

            Assert.That(new LanePathfinder(_network, null, rules).FindRoute(inbound, outbound).Lanes, Is.Empty);
            Assert.That(new LanePathfinder(_network).FindRoute(inbound, outbound).Lanes, Is.Not.Empty, "zonder regels geen verbod");
        }

        private EntityId<LaneKind> Lane(EntityId<SegmentKind> segment, LaneDirection direction)
        {
            var s = _network.GetSegment(segment);
            return Enumerable.Range(0, s.Lanes.Length)
                .Select(i => s.Lanes[i])
                .First(l => _network.GetLane(l).Spec.Direction == direction && (_network.GetLane(l).Spec.Types & LaneTypes.Vehicles) != 0);
        }
    }
}
