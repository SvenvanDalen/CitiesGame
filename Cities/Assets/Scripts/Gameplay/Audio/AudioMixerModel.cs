using System;
using Cities.Core.Settings;

namespace Cities.Gameplay.Audio
{
    /// <summary>De mixergroepen (E20-01): alles loopt via een groep, zodat de speler muziek, omgeving, effecten en interface apart regelt.</summary>
    public enum AudioGroup : byte
    {
        Master,
        Music,
        Ambient,
        Effects,
        Ui,
    }

    /// <summary>
    /// De mixer als rekenmodel (E20-01): volume per groep (0 tot 1) uit de gebruikersinstellingen, dempen, en de uiteindelijke winst van een
    /// groep (de groep maal Master). <see cref="ToDecibel"/> zet dat om voor de Unity-<c>AudioMixer</c>; de Behaviour hoeft alleen die
    /// waarden door te geven.
    /// </summary>
    public sealed class AudioMixerModel
    {
        /// <summary>Waarde in dB waaronder we stilte zeggen (de AudioMixer kent -80).</summary>
        public const float SilenceDecibel = -80f;

        private readonly float[] _volumes = { 1f, 1f, 1f, 1f, 1f };
        private readonly bool[] _muted = new bool[5];

        /// <summary>Een groep kreeg een ander volume of werd gedempt.</summary>
        public event Action<AudioGroup> Changed;

        public float Volume(AudioGroup group) => _volumes[Index(group)];

        public bool IsMuted(AudioGroup group) => _muted[Index(group)];

        public void SetVolume(AudioGroup group, float volume)
        {
            var clamped = float.IsNaN(volume) ? 0f : Math.Max(0f, Math.Min(1f, volume));
            var i = Index(group);
            if (_volumes[i].CompareTo(clamped) != 0)
            {
                _volumes[i] = clamped;
                Changed?.Invoke(group);
            }
        }

        public void SetMuted(AudioGroup group, bool muted)
        {
            var i = Index(group);
            if (_muted[i] != muted)
            {
                _muted[i] = muted;
                Changed?.Invoke(group);
            }
        }

        /// <summary>De winst van een groep: nul als hij of Master gedempt is, anders groepsvolume maal Master.</summary>
        public float Gain(AudioGroup group)
        {
            if (group == AudioGroup.Master)
            {
                return _muted[0] ? 0f : _volumes[0];
            }

            return _muted[0] || _muted[Index(group)] ? 0f : _volumes[0] * _volumes[Index(group)];
        }

        /// <summary>Lineaire winst naar dB; nul of lager wordt <see cref="SilenceDecibel"/>.</summary>
        public static float ToDecibel(float gain) => gain <= 0.0001f ? SilenceDecibel : Math.Max(SilenceDecibel, 20f * (float)Math.Log10(gain));

        /// <summary>De mixerwaarde in dB voor een groep; de Master-groep draagt zijn eigen volume.</summary>
        public float Decibel(AudioGroup group)
        {
            if (group == AudioGroup.Master)
            {
                return ToDecibel(Gain(group));
            }

            return ToDecibel(_muted[Index(group)] ? 0f : _volumes[Index(group)]);
        }

        /// <summary>Neemt alle groepsvolumes over uit de instellingen (E18-07).</summary>
        public void Apply(UserSettings settings)
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            SetVolume(AudioGroup.Master, settings.Get(SettingsCatalog.VolumeMaster));
            SetVolume(AudioGroup.Music, settings.Get(SettingsCatalog.VolumeMusic));
            SetVolume(AudioGroup.Ambient, settings.Get(SettingsCatalog.VolumeAmbient));
            SetVolume(AudioGroup.Effects, settings.Get(SettingsCatalog.VolumeEffects));
            SetVolume(AudioGroup.Ui, settings.Get(SettingsCatalog.VolumeUi));
        }

        private static int Index(AudioGroup group) =>
            Enum.IsDefined(typeof(AudioGroup), group) ? (int)group : throw new ArgumentOutOfRangeException(nameof(group), group, "Onbekende geluidsgroep.");
    }
}
