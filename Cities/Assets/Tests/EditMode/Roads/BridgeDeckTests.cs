using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using Cities.World.Roads;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Roads
{
    public class BridgeDeckTests
    {
        private static readonly List<LaneSpec> TwoLane = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);

        /// <summary>Terrein langs de x-as op drie stukken: tot 100 m en vanaf 200 m <paramref name="outer"/>, ertussen <paramref name="middle"/>.</summary>
        private sealed class Terrain : ITerrainHeight
        {
            private readonly float _outer;
            private readonly float _middle;

            public Terrain(float outer, float middle)
            {
                _outer = outer;
                _middle = middle;
            }

            public float SurfaceHeight(float2 position) => position.x > 100f && position.x < 200f ? _middle : _outer;
        }

        private static Bezier3 Deck(float height, float length = 300f) => Bezier3.Straight(new float3(0f, height, 0f), new float3(length, height, 0f));

        [Test]
        public void ClearanceIsTheDistanceFromTheDeckToTheGround()
        {
            var (lowest, highest) = BridgeDeck.Clearance(Deck(0f), new Terrain(0f, -8f));

            Assert.That(lowest, Is.Zero.Within(1e-4f));
            Assert.That(highest, Is.EqualTo(8f).Within(1e-4f));
            Assert.Throws<ArgumentNullException>(() => BridgeDeck.Clearance(Deck(0f), null));
        }

        [Test]
        public void SuggestPicksABridgeOverAValleyAndATunnelUnderAHill()
        {
            Assert.That(BridgeDeck.Suggest(Deck(0f), new Terrain(0f, -8f)), Is.EqualTo(RoadElevation.Bridge));
            Assert.That(BridgeDeck.Suggest(Deck(0f), new Terrain(0f, 8f)), Is.EqualTo(RoadElevation.Tunnel));
            Assert.That(BridgeDeck.Suggest(Deck(0f), new Terrain(0f, 1f)), Is.EqualTo(RoadElevation.Ground), "onder de vaste hoogte blijft het een gewone weg");
        }

        [Test]
        public void SuggestTakesTheBiggestDifferenceWhenBothApply()
        {
            Assert.That(BridgeDeck.Suggest(Deck(0f), new Terrain(6f, -9f)), Is.EqualTo(RoadElevation.Bridge));
            Assert.That(BridgeDeck.Suggest(Deck(0f), new Terrain(9f, -6f)), Is.EqualTo(RoadElevation.Tunnel));
        }

        [Test]
        public void ABridgeMustStayAboveTheTerrain()
        {
            Assert.That(BridgeDeck.Check(RoadElevation.Bridge, Deck(0f), new Terrain(0f, -8f)), Is.Null);
            Assert.That(BridgeDeck.Check(RoadElevation.Bridge, Deck(0f), new Terrain(0f, 8f)), Does.Contain("onder het terrein"));
        }

        [Test]
        public void ATunnelMustStayUnderTheTerrain()
        {
            Assert.That(BridgeDeck.Check(RoadElevation.Tunnel, Deck(0f), new Terrain(0f, 8f)), Is.Null);
            Assert.That(BridgeDeck.Check(RoadElevation.Tunnel, Deck(0f), new Terrain(0f, -8f)), Does.Contain("boven het terrein"));
        }

        [Test]
        public void TheRampMayNotBeSteeperThanFifteenPercent()
        {
            var gentle = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(100f, 14f, 0f));
            var steep = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(100f, 16f, 0f));
            var ground = new Terrain(-100f, -100f);

            Assert.That(BridgeDeck.Check(RoadElevation.Bridge, gentle, ground), Is.Null);
            Assert.That(BridgeDeck.Check(RoadElevation.Bridge, steep, ground), Does.Contain("Te steil voor een brug").And.Contain("15%"));
            Assert.That(BridgeDeck.Check(RoadElevation.Tunnel, steep, new Terrain(100f, 100f)), Does.Contain("Te steil voor een tunnel"));
        }

        [Test]
        public void AGroundRoadNeedsNoCheck()
        {
            Assert.That(BridgeDeck.Check(RoadElevation.Ground, Deck(50f), new Terrain(0f, 0f)), Is.Null);
        }

        [Test]
        public void BridgesAndTunnelsCostMoreThanRoad()
        {
            Assert.That(BridgeDeck.Cost(100f, 10, RoadElevation.Ground), Is.EqualTo(1000));
            Assert.That(BridgeDeck.Cost(100f, 10, RoadElevation.Bridge), Is.EqualTo(4000));
            Assert.That(BridgeDeck.Cost(100f, 10, RoadElevation.Tunnel), Is.EqualTo(6000));
        }

        [Test]
        public void TheDeckHangsUnderTheRoadWithSidesAndAnUnderside()
        {
            var curve = Bezier3.Straight(float3.zero, new float3(0f, 0f, 40f));

            var data = RoadMeshBuilder.Build(curve, TwoLane);
            var deck = RoadMeshBuilder.BuildDeck(curve, TwoLane);

            Assert.That(deck.Vertices.Count, Is.EqualTo(deck.Normals.Count).And.EqualTo(deck.Uvs.Count));
            Assert.That(deck.Vertices.Min(v => v.y), Is.EqualTo(-RoadMeshBuilder.DeckThickness).Within(1e-4f));
            Assert.That(deck.Vertices.Max(v => v.y), Is.Zero.Within(1e-4f), "het dek zit onder het wegdek");
            Assert.That(deck.Normals.Count(n => n.y < -0.9f), Is.GreaterThan(0), "onderkant kijkt naar beneden");
            Assert.That(deck.Normals.Count(n => math.abs(n.x) > 0.9f), Is.GreaterThan(0), "zijwanden kijken naar buiten");
            Assert.That(deck.Triangles[(int)RoadSurface.Curb], Is.Not.Empty);
            Assert.That(deck.Triangles[(int)RoadSurface.Asphalt], Is.Empty, "het wegdek zelf komt uit Build");
            Assert.That(data.Triangles[(int)RoadSurface.Asphalt], Is.Not.Empty);
        }

        [Test]
        public void TheDeckNeedsLanes()
        {
            var curve = Bezier3.Straight(float3.zero, new float3(0f, 0f, 40f));

            Assert.Throws<ArgumentNullException>(() => RoadMeshBuilder.BuildDeck(curve, null));
            Assert.Throws<ArgumentException>(() => RoadMeshBuilder.BuildDeck(curve, new List<LaneSpec>()));
        }
    }
}
