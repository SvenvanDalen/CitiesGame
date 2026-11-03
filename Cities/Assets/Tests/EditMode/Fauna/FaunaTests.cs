using System;
using System.Collections.Generic;
using Cities.World.Fauna;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Fauna
{
    public class FaunaTests
    {
        /// <summary>Vlak land van 4 × 4 km met een meer van 1 km breed (diepte 6 m) en een boskern waar niets open is.</summary>
        private sealed class TestHabitat : IFaunaHabitat
        {
            public const float Size = 4096f;

            public bool InBounds(float2 position) => position.x >= 0f && position.y >= 0f && position.x < Size && position.y < Size;

            public float GroundHeight(float2 position) => 10f + (position.x * 0.01f);

            public float WaterDepth(float2 position) => IsLake(position) ? 6f : 0f;

            public bool IsOpenLand(float2 position) => !IsLake(position) && !(position.y > 3000f);

            private static bool IsLake(float2 position) => position.x > 1000f && position.x < 2000f && position.y > 1000f && position.y < 2000f;
        }

        private static List<FaunaGroup> AllGroups(FaunaKind kind, ulong seed = 7UL)
        {
            var habitat = new TestHabitat();
            var result = new List<FaunaGroup>();
            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++)
                {
                    FaunaGroups.Spawn(kind, x, z, seed, habitat, result);
                }
            }

            return result;
        }

        [Test]
        public void GroupsAreTheSameForTheSameSeedAndDifferForAnother()
        {
            var first = AllGroups(FaunaKind.Birds);
            var again = AllGroups(FaunaKind.Birds);
            var other = AllGroups(FaunaKind.Birds, 8UL);

            Assert.That(first, Is.Not.Empty);
            Assert.That(again.Count, Is.EqualTo(first.Count));
            for (var i = 0; i < first.Count; i++)
            {
                Assert.That(again[i].Anchor, Is.EqualTo(first[i].Anchor));
                Assert.That(again[i].Count, Is.EqualTo(first[i].Count));
            }

            Assert.That(other[0].Anchor, Is.Not.EqualTo(first[0].Anchor));
        }

        [Test]
        public void BirdsFlyInFlocksAndSeagullsBelongToTheWater()
        {
            var groups = AllGroups(FaunaKind.Birds);

            Assert.That(groups.Count, Is.InRange(60, 150), "ongeveer 45% van 256 vakken");
            Assert.That(groups, Has.Some.Matches<FaunaGroup>(g => g.OnWater));
            Assert.That(groups, Has.Some.Matches<FaunaGroup>(g => !g.OnWater));
            foreach (var group in groups)
            {
                Assert.That(group.Count, Is.InRange(5, 12));
                Assert.That(group.Height, Is.InRange(35f, 85f));
                Assert.That(group.OnWater, Is.EqualTo(new TestHabitat().WaterDepth(group.Anchor) > 0.5f));
            }
        }

        [Test]
        public void FishLiveOnlyInDeepWaterAndDeerOnlyOnOpenLand()
        {
            var habitat = new TestHabitat();
            var fish = AllGroups(FaunaKind.Fish);
            var deer = AllGroups(FaunaKind.Deer);

            Assert.That(fish, Is.Not.Empty);
            Assert.That(deer, Is.Not.Empty);
            Assert.That(fish, Has.All.Matches<FaunaGroup>(g => habitat.WaterDepth(g.Anchor) >= FaunaGroups.MinimumFishDepth));
            Assert.That(deer, Has.All.Matches<FaunaGroup>(g => habitat.IsOpenLand(g.Anchor) && habitat.WaterDepth(g.Anchor) <= 0f));
            Assert.That(fish, Has.All.Matches<FaunaGroup>(g => g.Count >= 5 && g.Count <= 14));
            Assert.That(deer, Has.All.Matches<FaunaGroup>(g => g.Count >= 3 && g.Count <= 8));
        }

        [Test]
        public void SpawnRejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => FaunaGroups.Spawn(FaunaKind.Birds, 0, 0, 1UL, null, new List<FaunaGroup>()));
            Assert.Throws<ArgumentNullException>(() => FaunaGroups.Spawn(FaunaKind.Birds, 0, 0, 1UL, new TestHabitat(), null));
            var group = AllGroups(FaunaKind.Birds)[0];
            Assert.Throws<ArgumentNullException>(() => FaunaMotion.TryPose(group, 0, 0f, null, out _));
        }

        [Test]
        public void BirdsCircleAboveTheGroundAtFlyingSpeed()
        {
            var habitat = new TestHabitat();
            var group = FirstInside(FaunaKind.Birds, habitat);

            Assert.That(FaunaMotion.TryPose(group, 0, 10f, habitat, out var pose), Is.True);
            Assert.That(FaunaMotion.TryPose(group, 0, 11f, habitat, out var later), Is.True);
            var ground = habitat.GroundHeight(pose.Position.xz) + habitat.WaterDepth(pose.Position.xz);

            Assert.That(pose.Position.y - ground, Is.InRange(group.Height - 6.5f, group.Height + 6.5f));
            Assert.That(math.distance(pose.Position, later.Position), Is.EqualTo(group.Speed).Within(group.Speed * 0.1f), "snelheid langs de baan");
            Assert.That(math.abs(pose.Roll), Is.EqualTo(0.3f).Within(1e-4f));

            // De afstand tot het anker blijft binnen de baan, ook na een uur.
            for (var t = 0f; t < 3600f; t += 97f)
            {
                FaunaMotion.TryPose(group, 2, t, habitat, out var p);
                Assert.That(math.distance(p.Position.xz, group.Anchor), Is.InRange(group.Radius - 8.01f, group.Radius + 8.01f));
            }
        }

        [Test]
        public void BirdsAlternateBetweenGlidingAndFlapping()
        {
            var habitat = new TestHabitat();
            var group = FirstInside(FaunaKind.Birds, habitat);

            var glided = false;
            var flapped = false;
            var low = 0f;
            var high = 0f;
            for (var t = 0f; t < 120f; t += 0.05f)
            {
                FaunaMotion.TryPose(group, 1, t, habitat, out var pose);
                glided |= Math.Abs(pose.Flap - 0.15f) < 1e-6f;
                flapped |= Math.Abs(pose.Flap - 0.15f) > 0.2f;
                low = Math.Min(low, pose.Flap);
                high = Math.Max(high, pose.Flap);
            }

            Assert.That(glided && flapped, Is.True);
            Assert.That(low, Is.GreaterThanOrEqualTo(-0.65f - 1e-4f));
            Assert.That(high, Is.LessThanOrEqualTo(0.65f + 1e-4f));
            Assert.That(low, Is.LessThan(-0.3f));
            Assert.That(high, Is.GreaterThan(0.3f));
        }

        [Test]
        public void FishSwimBetweenBottomAndSurfaceAndVanishWhenTheWaterIsGone()
        {
            var habitat = new TestHabitat();
            var group = AllGroups(FaunaKind.Fish)[0];

            var seen = 0;
            for (var t = 0f; t < 600f; t += 3f)
            {
                for (var member = 0; member < group.Count; member++)
                {
                    if (FaunaMotion.TryPose(group, member, t, habitat, out var pose))
                    {
                        seen++;
                        var surface = habitat.GroundHeight(pose.Position.xz) + habitat.WaterDepth(pose.Position.xz);
                        Assert.That(pose.Position.y, Is.LessThan(surface - 0.1f));
                        Assert.That(pose.Position.y, Is.GreaterThan(surface - habitat.WaterDepth(pose.Position.xz)));
                    }
                }
            }

            Assert.That(seen, Is.GreaterThan(0));

            // Een groep waarvan het meer wegvalt (het ankerpunt ligt dan op het droge) toont geen enkele vis.
            var dry = new FaunaGroup(FaunaKind.Fish, group.Seed, new float2(100f, 100f), 8, 10f, 0.5f, 0f, true);
            for (var member = 0; member < dry.Count; member++)
            {
                Assert.That(FaunaMotion.TryPose(dry, member, 5f, habitat, out _), Is.False);
            }
        }

        [Test]
        public void DeerGrazeSlowlyOnTheGroundAndSkipPlacesThatAreNotOpen()
        {
            var habitat = new TestHabitat();
            var group = AllGroups(FaunaKind.Deer)[0];

            Assert.That(FaunaMotion.TryPose(group, 0, 20f, habitat, out var pose), Is.True);
            Assert.That(pose.Position.y, Is.EqualTo(habitat.GroundHeight(pose.Position.xz)).Within(1e-4f));
            Assert.That(pose.Pitch, Is.EqualTo(math.atan2(0.01f * 2f * math.sin(pose.Yaw), 2f)).Within(1e-3f), "hellingshoek langs de koers");

            FaunaMotion.TryPose(group, 0, 21f, habitat, out var later);
            Assert.That(math.distance(pose.Position, later.Position), Is.LessThan(1f), "grazen is stapvoets");

            // Het bos (z > 3000) is geen open land: een kudde die daar zou staan, is er niet.
            var forest = new FaunaGroup(FaunaKind.Deer, group.Seed, new float2(500f, 3500f), 4, 12f, 0.2f, 0f, false);
            Assert.That(FaunaMotion.TryPose(forest, 0, 1f, habitat, out _), Is.False);

            // Midden in het meer ook niet.
            var lake = new FaunaGroup(FaunaKind.Deer, group.Seed, new float2(1500f, 1500f), 4, 12f, 0.2f, 0f, false);
            Assert.That(FaunaMotion.TryPose(lake, 0, 1f, habitat, out _), Is.False);
        }

        [Test]
        public void MembersOutsideTheGroupOrTheMapAreNotThere()
        {
            var habitat = new TestHabitat();
            var group = FirstInside(FaunaKind.Birds, habitat);

            Assert.That(FaunaMotion.TryPose(group, -1, 0f, habitat, out _), Is.False);
            Assert.That(FaunaMotion.TryPose(group, group.Count, 0f, habitat, out _), Is.False);

            var offMap = new FaunaGroup(FaunaKind.Birds, group.Seed, new float2(-500f, -500f), 6, 50f, 9f, 40f, false);
            Assert.That(FaunaMotion.TryPose(offMap, 0, 0f, habitat, out _), Is.False);
        }

        [Test]
        public void ReachCoversTheOrbitPlusMargin()
        {
            var group = new FaunaGroup(FaunaKind.Birds, 1UL, float2.zero, 5, 60f, 9f, 40f, false);

            Assert.That(FaunaGroups.Reach(group), Is.EqualTo(80f));
            Assert.That(FaunaGroups.Unit(1UL, 1), Is.InRange(0f, 0.9999999f));
        }

        [Test]
        public void ShapesAreClosedSolidsOfTheRightSize()
        {
            AssertSize(FaunaShapes.Deer(), new float3(0.2f, 0.7f, 1f), new float3(0.7f, 1.2f, 1.3f));
            AssertSize(FaunaShapes.BirdBody(), new float3(0.1f, 0.1f, 0.5f), new float3(0.2f, 0.2f, 0.8f));
            AssertSize(FaunaShapes.BirdWing(), new float3(0.5f, 0f, 0.15f), new float3(0.8f, 0.1f, 0.4f));
            AssertSize(FaunaShapes.Fish(), new float3(0.05f, 0.1f, 0.25f), new float3(0.2f, 0.3f, 0.45f));
        }

        [Test]
        public void ShapeTrianglesFaceTheirNormals()
        {
            foreach (var shape in new[] { FaunaShapes.Deer(), FaunaShapes.BirdBody(), FaunaShapes.BirdWing(), FaunaShapes.Fish() })
            {
                Assert.That(shape.Vertices.Length, Is.EqualTo(shape.Normals.Length));
                Assert.That(shape.Triangles.Length % 3, Is.EqualTo(0));
                Assert.That(shape.Vertices.Length, Is.LessThan(1000), "weinig driehoeken: er staan er honderden in beeld");
                for (var i = 0; i < shape.Triangles.Length; i += 3)
                {
                    var a = shape.Triangles[i];
                    var b = shape.Triangles[i + 1];
                    var c = shape.Triangles[i + 2];
                    Assert.That(Math.Max(a, Math.Max(b, c)), Is.LessThan(shape.Vertices.Length));
                    var face = math.cross(shape.Vertices[b] - shape.Vertices[a], shape.Vertices[c] - shape.Vertices[a]);
                    var normal = shape.Normals[a] + shape.Normals[b] + shape.Normals[c];
                    Assert.That(math.dot(face, normal), Is.GreaterThanOrEqualTo(-1e-9f), "driehoek " + (i / 3) + " kijkt naar binnen");
                }

                foreach (var n in shape.Normals)
                {
                    Assert.That(math.length(n), Is.EqualTo(1f).Within(1e-4f));
                }
            }
        }

        private static void AssertSize(FaunaShape shape, float3 minSize, float3 maxSize)
        {
            var (min, max) = shape.Bounds();
            var size = max - min;

            Assert.That(size.x, Is.InRange(minSize.x, maxSize.x), "breedte");
            Assert.That(size.y, Is.InRange(minSize.y, maxSize.y), "hoogte");
            Assert.That(size.z, Is.InRange(minSize.z, maxSize.z), "lengte");
        }

        private static FaunaGroup FirstInside(FaunaKind kind, TestHabitat habitat)
        {
            foreach (var group in AllGroups(kind))
            {
                if (habitat.InBounds(group.Anchor - new float2(300f)) && habitat.InBounds(group.Anchor + new float2(300f)))
                {
                    return group;
                }
            }

            throw new InvalidOperationException("Geen groep gevonden.");
        }
    }
}
