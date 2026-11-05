using Cities.Gameplay.Population;
using Cities.Gameplay.Visuals;
using Cities.Simulation.Scheduling;
using UnityEngine;

namespace Cities.Presentation.Atmosphere
{
    /// <summary>
    /// Dag en nacht, weer en omgevingsgeluid in de scene (E26-13): draait de zon en stuurt licht en mist met <see cref="CityAtmosphere"/>.
    /// Het omgevingsgeluid (stad, natuur, water, wind) is gesynthetiseerde ruis; er zijn nog geen opgenomen geluiden.
    /// </summary>
    public sealed class AtmosphereBehaviour : MonoBehaviour
    {
        private const int ClipSeconds = 4;
        private const int SampleRate = 22050;

        [SerializeField] private SimulationHostBehaviour _simulation;
        [SerializeField] private PopulationHostBehaviour _population;
        [SerializeField] private Light _sun;

        [Tooltip("Inwoners waarbij de hele omgeving als stad klinkt.")]
        [SerializeField, Min(1)] private int _fullCityPopulation = 5000;

        [Tooltip("Maximaal volume van het omgevingsgeluid, 0 tot 1.")]
        [SerializeField, Range(0f, 1f)] private float _soundVolume = 0.35f;

        private CityAtmosphere _atmosphere;
        private AudioSource _city;
        private AudioSource _nature;
        private AudioSource _water;
        private AudioSource _wind;
        private long _lastTick;

        public CityAtmosphere Atmosphere => _atmosphere;

        private void Start()
        {
            _atmosphere = new CityAtmosphere(_simulation.Random.Seed);
            _lastTick = _simulation.Clock.Tick;
            _city = MakeSource("Stad", 0.02f);
            _nature = MakeSource("Natuur", 0.2f);
            _water = MakeSource("Water", 0.5f);
            _wind = MakeSource("Wind", 0.1f);
        }

        private void Update()
        {
            if (_atmosphere == null)
            {
                return;
            }

            var clock = _simulation.Clock;
            var calendar = clock.Calendar;
            var ticks = clock.Tick - _lastTick;
            _lastTick = clock.Tick;
            var hours = ticks <= 0 ? 0f : ticks * CityAtmosphere.HoursPerDay / calendar.TicksPerDay;
            var urban = Mathf.Clamp01(_population.Population.Population / (float)_fullCityPopulation);
            var camera = Camera.main;
            var height = camera != null ? camera.transform.position.y : 50f;
            _atmosphere.Update(calendar.Date, calendar.TimeOfDay, hours, height, urban, 1f - urban, 0f);

            if (_sun != null)
            {
                var sky = _atmosphere.Sky;
                var forward = sky.ShadowDirection;
                _sun.transform.rotation = Quaternion.LookRotation(new Vector3(-forward.x, -forward.y, -forward.z));
                _sun.intensity = Mathf.Max(0.02f, _atmosphere.SunLight * 1.6f);
                _sun.color = new Color(sky.SunColor.x, sky.SunColor.y, sky.SunColor.z);
            }

            RenderSettings.ambientIntensity = Mathf.Lerp(0.15f, 1f, _atmosphere.Ambient);
            RenderSettings.fog = _atmosphere.Fog > 0.01f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _atmosphere.Fog * 0.004f;

            var sound = _atmosphere.Sound;
            _city.volume = sound.City * _soundVolume;
            _nature.volume = sound.Nature * _soundVolume;
            _water.volume = sound.Water * _soundVolume;
            _wind.volume = sound.Wind * _soundVolume;
        }

        private AudioSource MakeSource(string label, float smoothing)
        {
            var source = new GameObject("Omgevingsgeluid " + label).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.clip = NoiseClip(label, smoothing);
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            return source;
        }

        // Laagdoorlaatruis: een kleine smoothing geeft donker gerommel (stad), een grote helder gesis (water, wind).
        private static AudioClip NoiseClip(string label, float smoothing)
        {
            var samples = new float[ClipSeconds * SampleRate];
            var state = (uint)label.GetHashCode() | 1u;
            var previous = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                var white = ((state & 0xFFFF) / 32768f) - 1f;
                previous += (white - previous) * Mathf.Clamp(smoothing, 0.005f, 1f);
                samples[i] = previous;
            }

            var clip = AudioClip.Create("Ruis " + label, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
