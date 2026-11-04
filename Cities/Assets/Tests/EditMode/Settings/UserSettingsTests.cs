using System;
using System.Linq;
using Cities.Core.Settings;
using Cities.Tests.EditMode.Controls;
using NUnit.Framework;

namespace Cities.Tests.EditMode.Settings
{
    public class UserSettingsTests
    {
        [Test]
        public void EverythingStartsAtItsDefault()
        {
            var settings = new UserSettings();

            Assert.That(settings.Get(SettingsCatalog.VolumeMaster), Is.EqualTo(0.8f));
            Assert.That(settings.GetBool(SettingsCatalog.Fullscreen), Is.True);
            Assert.That(settings.GetChoice(SettingsCatalog.Quality), Is.EqualTo(2));
            Assert.That(settings.ToJson(), Is.EqualTo("{}"), "alleen afwijkingen worden bewaard");
        }

        [Test]
        public void CategoriesCoverDisplayAudioControlsAndGameplay()
        {
            var settings = new UserSettings();

            Assert.That(settings.Categories, Is.EqualTo(new[] { "Beeld", "Geluid", "Besturing", "Gameplay" }));
            Assert.That(settings.InCategory(SettingsCatalog.Audio).Count(), Is.EqualTo(5));
        }

        [Test]
        public void ValuesAreClampedAndChoicesRoundedToWholeNumbers()
        {
            var settings = new UserSettings();

            Assert.That(settings.Set(SettingsCatalog.VolumeMusic, 5f), Is.EqualTo(1f));
            Assert.That(settings.Set(SettingsCatalog.VolumeMusic, -1f), Is.Zero);
            Assert.That(settings.Set(SettingsCatalog.Quality, 9f), Is.EqualTo(3f));
            Assert.That(settings.Set(SettingsCatalog.Quality, 0.6f), Is.EqualTo(1f));
            Assert.That(settings.Set(SettingsCatalog.Fullscreen, 0.4f), Is.Zero);
            Assert.That(settings.Set(SettingsCatalog.VolumeUi, float.NaN), Is.EqualTo(0.8f), "NaN wordt de standaard");
        }

        [Test]
        public void ChangedFiresOnlyWhenTheValueChanges()
        {
            var settings = new UserSettings();
            var keys = new System.Collections.Generic.List<string>();
            settings.Changed += keys.Add;

            settings.Set(SettingsCatalog.VolumeMusic, 0.3f);
            settings.Set(SettingsCatalog.VolumeMusic, 0.3f);
            settings.SetBool(SettingsCatalog.EdgeScroll, true);
            settings.Reset(SettingsCatalog.VolumeMusic);
            settings.Reset(SettingsCatalog.VolumeMusic);

            Assert.That(keys, Is.EqualTo(new[] { SettingsCatalog.VolumeMusic, SettingsCatalog.EdgeScroll, SettingsCatalog.VolumeMusic }));
            settings.ResetAll();
            Assert.That(settings.GetBool(SettingsCatalog.EdgeScroll), Is.False);
        }

        [Test]
        public void SettingsSurviveASaveAndLoadThroughTheStore()
        {
            var store = new MemorySettingsStore();
            var settings = new UserSettings();
            settings.Set(SettingsCatalog.VolumeMusic, 0.25f);
            settings.Set(SettingsCatalog.Quality, 0f);
            settings.SetBool(SettingsCatalog.Fullscreen, false);
            settings.Save(store);

            var loaded = new UserSettings();
            var count = loaded.Load(store);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(loaded.Get(SettingsCatalog.VolumeMusic), Is.EqualTo(0.25f));
            Assert.That(loaded.GetChoice(SettingsCatalog.Quality), Is.Zero);
            Assert.That(loaded.GetBool(SettingsCatalog.Fullscreen), Is.False);
            Assert.That(loaded.ToJson(), Is.EqualTo(settings.ToJson()), "deterministische uitvoer");
            Assert.That(store.Contents.Keys, Is.EqualTo(new[] { UserSettings.StoreName }));
        }

        [Test]
        public void MissingOrBrokenFilesFallBackToDefaults()
        {
            var settings = new UserSettings();

            Assert.That(settings.Load(new MemorySettingsStore()), Is.Zero);
            Assert.That(settings.LoadJson(null), Is.Zero);
            Assert.That(settings.LoadJson("niet eens json"), Is.Zero);
            var count = settings.LoadJson("{\n  \"audio.music\": 0.1,\n  \"onbekend\": 3,\n  \"audio.ui\": abc,\n  \"audio.master\": 9\n}");

            Assert.That(count, Is.EqualTo(2));
            Assert.That(settings.Get(SettingsCatalog.VolumeMusic), Is.EqualTo(0.1f));
            Assert.That(settings.Get(SettingsCatalog.VolumeMaster), Is.EqualTo(1f), "buiten bereik wordt begrensd");
            Assert.That(settings.Get(SettingsCatalog.VolumeUi), Is.EqualTo(0.8f));
        }

        [Test]
        public void UnknownKeysAndBadDefinitionsAreRejected()
        {
            var settings = new UserSettings();

            Assert.Throws<ArgumentException>(() => settings.Get("nope"));
            Assert.Throws<ArgumentException>(() => settings.Set(null, 1f));
            Assert.Throws<ArgumentNullException>(() => settings.Save(null));
            Assert.Throws<ArgumentNullException>(() => settings.Load(null));
            Assert.Throws<ArgumentNullException>(() => new UserSettings(null));
            Assert.Throws<ArgumentException>(() => new SettingDefinition(" ", "c", "l", SettingKind.Toggle, 0f));
            Assert.Throws<ArgumentException>(() => new SettingDefinition("k", "c", "l", SettingKind.Choice, 0f, choices: new[] { "een" }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SettingDefinition("k", "c", "l", SettingKind.Slider, 5f, 0f, 1f));
        }

        [Test]
        public void AutomaticUiScaleFollowsTheScreenHeight()
        {
            Assert.That(UiScaling.Automatic(1920, 1080, 96f), Is.EqualTo(1f));
            Assert.That(UiScaling.Automatic(3840, 2160, 96f), Is.EqualTo(2f));
            Assert.That(UiScaling.Automatic(2560, 1440, 96f), Is.EqualTo(1.3333f).Within(0.001f));
            Assert.That(UiScaling.Automatic(3440, 1440, 96f), Is.EqualTo(UiScaling.Automatic(2560, 1440, 96f)), "ultrawide schaalt op de hoogte");
            Assert.That(UiScaling.Automatic(1280, 720, 96f), Is.EqualTo(0.6667f).Within(0.001f));
        }

        [Test]
        public void ADenseDisplayRaisesTheScaleOnLowerResolutionsOnly()
        {
            Assert.That(UiScaling.Automatic(1920, 1080, 144f), Is.EqualTo(1.5f));
            Assert.That(UiScaling.Automatic(1920, 1080, 288f), Is.EqualTo(1.5f), "begrensd");
            Assert.That(UiScaling.Automatic(3840, 2160, 144f), Is.EqualTo(2f), "4K heeft de hoogte al");
            Assert.That(UiScaling.Automatic(100, 100, 96f), Is.EqualTo(UiScaling.MinScale), "ondergrens");
            Assert.That(UiScaling.Automatic(7680, 4320, 96f), Is.EqualTo(UiScaling.MaxScale), "bovengrens");
            Assert.Throws<ArgumentOutOfRangeException>(() => UiScaling.Automatic(0, 1080, 96f));
        }

        [Test]
        public void ManualScaleOverridesTheAutomaticOne()
        {
            var settings = new UserSettings();

            Assert.That(UiScaling.Effective(settings, 3840, 2160, 96f), Is.EqualTo(2f));
            settings.SetBool(SettingsCatalog.UiScaleAuto, false);
            settings.Set(SettingsCatalog.UiScale, 1.25f);
            Assert.That(UiScaling.Effective(settings, 3840, 2160, 96f), Is.EqualTo(1.25f));
            Assert.Throws<ArgumentNullException>(() => UiScaling.Effective(null, 1, 1, 96f));
        }

        [Test]
        public void UltrawideStartsAtAboutTwentyOneByNine()
        {
            Assert.That(UiScaling.IsUltrawide(3440, 1440), Is.True);
            Assert.That(UiScaling.IsUltrawide(2560, 1080), Is.True);
            Assert.That(UiScaling.IsUltrawide(1920, 1080), Is.False);
            Assert.That(UiScaling.IsUltrawide(100, 0), Is.False);
        }
    }
}
