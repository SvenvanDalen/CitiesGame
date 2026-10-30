using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Network
{
    public class LaneConnectorTests
    {
        private static readonly float2 North = new float2(0f, 1f);
        private static readonly float2 East = new float2(1f, 0f);
        private static readonly float2 South = new float2(0f, -1f);
        private static readonly float2 West = new float2(-1f, 0f);

        /// <summary>Een arm van een tweebaansweg met stoepen; de lanes krijgen nummers vanaf <paramref name="first"/>.</summary>
        private static NodeArm Arm(float2 heading, bool nodeIsEnd, int first, IReadOnlyList<LaneSpec> profile) =>
            new NodeArm(heading, nodeIsEnd, profile.Select((spec, i) => new ArmLane(first + i, spec)).ToList());

        private static readonly List<LaneSpec> TwoLane = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);

        [Test]
        public void TurnsAreSeenFromTheDriver()
        {
            // Binnenkomen vanuit het zuiden (de arm wijst naar het zuiden), dus rijdend naar het noorden.
            var south = Arm(South, true, 0, TwoLane);

            Assert.That(LaneConnector.Classify(south, Arm(North, false, 10, TwoLane), false), Is.EqualTo(Turn.Straight));
            Assert.That(LaneConnector.Classify(south, Arm(East, false, 10, TwoLane), false), Is.EqualTo(Turn.Right));
            Assert.That(LaneConnector.Classify(south, Arm(West, false, 10, TwoLane), false), Is.EqualTo(Turn.Left));
            Assert.That(LaneConnector.Classify(south, south, true), Is.EqualTo(Turn.UTurn));
        }

        [Test]
        public void CrossroadsConnectEveryArmButNotBack()
        {
            // Profiel: 0 heen, 1 terug, 2 en 3 stoepen. Elk segment eindigt op het kruispunt.
            var arms = new[] { Arm(South, true, 0, TwoLane), Arm(East, true, 10, TwoLane), Arm(North, true, 20, TwoLane), Arm(West, true, 30, TwoLane) };

            var pairs = LaneConnector.Connect(arms);

            // Uit het zuiden komt lane 0 binnen; vertrekken gaat over de terug-lanes (1) van de andere armen.
            Assert.That(pairs.Where(p => p.From == 0).Select(p => (p.To, p.Turn)), Is.EquivalentTo(new[] { (11, Turn.Right), (21, Turn.Straight), (31, Turn.Left) }));
            Assert.That(pairs.Any(p => p.From == 0 && p.To == 1), Is.False, "geen keren op een kruispunt");
            Assert.That(pairs.Count(p => p.From == 2), Is.EqualTo(6), "een stoep sluit aan op beide stoepen van de drie andere armen");
            Assert.That(pairs.All(p => (p.From % 10 < 2) == (p.To % 10 < 2)), Is.True, "rijbaan en stoep blijven gescheiden");
        }

        [Test]
        public void MultiLaneRoadsTurnFromTheOuterLanesAndGoStraightLaneByLane()
        {
            var fourLane = LaneProfiles.Road(2, bikeLanes: false, sidewalks: false);

            // Profiel: 0 heen bij de hartlijn, 1 terug, 2 heen buiten (rechts), 3 terug. Zuid eindigt op de node; noord en oost beginnen er.
            var arms = new[] { Arm(South, true, 0, fourLane), Arm(North, false, 10, fourLane), Arm(East, false, 20, fourLane) };

            var pairs = LaneConnector.Connect(arms);

            Assert.That(pairs.Where(p => p.Turn == Turn.Straight && p.From < 10).Select(p => (p.From, p.To)), Is.EquivalentTo(new[] { (0, 10), (2, 12) }));
            Assert.That(pairs.Where(p => p.Turn == Turn.Right && p.From < 10).Select(p => (p.From, p.To)), Is.EquivalentTo(new[] { (2, 22) }));
        }

        [Test]
        public void ExtraLanesMergeIntoTheOuterLane()
        {
            var fourLane = LaneProfiles.Road(2, bikeLanes: false, sidewalks: false);
            var twoLane = LaneProfiles.Road(1, bikeLanes: false, sidewalks: false);
            var arms = new[] { Arm(South, true, 0, fourLane), Arm(North, false, 10, twoLane) };

            var pairs = LaneConnector.Connect(arms).Where(p => p.From < 10).Select(p => (p.From, p.To));

            Assert.That(pairs, Is.EquivalentTo(new[] { (0, 10), (2, 10) }));
        }

        [Test]
        public void DeadEndsTurnAroundOnTheLeftLane()
        {
            var pairs = LaneConnector.Connect(new[] { Arm(South, true, 0, TwoLane) });

            Assert.That(pairs, Is.EquivalentTo(new[] { new LanePair(0, 1, Turn.UTurn) }));
        }

        [Test]
        public void BikesStayOnBikeLanes()
        {
            var withBikes = LaneProfiles.Road(1, bikeLanes: true, sidewalks: false);
            var arms = new[] { Arm(South, true, 0, withBikes), Arm(North, false, 10, withBikes) };

            var pairs = LaneConnector.Connect(arms);

            // Profiel: 0 heen, 1 terug, 2 fiets heen, 3 fiets terug.
            Assert.That(pairs.Where(p => p.From == 2).Select(p => p.To), Is.EqualTo(new[] { 12 }));
            Assert.That(pairs.Where(p => p.From == 0).Select(p => p.To), Is.EqualTo(new[] { 10 }));
            Assert.That(new LanePair(2, 12, Turn.Straight).ToString(), Is.EqualTo("2 → 12 (Straight)"));
        }

        [Test]
        public void ArmsMustHaveLanes()
        {
            Assert.Throws<ArgumentNullException>(() => _ = new NodeArm(North, true, null));
            Assert.Throws<ArgumentNullException>(() => LaneConnector.Connect(null));
        }
    }
}
