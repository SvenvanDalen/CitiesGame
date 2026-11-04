using System;
using Cities.Core.Persistence;
using Cities.Core.Randomness;
using Cities.Simulation.Disasters;
using Cities.Tests.EditMode.Persistence;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Disasters
{
    public class DisasterSettingsTests
    {
        private static readonly float2 Area = new float2(1000f, 1000f);

        private static DisasterSpec Fire(float chance) => new DisasterSpec("Brand", chance, 0, 1, 0, 1000, 0);

        [Test]
        public void ATypeWithoutSettingsUsesTheDefaults()
        {
            var settings = new DisasterSettings();

            Assert.That(settings.RandomEnabled, Is.True);
            Assert.That(settings.IsEnabled("Brand"), Is.True);
            Assert.That(settings.FrequencyFactor("Brand"), Is.EqualTo(1f));
            Assert.That(settings.DamageFactor("Brand"), Is.EqualTo(1f));
            Assert.That(settings.Customised, Is.Empty);
        }

        [Test]
        public void SettingsAreKeptPerTypeAndIndependently()
        {
            var settings = new DisasterSettings();

            settings.SetEnabled("Brand", false);
            settings.SetFrequencyFactor("Brand", 2.5f);
            settings.SetDamageFactor("Storm", 0.5f);

            Assert.That(settings.IsEnabled("Brand"), Is.False);
            Assert.That(settings.FrequencyFactor("Brand"), Is.EqualTo(2.5f));
            Assert.That(settings.DamageFactor("Brand"), Is.EqualTo(1f), "schade van Brand is niet aangeraakt");
            Assert.That(settings.IsEnabled("Storm"), Is.True);
            Assert.That(settings.DamageFactor("Storm"), Is.EqualTo(0.5f));
            Assert.That(settings.Customised, Is.EqualTo(new[] { "Brand", "Storm" }));
        }

        [Test]
        public void FactorsOutsideTheRangeAreRejected()
        {
            var settings = new DisasterSettings();

            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetFrequencyFactor("Brand", -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetDamageFactor("Brand", 5.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetDamageFactor("Brand", float.NaN));
            Assert.Throws<ArgumentException>(() => settings.SetEnabled(" ", true));
            Assert.Throws<ArgumentNullException>(() => settings.ChancePerDay(null));
            Assert.That(settings.Customised, Is.Empty);
        }

        [Test]
        public void FrequencyScalesTheChanceAndIsCappedAtOne()
        {
            var settings = new DisasterSettings();
            settings.SetFrequencyFactor("Brand", 3f);

            Assert.That(settings.ChancePerDay(Fire(0.1f)), Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(settings.ChancePerDay(Fire(0.5f)), Is.EqualTo(1f));
            settings.SetFrequencyFactor("Brand", 0f);
            Assert.That(settings.ChancePerDay(Fire(0.5f)), Is.Zero);
        }

        [Test]
        public void DamageFactorScalesTheCostAndRoundsToWholeUnits()
        {
            var settings = new DisasterSettings();
            settings.SetDamageFactor("Brand", 0.5f);

            Assert.That(settings.ScaledCost("Brand", 1001), Is.EqualTo(501));
            Assert.That(settings.ScaledCost("Storm", 1001), Is.EqualTo(1001));
            settings.SetDamageFactor("Brand", 0f);
            Assert.That(settings.ScaledCost("Brand", 1001), Is.Zero);
        }

        [Test]
        public void TheManagerUsesTheSettingsForCostAndChance()
        {
            var manager = new DisasterManager();
            manager.Register(Fire(0.5f));
            manager.Settings.SetDamageFactor("Brand", 2f);
            manager.Trigger("Brand", float2.zero);
            var random = new DeterministicRandom(1);

            Assert.That(manager.Step(ref random, Area), Is.EqualTo(2000), "dubbele schade");

            manager.Settings.SetFrequencyFactor("Brand", 0f);
            for (var i = 0; i < 30; i++)
            {
                manager.Step(ref random, Area);
            }

            Assert.That(manager.Running, Is.Empty, "frequentie nul: nooit vanzelf");

            manager.Settings.SetFrequencyFactor("Brand", 2f);
            manager.Step(ref random, Area);
            Assert.That(manager.Running, Has.Count.EqualTo(1), "kans 0,5 keer 2 is zeker");
        }

        [Test]
        public void TurningOffRandomOrATypeStopsNewDisastersButNotManualOnes()
        {
            var manager = new DisasterManager();
            manager.Register(Fire(1f));
            var random = new DeterministicRandom(3);

            manager.RandomEnabled = false;
            manager.Step(ref random, Area);
            Assert.That(manager.Running, Is.Empty);
            Assert.That(manager.Settings.RandomEnabled, Is.False);

            manager.RandomEnabled = true;
            manager.SetEnabled("Brand", false);
            manager.Step(ref random, Area);
            Assert.That(manager.Running, Is.Empty);
            Assert.That(manager.IsEnabled("Brand"), Is.False);
            Assert.That(manager.Trigger("Brand", float2.zero), Is.Not.Null);
        }

        [Test]
        public void ResetRestoresTheDefaults()
        {
            var settings = new DisasterSettings { RandomEnabled = false };
            settings.SetFrequencyFactor("Brand", 4f);

            settings.Reset();

            Assert.That(settings.RandomEnabled, Is.True);
            Assert.That(settings.FrequencyFactor("Brand"), Is.EqualTo(1f));
            Assert.That(settings.Customised, Is.Empty);
        }

        [Test]
        public void TheSectionRoundTripsDeterministically()
        {
            var settings = new DisasterSettings { RandomEnabled = false };
            settings.SetEnabled("Storm", false);
            settings.SetFrequencyFactor("Storm", 0.25f);
            settings.SetDamageFactor("Brand", 3f);
            var bytes = SaveBytes.Write(new SaveMetadata(), new DisasterSettingsSection(settings));

            var loaded = new DisasterSettings();
            loaded.SetDamageFactor("Aardbeving", 2f);
            SaveBytes.Read(bytes, new DisasterSettingsSection(loaded));

            Assert.That(loaded.RandomEnabled, Is.False);
            Assert.That(loaded.IsEnabled("Storm"), Is.False);
            Assert.That(loaded.FrequencyFactor("Storm"), Is.EqualTo(0.25f));
            Assert.That(loaded.DamageFactor("Brand"), Is.EqualTo(3f));
            Assert.That(loaded.DamageFactor("Aardbeving"), Is.EqualTo(1f), "oude instelling is vervangen");
            Assert.That(SaveBytes.Write(new SaveMetadata(), new DisasterSettingsSection(loaded)), Is.EqualTo(bytes));
            Assert.That(new DisasterSettingsSection(loaded).Id, Is.EqualTo("DSET"));
        }

        [Test]
        public void ACorruptSaveIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new DisasterSettingsSection(null));
            var section = new DisasterSettingsSection(new DisasterSettings());

            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Entry(w, -1, "Brand", 1f, 1f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Entry(w, 1, " ", 1f, 1f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Entry(w, 1, "Brand", 9f, 1f)), 1));
            Assert.Throws<SaveFormatException>(() => section.Read(SaveBytes.ReaderFor(w => Entry(w, 1, "Brand", 1f, -1f)), 1));
        }

        private static void Entry(SaveWriter w, int count, string name, float frequency, float damage)
        {
            w.Write(true);
            w.Write(count);
            if (count < 1)
            {
                return;
            }

            w.Write(name);
            w.Write(true);
            w.Write(frequency);
            w.Write(damage);
        }
    }
}
