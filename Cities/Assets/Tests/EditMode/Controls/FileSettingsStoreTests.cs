using System;
using System.IO;
using Cities.Core.Settings;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Controls
{
    public class FileSettingsStoreTests
    {
        private string _directory;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "cities-settings-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void WritesReadsOverwritesAndDeletes()
        {
            var store = new FileSettingsStore(_directory);

            Assert.That(store.TryRead("controls", out var missing), Is.False);
            Assert.That(missing, Is.Null);

            store.Write("controls", "{\"a\":1}");
            store.Write("controls", "{\"a\":2}");

            Assert.That(store.TryRead("controls", out var contents), Is.True);
            Assert.That(contents, Is.EqualTo("{\"a\":2}"));
            Assert.That(File.Exists(store.PathFor("controls") + ".tmp"), Is.False, "Geen tijdelijk bestand achtergebleven");
            Assert.That(store.PathFor("controls"), Is.EqualTo(Path.Combine(_directory, "controls.json")));

            store.Delete("controls");
            store.Delete("controls");
            Assert.That(store.TryRead("controls", out _), Is.False);
        }

        [Test]
        public void NullContentsWritesAnEmptyFile()
        {
            var store = new FileSettingsStore(_directory);

            store.Write("graphics_v2", null);

            Assert.That(store.TryRead("graphics_v2", out var contents), Is.True);
            Assert.That(contents, Is.Empty);
        }

        [TestCase("../controls")]
        [TestCase("Controls")]
        [TestCase("con trols")]
        [TestCase("")]
        [TestCase(null)]
        public void RejectsNamesThatCouldLeaveTheFolder(string name)
        {
            var store = new FileSettingsStore(_directory);

            Assert.Throws<ArgumentException>(() => store.Write(name, "x"));
        }

        [Test]
        public void DirectoryIsRequired()
        {
            Assert.Throws<ArgumentException>(() => new FileSettingsStore(string.Empty));
            Assert.That(new FileSettingsStore(_directory).Directory, Is.EqualTo(_directory));
        }
    }
}
