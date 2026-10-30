using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Simulation.Network;
using Cities.World.Roads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Roads
{
    public class RoadMeshBuilderTests
    {
        private static readonly List<LaneSpec> TwoLane = LaneProfiles.Road(1, bikeLanes: false, sidewalks: true);

        private static Bezier3 North(float length, float y = 5f) => Bezier3.Straight(new float3(100f, y, 100f), new float3(100f, y, 100f + length));

        /// <summary>De normaal van een driehoek zoals Unity die ziet (met de klok mee is voorkant).</summary>
        private static float3 FaceNormal(RoadMeshData data, int a, int b, int c) =>
            math.normalize(math.cross(data.Vertices[b] - data.Vertices[a], data.Vertices[c] - data.Vertices[a]));

        private static IEnumerable<float3> Faces(RoadMeshData data, RoadSurface surface)
        {
            var triangles = data.Triangles[(int)surface];
            for (var i = 0; i < triangles.Count; i += 3)
            {
                yield return FaceNormal(data, triangles[i], triangles[i + 1], triangles[i + 2]);
            }
        }

        [Test]
        public void ARoadHasEveryKindOfSurfaceWithConsistentArrays()
        {
            var data = RoadMeshBuilder.Build(North(60f), LaneProfiles.Road(2, bikeLanes: true, sidewalks: true));

            for (var surface = RoadSurface.Asphalt; surface <= RoadSurface.MarkingDashed; surface++)
            {
                Assert.That(data.Triangles[(int)surface], Is.Not.Empty, surface.ToString());
                Assert.That(data.Triangles[(int)surface].Count % 3, Is.EqualTo(0));
            }

            Assert.That(data.Normals.Count, Is.EqualTo(data.Vertices.Count));
            Assert.That(data.Uvs.Count, Is.EqualTo(data.Vertices.Count));
            Assert.That(data.Triangles.SelectMany(t => t).Max(), Is.LessThan(data.Vertices.Count));
        }

        [Test]
        public void TopSurfacesFaceUpAndCurbsFaceTheRoad()
        {
            var data = RoadMeshBuilder.Build(North(40f), TwoLane);

            foreach (var surface in new[] { RoadSurface.Asphalt, RoadSurface.Sidewalk, RoadSurface.MarkingSolid })
            {
                Assert.That(Faces(data, surface).All(n => n.y > 0.99f), Is.True, surface.ToString());
            }

            // Rechts van de weg (+x) ligt een stoep: de wanden kijken naar de weg (-x) of naar buiten (+x), nooit omhoog of omlaag.
            var curb = Faces(data, RoadSurface.Curb).ToList();
            Assert.That(curb.All(n => math.abs(n.y) < 1e-3f), Is.True);
            Assert.That(curb.Count(n => n.x < -0.99f), Is.GreaterThan(0));
            Assert.That(curb.Count(n => n.x > 0.99f), Is.GreaterThan(0));
        }

        [Test]
        public void SidewalksStandOneCurbAboveTheRoadAndFollowItsHeight()
        {
            var data = RoadMeshBuilder.Build(North(40f, 12f), TwoLane);

            var asphalt = data.Triangles[(int)RoadSurface.Asphalt].Select(i => data.Vertices[i].y).Distinct().ToList();
            var sidewalk = data.Triangles[(int)RoadSurface.Sidewalk].Select(i => data.Vertices[i].y).Distinct().ToList();

            Assert.That(asphalt.All(y => Math.Abs(y - 12f) < 1e-4f), Is.True);
            Assert.That(sidewalk.All(y => Math.Abs(y - (12f + RoadMeshBuilder.CurbHeight)) < 1e-4f), Is.True);
            Assert.That(data.Vertices.Min(v => v.y), Is.EqualTo(12f - RoadMeshBuilder.SkirtDepth).Within(1e-4f), "de rok hangt onder het wegdek");
        }

        [Test]
        public void LanesFillTheirOwnWidth()
        {
            var data = RoadMeshBuilder.Build(North(30f), TwoLane);

            var xs = data.Triangles[(int)RoadSurface.Asphalt].Select(i => data.Vertices[i].x - 100f).ToList();
            Assert.That(xs.Min(), Is.EqualTo(-3.25f).Within(1e-3f));
            Assert.That(xs.Max(), Is.EqualTo(3.25f).Within(1e-3f));
        }

        [Test]
        public void MarkingsFollowTheLaneLayout()
        {
            var oneLane = RoadMeshBuilder.Build(North(40f), TwoLane);
            var twoLanes = RoadMeshBuilder.Build(North(40f), LaneProfiles.Road(2, bikeLanes: true, sidewalks: true));
            var oneWay = RoadMeshBuilder.Build(North(40f), LaneProfiles.Road(2, bikeLanes: false, sidewalks: false, oneWay: true));

            Assert.That(oneLane.Triangles[(int)RoadSurface.MarkingDashed], Is.Empty, "een lane per kant: alleen de middenlijn");
            Assert.That(twoLanes.Triangles[(int)RoadSurface.MarkingDashed], Is.Not.Empty, "gestippeld tussen lanes van dezelfde richting");
            Assert.That(oneWay.Triangles[(int)RoadSurface.MarkingDashed], Is.Not.Empty);
            Assert.That(twoLanes.Triangles[(int)RoadSurface.Bike], Is.Not.Empty);
            Assert.That(oneWay.Triangles[(int)RoadSurface.Sidewalk], Is.Empty);
        }

        [Test]
        public void TextureCoordinatesRunOneUnitPerEightMeters()
        {
            var data = RoadMeshBuilder.Build(North(40f), TwoLane);

            var v = data.Uvs.Select(uv => uv.y).ToList();
            Assert.That(v.Min(), Is.EqualTo(0f));
            Assert.That(v.Max(), Is.EqualTo(40f / RoadMeshBuilder.TextureMeters).Within(1e-3f));
            Assert.That(data.Uvs.All(uv => uv.x >= 0f && uv.x <= 1f), Is.True);
        }

        [Test]
        public void SampleCountGrowsWithLengthAndCurvatureButStaysBounded()
        {
            var shortRoad = RoadMeshBuilder.SampleCount(North(2f));
            var longRoad = RoadMeshBuilder.SampleCount(North(300f));
            var bend = RoadMeshBuilder.SampleCount(Bezier3.FromTangents(float3.zero, new float3(0f, 0f, 1f), new float3(30f, 0f, 30f), new float3(1f, 0f, 0f)));

            Assert.That(shortRoad, Is.EqualTo(2));
            Assert.That(longRoad, Is.EqualTo(101));
            Assert.That(bend, Is.GreaterThan(RoadMeshBuilder.SampleCount(Bezier3.Straight(float3.zero, new float3(30f, 0f, 30f)))), "een bocht krijgt meer stappen dan een rechte van dezelfde lengte");
            Assert.That(RoadMeshBuilder.SampleCount(North(100000f)), Is.EqualTo(RoadMeshBuilder.MaxSamples));
        }

        [Test]
        public void CurvedRoadsStayOnTheirCurve()
        {
            var curve = Bezier3.FromTangents(float3.zero, new float3(0f, 0f, 1f), new float3(60f, 3f, 60f), new float3(1f, 0f, 0f));

            var data = RoadMeshBuilder.Build(curve, TwoLane);

            var last = data.Vertices.Where(v => math.distance(v.xz, curve.P3.xz) < 4f).ToList();
            Assert.That(last, Is.Not.Empty);
            Assert.That(data.Vertices.Max(v => v.y), Is.EqualTo(3f + RoadMeshBuilder.CurbHeight).Within(1e-3f), "volgt de hoogte van de hartlijn");
        }

        [Test]
        public void InvalidInputIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => RoadMeshBuilder.Build(North(10f), null));
        }

        [Test]
        public void TerrainFallOffIsOneOnTheRoadAndZeroBeyondTheShoulder()
        {
            Assert.That(RoadTerrainShaper.Weight(5f, 12f, 28f), Is.EqualTo(1f));
            Assert.That(RoadTerrainShaper.Weight(12f, 12f, 28f), Is.EqualTo(1f));
            Assert.That(RoadTerrainShaper.Weight(20f, 12f, 28f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(RoadTerrainShaper.Weight(40f, 12f, 28f), Is.EqualTo(0f));
        }

        [Test]
        public void HalfWidthIsTheOutermostEdge()
        {
            Assert.That(RoadTerrainShaper.HalfWidth(TwoLane), Is.EqualTo(5.25f).Within(1e-3f));
            Assert.That(RoadTerrainShaper.HalfWidth(LaneProfiles.Road(2, false, false)), Is.EqualTo(6.5f).Within(1e-3f));
            Assert.Throws<ArgumentNullException>(() => RoadTerrainShaper.HalfWidth(null));
        }
    }
}
