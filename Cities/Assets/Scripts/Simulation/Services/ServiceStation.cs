using System;
using Unity.Mathematics;

namespace Cities.Simulation.Services
{
    public enum ServiceKind : byte
    {
        Fire,
        Police,
        Health,
        Death,
    }

    /// <summary>
    /// Een dienstgebouw (E10-01): brandweerkazerne, politiebureau, ziekenhuis of begraafplaats. Het dekt alles binnen <see cref="Radius"/>,
    /// heeft een vast aantal voertuigen (<see cref="Vehicles"/>) en kost elke maand <see cref="UpkeepPerMonth"/>.
    /// </summary>
    public sealed class ServiceStation
    {
        public ServiceStation(ServiceKind kind, float2 position, float radius, int vehicles, long upkeepPerMonth)
        {
            if (!(radius > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Het dekkingsbereik moet groter dan nul zijn.");
            }

            if (vehicles < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(vehicles), vehicles, "Een dienstgebouw heeft minstens één voertuig nodig.");
            }

            if (upkeepPerMonth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upkeepPerMonth), upkeepPerMonth, "De onderhoudskosten kunnen niet negatief zijn.");
            }

            Kind = kind;
            Position = position;
            Radius = radius;
            Vehicles = vehicles;
            UpkeepPerMonth = upkeepPerMonth;
            FreeVehicles = vehicles;
        }

        public ServiceKind Kind { get; }

        public float2 Position { get; }

        public float Radius { get; }

        public int Vehicles { get; }

        public long UpkeepPerMonth { get; }

        /// <summary>Voertuigen die op de kazerne staan en dus kunnen uitrukken.</summary>
        public int FreeVehicles { get; internal set; }

        public bool Covers(float2 point) => math.lengthsq(point - Position) <= Radius * Radius;
    }
}
