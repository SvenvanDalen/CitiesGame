using System;
using System.Text;
using Cities.Core.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SaveReaderWriterTests
    {
        [Test]
        public void ValuesRoundTrip()
        {
            var reader = SaveBytes.ReaderFor(w =>
            {
                w.Write(true);
                w.Write((byte)200);
                w.Write(-123456789);
                w.Write(4000000000u);
                w.Write(long.MinValue);
                w.Write(-0.5f);
                w.Write(Math.PI);
                w.Write("Sint-Michielsgestel – ’s-Hertogenbosch");
                w.Write((string)null);
                w.WriteArray<float>(new[] { 1.5f, float.NaN, float.NegativeInfinity });
                Assert.That(w.Length, Is.GreaterThan(40));
            });

            Assert.That(reader.ReadBool(), Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(200));
            Assert.That(reader.ReadInt32(), Is.EqualTo(-123456789));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(4000000000u));
            Assert.That(reader.ReadInt64(), Is.EqualTo(long.MinValue));
            Assert.That(reader.ReadSingle(), Is.EqualTo(-0.5f));
            Assert.That(reader.ReadDouble(), Is.EqualTo(Math.PI));
            Assert.That(reader.ReadString(), Is.EqualTo("Sint-Michielsgestel – ’s-Hertogenbosch"));
            Assert.That(reader.ReadString(), Is.Empty);
            var floats = new float[3];
            reader.ReadArray<float>(floats);
            Assert.That(floats[0], Is.EqualTo(1.5f));
            Assert.That(float.IsNaN(floats[1]), Is.True);
            Assert.That(floats[2], Is.EqualTo(float.NegativeInfinity));
            reader.ExpectEnd();
        }

        [Test]
        public void LongStringsUseMultiByteLengths()
        {
            var text = new string('x', 70000);
            var reader = SaveBytes.ReaderFor(w => w.Write(text));

            Assert.That(reader.ReadString(), Is.EqualTo(text));
        }

        [Test]
        public void ReadingPastTheEndFails()
        {
            var reader = SaveBytes.ReaderFor(w => w.Write(true));

            reader.ReadBool();

            var error = Assert.Throws<SaveFormatException>(() => reader.ReadInt32());
            Assert.That(error.Message, Does.Contain("TEST"));
        }

        [Test]
        public void ArrayLengthMustMatch()
        {
            var reader = SaveBytes.ReaderFor(w => w.WriteArray<int>(new[] { 1, 2, 3 }));

            Assert.Throws<SaveFormatException>(() => reader.ReadArray<int>(new int[4]));
        }

        [Test]
        public void ArrayWithStoredLengthHonoursTheLimit()
        {
            Assert.That(SaveBytes.ReaderFor(w => w.WriteArray<int>(new[] { 7, 8 })).ReadArray<int>(2), Is.EqualTo(new[] { 7, 8 }));
            Assert.Throws<SaveFormatException>(() => SaveBytes.ReaderFor(w => w.WriteArray<int>(new[] { 7, 8 })).ReadArray<int>(1));
            Assert.Throws<SaveFormatException>(() => SaveBytes.ReaderFor(w => w.Write(-1)).ReadArray<int>(10));
            Assert.Throws<SaveFormatException>(() => SaveBytes.ReaderFor(w => w.Write(5)).ReadArray<int>(10), "Lengte zonder data");
        }

        [Test]
        public void UnreadBytesAreReported()
        {
            var reader = SaveBytes.ReaderFor(w => w.Write(1L));

            reader.ReadInt32();

            Assert.That(reader.Remaining, Is.EqualTo(4));
            Assert.Throws<SaveFormatException>(() => reader.ExpectEnd());
        }

        [Test]
        public void InvalidStringLengthIsRejected()
        {
            var reader = SaveBytes.ReaderFor(w =>
            {
                for (var i = 0; i < 5; i++)
                {
                    w.Write((byte)0xFF);
                }
            });

            Assert.Throws<SaveFormatException>(() => reader.ReadString());
        }

        [Test]
        public void ShuffledArraysRoundTrip()
        {
            var heights = new float[1000];
            for (var i = 0; i < heights.Length; i++)
            {
                heights[i] = 20f + (i * 0.013f);
            }

            var reader = SaveBytes.ReaderFor(w =>
            {
                w.WriteShuffled<float>(heights);
                w.WriteShuffled<float>(Array.Empty<float>());
            });

            var loaded = new float[1000];
            reader.ReadShuffled<float>(loaded);
            reader.ReadShuffled<float>(Span<float>.Empty);
            reader.ExpectEnd();
            Assert.That(loaded, Is.EqualTo(heights));
        }

        [Test]
        public void ShuffledLengthMustMatch()
        {
            var reader = SaveBytes.ReaderFor(w => w.WriteShuffled<int>(new[] { 1, 2 }));

            Assert.Throws<SaveFormatException>(() => reader.ReadShuffled<int>(new int[3]));
        }

        [Test]
        public void ShuffleGroupsBytesPerPosition()
        {
            var source = new byte[] { 1, 2, 3, 4, 5, 6 };
            var shuffled = new byte[6];
            var restored = new byte[6];

            ByteShuffle.Shuffle(source, shuffled, 2);
            ByteShuffle.Unshuffle(shuffled, restored, 2);

            Assert.That(shuffled, Is.EqualTo(new byte[] { 1, 3, 5, 2, 4, 6 }));
            Assert.That(restored, Is.EqualTo(source));
            Assert.Throws<ArgumentOutOfRangeException>(() => ByteShuffle.Shuffle(source, shuffled, 0));
            Assert.Throws<ArgumentException>(() => ByteShuffle.Shuffle(source, new byte[5], 2));
            Assert.Throws<ArgumentException>(() => ByteShuffle.Unshuffle(source, shuffled, 4));
        }

        [Test]
        public void Crc32MatchesTheStandardCheckValue()
        {
            var data = Encoding.ASCII.GetBytes("123456789");

            Assert.That(Crc32.Compute(data), Is.EqualTo(0xCBF43926u));
            Assert.That(Crc32.Append(Crc32.Compute(data.AsSpan(0, 4)), data.AsSpan(4)), Is.EqualTo(0xCBF43926u));
            Assert.That(Crc32.Compute(ReadOnlySpan<byte>.Empty), Is.EqualTo(0u));
        }

        [Test]
        public void ExceptionConstructors()
        {
            Assert.That(new SaveFormatException().Message, Is.Not.Empty);
            Assert.That(new SaveFormatException("x", new InvalidOperationException()).InnerException, Is.TypeOf<InvalidOperationException>());
        }
    }
}
