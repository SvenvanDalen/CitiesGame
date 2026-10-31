using System;

namespace Cities.Gameplay.Zoning
{
    /// <summary>De rekenkant van de bouwfase (E05-06): hoe lang een gebouw in aanbouw is en hoe ver het is.</summary>
    public static class ConstructionSchedule
    {
        /// <summary>
        /// Duur in simulatieticks: <paramref name="days"/> speldagen van <paramref name="ticksPerDay"/> ticks, vermenigvuldigd met
        /// <paramref name="timeScale"/> (1 is de waarde uit het gebouwtype, 0,5 bouwt twee keer zo snel). 0 dagen is meteen klaar.
        /// </summary>
        public static long DurationTicks(int days, int ticksPerDay, float timeScale)
        {
            if (days < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(days), days, "Bouwtijd kan niet negatief zijn.");
            }

            if (ticksPerDay < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(ticksPerDay), ticksPerDay, "Minstens 1 tick per dag.");
            }

            if (timeScale < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(timeScale), timeScale, "De bouwsnelheid kan niet negatief zijn.");
            }

            return (long)Math.Round((double)days * ticksPerDay * timeScale, MidpointRounding.AwayFromZero);
        }

        /// <summary>Voortgang van 0 tot 1; een bouw zonder duur is altijd klaar.</summary>
        public static float Progress(long startTick, long durationTicks, long nowTick)
        {
            if (durationTicks <= 0)
            {
                return 1f;
            }

            return (float)Math.Max(0.0, Math.Min(1.0, (double)(nowTick - startTick) / durationTicks));
        }
    }
}
