using System.Collections.Generic;
using System.IO;
using Cities.Core.Configuration;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Configuration
{
    public class DefinitionOverridesTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cities-overrides-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_folder, true);

        [Test]
        public void ReadsOneFilePerDefinition()
        {
            File.WriteAllText(Path.Combine(_folder, "vehicle.city-bus.json"), "{ \"_spec\": { \"MaxSpeedKmh\": 70 } }");
            File.WriteAllText(Path.Combine(_folder, "policy.free-transit.json"), "{}");
            File.WriteAllText(Path.Combine(_folder, "notes.txt"), "genegeerd");
            var errors = new List<string>();

            var overrides = DefinitionOverrides.ReadFolder(_folder, errors);

            Assert.That(errors, Is.Empty);
            Assert.That(overrides.Keys, Is.EquivalentTo(new[] { "vehicle.city-bus", "policy.free-transit" }));
            Assert.That(overrides["vehicle.city-bus"], Does.Contain("MaxSpeedKmh"));
        }

        [Test]
        public void InvalidNamesAndEmptyFilesAreReported()
        {
            File.WriteAllText(Path.Combine(_folder, "City Bus.json"), "{}");
            File.WriteAllText(Path.Combine(_folder, "vehicle.tram.json"), "  ");
            var errors = new List<string>();

            var overrides = DefinitionOverrides.ReadFolder(_folder, errors);

            Assert.That(overrides, Is.Empty);
            Assert.That(errors, Has.Some.Contains("geen geldig definitie-ID"));
            Assert.That(errors, Has.Some.Contains("leeg"));
        }

        [Test]
        public void MissingFolderGivesNoOverrides()
        {
            var errors = new List<string>();

            Assert.That(DefinitionOverrides.ReadFolder(Path.Combine(_folder, "bestaat-niet"), errors), Is.Empty);
            Assert.That(DefinitionOverrides.ReadFolder(null, errors), Is.Empty);
            Assert.That(errors, Is.Empty);
        }
    }
}
