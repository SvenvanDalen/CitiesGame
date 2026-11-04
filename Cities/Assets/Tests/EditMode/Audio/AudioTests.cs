using System;
using System.Linq;
using Cities.Core.Settings;
using Cities.Gameplay.Audio;
using Cities.Simulation.Disasters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Cities.Tests.EditMode.Audio
{
    public class AudioTests
    {
        [Test]
        public void GroupGainIsTheGroupVolumeTimesMaster()
        {
            var mixer = new AudioMixerModel();
            mixer.SetVolume(AudioGroup.Master, 0.5f);
            mixer.SetVolume(AudioGroup.Music, 0.5f);

            Assert.That(mixer.Gain(AudioGroup.Music), Is.EqualTo(0.25f));
            Assert.That(mixer.Gain(AudioGroup.Master), Is.EqualTo(0.5f));
            mixer.SetMuted(AudioGroup.Master, true);
            Assert.That(mixer.Gain(AudioGroup.Music), Is.Zero);
            Assert.That(mixer.Gain(AudioGroup.Master), Is.Zero);
            mixer.SetMuted(AudioGroup.Master, false);
            mixer.SetMuted(AudioGroup.Music, true);
            Assert.That(mixer.Gain(AudioGroup.Music), Is.Zero);
            Assert.That(mixer.Gain(AudioGroup.Effects), Is.EqualTo(0.5f));
        }

        [Test]
        public void VolumesAreClampedAndChangesAreReportedOnce()
        {
            var mixer = new AudioMixerModel();
            var changes = 0;
            mixer.Changed += _ => changes++;

            mixer.SetVolume(AudioGroup.Ui, 4f);
            mixer.SetVolume(AudioGroup.Ui, 1f);
            mixer.SetVolume(AudioGroup.Ui, float.NaN);
            mixer.SetMuted(AudioGroup.Ui, true);
            mixer.SetMuted(AudioGroup.Ui, true);

            Assert.That(mixer.Volume(AudioGroup.Ui), Is.Zero);
            Assert.That(changes, Is.EqualTo(2), "van 1 naar 0, en dempen");
            Assert.That(mixer.IsMuted(AudioGroup.Ui), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => mixer.Volume((AudioGroup)42));
        }

        [Test]
        public void DecibelsFollowTheGain()
        {
            Assert.That(AudioMixerModel.ToDecibel(1f), Is.Zero);
            Assert.That(AudioMixerModel.ToDecibel(0.5f), Is.EqualTo(-6.02f).Within(0.01f));
            Assert.That(AudioMixerModel.ToDecibel(0f), Is.EqualTo(AudioMixerModel.SilenceDecibel));
            var mixer = new AudioMixerModel();
            mixer.SetVolume(AudioGroup.Music, 0.1f);
            Assert.That(mixer.Decibel(AudioGroup.Music), Is.EqualTo(-20f).Within(0.01f), "groepen zijn los van Master in de mixer");
            mixer.SetMuted(AudioGroup.Music, true);
            Assert.That(mixer.Decibel(AudioGroup.Music), Is.EqualTo(AudioMixerModel.SilenceDecibel));
            mixer.SetVolume(AudioGroup.Master, 0.5f);
            Assert.That(mixer.Decibel(AudioGroup.Master), Is.EqualTo(-6.02f).Within(0.01f));
        }

        [Test]
        public void TheMixerTakesItsVolumesFromTheSettings()
        {
            var settings = new UserSettings();
            settings.Set(SettingsCatalog.VolumeMusic, 0.1f);
            settings.Set(SettingsCatalog.VolumeEffects, 0.2f);
            var mixer = new AudioMixerModel();

            mixer.Apply(settings);

            Assert.That(mixer.Volume(AudioGroup.Master), Is.EqualTo(0.8f));
            Assert.That(mixer.Volume(AudioGroup.Music), Is.EqualTo(0.1f));
            Assert.That(mixer.Volume(AudioGroup.Effects), Is.EqualTo(0.2f));
            Assert.That(mixer.Volume(AudioGroup.Ambient), Is.EqualTo(0.8f));
            Assert.Throws<ArgumentNullException>(() => mixer.Apply(null));
        }

        [Test]
        public void AmbientLayersFollowTheSurroundingsAtGroundLevel()
        {
            var forest = AmbientMix.Compute(new AmbientContext(5f, 0f, 1f, 0f, 0f));
            var harbour = AmbientMix.Compute(new AmbientContext(5f, 0.5f, 0f, 1f, 0f));

            Assert.That(forest.Nature, Is.EqualTo(1f));
            Assert.That(forest.City, Is.Zero);
            Assert.That(harbour.Water, Is.EqualTo(1f));
            Assert.That(harbour.City, Is.EqualTo(0.5f));
            Assert.That(AmbientMix.Compute(new AmbientContext(5f, 1f, 1f, 0f, 0f)).Nature, Is.EqualTo(0.3f).Within(1e-5f), "stad overstemt natuur");
        }

        [Test]
        public void HighUpGroundSoundsFadeAndWindGrows()
        {
            var low = AmbientMix.Compute(new AmbientContext(5f, 1f, 1f, 1f, 20f));
            var high = AmbientMix.Compute(new AmbientContext(2000f, 1f, 1f, 1f, 20f));

            Assert.That(high.Nature, Is.Zero);
            Assert.That(high.Water, Is.Zero);
            Assert.That(high.City, Is.EqualTo(AmbientMix.CityHumFloor), "zachte stadsdreun blijft");
            Assert.That(low.Wind, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(high.Wind, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(AmbientMix.Compute(new AmbientContext(-10f, 0f, 1f, 0f, 0f)).Nature, Is.EqualTo(1f), "onder de grond telt als grondniveau");
        }

        [Test]
        public void AttenuationFallsOffWithDistance()
        {
            Assert.That(AudioAttenuation.Gain(5f, 10f, 110f), Is.EqualTo(1f));
            Assert.That(AudioAttenuation.Gain(60f, 10f, 110f), Is.EqualTo(0.25f));
            Assert.That(AudioAttenuation.Gain(110f, 10f, 110f), Is.Zero);
            Assert.That(AudioAttenuation.Gain(500f, 10f, 110f), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => AudioAttenuation.Gain(1f, 10f, 10f));
        }

        [Test]
        public void TheBudgetKeepsTheMostImportantAndAudibleVoices()
        {
            var budget = new VoiceBudget(2, 10f, 110f);
            var requests = new[]
            {
                new VoiceRequest(1, 0, 20f),
                new VoiceRequest(2, 0, 80f),
                new VoiceRequest(3, 10, 100f),
                new VoiceRequest(4, 0, 500f),
            };

            var plan = budget.Update(requests);

            Assert.That(plan.Slots.Keys.OrderBy(k => k), Is.EqualTo(new[] { 1, 3 }), "prioriteit wint, daarna hoorbaarheid");
            Assert.That(plan.Culled, Is.EqualTo(new[] { 2 }), "hoorbaar maar geen plek");
            Assert.That(plan.Started, Is.EqualTo(new[] { 1, 3 }));
            Assert.That(plan.Playing, Is.EqualTo(2));
            Assert.That(budget.Playing, Is.EqualTo(2));
        }

        [Test]
        public void PlayingVoicesKeepTheirSlotAndFreedSlotsAreReused()
        {
            var budget = new VoiceBudget(2, 10f, 110f);
            var first = budget.Update(new[] { new VoiceRequest(1, 0, 20f), new VoiceRequest(2, 0, 30f) });

            var second = budget.Update(new[] { new VoiceRequest(2, 0, 30f), new VoiceRequest(3, 0, 25f) });

            Assert.That(second.Slots[2], Is.EqualTo(first.Slots[2]), "geen sprong");
            Assert.That(second.Stopped, Is.EqualTo(new[] { 1 }));
            Assert.That(second.Started, Is.EqualTo(new[] { 3 }));
            Assert.That(second.Slots[3], Is.EqualTo(first.Slots[1]), "slot van bron 1 hergebruikt");
        }

        [Test]
        public void VoicesOutOfRangeNeverPlayAndDuplicatesCountOnce()
        {
            var budget = new VoiceBudget(3, 10f, 110f);

            var plan = budget.Update(new[] { new VoiceRequest(1, 0, 20f), new VoiceRequest(1, 5, 25f), new VoiceRequest(2, 0, 900f), new VoiceRequest(3, 0, 20f, 0f) });

            Assert.That(plan.Slots.Keys, Is.EqualTo(new[] { 1 }));
            Assert.That(plan.Culled, Is.Empty);
            budget.Clear();
            Assert.That(budget.Playing, Is.Zero);
            Assert.Throws<ArgumentNullException>(() => budget.Update(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceBudget(0, 1f, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceBudget(1, 5f, 5f));
        }

        [Test]
        public void ASourceOnTheRightPansRightAndOneBehindIsFlagged()
        {
            var forward = new float2(0f, 1f);

            var right = DirectionalAudio.Place(float2.zero, forward, new float2(50f, 0f), 10f, 110f);
            var left = DirectionalAudio.Place(float2.zero, forward, new float2(-50f, 0f), 10f, 110f);
            var behind = DirectionalAudio.Place(float2.zero, forward, new float2(0f, -50f), 10f, 110f);
            var ahead = DirectionalAudio.Place(float2.zero, forward, new float2(0f, 50f), 10f, 110f);

            Assert.That(right.Pan, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(left.Pan, Is.EqualTo(-1f).Within(1e-5f));
            Assert.That(behind.Behind, Is.True);
            Assert.That(ahead.Behind, Is.False);
            Assert.That(ahead.Pan, Is.Zero.Within(1e-5f));
            Assert.That(right.Gain, Is.EqualTo(0.36f).Within(1e-5f));
            Assert.That(DirectionalAudio.Place(float2.zero, forward, float2.zero, 10f, 110f).Pan, Is.Zero, "op dezelfde plek");
            Assert.Throws<ArgumentException>(() => DirectionalAudio.Place(float2.zero, float2.zero, new float2(1f, 1f), 1f, 2f));
        }

        [Test]
        public void DisasterPhasesHaveTheirOwnAlarmAndPriority()
        {
            Assert.That(DirectionalAudio.AlarmFor(DisasterPhase.Warning), Is.EqualTo("alert.siren-warning"));
            Assert.That(DirectionalAudio.AlarmFor(DisasterPhase.Active), Is.EqualTo("alert.alarm-active"));
            Assert.That(DirectionalAudio.AlarmFor(DisasterPhase.Aftermath), Is.EqualTo("alert.all-clear"));
            Assert.That(DirectionalAudio.AlarmFor(DisasterPhase.Ended), Is.Null);
            Assert.That(DirectionalAudio.Priority(DisasterPhase.Active), Is.GreaterThan(DirectionalAudio.EmergencyVehiclePriority));
            Assert.That(DirectionalAudio.Priority(DisasterPhase.Ended), Is.Zero);
        }

        [Test]
        public void UiSoundsHaveACooldownAndRotateVariants()
        {
            var bank = new UiSoundBank();

            var a = bank.Request(UiSound.Click, 0.0);
            var tooSoon = bank.Request(UiSound.Click, 0.01);
            var b = bank.Request(UiSound.Click, 0.1);
            var c = bank.Request(UiSound.Click, 0.2);
            var d = bank.Request(UiSound.Click, 0.3);

            Assert.That(a.Play, Is.True);
            Assert.That(tooSoon.Play, Is.False);
            Assert.That(new[] { a.Variant, b.Variant, c.Variant, d.Variant }, Is.EqualTo(new[] { 0, 1, 2, 0 }));
            Assert.That(bank.Request(UiSound.Error, 0.0).Play, Is.True, "elk geluid heeft zijn eigen wachttijd");
            Assert.That(UiSoundBank.CooldownOf(UiSound.Notification), Is.EqualTo(1f));
            Assert.That(UiSoundBank.VariantsOf(UiSound.Error), Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => bank.Request((UiSound)99, 0.0));
        }

        private static MusicTrack[] Tracks() => new[]
        {
            new MusicTrack("dag-1", 120f, MusicContext.Day),
            new MusicTrack("dag-2", 120f, MusicContext.Day),
            new MusicTrack("dag-3", 120f, MusicContext.Day),
            new MusicTrack("nacht-1", 120f, MusicContext.Night),
            new MusicTrack("nacht-2", 120f, MusicContext.Night),
            new MusicTrack("ramp-1", 60f, MusicContext.Disaster),
        };

        [Test]
        public void MusicStartsWithATrackThatFitsTheMoment()
        {
            var director = new MusicDirector(Tracks(), 1);

            director.Update(0f, MusicContext.Night);

            Assert.That(director.Current.Id, Does.StartWith("nacht"));
            Assert.That(director.CurrentGain, Is.EqualTo(1f), "geen overgang bij de eerste keer");
            Assert.That(director.Outgoing, Is.Null);
        }

        [Test]
        public void ChangingTheMomentCrossfadesToAFittingTrack()
        {
            var director = new MusicDirector(Tracks(), 1);
            director.Update(0f, MusicContext.Day);
            var day = director.Current;

            director.Update(0.5f, MusicContext.Disaster);

            Assert.That(director.Current.Id, Is.EqualTo("ramp-1"));
            Assert.That(director.Outgoing, Is.SameAs(day));
            Assert.That(director.CurrentGain, Is.EqualTo(0f));
            director.Update(0.5f, MusicContext.Disaster);
            Assert.That(director.CurrentGain, Is.EqualTo(0.5f).Within(1e-5f), "ramp: korte overgang van 1 s");
            Assert.That(director.OutgoingGain, Is.EqualTo(0.5f).Within(1e-5f));
            director.Update(0.6f, MusicContext.Disaster);
            Assert.That(director.Outgoing, Is.Null);
            Assert.That(director.CurrentGain, Is.EqualTo(1f));
        }

        [Test]
        public void ATrackThatStillFitsIsNotInterrupted()
        {
            var director = new MusicDirector(Tracks(), 1);
            director.Update(0f, MusicContext.Day);
            var day = director.Current;

            director.Update(10f, MusicContext.Day);

            Assert.That(director.Current, Is.SameAs(day));
            Assert.That(director.Elapsed, Is.EqualTo(10f));
        }

        [Test]
        public void AFinishingTrackIsFollowedByAFreshOne()
        {
            var director = new MusicDirector(Tracks(), 5);
            director.Update(0f, MusicContext.Day);
            var seen = new System.Collections.Generic.List<string> { director.Current.Id };

            for (var i = 0; i < 6; i++)
            {
                director.Update(117f, MusicContext.Day);
                seen.Add(director.Current.Id);
            }

            Assert.That(seen.Distinct().Count(), Is.EqualTo(3), "alle dagstukken komen aan bod");
            for (var i = 1; i < seen.Count; i++)
            {
                Assert.That(seen[i], Is.Not.EqualTo(seen[i - 1]), "nooit twee keer hetzelfde achter elkaar");
            }
        }

        [Test]
        public void TheSameSeedPicksTheSameSequenceAndSingleTrackListsKeepPlaying()
        {
            string Run(ulong seed)
            {
                var director = new MusicDirector(Tracks(), seed);
                var order = string.Empty;
                for (var i = 0; i < 5; i++)
                {
                    director.Update(117f, MusicContext.Day);
                    order += director.Current.Id + ",";
                }

                return order;
            }

            Assert.That(Run(9), Is.EqualTo(Run(9)));
            var solo = new MusicDirector(new[] { new MusicTrack("a", 10f, MusicContext.Day) }, 1);
            solo.Update(0f, MusicContext.Day);
            solo.Update(20f, MusicContext.Day);
            Assert.That(solo.Current.Id, Is.EqualTo("a"));
            Assert.That(solo.Outgoing, Is.Null);
            var silent = new MusicDirector(new MusicTrack[0], 1);
            silent.Update(1f, MusicContext.Day);
            Assert.That(silent.Current, Is.Null);
            Assert.That(silent.CurrentGain, Is.Zero);
        }

        [Test]
        public void MusicInputsAreValidated()
        {
            Assert.Throws<ArgumentNullException>(() => new MusicDirector(null, 1));
            Assert.Throws<ArgumentException>(() => new MusicTrack(" ", 1f, MusicContext.Day));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MusicTrack("a", 0f, MusicContext.Day));
            Assert.Throws<ArgumentException>(() => new MusicTrack("a", 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MusicDirector(Tracks(), 1).Update(-1f, MusicContext.Day));
        }
    }
}
