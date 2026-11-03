using System;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    public enum CultureKind : byte
    {
        Library = 0,
        Theatre = 1,
        Museum = 2,
        Landmark = 3,
    }

    /// <summary>
    /// Een cultuurgebouw (E10-10): bibliotheek, theater, museum of bezienswaardigheid. Het heeft een bereik (<see cref="Radius"/>) en een vast
    /// aantal bezoeken per dag (<see cref="DailyCapacity"/>). Een bezoeker uit dat bereik tikt één bezoek af en krijgt <see cref="Happiness"/>
    /// erbij; is het gebouw vol, dan gaat hij zonder. Een bezienswaardigheid trekt daarnaast <see cref="TouristsPerDay"/> toeristen aan.
    /// <see cref="NewDay"/> zet de teller terug.
    /// </summary>
    public sealed class CultureVenue
    {
        public CultureVenue(CultureKind kind, float2 position, float radius, int dailyCapacity, float happiness, int touristsPerDay, long upkeepPerMonth)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Het bereik moet groter dan nul zijn.");
            }

            if (dailyCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(dailyCapacity), dailyCapacity, "Een cultuurgebouw heeft minstens één bezoek per dag nodig.");
            }

            if (!(happiness >= 0f) || touristsPerDay < 0 || upkeepPerMonth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(happiness), "Geluk, toeristen en onderhoud kunnen niet negatief zijn.");
            }

            Kind = kind;
            Position = position;
            Radius = radius;
            DailyCapacity = dailyCapacity;
            Happiness = happiness;
            TouristsPerDay = touristsPerDay;
            UpkeepPerMonth = upkeepPerMonth;
        }

        public CultureKind Kind { get; }

        public float2 Position { get; }

        public float Radius { get; }

        public int DailyCapacity { get; }

        /// <summary>Geluk (0 tot 1) dat één bezoek oplevert.</summary>
        public float Happiness { get; }

        public int TouristsPerDay { get; }

        public long UpkeepPerMonth { get; }

        public int Visits { get; private set; }

        public bool IsFull => Visits >= DailyCapacity;

        // shortcut: bereiken, capaciteiten en kosten zijn schattingen, kalibreren zodra de stad draait.
        public static CultureVenue Library(float2 position) => new CultureVenue(CultureKind.Library, position, 800f, 300, 0.05f, 0, 40000);

        public static CultureVenue Theatre(float2 position) => new CultureVenue(CultureKind.Theatre, position, 2000f, 400, 0.15f, 0, 120000);

        public static CultureVenue Museum(float2 position) => new CultureVenue(CultureKind.Museum, position, 2500f, 500, 0.12f, 150, 150000);

        public static CultureVenue Landmark(float2 position) => new CultureVenue(CultureKind.Landmark, position, 4000f, 2000, 0.1f, 600, 200000);

        public bool Covers(float2 point) => math.lengthsq(point - Position) <= Radius * Radius;

        /// <summary>Eén bezoek vanuit <paramref name="home"/>: het geluk dat het oplevert, of 0 buiten bereik of als het gebouw vol is.</summary>
        public float Visit(float2 home)
        {
            if (IsFull || !Covers(home))
            {
                return 0f;
            }

            Visits++;
            return Happiness;
        }

        public void NewDay() => Visits = 0;
    }
}
