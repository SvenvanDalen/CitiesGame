using System;
using System.Linq;
using Cities.World.Buildings;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.Buildings
{
    public class ProceduralBuildingTests
    {
        private static ProceduralBuildingSpec Spec(int w = 4, int d = 3, int floors = 3, RoofStyle roof = RoofStyle.Flat, ulong seed = 1) =>
            new ProceduralBuildingSpec(w, d, 8f, floors, roof, seed);

        [Test]
        public void EveryFloorHasFourCornersAndFacadesOnAllSides()
        {
            var modules = ProceduralBuilding.Build(Spec());

            for (var floor = 0; floor < 3; floor++)
            {
                Assert.That(modules.Count(m => m.Floor == floor && m.Kind == BuildingModuleKind.Corner), Is.EqualTo(4));
                var facades = modules.Where(m => m.Floor == floor && m.Kind != BuildingModuleKind.Corner).ToList();
                Assert.That(facades.Select(m => m.YawDegrees).Distinct().Count(), Is.EqualTo(4));
            }
        }

        [Test]
        public void FacadeModulesFillTheSideExactly()
        {
            var modules = ProceduralBuilding.Build(Spec(4, 3, 1));
            var front = modules.Where(m => m.Floor == 0 && m.YawDegrees == 180f && m.Kind != BuildingModuleKind.Corner).ToList();

            var total = front.Sum(m => m.Width) + (2f * ProceduralBuilding.CornerSize);

            Assert.That(total, Is.EqualTo(4 * 8f).Within(0.001f));
        }

        [Test]
        public void ThereIsExactlyOneDoorOnTheGroundFloorFront()
        {
            var modules = ProceduralBuilding.Build(Spec());
            var doors = modules.Where(m => m.Kind == BuildingModuleKind.Door).ToList();

            Assert.That(doors, Has.Count.EqualTo(1));
            Assert.That(doors[0].Floor, Is.EqualTo(0));
            Assert.That(doors[0].Z, Is.LessThan(0f), "De voorkant ligt aan -Z");
        }

        [Test]
        public void SameSeedGivesSameBuildingAndOtherSeedsVary()
        {
            var first = ProceduralBuilding.Build(Spec(seed: 5)).Select(m => m.Kind).ToList();
            var again = ProceduralBuilding.Build(Spec(seed: 5)).Select(m => m.Kind).ToList();
            var other = ProceduralBuilding.Build(Spec(seed: 6)).Select(m => m.Kind).ToList();

            Assert.That(again, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
        }

        [Test]
        public void WindowChanceControlsTheFacadeMix()
        {
            var none = ProceduralBuilding.Build(new ProceduralBuildingSpec(6, 6, 8f, 4, RoofStyle.Flat, 1, windowChance: 0f));
            var all = ProceduralBuilding.Build(new ProceduralBuildingSpec(6, 6, 8f, 4, RoofStyle.Flat, 1, windowChance: 1f));

            Assert.That(none.Any(m => m.Kind == BuildingModuleKind.Window), Is.False);
            Assert.That(all.Any(m => m.Kind == BuildingModuleKind.Wall), Is.False);
        }

        [Test]
        public void FlatRoofHasASlabAndFourParapets()
        {
            var modules = ProceduralBuilding.Build(Spec());

            Assert.That(modules.Count(m => m.Kind == BuildingModuleKind.FlatRoof), Is.EqualTo(1));
            Assert.That(modules.Count(m => m.Kind == BuildingModuleKind.Parapet), Is.EqualTo(4));
            Assert.That(modules.Any(m => m.Kind == BuildingModuleKind.RoofSlope), Is.False);
        }

        [Test]
        public void GabledRoofHasTwoSlopesAndTwoGables()
        {
            var modules = ProceduralBuilding.Build(Spec(roof: RoofStyle.Gabled));

            Assert.That(modules.Count(m => m.Kind == BuildingModuleKind.RoofSlope), Is.EqualTo(2));
            Assert.That(modules.Count(m => m.Kind == BuildingModuleKind.Gable), Is.EqualTo(2));
            Assert.That(modules.Any(m => m.Kind == BuildingModuleKind.FlatRoof), Is.False);
        }

        [Test]
        public void GabledRoofRidgeFollowsTheLongSide()
        {
            var wide = ProceduralBuilding.Build(Spec(6, 3, 1, RoofStyle.Gabled)).Where(m => m.Kind == BuildingModuleKind.RoofSlope).ToList();
            var deep = ProceduralBuilding.Build(Spec(3, 6, 1, RoofStyle.Gabled)).Where(m => m.Kind == BuildingModuleKind.RoofSlope).ToList();

            Assert.That(wide[0].Width, Is.EqualTo(6 * 8f).Within(0.001f));
            Assert.That(deep[0].Width, Is.EqualTo(6 * 8f).Within(0.001f));
            Assert.That(wide.All(m => m.Z != 0f), Is.True, "Bij een brede kavel liggen de vlakken voor en achter de nok");
            Assert.That(deep.All(m => m.X != 0f), Is.True);
        }

        [Test]
        public void HeightMatchesTheModules()
        {
            var flat = Spec(floors: 2);
            Assert.That(ProceduralBuilding.Height(flat), Is.EqualTo((2 * 3f) + ProceduralBuilding.RoofThickness + ProceduralBuilding.ParapetHeight).Within(0.001f));

            var gabled = Spec(4, 3, 2, RoofStyle.Gabled);
            Assert.That(ProceduralBuilding.Height(gabled), Is.GreaterThan(6f));
        }

        [Test]
        public void InvalidSpecsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralBuilding.Build(Spec(0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralBuilding.Build(Spec(floors: 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralBuilding.Build(Spec(floors: ProceduralBuilding.MaxFloors + 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralBuilding.Build(new ProceduralBuildingSpec(4, 3, 0f, 1, RoofStyle.Flat, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralBuilding.Build(new ProceduralBuildingSpec(1, 1, 1f, 1, RoofStyle.Flat, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralBuilding.Height(Spec(floors: 0)));
        }

        [TestCase(RoofStyle.Flat)]
        [TestCase(RoofStyle.Gabled)]
        public void MeshStaysInsideTheLotAndOnTheGround(RoofStyle roof)
        {
            var spec = Spec(4, 3, 3, roof);
            var mesh = ProceduralBuildingMesh.Build(spec);

            var xs = mesh.Vertices.Select(v => v.x).ToList();
            var ys = mesh.Vertices.Select(v => v.y).ToList();
            var zs = mesh.Vertices.Select(v => v.z).ToList();
            Assert.That(xs.Max(), Is.LessThanOrEqualTo((4 * 8f / 2f) + BuildingTemplate.Overhang));
            Assert.That(xs.Min(), Is.GreaterThanOrEqualTo(-((4 * 8f / 2f) + BuildingTemplate.Overhang)));
            Assert.That(zs.Max(), Is.LessThanOrEqualTo((3 * 8f / 2f) + BuildingTemplate.Overhang));
            Assert.That(zs.Min(), Is.GreaterThanOrEqualTo(-((3 * 8f / 2f) + BuildingTemplate.Overhang)));
            Assert.That(ys.Min(), Is.EqualTo(0f).Within(BuildingTemplate.GroundTolerance));
            Assert.That(ys.Max(), Is.EqualTo(ProceduralBuilding.Height(spec)).Within(0.6f));
        }

        [TestCase(RoofStyle.Flat)]
        [TestCase(RoofStyle.Gabled)]
        public void TrianglesFaceTheirNormals(RoofStyle roof)
        {
            var mesh = ProceduralBuildingMesh.Build(Spec(3, 4, 2, roof));

            foreach (var submesh in mesh.Submeshes)
            {
                for (var i = 0; i < submesh.Count; i += 3)
                {
                    var a = mesh.Vertices[submesh[i]];
                    var b = mesh.Vertices[submesh[i + 1]];
                    var c = mesh.Vertices[submesh[i + 2]];
                    var geometric = Vector3.Cross(b - a, c - a);
                    Assert.That(Vector3.Dot(geometric, mesh.Normals[submesh[i]]), Is.GreaterThanOrEqualTo(0f));
                    Assert.That(mesh.Normals[submesh[i]].magnitude, Is.EqualTo(1f).Within(0.001f));
                }
            }
        }

        [Test]
        public void MeshArraysAreConsistentAndWindowsGetTheirOwnSubmesh()
        {
            var mesh = ProceduralBuildingMesh.Build(Spec());

            Assert.That(mesh.Normals, Has.Count.EqualTo(mesh.Vertices.Count));
            Assert.That(mesh.Uvs, Has.Count.EqualTo(mesh.Vertices.Count));
            Assert.That(mesh.Submeshes, Has.Length.EqualTo(BuildingMeshData.SubmeshCount));
            Assert.That(mesh.Submeshes[BuildingMeshData.WindowSubmesh], Is.Not.Empty);
            Assert.That(mesh.Submeshes[BuildingMeshData.DoorSubmesh], Is.Not.Empty);
            Assert.That(mesh.Submeshes[BuildingMeshData.RoofSubmesh], Is.Not.Empty);
            Assert.That(mesh.TriangleCount, Is.EqualTo(mesh.Submeshes.Sum(s => s.Count) / 3));
            Assert.That(mesh.Submeshes.SelectMany(s => s).Max(), Is.LessThan(mesh.Vertices.Count));
        }

        [Test]
        public void SubmeshAssignmentFollowsTheKind()
        {
            Assert.That(ProceduralBuildingMesh.SubmeshFor(BuildingModuleKind.Window), Is.EqualTo(BuildingMeshData.WindowSubmesh));
            Assert.That(ProceduralBuildingMesh.SubmeshFor(BuildingModuleKind.Door), Is.EqualTo(BuildingMeshData.DoorSubmesh));
            Assert.That(ProceduralBuildingMesh.SubmeshFor(BuildingModuleKind.Gable), Is.EqualTo(BuildingMeshData.RoofSubmesh));
            Assert.That(ProceduralBuildingMesh.SubmeshFor(BuildingModuleKind.Corner), Is.EqualTo(BuildingMeshData.WallSubmesh));
            Assert.That(ProceduralBuildingMesh.SubmeshFor(BuildingModuleKind.Parapet), Is.EqualTo(BuildingMeshData.WallSubmesh));
        }
    }
}
