using System;
using System.Collections.Generic;
using System.Linq;

namespace Cities.Simulation.Vehicles
{
    public enum RightOfWay
    {
        /// <summary>Voorrangsweg: mag altijd proberen te rijden.</summary>
        Priority,

        /// <summary>Voorrang verlenen aan voorrangsverkeer en aan voertuigen die al in de rotonde rijden.</summary>
        Yield,

        /// <summary>Eerst stilstaan, dan voorrang verlenen en op volgorde van aankomst rijden.</summary>
        Stop,

        /// <summary>Verkeer van rechts gaat voor.</summary>
        RightBeforeLeft,

        /// <summary>Oprijden van een rotonde: verkeer dat al in de rotonde rijdt gaat voor.</summary>
        Roundabout,
    }

    /// <summary>
    /// Een voertuig dat een kruispunt wil op. <see cref="Parts"/> zijn de kruispuntdelen van zijn route, <see cref="Bearing"/> de richting
    /// in graden (0 noord, met de klok mee) waar de weg vanaf het midden van het kruispunt ligt.
    /// </summary>
    public readonly struct Approach
    {
        public Approach(int vehicle, RightOfWay rule, IReadOnlyCollection<int> parts, float bearing, bool stopped, float waitTime, bool circulating = false)
        {
            Vehicle = vehicle;
            Rule = rule;
            Parts = parts ?? throw new ArgumentNullException(nameof(parts));
            Bearing = bearing;
            Stopped = stopped;
            WaitTime = waitTime;
            Circulating = circulating;
        }

        private Approach(Approach source, bool transitPriority)
        {
            Vehicle = source.Vehicle;
            Rule = source.Rule;
            Parts = source.Parts;
            Bearing = source.Bearing;
            Stopped = source.Stopped;
            WaitTime = source.WaitTime;
            Circulating = source.Circulating;
            TransitPriority = transitPriority;
        }

        public int Vehicle { get; }

        public RightOfWay Rule { get; }

        public IReadOnlyCollection<int> Parts { get; }

        public float Bearing { get; }

        public bool Stopped { get; }

        /// <summary>Seconden die het voertuig al wacht.</summary>
        public float WaitTime { get; }

        /// <summary>Het voertuig rijdt al in de rotonde en heeft daar voorrang.</summary>
        public bool Circulating { get; }

        /// <summary>Een lijnvoertuig met OV-voorrang (E09-11): gaat voor op de wachtplichtige regels van dit kruispunt.</summary>
        public bool TransitPriority { get; }

        /// <summary>Kopie van deze aanrijding met OV-voorrang aan of uit.</summary>
        public Approach WithTransitPriority(bool value = true) => new Approach(this, value);
    }

    /// <summary>
    /// Voorrangsregels (E08-05): stop, voorrang verlenen, rotonde en rechts-voor-links. De regels bepalen alleen wie mag proberen; het
    /// voertuig rijdt pas na een geslaagde reservering in <see cref="IntersectionReservations"/>, dezelfde laag als voor lanewissels en
    /// kruispuntdelen. Daardoor kan een foute regel nooit tot een botsing leiden, alleen tot wachten.
    /// </summary>
    public static class IntersectionRules
    {
        /// <summary>Na zoveel seconden wachten gaat rechts-voor-links door, anders blokkeren vier voertuigen elkaar voor altijd.</summary>
        public const float PatienceSeconds = 5f;

        public static bool MayProceed(Approach me, IEnumerable<Approach> others)
        {
            if (others is null)
            {
                throw new ArgumentNullException(nameof(others));
            }

            if (me.Circulating)
            {
                return true;
            }

            var mine = me;
            var conflicting = others.Where(o => o.Vehicle != mine.Vehicle && o.Parts.Intersect(mine.Parts).Any()).ToList();

            // OV-voorrang: een lijnvoertuig rijdt door tenzij er al iemand in de rotonde rijdt of een ander lijnvoertuig dezelfde voorrang heeft;
            // in dat laatste geval beslist de gewone regel (en dus de reservering) wie eerst gaat.
            if (me.TransitPriority && !conflicting.Any(o => o.Circulating || o.TransitPriority))
            {
                return true;
            }

            return me.Rule switch
            {
                RightOfWay.Priority => true,
                RightOfWay.Yield => !conflicting.Any(HasPriority),
                RightOfWay.Stop => me.Stopped && !conflicting.Any(o => HasPriority(o) || WaitsLonger(o, mine)),
                RightOfWay.RightBeforeLeft => me.WaitTime >= PatienceSeconds || !conflicting.Any(o => HasPriority(o) || FromRight(mine, o)),
                RightOfWay.Roundabout => !conflicting.Any(o => o.Circulating),
                _ => throw new ArgumentOutOfRangeException(nameof(me), me.Rule, "Onbekende voorrangsregel."),
            };
        }

        /// <summary>Reserveert de route als de regels het toestaan; geeft terug of het voertuig mag rijden.</summary>
        public static bool TryEnter(Approach me, IEnumerable<Approach> others, IntersectionReservations reservations)
        {
            if (reservations is null)
            {
                throw new ArgumentNullException(nameof(reservations));
            }

            return MayProceed(me, others) && reservations.TryReserve(me.Vehicle, me.Parts);
        }

        private static bool HasPriority(Approach other) => other.Rule == RightOfWay.Priority || other.Circulating || other.TransitPriority;

        private static bool WaitsLonger(Approach other, Approach me)
        {
            var order = other.WaitTime.CompareTo(me.WaitTime);
            return other.Stopped && (order > 0 || (order == 0 && other.Vehicle < me.Vehicle));
        }

        // Rechtsverkeer: wie rechts van mij komt ligt 90 graden tegen de klok in vanaf mijn richting; ruim marge voor scheve kruispunten.
        private static bool FromRight(Approach me, Approach other)
        {
            var delta = ((other.Bearing - me.Bearing + 540f) % 360f) - 180f;
            return delta > -150f && delta < -30f;
        }
    }
}
