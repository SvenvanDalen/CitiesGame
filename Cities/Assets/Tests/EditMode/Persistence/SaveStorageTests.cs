using System;
using System.IO;
using Cities.Core.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SaveStorageTests
    {
        private string _directory;
        private SaveStorage _storage;

        [SetUp]
        public void SetUp()
        {
            _directory = SaveBytes.TempDirectory();
            _storage = new SaveStorage(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void WritesReadsListsAndDeletes()
        {
            Assert.That(_storage.List(), Is.Empty, "Map bestaat nog niet");

            Write("Havenstad", 1);
            Write("Havenstad (2)", 2);
            File.SetLastWriteTimeUtc(_storage.PathFor("Havenstad"), DateTime.UtcNow.AddHours(-1));
            File.WriteAllText(Path.Combine(_directory, "notitie.txt"), "geen save");
            File.WriteAllText(Path.Combine(_directory, "bad..name" + SaveFile.Extension), "ongeldige naam");

            Assert.That(_storage.List(), Is.EqualTo(new[] { "Havenstad (2)", "Havenstad" }), "Nieuwste eerst, alleen geldige saves");
            Assert.That(_storage.Exists("Havenstad"), Is.True);
            Assert.That(Read("Havenstad"), Is.EqualTo(1));

            Write("Havenstad", 3);
            Assert.That(Read("Havenstad"), Is.EqualTo(3), "Overschreven");

            _storage.Delete("Havenstad");
            _storage.Delete("Havenstad");
            Assert.That(_storage.Exists("Havenstad"), Is.False);
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }

        [Test]
        public void FailedWriteKeepsThePreviousSave()
        {
            Write("Havenstad", 1);

            Assert.Throws<IOException>(() => _storage.Write("Havenstad", stream =>
            {
                stream.WriteByte(9);
                throw new IOException("Schijf vol");
            }));

            Assert.That(Read("Havenstad"), Is.EqualTo(1));
            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
        }

        [TestCase("Havenstad")]
        [TestCase("Stad 2 (kopie)")]
        [TestCase("Ångström_x-1")]
        public void ValidNames(string name) => Assert.That(SaveStorage.IsValidName(name), Is.True);

        [TestCase("")]
        [TestCase(null)]
        [TestCase(" stad")]
        [TestCase("stad ")]
        [TestCase("../stad")]
        [TestCase("c:stad")]
        [TestCase("stad.ctysave")]
        public void InvalidNames(string name)
        {
            Assert.That(SaveStorage.IsValidName(name), Is.False);
            Assert.Throws<ArgumentException>(() => _storage.PathFor(name));
        }

        [Test]
        public void NamesHaveAMaximumLength()
        {
            Assert.That(SaveStorage.IsValidName(new string('a', SaveStorage.MaxNameLength)), Is.True);
            Assert.That(SaveStorage.IsValidName(new string('a', SaveStorage.MaxNameLength + 1)), Is.False);
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentException>(() => new SaveStorage(string.Empty));
            Assert.Throws<ArgumentNullException>(() => _storage.Write("Havenstad", null));
            Assert.That(_storage.Directory, Is.EqualTo(_directory));
        }

        private void Write(string name, byte value) => _storage.Write(name, stream => stream.WriteByte(value));

        private int Read(string name)
        {
            using (var stream = _storage.OpenRead(name))
            {
                return stream.ReadByte();
            }
        }
    }
}
