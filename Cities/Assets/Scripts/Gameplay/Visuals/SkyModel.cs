using System;
using Cities.Simulation.Timing;
using Unity.Mathematics;

namespace Cities.Gameplay.Visuals
{
    /// <summary>Hoe de hemel eruitziet op een moment (E19-03).</summary>
    public readonly struct SkyState
    {
#pragma warning disable S107 // Gegevensklasse: elke parameter is een eigen eigenschap
        public SkyState(float3 sunDirection, float sunElevation, float sunIntensity, float3 sunColor, float3 moonDirection, float moonPhase, float moonIntensity, float ambient)
        {
            SunDirection = sunDirection;
            SunElevation = sunElevation;
            SunIntensity = sunIntensity;
            SunColor = sunColor;
            MoonDirection = moonDirection;
            MoonPhase = moonPhase;
            MoonIntensity = moonIntensity;
            Ambient = ambient;
        }
#pragma warning restore S107

        /// <summary>Eenheidsvector naar de zon in kaartassen: x oost, y omhoog, z noord.</summary>
        public float3 SunDirection { get; }

        /// <summary>Hoek van de zon boven de horizon in graden; negatief is onder de horizon.</summary>
        public float SunElevation { get; }

        /// <summary>Zonlicht, 0 (donker) tot 1 (hoge zon).</summary>
        public float SunIntensity { get; }

        public float3 SunColor { get; }

        public float3 MoonDirection { get; }

        /// <summary>Maanfase: 0 nieuwe maan, 0,5 volle maan, naar 1 weer nieuw.</summary>
        public float MoonPhase { get; }

        /// <summary>Maanlicht, 0 tot ongeveer 0,1; alleen als de maan boven de horizon staat.</summary>
        public float MoonIntensity { get; }

        /// <summary>Omgevingslicht (hemelverlichting), 0 tot 1.</summary>
        public float Ambient { get; }

        public bool IsNight => SunElevation < -6f;

        /// <summary>Het licht dat schaduwen werpt: de zon overdag, de maan 's nachts.</summary>
        public float3 ShadowDirection => SunElevation > -2f ? SunDirection : MoonDirection;
    }

    /// <summary>
    /// Dag-nachtcyclus (E19-03): de stand van zon en maan, lichtkleur en hemellicht uit de datum en het uur van de spelklok. De zonnestand volgt
    /// de echte formules (declinatie uit de dag van het jaar, uurhoek uit de tijd), dus de dagen zijn in de zomer lang en in de winter kort.
    /// De snelheid hangt aan de klok: staat die op pauze, dan staat de hemel stil.
    /// </summary>
    public static class SkyModel
    {
        public const float DefaultLatitude = 52f;

        /// <summary>Lengte van een maanmaand in dagen.</summary>
        public const float LunarMonthDays = 29.53f;

        private static readonly float3 WarmLight = new float3(1f, 0.55f, 0.3f);
        private static readonly float3 DayLight = new float3(1f, 0.97f, 0.92f);

        /// <param name="timeOfDay">0 middernacht, 0,5 het middaguur.</param>
        public static SkyState Compute(GameDate date, float timeOfDay, float latitudeDegrees = DefaultLatitude)
        {
            if (!(latitudeDegrees >= -90f && latitudeDegrees <= 90f))
            {
                throw new ArgumentOutOfRangeException(nameof(latitudeDegrees), latitudeDegrees, "De breedtegraad ligt tussen -90 en 90.");
            }

            var time = timeOfDay - math.floor(timeOfDay);
            var sun = Direction(date.ToDateTime().DayOfYear, time, latitudeDegrees, out var elevation);

            var daylight = math.smoothstep(-4f, 15f, elevation);
            var color = math.lerp(WarmLight, DayLight, math.smoothstep(2f, 30f, elevation));

            var phase = MoonPhase(date);
            // De maan loopt per maand een volle dag achter op de zon; bij volle maan staat ze tegenover de zon.
            var moonTime = time - phase;
            var moon = Direction(date.ToDateTime().DayOfYear, moonTime - math.floor(moonTime), latitudeDegrees, out var moonElevation);
            var illumination = 0.5f - (0.5f * math.cos(phase * 2f * math.PI));
            var moonLight = moonElevation > 0f ? 0.1f * illumination * math.smoothstep(0f, 10f, moonElevation) : 0f;

            var ambient = math.max(0.08f + (0.5f * moonLight), 0.08f + (0.92f * daylight));
            return new SkyState(sun, elevation, daylight, color, moon, phase, moonLight, math.saturate(ambient));
        }

        /// <summary>Maanfase (0 tot 1) voor een datum, met nieuwe maan op 6 januari 2000.</summary>
        public static float MoonPhase(GameDate date)
        {
            var days = (date.ToDateTime() - new DateTime(2000, 1, 6, 0, 0, 0, DateTimeKind.Unspecified)).TotalDays;
            var phase = (float)((days % LunarMonthDays) / LunarMonthDays);
            return phase < 0f ? phase + 1f : phase;
        }

        private static float3 Direction(int dayOfYear, float timeOfDay, float latitudeDegrees, out float elevationDegrees)
        {
            var declination = math.radians(23.44f) * math.sin(2f * math.PI * (284 + dayOfYear) / 365f);
            var hour = (timeOfDay - 0.5f) * 2f * math.PI;
            var latitude = math.radians(latitudeDegrees);

            var sinElevation = (math.sin(latitude) * math.sin(declination)) + (math.cos(latitude) * math.cos(declination) * math.cos(hour));
            var elevation = math.asin(math.clamp(sinElevation, -1f, 1f));
            elevationDegrees = math.degrees(elevation);

            // Azimut vanaf het zuiden (positief naar het westen), daarna omgezet naar kompasrichting.
            var azimuth = math.atan2(math.sin(hour), (math.cos(hour) * math.sin(latitude)) - (math.tan(declination) * math.cos(latitude)));
            var compass = azimuth + math.PI;
            var horizontal = math.cos(elevation);
            return new float3(math.sin(compass) * horizontal, math.sin(elevation), math.cos(compass) * horizontal);
        }
    }
}
