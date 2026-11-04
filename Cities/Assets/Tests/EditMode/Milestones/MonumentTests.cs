using System;
using System.Linq;
using Cities.Core.Persistence;
using Cities.Simulation.Milestones;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Milestones
{
    public class MonumentTests
    {
        private static readonly CityConditions Big = new CityConditions(60_000, 0.9f, 80f);

        [Test]
        public void AMonumentNeedsPopulationServicesAndHappiness()
        {
            var cathedral = MonumentCatalog.Find("monument.cathedral");

            Assert.That(cathedral.IsMetBy(new CityConditions(10_000, 0.5f, 55f)), Is.True);
            Assert.That(cathedral.IsMetBy(new CityConditions(9_999, 0.5f, 55f)), Is.False);
            Assert.That(cathedral.IsMetBy(new CityConditions(10_000, 0.4f, 55f)), Is.False);
            Assert.That(cathedral.IsMetBy(new CityConditions(10_000, 0.5f, 50f)), Is.False);
        }

        [Test]
        public void MissingConditionsAreExplained()
        {
            var cathedral = MonumentCatalog.Find("monument.cathedral");

            var missing = cathedral.Missing(new CityConditions(9_000, 0.3f, 40f));

            Assert.That(missing, Has.Count.EqualTo(3));
            Assert.That(missing[0], Does.Contain("1").And.Contain("inwoners"));
            Assert.That(missing[1], Does.Contain("50%").And.Contain("30%"));
            Assert.That(missing[2], Does.Contain("geluk"));
            Assert.That(cathedral.Missing(Big), Is.Empty);
        }

        [Test]
        public void UpdateMakesMonumentsAvailableOnceAndTheyStayAvailable()
        {
            var tracker = new MonumentTracker();

            var fresh = tracker.Update(Big);

            Assert.That(fresh.Select(m => m.Id), Is.EqualTo(new[] { "monument.cathedral", "monument.stadium", "monument.opera" }));
            Assert.That(tracker.Update(Big), Is.Empty, "alleen nieuwe");
            Assert.That(tracker.Update(new CityConditions(0, 0f, 0f)), Is.Empty);
            Assert.That(tracker.IsAvailable("monument.opera"), Is.True, "blijft beschikbaar na terugval");
            Assert.That(tracker.IsAvailable("monument.exhibition"), Is.False);
        }

        [Test]
        public void ABuildMustBeAvailableAndHappensOnlyOnce()
        {
            var tracker = new MonumentTracker();

            Assert.That(tracker.Build("monument.opera"), Is.EqualTo(MonumentBuildResult.Locked));
            Assert.That(tracker.Build("monument.nope"), Is.EqualTo(MonumentBuildResult.Unknown));
            Assert.That(tracker.Build(null), Is.EqualTo(MonumentBuildResult.Unknown));
            tracker.Update(Big);
            Assert.That(tracker.Build("monument.opera"), Is.EqualTo(MonumentBuildResult.Built));
            Assert.That(tracker.Build("monument.opera"), Is.EqualTo(MonumentBuildResult.AlreadyBuilt));
            Assert.That(tracker.IsBuilt("monument.opera"), Is.True);
            Assert.That(tracker.Remove("monument.opera"), Is.True);
            Assert.That(tracker.Remove("monument.opera"), Is.False);
            Assert.That(tracker.Build("monument.opera"), Is.EqualTo(MonumentBuildResult.Built), "opnieuw bouwen kan");
        }

        [Test]
        public void BuiltMonumentsMultiplyTheirBonuses()
        {
            var tracker = new MonumentTracker();
            tracker.Update(Big);

            Assert.That(tracker.Multiplier("tourism.visitors"), Is.EqualTo(1f), "nog niets gebouwd");
            tracker.Build("monument.cathedral");
            tracker.Build("monument.opera");

            Assert.That(tracker.Multiplier("tourism.visitors"), Is.EqualTo(1.1f * 1.15f).Within(1e-5f));
            Assert.That(tracker.Multiplier("tax.income"), Is.EqualTo(1.02f).Within(1e-5f));
            Assert.That(tracker.Multiplier("onbekend"), Is.EqualTo(1f));
        }

        [Test]
        public void SpecsAndBonusesRejectBadValues()
        {
            Assert.Throws<ArgumentException>(() => new MonumentBonus(" ", 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonumentBonus("a", -1f));
            Assert.Throws<ArgumentException>(() => new MonumentSpec(" ", "x", 1, 0f, 0f, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonumentSpec("a", "x", -1, 0f, 0f, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonumentSpec("a", "x", 1, 2f, 0f, 0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MonumentSpec("a", "x", 1, 0f, 101f, 0, null));
            Assert.Throws<ArgumentNullException>(() => new MonumentTracker(null));
            Assert.That(new MonumentSpec("a", null, 1, 0f, 0f, 0, null).Name, Is.EqualTo("a"));
        }

        [Test]
        public void TheSectionRoundTripsDeterministically()
        {
            var tracker = new MonumentTracker();
            tracker.Update(Big);
            tracker.Build("monument.stadium");
            var bytes = SaveBytes.Write(new SaveMetadata(), new MonumentSection(tracker));

            var loaded = new MonumentTracker();
            loaded.Update(new CityConditions(1_000_000, 1f, 100f));
            SaveBytes.Read(bytes, new MonumentSection(loaded));

            Assert.That(loaded.Available, Is.EqualTo(new[] { "monument.cathedral", "monument.opera", "monument.stadium" }));
            Assert.That(loaded.Built, Is.EqualTo(new[] { "monument.stadium" }));
            Assert.That(SaveBytes.Write(new SaveMetadata(), new MonumentSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new MonumentSection(loaded).Id, Is.EqualTo("MONU"));
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new MonumentSection(null));
            var section = new MonumentSection(new MonumentTracker());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => w.Write(-1)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => { w.Write(1); w.Write("monument.nope"); w.Write(0); }), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w =>
            {
                w.Write(2);
                w.Write("monument.opera");
                w.Write("monument.opera");
            }), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w =>
            {
                w.Write(0);
                w.Write(1);
                w.Write("monument.opera");
            }), 1), "gebouwd maar nooit beschikbaar");
        }
    }
}
