using System;
using System.Collections.Generic;

namespace Cities.Simulation.Vehicles
{
    /// <summary>
    /// Congestie van één lane (E08-06). De bezetting is het deel van de lane dat door voertuigen plus hun minimale opening wordt gevuld
    /// (1 is bumper aan bumper). De segmentsnelheid daalt lineair met die bezetting (Greenshields) maar zakt nooit onder
    /// <see cref="MinSpeedFactor"/>, zodat een volle lane kruipt in plaats van stilstaat. Files ontstaan en lossen vanzelf op in
    /// <see cref="LaneTraffic.Step"/>: een dicht einde laat voertuigen opstapelen, een open einde laat ze weer wegrijden.
    /// </summary>
    public static class Congestion
    {
        /// <summary>Laagste deel van de vrije snelheid dat een volle lane nog haalt.</summary>
        public const float MinSpeedFactor = 0.05f;

        public static float Occupancy(IReadOnlyList<VehicleAgent> vehicles, float laneLength)
        {
            if (vehicles is null)
            {
                throw new ArgumentNullException(nameof(vehicles));
            }

            if (!(laneLength > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(laneLength), laneLength, "De lane heeft een lengte groter dan nul nodig.");
            }

            var filled = 0f;
            for (var i = 0; i < vehicles.Count; i++)
            {
                var car = vehicles[i];
                filled += car.Length + car.Driver.MinGap;
            }

            return Math.Min(1f, filled / laneLength);
        }

        /// <summary>Snelheid in m/s die het segment bij deze bezetting toelaat, gegeven de vrije snelheid.</summary>
        public static float SegmentSpeed(float freeSpeed, float occupancy) =>
            freeSpeed * Math.Max(MinSpeedFactor, 1f - Math.Clamp(occupancy, 0f, 1f));
    }

    /// <summary>
    /// Deadlock-detectie met herstel (E08-06): een voertuig dat te lang stilstaat terwijl het niet legitiem wacht (rood licht, voorrang)
    /// zit vast. <see cref="Update"/> meldt dat na <see cref="Timeout"/> seconden; de aanroeper herstelt dan, bijvoorbeeld door het voertuig
    /// te verwijderen of naar een vrije lane te zetten. De teller begint daarna opnieuw.
    /// </summary>
    public sealed class GridlockDetector
    {
        /// <summary>Seconden stilstaan zonder reden voordat een voertuig als vastgelopen geldt.</summary>
        public const float Timeout = 60f;

        /// <summary>Snelheid in m/s waaronder een voertuig als stilstaand telt.</summary>
        public const float StandstillBelow = 0.1f;

        private readonly Dictionary<int, float> _stuck = new Dictionary<int, float>();

        /// <summary>Geeft true zodra voertuig <paramref name="id"/> vastzit. <paramref name="waitingLegitimately"/> zet de teller terug.</summary>
        public bool Update(int id, float speed, bool waitingLegitimately, float dt)
        {
            if (waitingLegitimately || speed >= StandstillBelow)
            {
                _stuck.Remove(id);
                return false;
            }

            var time = (_stuck.TryGetValue(id, out var t) ? t : 0f) + dt;
            if (time < Timeout)
            {
                _stuck[id] = time;
                return false;
            }

            _stuck.Remove(id);
            return true;
        }

        /// <summary>Vergeet een voertuig dat uit de simulatie is.</summary>
        public void Forget(int id) => _stuck.Remove(id);
    }
}
