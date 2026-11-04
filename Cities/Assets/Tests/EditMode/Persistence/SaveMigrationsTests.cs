using System;
using Cities.Core.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Persistence
{
    public class SaveMigrationsTests
    {
        /// <summary>Een v1-save wordt via v2 naar v3 omgezet; de lezer ziet alleen v3.</summary>
        [Test]
        public void LoadWalksTheChainToTheCurrentVersion()
        {
            var metadata = new SaveMetadata { CityName = "Oud", SavedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
            var oldBytes = SaveBytes.Write(metadata, new TestSection("TEST", new byte[] { 1 }, version: 1));
            var section = new TestSection("TEST", null, version: 3);

            // De test-sectie schrijft/leest een array; de stap werkt op ruwe bytes, dus zet de lengte mee om.
            var migrations = new SaveMigrations()
                .Add("TEST", 1, data => WithLength(data, 2))
                .Add("TEST", 2, data => data);
            using (var stream = new System.IO.MemoryStream(oldBytes))
            {
                var result = SaveFile.Read(stream, new ISaveSection[] { section }, migrations);
                Assert.That(section.ReadVersion, Is.EqualTo(3));
                Assert.That(section.Applied, Is.EqualTo(new byte[] { 1, 2 }));
                Assert.That(result.UpgradedSections, Is.EqualTo(new[] { "TEST v1->v3" }));
            }
        }

        private static byte[] WithLength(byte[] raw, byte extra)
        {
            // raw = [int32 count][bytes]; voeg één byte toe en pas de lengte aan.
            var count = BitConverter.ToInt32(raw, 0);
            var result = new byte[raw.Length + 1];
            BitConverter.GetBytes(count + 1).CopyTo(result, 0);
            Array.Copy(raw, 4, result, 4, raw.Length - 4);
            result[result.Length - 1] = extra;
            return result;
        }

        [Test]
        public void MissingStepStopsTheChainAndTheSectionReadsTheOldVersion()
        {
            var migrations = new SaveMigrations().Add("TEST", 1, data => data);
            var result = migrations.Upgrade("TEST", 1, 4, new byte[] { 9 }, out var reached);

            Assert.That(reached, Is.EqualTo(2));
            Assert.That(result, Is.EqualTo(new byte[] { 9 }));
            Assert.That(migrations.HasStep("TEST", 1), Is.True);
            Assert.That(migrations.HasStep("TEST", 2), Is.False);
        }

        [Test]
        public void CurrentVersionIsNotTouched()
        {
            var migrations = new SaveMigrations().Add("TEST", 1, _ => throw new InvalidOperationException("mag niet"));

            migrations.Upgrade("TEST", 2, 2, System.Array.Empty<byte>(), out var reached);

            Assert.That(reached, Is.EqualTo(2));
        }

        [Test]
        public void FailingStepBecomesSaveFormatException()
        {
            var migrations = new SaveMigrations().Add("TEST", 1, _ => throw new InvalidOperationException("stuk"));

            var exception = Assert.Throws<SaveFormatException>(() => migrations.Upgrade("TEST", 1, 2, System.Array.Empty<byte>(), out _));
            Assert.That(exception.Message, Does.Contain("TEST").And.Contain("stuk"));
        }

        [Test]
        public void StepReturningNullIsRejected()
        {
            var migrations = new SaveMigrations().Add("TEST", 1, _ => null);

            Assert.Throws<SaveFormatException>(() => migrations.Upgrade("TEST", 1, 2, System.Array.Empty<byte>(), out _));
        }

        [Test]
        public void InvalidRegistrationsAreRejected()
        {
            var migrations = new SaveMigrations().Add("TEST", 1, d => d);

            Assert.Throws<ArgumentException>(() => migrations.Add("", 1, d => d));
            Assert.Throws<ArgumentOutOfRangeException>(() => migrations.Add("TEST", 0, d => d));
            Assert.Throws<ArgumentNullException>(() => migrations.Add("TEST", 5, null));
            Assert.Throws<ArgumentException>(() => migrations.Add("TEST", 1, d => d));
        }
    }
}
