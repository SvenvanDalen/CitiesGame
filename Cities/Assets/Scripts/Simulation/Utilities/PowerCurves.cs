using Unity.Mathematics;

namespace Cities.Simulation.Utilities
{
    /// <summary>Hoeveel van zijn capaciteit een zon- of windcentrale op dit moment levert (E06-02), als fractie van 0 tot 1.</summary>
    public static class PowerCurves
    {
        /// <summary>Windsnelheid in m/s waarbij een turbine begint te draaien.</summary>
        public const float WindCutIn = 3f;

        /// <summary>Windsnelheid in m/s waarbij een turbine zijn volle vermogen haalt.</summary>
        public const float WindRated = 12f;

        /// <summary>Windsnelheid in m/s waarboven een turbine uit de wind draait.</summary>
        public const float WindCutOut = 25f;

        /// <summary>Zonnestroom: een halve sinus tussen zonsopgang (6 uur) en zonsondergang (18 uur), 's nachts nul.</summary>
        public static float Solar(float timeOfDay)
        {
            var t = timeOfDay - math.floor(timeOfDay);
            return t <= 0.25f || t >= 0.75f ? 0f : math.sin((t - 0.25f) * 2f * math.PI);
        }

        /// <summary>Windstroom: nul tot de inschakelsnelheid, daarna met de derde macht naar vol vermogen, vlak tot de uitschakelsnelheid.</summary>
        public static float Wind(float speed)
        {
            if (speed < WindCutIn || speed >= WindCutOut)
            {
                return 0f;
            }

            var f = (speed - WindCutIn) / (WindRated - WindCutIn);
            return f >= 1f ? 1f : f * f * f;
        }
    }
}
