using System.Globalization;
using Cities.Simulation.Timing;

namespace Cities.Presentation.UI
{
    /// <summary>Teksten en waarden van de HUD (E18-03): geld, inwoners, datum en tijd, snelheid en de RCI-meters.</summary>
    public static class HudText
    {
        private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

        public static string Money(long amount) => "€ " + amount.ToString("N0", Dutch);

        public static string Population(long residents) => residents.ToString("N0", Dutch);

        /// <summary>Datum en kloktijd, bijvoorbeeld "03-02-2030 14:30"; <paramref name="timeOfDay"/> loopt van 0 tot 1.</summary>
        public static string DateTime(GameDate date, float timeOfDay)
        {
            var minutes = (int)(timeOfDay * 1440f) % 1440;
            return $"{date.Day:D2}-{date.Month:D2}-{date.Year:D4} {minutes / 60:D2}:{minutes % 60:D2}";
        }

        public static string Speed(SimulationSpeed speed)
        {
            switch (speed)
            {
                case SimulationSpeed.Paused: return "Pauze";
                case SimulationSpeed.Normal: return "1×";
                case SimulationSpeed.Fast: return "2×";
                default: return "3×";
            }
        }

        /// <summary>Meterwaarde 0 tot 1 uit de vraag van -1 tot 1; 0,5 is in balans.</summary>
        public static float Meter(float demand) => System.Math.Min(1f, System.Math.Max(0f, (demand + 1f) * 0.5f));

        /// <summary>Kleur van een vraagmeter: groen bij vraag, oranje bij overschot, anders neutraal.</summary>
        public static MeterColor MeterColorFor(float demand) =>
            demand > 0.1f ? MeterColor.Success : demand < -0.1f ? MeterColor.Warning : MeterColor.Accent;
    }
}
