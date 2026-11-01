using System;
using System.Linq;
using Cities.Simulation.Network;
using Cities.World.Roads;
using Cities.World.Terrain;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Roads
{
    public class ViaductTests
    {
        [Test]
        public void ElevatedStartsAtTheMinimumClearance()
        {
            Assert.That(Viaduct.IsElevated(2.9f), Is.False);
            Assert.That(Viaduct.IsElevated(Viaduct.MinClearance), Is.True);
        }

        [Test]
        public void RampLengthFollowsTheMaximumGrade()
        {
            Assert.That(Viaduct.RampLength(6f), Is.EqualTo(100f).Within(1e-3f), "6 m stijgen bij 6% is 100 m oprit");
            Assert.That(Viaduct.RampLength(-5f), Is.Zero);
        }

        [Test]
        public void GradeIsMeasuredAlongTheDeckAndLimited()
        {
            var gentle = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(200f, 6f, 0f));
            var steep = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(100f, 20f, 0f));
            var vertical = Bezier3.Straight(new float3(0f, 0f, 0f), new float3(0f, 10f, 0f));

            Assert.That(Viaduct.SteepestGrade(gentle), Is.EqualTo(0.03f).Within(1e-4f));
            Assert.That(Viaduct.GradeAllowed(gentle), Is.True);
            Assert.That(Viaduct.GradeAllowed(steep), Is.False);
            Assert.That(Viaduct.SteepestGrade(vertical), Is.Zero, "zonder horizontale afstand geen helling");
        }

        [Test]
        public void PiersStandAtMostThirtyMetresApartOnlyUnderTheElevatedPart()
        {
            var ground = new FlatTerrain(10f);
            var deck = Bezier3.Straight(new float3(0f, 10f, 0f), new float3(300f, 20f, 0f));

            var piers = Viaduct.Piers(deck, ground);

            Assert.That(piers, Is.Not.Empty);
            Assert.That(piers.Count, Is.LessThan(11), "de eerste stukken liggen op de grond");
            Assert.That(piers, Has.All.Matches<ViaductPier>(p => p.Height >= Viaduct.MinClearance));
            Assert.That(piers.Select(p => p.Base.y), Has.All.EqualTo(10f), "pijlers staan op de grond");
            Assert.That(piers[piers.Count - 1].Height, Is.EqualTo(10f).Within(1e-3f), "aan het eind 10 m boven de grond");
            Assert.That(piers[1].Base.x - piers[0].Base.x, Is.InRange(25f, Viaduct.PierSpacing), "hooguit de maximale afstand");
        }

        [Test]
        public void RoadOnTheGroundHasNoPiersAndMissingTerrainIsRejected()
        {
            var flat = Bezier3.Straight(new float3(0f, 10f, 0f), new float3(100f, 10f, 0f));

            Assert.That(Viaduct.Piers(flat, new FlatTerrain(10f)), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => Viaduct.Piers(flat, null));
        }
    }
}
