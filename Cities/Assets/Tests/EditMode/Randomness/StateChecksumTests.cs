using System;
using System.Collections.Generic;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Randomness
{
    public class StateChecksumTests
    {
        [Test]
        public void EqualContentGivesEqualChecksums()
        {
            var a = new TestSection("AAAA", TestSection.Pattern(1001));
            var b = new TestSection("AAAA", TestSection.Pattern(1001));

            var first = StateChecksum.Compute(a);

            Assert.That(StateChecksum.Compute(b).Hash, Is.EqualTo(first.Hash));
            Assert.That(first.Id, Is.EqualTo("AAAA"));
            Assert.That(first.Length, Is.EqualTo(1001 + sizeof(int)), "Lengte plus inhoud");
            Assert.That(first.ToString(), Does.StartWith("AAAA ").And.Contain("1005 bytes"));
        }

        [TestCase(0)]
        [TestCase(7)]
        [TestCase(1000)]
        public void ChangingOneByteChangesTheChecksum(int position)
        {
            var payload = TestSection.Pattern(1003);
            var before = StateChecksum.Compute(new TestSection("AAAA", payload)).Hash;

            payload[position] ^= 1;

            Assert.That(StateChecksum.Compute(new TestSection("AAAA", payload)).Hash, Is.Not.EqualTo(before));
        }

        [Test]
        public void TrailingZerosAreNotIgnored()
        {
            Assert.That(StateChecksum.Hash(new byte[3]), Is.Not.EqualTo(StateChecksum.Hash(new byte[4])));
            Assert.That(StateChecksum.Hash(new byte[8]), Is.Not.EqualTo(StateChecksum.Hash(new byte[16])));
            Assert.That(StateChecksum.Hash(ReadOnlySpan<byte>.Empty), Is.Not.EqualTo(StateChecksum.Hash(new byte[1])));
        }

        [Test]
        public void FirstDifferenceNamesTheSectionThatDiverged()
        {
            var expected = StateChecksum.Compute(Sections(1, 2, 3));
            var same = StateChecksum.Compute(Sections(1, 2, 3));
            var traffic = StateChecksum.Compute(Sections(1, 9, 3));
            var shorter = expected.Take(2).ToList();

            Assert.That(StateChecksum.FirstDifference(expected, same), Is.Null);
            Assert.That(StateChecksum.FirstDifference(expected, traffic), Is.EqualTo("S001"));
            Assert.That(StateChecksum.FirstDifference(expected, shorter), Is.EqualTo("S002"));
            Assert.That(StateChecksum.FirstDifference(shorter, expected), Is.EqualTo("S002"));
            Assert.That(StateChecksum.Combine(expected), Is.EqualTo(StateChecksum.Combine(same)));
            Assert.That(StateChecksum.Combine(expected), Is.Not.EqualTo(StateChecksum.Combine(traffic)));
        }

        [Test]
        public void SectionIdsAreIncludedInTheCombinedValue()
        {
            var a = StateChecksum.Compute(new[] { new TestSection("AAAA", new byte[] { 1 }) });
            var b = StateChecksum.Compute(new[] { new TestSection("BBBB", new byte[] { 1 }) });

            Assert.That(StateChecksum.Combine(a), Is.Not.EqualTo(StateChecksum.Combine(b)));
            Assert.That(StateChecksum.FirstDifference(a, b), Is.EqualTo("AAAA"));
        }

        [Test]
        public void ArgumentsAreChecked()
        {
            var empty = new List<SectionChecksum>();

            Assert.Throws<ArgumentNullException>(() => StateChecksum.Compute((ISaveSection)null));
            Assert.Throws<ArgumentNullException>(() => StateChecksum.Compute((IEnumerable<ISaveSection>)null));
            Assert.Throws<ArgumentNullException>(() => StateChecksum.Combine(null));
            Assert.Throws<ArgumentNullException>(() => StateChecksum.FirstDifference(null, empty));
            Assert.Throws<ArgumentNullException>(() => StateChecksum.FirstDifference(empty, null));
        }

        private static IEnumerable<ISaveSection> Sections(params byte[] values) =>
            values.Select((value, i) => new TestSection($"S{i:000}", new[] { value }));
    }
}
