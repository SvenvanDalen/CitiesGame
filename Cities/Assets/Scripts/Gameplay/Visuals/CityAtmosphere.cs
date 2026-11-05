using Cities.Core.Randomness;
using Cities.Gameplay.Audio;
using Cities.Simulation.Timing;
using Unity.Mathematics;

namespace Cities.Gameplay.Visuals
{
    /// <summary>
    /// Dag en nacht, weer en omgevingsgeluid als één model (E26-13): de stand van de zon uit de spelkalender, het weer dat meeloopt met de
    /// spelklok, en uit beide de lichtsterkte, mist en de mix van stads-, natuur-, water- en windgeluid.
    /// </summary>
    public sealed class CityAtmosphere
    {
        public const float HoursPerDay = 24f;

        private DeterministicRandom _random;

        public CityAtmosphere(ulong seed)
        {
            _random = new DeterministicRandom(seed, 0x57EA7);
            Weather = new WeatherSystem();
        }

        public WeatherSystem Weather { get; }

        public SkyState Sky { get; private set; }

        /// <summary>Lichtsterkte van de zon na bewolking, 0 tot 1.</summary>
        public float SunLight { get; private set; }

        /// <summary>Omgevingslicht, 0 tot 1.</summary>
        public float Ambient { get; private set; }

        /// <summary>Mistdichtheid voor de camera, 0 tot 1.</summary>
        public float Fog { get; private set; }

        public AmbientLevels Sound { get; private set; }

        /// <summary>
        /// Werkt de toestand bij. <paramref name="hours"/> is de verstreken speltijd sinds de vorige aanroep; <paramref name="urban"/> en
        /// <paramref name="nature"/> (0 tot 1) komen uit de bebouwing rond de camera.
        /// </summary>
        public void Update(GameDate date, float timeOfDay, float hours, float cameraHeight, float urban, float nature, float water)
        {
            Weather.Advance(hours, date.Month, ref _random);
            Sky = SkyModel.Compute(date, timeOfDay);
            SunLight = Sky.SunIntensity * (1f - (0.6f * Weather.CloudCover));
            Ambient = math.saturate(Sky.Ambient * (1f - (0.3f * Weather.CloudCover)));
            Fog = Weather.FogAmount;
            Sound = AmbientMix.Compute(new AmbientContext(cameraHeight, urban, nature, water, Weather.WindSpeed));
        }
    }
}
