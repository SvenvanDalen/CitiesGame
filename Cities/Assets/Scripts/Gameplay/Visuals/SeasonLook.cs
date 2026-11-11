using System;
using Cities.Core.Randomness;
using Unity.Mathematics;

namespace Cities.Gameplay.Visuals
{
    public enum Season : byte
    {
        Winter,
        Spring,
        Summer,
        Autumn,
    }

    /// <summary>
    /// Seizoenen (E19-12): bladkleur en sneeuwlaag over het jaar, voor het noordelijk halfrond. Alles is een vloeiende functie van de dag
    /// van het jaar, met per jaar een kleine verschuiving uit de seed zodat geen twee winters hetzelfde beginnen. Sneeuw ligt alleen in
    /// de winter en is bewust dun; wegen worden er niet door geblokkeerd (dat is E25-01).
    /// </summary>
    public sealed class SeasonLook
    {
        public const int DaysPerYear = 365;

        /// <summary>Maximale verschuiving van de seizoensgrenzen in dagen.</summary>
        public const int MaxShiftDays = 7;

        /// <summary>Hoogste sneeuwlaag (0 tot 1).</summary>
        public const float MaxSnow = 0.8f;

        private readonly ulong _seed;

        public SeasonLook(ulong seed)
        {
            _seed = seed;
        }

        /// <summary>Het seizoen voor een maand (1 tot 12): december tot en met februari is winter.</summary>
        public static Season SeasonOf(int month)
        {
            if (month < 1 || month > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), month, "Een maand ligt tussen 1 en 12.");
            }

            return (Season)((month % 12) / 3);
        }

        /// <summary>Verschuiving in dagen (-7 tot 7) voor een jaar.</summary>
        public int ShiftDays(int year)
        {
            var roll = (SeedHash.Combine(_seed, (ulong)(uint)year) >> 40) / (float)(1 << 24);
            return (int)math.round((roll * 2f - 1f) * MaxShiftDays);
        }

        /// <summary>
        /// Hoe groen het gebladerte is: 0 kaal, 1 vol groen. Vol in de zomer, valt af in de herfst, kaal in de winter en loopt in de
        /// lente weer aan. <paramref name="dayOfYear"/> loopt van 1 tot 366.
        /// </summary>
        public float Foliage(int dayOfYear, int year)
        {
            var day = Day(dayOfYear, year);
            var fade = Ramp(day, 258f, 320f);
            var bud = Ramp(day, 85f, 140f);
            return day < 200f ? bud : 1f - fade;
        }

        /// <summary>Hoe herfstachtig de bladeren zijn (0 groen, 1 oranje en rood); piekt aan het begin van de bladval.</summary>
        public float AutumnTint(int dayOfYear, int year)
        {
            var day = Day(dayOfYear, year);
            return day < 230f ? 0f : math.saturate(Ramp(day, 230f, 285f) * (1f - Ramp(day, 320f, 345f)));
        }

        /// <summary>De sneeuwlaag, 0 tot <see cref="MaxSnow"/>: bouwt op in december, ligt het dikst in januari en smelt in februari.</summary>
        public float SnowCover(int dayOfYear, int year)
        {
            var day = Day(dayOfYear, year);
            if (day > 60f && day < 335f)
            {
                return 0f;
            }

            var winterDay = day >= 335f ? day - 335f : day + 30f;
            var build = Ramp(winterDay, 0f, 25f);
            var melt = 1f - Ramp(winterDay, 55f, 90f);
            return MaxSnow * build * melt;
        }

        // De seizoenskalender schuift met het jaar mee, de dag blijft binnen 1..365.
        private float Day(int dayOfYear, int year)
        {
            if (dayOfYear < 1 || dayOfYear > DaysPerYear + 1)
            {
                throw new ArgumentOutOfRangeException(nameof(dayOfYear), dayOfYear, "Een dag van het jaar ligt tussen 1 en 366.");
            }

            return math.clamp(dayOfYear - ShiftDays(year), 1f, DaysPerYear);
        }

        private static float Ramp(float value, float from, float to) => math.saturate((value - from) / (to - from));
    }
}
