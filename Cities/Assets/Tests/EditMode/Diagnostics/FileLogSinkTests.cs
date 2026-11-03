using System;
using System.Collections.Generic;
using System.IO;
using Cities.Core.Diagnostics;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Diagnostics
{
    public class FileLogSinkTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 3, 14, 5, 9, 123, DateTimeKind.Utc);

        private string _directory;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "cities-logs-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void WritesLinesToASessionFileAndForwardsThem()
        {
            var forwarded = new List<string>();
            string text;
            string path;
            using (var sink = new FileLogSink(_directory, new ListSink(forwarded), utcNow: () => Now))
            {
                path = sink.Path;
                sink.Write(LogLevel.Warning, "Save", "Kapot");
                sink.WriteFatal("Crash", new InvalidOperationException("boem"));
                Assert.That(sink.Directory, Is.EqualTo(_directory));
                Assert.That(sink.MaxFiles, Is.EqualTo(FileLogSink.DefaultMaxFiles));
                text = ReadShared(path);
            }

            Assert.That(Path.GetFileName(path), Is.EqualTo("cities-20261103-140509.log"));
            Assert.That(text, Does.Contain("14:05:09.123 Warning [Save] Kapot"));
            Assert.That(text, Does.Contain("FATAL   [Crash] System.InvalidOperationException: boem"));
            Assert.That(forwarded, Is.EqualTo(new[] { "Save:Kapot" }));
        }

        [Test]
        public void WorksWithoutNextSinkAndAfterDispose()
        {
            var sink = new FileLogSink(_directory, utcNow: () => Now);
            sink.Write(LogLevel.Info, "A", "x");
            sink.Dispose();
            sink.Dispose();

            Assert.DoesNotThrow(() => sink.Write(LogLevel.Info, "A", "na dispose"));
            Assert.That(File.ReadAllText(sink.Path), Does.Contain("[A] x"));
            Assert.That(File.ReadAllText(sink.Path), Does.Not.Contain("na dispose"));
        }

        [Test]
        public void OldLogFilesArePrunedToTheMaximum()
        {
            Directory.CreateDirectory(_directory);
            for (var day = 1; day <= 5; day++)
            {
                File.WriteAllText(Path.Combine(_directory, $"cities-2026110{day}-000000.log"), "oud");
            }

            File.WriteAllText(Path.Combine(_directory, "notitie.txt"), "blijft");

            using (var sink = new FileLogSink(_directory, maxFiles: 3, utcNow: () => Now))
            {
                var logs = Directory.GetFiles(_directory, "cities-*.log");
                Assert.That(logs, Has.Length.EqualTo(3), "Twee oude plus de nieuwe sessie");
                Assert.That(File.Exists(Path.Combine(_directory, "cities-20261105-000000.log")), Is.True, "Nieuwste blijft");
                Assert.That(File.Exists(Path.Combine(_directory, "cities-20261101-000000.log")), Is.False, "Oudste weg");
                Assert.That(File.Exists(sink.Path), Is.True);
            }

            Assert.That(File.Exists(Path.Combine(_directory, "notitie.txt")), Is.True);
        }

        [Test]
        public void PruneIgnoresAMissingDirectory() =>
            Assert.DoesNotThrow(() => FileLogSink.Prune(Path.Combine(_directory, "bestaat-niet"), 3));

        [Test]
        public void ArgumentsAreChecked()
        {
            Assert.Throws<ArgumentException>(() => new FileLogSink(string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FileLogSink(_directory, maxFiles: 0));
        }

        private static string ReadShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private sealed class ListSink : ILogSink
        {
            private readonly List<string> _lines;

            public ListSink(List<string> lines) => _lines = lines;

            public void Write(LogLevel level, string category, string message) => _lines.Add(category + ":" + message);
        }
    }
}
