using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cities.World.Buildings;
using Cities.World.Grid;
using NUnit.Framework;
using UnityEngine;

namespace Cities.Tests.EditMode.Buildings
{
    public class CustomBuildingTests
    {
        private const string Cube =
            "# kubus 2x2x2 met de onderkant op y=5, ver weg van de oorsprong\n" +
            "v 10 5 10\nv 12 5 10\nv 12 7 10\nv 10 7 10\nv 10 5 12\nv 12 5 12\nv 12 7 12\nv 10 7 12\n" +
            "usemtl muur\n" +
            "f 1 4 3 2\nf 5 6 7 8\nf 1 2 6 5\nf 4 8 7 3\nf 1 5 8 4\nf 2 3 7 6\n";

        private const string Manifest =
            "id = building.mijn-huis\nname = Mijn huis\nfootprint = 2x2\nzone = Residential\nlevel = 1\nhouseholds = 2\ndays = 10\n";

        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cities-custom-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        private void Write(string name, string manifest, string model)
        {
            var directory = Path.Combine(_folder, name);
            Directory.CreateDirectory(directory);
            if (manifest != null)
            {
                File.WriteAllText(Path.Combine(directory, CustomBuildingManifest.FileName), manifest);
            }

            if (model != null)
            {
                File.WriteAllText(Path.Combine(directory, "model.obj"), model);
            }
        }

        [Test]
        public void ParserTriangulatesQuadsAndGroupsByMaterial()
        {
            var model = ObjParser.Parse(Cube);

            Assert.That(model.TriangleCount, Is.EqualTo(12));
            Assert.That(model.Materials, Is.EqualTo(new[] { "muur" }));
            Assert.That(model.Vertices.Count, Is.EqualTo(model.Normals.Count));
            Assert.That(model.Uvs.Count, Is.EqualTo(model.Vertices.Count));
        }

        [Test]
        public void ParserMirrorsXAndKeepsTrianglesFacingOutwards()
        {
            var model = ObjParser.Parse(Cube);
            var (min, max) = model.Bounds();
            Assert.That(max.x, Is.EqualTo(-10f));
            Assert.That(min.x, Is.EqualTo(-12f));

            var center = (min + max) * 0.5f;
            var triangles = model.Submeshes[0];
            for (var i = 0; i < triangles.Count; i += 3)
            {
                var a = model.Vertices[triangles[i]];
                var b = model.Vertices[triangles[i + 1]];
                var c = model.Vertices[triangles[i + 2]];
                var outward = Vector3.Cross(b - a, c - a);
                Assert.That(Vector3.Dot(outward, ((a + b + c) / 3f) - center), Is.GreaterThan(0f), "Driehoek wijst naar buiten");
                Assert.That(Vector3.Dot(outward.normalized, model.Normals[triangles[i]]), Is.GreaterThan(0.99f), "Berekende normaal klopt");
            }
        }

        [Test]
        public void ParserHandlesNegativeIndicesAndGivenNormalsAndUvs()
        {
            var model = ObjParser.Parse("v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nvn 0 0 1\nf -3/-3/1 -2/-2/1 -1/-1/1\n");

            Assert.That(model.TriangleCount, Is.EqualTo(1));
            Assert.That(model.Materials, Is.EqualTo(new[] { ObjParser.DefaultMaterial }));
            Assert.That(model.Uvs[1], Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(model.Normals.All(n => n == new Vector3(0f, 0f, 1f)), Is.True);
        }

        [TestCase("")]
        [TestCase("v 1 2 3\n")]
        [TestCase("v 1 2\nf 1 1 1\n")]
        [TestCase("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2\n")]
        [TestCase("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 9\n")]
        [TestCase("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 x\n")]
        [TestCase("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 0\n")]
        [TestCase("v 0 0 NaN\nv 1 0 0\nv 0 1 0\nf 1 2 3\n")]
        public void BrokenObjIsRejected(string text)
        {
            Assert.Throws<FormatException>(() => ObjParser.Parse(text));
        }

        [Test]
        public void ManifestParsesAllFields()
        {
            var errors = new List<string>();
            var manifest = CustomBuildingManifest.Parse(
                Manifest + "model = huis.obj\ndensity = High\ncost = 0\nupkeep = 5\nworkplaces = 0\nscale = 1.5\nyaw = 450\n# commentaar\n", errors);

            Assert.That(errors, Is.Empty, string.Join("; ", errors));
            Assert.That(manifest.Id, Is.EqualTo("building.mijn-huis"));
            Assert.That(manifest.Name, Is.EqualTo("Mijn huis"));
            Assert.That(manifest.ModelFile, Is.EqualTo("huis.obj"));
            Assert.That(manifest.Spec.FootprintWidth, Is.EqualTo(2));
            Assert.That(manifest.Spec.Zone, Is.EqualTo(ZoneType.Residential));
            Assert.That(manifest.Spec.Density, Is.EqualTo(ZoneDensity.High));
            Assert.That(manifest.Spec.UpkeepPerMonth, Is.EqualTo(5));
            Assert.That(manifest.Scale, Is.EqualTo(1.5f));
            Assert.That(manifest.YawDegrees, Is.EqualTo(90));
        }

        [TestCase("name = x\nfootprint = 2x2\n", "id")]
        [TestCase("id = huis\nname = x\n", "id")]
        [TestCase("id = vehicle.bus\nname = x\n", "id")]
        [TestCase("id = building.a\n", "naam")]
        [TestCase("id = building.a\nname = x\nfootprint = groot\n", "footprint")]
        [TestCase("id = building.a\nname = x\nzone = Moestuin\n", "Zone")]
        [TestCase("id = building.a\nname = x\nlevel = hoog\n", "geheel")]
        [TestCase("id = building.a\nname = x\ncost = veel\n", "geheel")]
        [TestCase("id = building.a\nname = x\nscale = -1\n", "schaal")]
        [TestCase("id = building.a\nname = x\nyaw = 45\n", "yaw")]
        [TestCase("id = building.a\nname = x\nmodel = ../geheim.obj\n", "map")]
        [TestCase("id = building.a\nname = x\nkleur = rood\n", "onbekende")]
        [TestCase("id = building.a\nname = x\nzomaar een regel\n", "sleutel")]
        [TestCase("id = building.a\nname = x\nfootprint = 20x2\n", "FootprintWidth")]
        public void InvalidManifestsAreReported(string text, string expectedPart)
        {
            var errors = new List<string>();

            Assert.That(CustomBuildingManifest.Parse(text, errors), Is.Null);
            Assert.That(errors, Has.Some.Contains(expectedPart));
        }

        [Test]
        public void FitPutsTheModelOnTheGroundCenteredAndInsideTheFootprint()
        {
            var errors = new List<string>();
            var manifest = CustomBuildingManifest.Parse(Manifest, errors);
            var fitted = CustomBuildingLibrary.Fit(ObjParser.Parse(Cube), manifest, 8f);

            var (min, max) = fitted.Bounds();

            Assert.That(min.y, Is.EqualTo(0f).Within(1e-4f));
            Assert.That((min.x + max.x) * 0.5f, Is.EqualTo(0f).Within(1e-4f));
            Assert.That((min.z + max.z) * 0.5f, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(max.x - min.x, Is.EqualTo(2 * 8f * CustomBuildingLibrary.AutoFit).Within(1e-3f), "Past precies in de footprint");
        }

        [Test]
        public void ExplicitScaleAndYawAreApplied()
        {
            var errors = new List<string>();
            var manifest = CustomBuildingManifest.Parse(Manifest + "scale = 3\nyaw = 90\n", errors);
            var model = new ObjModel();
            model.Vertices.AddRange(new[] { new Vector3(0, 0, 0), new Vector3(4, 0, 0), new Vector3(0, 1, 0) });
            model.Normals.AddRange(new[] { Vector3.forward, Vector3.forward, Vector3.forward });
            model.Uvs.AddRange(new[] { Vector2.zero, Vector2.zero, Vector2.zero });
            model.Materials.Add("m");
            model.Submeshes.Add(new List<int> { 0, 1, 2 });

            var fitted = CustomBuildingLibrary.Fit(model, manifest, 8f);
            var (min, max) = fitted.Bounds();

            Assert.That(max.z - min.z, Is.EqualTo(12f).Within(1e-3f), "De lengte van 4 m ligt na 90 graden langs Z en is 3 keer geschaald");
            Assert.That(max.x - min.x, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void LibraryLoadsValidFoldersAndReportsBrokenOnes()
        {
            Write("goed", Manifest, Cube);
            Write("zonder-model", Manifest.Replace("mijn-huis", "geen-model"), null);
            Write("kapot-model", Manifest.Replace("mijn-huis", "kapot"), "v 1\n");
            Write("kapot-manifest", "id = ???", Cube);
            Write("dubbel", Manifest, Cube);
            Write("geen-bestand", null, Cube);
            var errors = new List<string>();

            var buildings = CustomBuildingLibrary.LoadFolder(_folder, 8f, errors);

            Assert.That(buildings.Select(b => b.Manifest.Id), Is.EqualTo(new[] { "building.mijn-huis" }));
            Assert.That(errors, Has.Some.StartsWith("zonder-model:").And.Some.Contains("ontbreekt"));
            Assert.That(errors, Has.Some.StartsWith("kapot-model:"));
            Assert.That(errors, Has.Some.StartsWith("kapot-manifest:"));
            Assert.That(errors, Has.Some.Contains("al in gebruik"));
            Assert.That(errors.Any(e => e.StartsWith("geen-bestand")), Is.False);
        }

        [Test]
        public void MissingFolderGivesAnEmptyLibrary()
        {
            Assert.That(CustomBuildingLibrary.LoadFolder(Path.Combine(_folder, "bestaat-niet"), 8f, new List<string>()), Is.Empty);
            Assert.That(CustomBuildingLibrary.LoadFolder(null, 8f, new List<string>()), Is.Empty);
        }

        [Test]
        public void ValidateFlagsModelsWithoutRenderersOrHeight()
        {
            var errors = new List<string>();
            var manifest = CustomBuildingManifest.Parse(Manifest, errors);
            var flat = new ObjModel();
            flat.Vertices.AddRange(new[] { new Vector3(-6, 0, -6), new Vector3(6, 0, -6), new Vector3(0, 0, 6) });

            CustomBuildingLibrary.Validate(new CustomBuilding(manifest, flat), 8f, errors);

            Assert.That(errors, Has.Some.Contains("renderer"));
        }
    }
}
