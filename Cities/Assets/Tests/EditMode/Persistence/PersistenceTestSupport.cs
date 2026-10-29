using System;
using System.Collections.Generic;
using System.IO;
using Cities.Core.Diagnostics;
using Cities.Core.Persistence;

namespace Cities.Tests.EditMode.Persistence
{
    /// <summary>Sectie met een vaste inhoud; onthoudt wat er geladen en toegepast is.</summary>
    internal sealed class TestSection : ISaveSection
    {
        public TestSection(string id, byte[] payload, int version = 1)
        {
            Id = id;
            Payload = payload;
            Version = version;
        }

        public string Id { get; }

        public int Version { get; set; }

        public byte[] Payload { get; set; }

        public byte[] Applied { get; private set; }

        public int ReadVersion { get; private set; }

        public void Write(SaveWriter writer) => writer.WriteArray<byte>(Payload);

        public Action Read(SaveReader reader, int version)
        {
            var data = reader.ReadArray<byte>(1 << 24);
            reader.ExpectEnd();
            ReadVersion = version;
            return () => Applied = data;
        }

        /// <summary>Goed comprimeerbare inhoud (zoals een grotendeels lege kaartlaag).</summary>
        public static byte[] Pattern(int length)
        {
            var data = new byte[length];
            for (var i = 0; i < length; i++)
            {
                data[i] = (byte)(i % 7 == 0 ? i / 7 : 0);
            }

            return data;
        }
    }

    internal sealed class RecordingLogSink : ILogSink, IDisposable
    {
        private readonly ILogSink _previous;

        public RecordingLogSink()
        {
            _previous = SystemLog.Sink;
            SystemLog.Sink = this;
        }

        public List<(LogLevel Level, string Message)> Lines { get; } = new List<(LogLevel, string)>();

        public void Write(LogLevel level, string category, string message) => Lines.Add((level, message));

        public void Dispose() => SystemLog.Sink = _previous;
    }

    internal static class SaveBytes
    {
        public static byte[] Write(SaveMetadata metadata, params ISaveSection[] sections)
        {
            using (var stream = new MemoryStream())
            {
                SaveFile.Write(stream, metadata, sections);
                return stream.ToArray();
            }
        }

        public static SaveLoadResult Read(byte[] bytes, params ISaveSection[] sections)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return SaveFile.Read(stream, sections);
            }
        }

        /// <summary>Schrijft en leest één sectie-inhoud zonder bestand, voor tests van een lezer.</summary>
        public static SaveReader ReaderFor(Action<SaveWriter> write)
        {
            var writer = new SaveWriter(64);
            write(writer);
            var segment = writer.ToSegment();
            var data = new byte[segment.Count];
            Array.Copy(segment.Array, segment.Offset, data, 0, segment.Count);
            return new SaveReader(data, "TEST");
        }

        public static string TempDirectory() => Path.Combine(Path.GetTempPath(), "cities-saves-" + Guid.NewGuid().ToString("N"));
    }
}
