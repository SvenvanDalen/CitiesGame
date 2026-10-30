using System;
using System.Collections.Generic;
using System.Linq;
using Cities.World.Roads;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Roads
{
    public class RoadJunctionBuilderTests
    {
        private const float Carriageway = 3.25f;
        private const float Sidewalk = 2f;

        private static float3 Compass(float degrees) => new float3(math.sin(math.radians(degrees)), 0f, math.cos(math.radians(degrees)));

        private static List<JunctionArm> Arms(params float[] degrees) =>
            degrees.Select(d => new JunctionArm(Compass(d), Carriageway, Sidewalk)).ToList();

        private static List<JunctionMouth> Mouths(IReadOnlyList<JunctionArm> arms)
        {
            var trims = RoadJunctionBuilder.Trims(arms);
            return arms.Select((arm, i) => new JunctionMouth(arm.Direction * trims[i], arm.Direction, arm.Carriageway, arm.Sidewalk)).ToList();
        }

        private static float Area(RoadMeshData data, RoadSurface surface)
        {
            var triangles = data.Triangles[(int)surface];
            var area = 0f;
            for (var i = 0; i < triangles.Count; i += 3)
            {
                var a = data.Vertices[triangles[i]];
                var b = data.Vertices[triangles[i + 1]];
                var c = data.Vertices[triangles[i + 2]];
                area += math.length(math.cross(b - a, c - a)) * 0.5f;
            }

            return area;
        }

        [Test]
        public void ACrossingOfTwoRoadsStopsNineAndAQuarterMetersFromTheMiddle()
        {
            var trims = RoadJunctionBuilder.Trims(Arms(0f, 90f, 180f, 270f));

            // De randen snijden op 3,25 m en de afronding van 6 m begint 6 m verder.
            Assert.That(trims, Is.All.EqualTo(9.25f).Within(1e-3f));
        }

        [Test]
        public void SharpAnglesAskForLongerTrimsThanRightAngles()
        {
            var trims = RoadJunctionBuilder.Trims(Arms(0f, 40f, 180f));

            Assert.That(trims[0], Is.GreaterThan(9.25f));
            Assert.That(trims[1], Is.GreaterThan(9.25f));
            Assert.That(trims[2], Is.EqualTo(RoadJunctionBuilder.MinTrim), "de weg tegenover de splitsing heeft geen hoek");
            Assert.That(trims, Is.All.LessThanOrEqualTo(RoadJunctionBuilder.MaxTrim));
        }

        [Test]
        public void JunctionsNeedThreeToEightArms()
        {
            Assert.Throws<ArgumentException>(() => RoadJunctionBuilder.Trims(Arms(0f, 180f)));
            Assert.Throws<ArgumentException>(() => RoadJunctionBuilder.Trims(Arms(0f, 40f, 80f, 120f, 160f, 200f, 240f, 280f, 320f)));
            Assert.Throws<ArgumentException>(() => RoadJunctionBuilder.Trims(null));
            Assert.Throws<ArgumentException>(() => RoadJunctionBuilder.Build(null));
        }

        [Test]
        public void ACrossingFillsTheCrossAndRoundsTheCorners()
        {
            var data = RoadJunctionBuilder.Build(Mouths(Arms(0f, 90f, 180f, 270f)));

            // Twee rijbanen van 6,5 x 18,5 m, min het gedeelde midden, plus vier hoeken van 6 m min een kwartcirkel.
            var expected = (2f * 6.5f * 18.5f) - (6.5f * 6.5f) + (4f * ((6f * 6f) - (math.PI * 36f / 4f)));
            Assert.That(Area(data, RoadSurface.Asphalt), Is.EqualTo(expected).Within(expected * 0.02f));
        }

        [Test]
        public void EverySurfaceFacesTheRightWay()
        {
            var data = RoadJunctionBuilder.Build(Mouths(Arms(0f, 90f, 180f, 270f)));

            foreach (var surface in new[] { RoadSurface.Asphalt, RoadSurface.Sidewalk, RoadSurface.MarkingSolid })
            {
                var triangles = data.Triangles[(int)surface];
                Assert.That(triangles, Is.Not.Empty, surface.ToString());
                for (var i = 0; i < triangles.Count; i += 3)
                {
                    var normal = math.normalize(math.cross(data.Vertices[triangles[i + 1]] - data.Vertices[triangles[i]], data.Vertices[triangles[i + 2]] - data.Vertices[triangles[i]]));
                    Assert.That(normal.y, Is.GreaterThan(0.99f), surface.ToString());
                }
            }

            Assert.That(data.Triangles[(int)RoadSurface.Curb], Is.Not.Empty);
            Assert.That(data.Normals.Count, Is.EqualTo(data.Vertices.Count));
            Assert.That(data.Uvs.Count, Is.EqualTo(data.Vertices.Count));
            Assert.That(data.Triangles.SelectMany(t => t).Max(), Is.LessThan(data.Vertices.Count));
        }

        [Test]
        public void EachArmWithASidewalkGetsAZebraCrossing()
        {
            var withSidewalk = RoadJunctionBuilder.Build(Mouths(Arms(0f, 90f, 180f, 270f)));
            var arms = new[] { 0f, 90f, 180f, 270f }.Select(d => new JunctionArm(Compass(d), Carriageway, 0f)).ToList();
            var without = RoadJunctionBuilder.Build(Mouths(arms));

            Assert.That(withSidewalk.Triangles[(int)RoadSurface.MarkingSolid].Count / 6, Is.EqualTo(4 * 7), "zeven banen op vier armen");
            Assert.That(without.Triangles[(int)RoadSurface.MarkingSolid], Is.Empty);
            Assert.That(without.Triangles[(int)RoadSurface.Sidewalk], Is.Empty);
            Assert.That(without.Triangles[(int)RoadSurface.Curb], Is.Not.Empty, "alleen een rok tegen kieren");
        }

        [Test]
        public void AnyNumberOfArmsInAnyAngleBuildsAJunction()
        {
            var layouts = new[]
            {
                new[] { 0f, 120f, 240f },
                new[] { 0f, 90f, 180f },
                new[] { 10f, 20f, 200f },
                new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f },
                new[] { 0f, 70f, 80f, 175f, 260f },
            };

            foreach (var degrees in layouts)
            {
                var arms = Arms(degrees);
                var data = RoadJunctionBuilder.Build(Mouths(arms));

                Assert.That(Area(data, RoadSurface.Asphalt), Is.GreaterThan(30f), string.Join(",", degrees));
                Assert.That(data.Vertices.All(v => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)), Is.True, string.Join(",", degrees));
            }
        }

        [Test]
        public void TheJunctionFollowsTheHeightOfItsArms()
        {
            var arms = Arms(0f, 90f, 180f, 270f);
            var trims = RoadJunctionBuilder.Trims(arms);
            var mouths = arms.Select((arm, i) => new JunctionMouth((arm.Direction * trims[i]) + new float3(0f, 5f, 0f), arm.Direction, Carriageway, Sidewalk)).ToList();

            var data = RoadJunctionBuilder.Build(mouths);

            var pavement = data.Triangles[(int)RoadSurface.Asphalt].Select(i => data.Vertices[i].y).Distinct().ToList();
            Assert.That(pavement.All(y => math.abs(y - 5f) < 1e-3f), Is.True);
        }
    }
}
