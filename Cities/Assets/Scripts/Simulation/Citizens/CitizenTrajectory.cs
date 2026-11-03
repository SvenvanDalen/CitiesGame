using Unity.Mathematics;

namespace Cities.Simulation.Citizens
{
    /// <summary>Zoekt de plek van een bestemming (woning, werk, school, winkel, park) voor een burger op; false als die onbekend is.</summary>
    public interface IDestinationMap
    {
        bool TryGetPosition(in Citizen citizen, Destination destination, out float2 position);
    }

    /// <summary>Waar een burger nu is en of hij te voet onderweg is (alleen dan is hij als voetganger te zien).</summary>
    public readonly struct Placement
    {
        public Placement(float2 position, bool walking)
        {
            Position = position;
            Walking = walking;
            Valid = true;
        }

        public bool Valid { get; }

        public float2 Position { get; }

        public bool Walking { get; }
    }

    /// <summary>
    /// De plek van een burger als pure functie van schema en tijd (E07-07). Op het hele uur begint een reis van de vorige naar de nieuwe
    /// bestemming, die <see cref="TripHours"/> duurt. Omdat een voetganger dichtbij en een statistische burger ver weg dezelfde functie
    /// gebruiken, springt niemand bij het wisselen van detailniveau. Reizen langer dan <see cref="WalkLimit"/> gaan per voertuig en zijn
    /// geen voetganger.
    /// </summary>
    public static class CitizenTrajectory
    {
        /// <summary>Duur van een reis in uren.</summary>
        public const float TripHours = 0.5f;

        /// <summary>Verste afstand in meters die iemand te voet aflegt.</summary>
        public const float WalkLimit = 1500f;

        // shortcut: rechte lijn tussen de bestemmingen; upgrade naar de route over het voetpad zodra voetgangers het PathNetwork volgen.
        public static Placement PlaceAt(IDestinationMap map, in Citizen citizen, uint id, int day, float hour)
        {
            var whole = (int)math.floor(hour);
            var previousDay = whole == 0 ? day - 1 : day;
            var previousHour = whole == 0 ? DailySchedule.HoursPerDay - 1 : whole - 1;
            var current = DailySchedule.DestinationFor(citizen, DailySchedule.ActivityAt(citizen, id, day, whole));
            var previous = DailySchedule.DestinationFor(citizen, DailySchedule.ActivityAt(citizen, id, previousDay, previousHour));
            if (!map.TryGetPosition(citizen, current, out var to))
            {
                return default;
            }

            if (current == previous || !map.TryGetPosition(citizen, previous, out var from))
            {
                return new Placement(to, false);
            }

            var progress = math.saturate((hour - whole) / TripHours);
            var moving = progress < 1f && math.distance(from, to) <= WalkLimit;
            return new Placement(math.lerp(from, to, progress), moving);
        }
    }
}
