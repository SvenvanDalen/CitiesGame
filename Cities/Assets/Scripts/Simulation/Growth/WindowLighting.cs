using System;
using Cities.Core.Randomness;

namespace Cities.Simulation.Growth
{
    /// <summary>
    /// Nachtverlichting van gebouwen (E05-13): welke ramen branden. Het aandeel verlichte ramen volgt de tijd van de dag (overdag uit,
    /// 's avonds het meest, 's nachts bijna alles uit) maal de bezetting van het gebouw. Elk raam heeft een vaste eigen drempel uit de seed
    /// van het gebouw, dus het patroon flikkert niet: als het aandeel stijgt, gaan er ramen bij en gaan er geen andere uit.
    /// </summary>
    public static class WindowLighting
    {
        // Aandeel verlichte ramen van een volledig bezet gebouw per uur van de dag; tussen de punten wordt lineair gemengd.
        private static readonly float[] HourlyShare =
        {
            0.05f, 0.04f, 0.04f, 0.03f, 0.03f, 0.10f, // 00:00 tot 05:00
            0.30f, 0.20f, 0.05f, 0.02f, 0.02f, 0.02f, // 06:00 tot 11:00
            0.02f, 0.02f, 0.02f, 0.02f, 0.05f, 0.30f, // 12:00 tot 17:00
            0.70f, 0.95f, 1.00f, 0.90f, 0.55f, 0.25f, // 18:00 tot 23:00
        };

        /// <summary>Aandeel verlichte ramen (0 tot 1) voor <paramref name="timeOfDay"/> (0 tot 1, 0 is middernacht) en bezetting 0 tot 1.</summary>
        public static float LitFraction(float timeOfDay, float occupancy)
        {
            var day = timeOfDay - (float)Math.Floor(timeOfDay);
            var position = day * HourlyShare.Length;
            var index = (int)position % HourlyShare.Length;
            var next = (index + 1) % HourlyShare.Length;
            var blend = position - (float)Math.Floor(position);
            var share = HourlyShare[index] + ((HourlyShare[next] - HourlyShare[index]) * blend);
            return share * Math.Min(Math.Max(occupancy, 0f), 1f);
        }

        /// <summary>Of raam <paramref name="windowIndex"/> van het gebouw met <paramref name="buildingSeed"/> nu brandt.</summary>
        public static bool IsLit(int windowIndex, ulong buildingSeed, float timeOfDay, float occupancy) =>
            Threshold(windowIndex, buildingSeed) < LitFraction(timeOfDay, occupancy);

        /// <summary>De vaste drempel van een raam, gelijk verdeeld over [0, 1).</summary>
        public static float Threshold(int windowIndex, ulong buildingSeed) =>
            (SeedHash.Combine(buildingSeed, (ulong)windowIndex) >> 40) / (float)(1UL << 24);
    }
}
