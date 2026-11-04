using System;
using System.Collections.Generic;
using Cities.Simulation.Timing;

namespace Cities.Simulation.Events
{
    /// <summary>Instelbare maatregelen rond een locatie (E08-17): extra bussen, afgezette rijstroken en eenrichtingsverkeer.</summary>
    public readonly struct EventTrafficSettings
    {
        public const int MaxBuses = 50;
        public const int MaxClosedLanes = 4;

        public EventTrafficSettings(int extraBuses, int closedLanes, bool oneWay)
        {
            if (extraBuses < 0 || extraBuses > MaxBuses)
            {
                throw new ArgumentOutOfRangeException(nameof(extraBuses), "Aantal extra bussen buiten bereik.");
            }

            if (closedLanes < 0 || closedLanes > MaxClosedLanes)
            {
                throw new ArgumentOutOfRangeException(nameof(closedLanes), "Aantal afgezette rijstroken buiten bereik.");
            }

            ExtraBuses = extraBuses;
            ClosedLanes = closedLanes;
            OneWay = oneWay;
        }

        public int ExtraBuses { get; }

        public int ClosedLanes { get; }

        public bool OneWay { get; }
    }

    /// <summary>Personen en vervoerswijze in één uur rond een evenement. <see cref="HourOffset"/> is relatief aan het beginuur.</summary>
    public readonly struct EventHourTrips
    {
        public EventHourTrips(int hourOffset, bool arriving, long cars, long transit, long active)
        {
            HourOffset = hourOffset;
            Arriving = arriving;
            Cars = cars;
            Transit = transit;
            Active = active;
        }

        public int HourOffset { get; }

        /// <summary>Waar: aankomst voor het evenement, onwaar: vertrek erna.</summary>
        public bool Arriving { get; }

        /// <summary>Personen die met de auto komen of gaan.</summary>
        public long Cars { get; }

        /// <summary>Personen met trein of bus, inclusief extra bussen.</summary>
        public long Transit { get; }

        /// <summary>Personen te voet of per fiets.</summary>
        public long Active { get; }

        public long Persons => Cars + Transit + Active;

        /// <summary>Voertuigen op de weg bij de bezetting uit <see cref="EventTrafficModel.PersonsPerCarTimes10"/>.</summary>
        public long Vehicles => EventTrafficModel.VehiclesFor(Cars);
    }

    /// <summary>Uitkomst voor één evenement: de uurpieken, parkeerdruk rond de locatie en belasting van de toegangswegen.</summary>
    public sealed class EventTrafficReport
    {
        internal EventTrafficReport(EventTrafficDemand demand, EventTrafficSettings settings, IReadOnlyList<EventHourTrips> hours, int freeParking)
        {
            Demand = demand;
            Settings = settings;
            Hours = hours;
            FreeParking = Math.Max(0, freeParking);
            long arrivingVehicles = 0;
            foreach (var h in hours)
            {
                PeakVehiclesPerHour = Math.Max(PeakVehiclesPerHour, h.Vehicles);
                PeakTransitPerHour = Math.Max(PeakTransitPerHour, h.Transit);
                if (h.Arriving)
                {
                    arrivingVehicles += h.Vehicles;
                }
            }

            ParkingDemand = arrivingVehicles;
            ParkingOverflow = Math.Max(0, ParkingDemand - FreeParking);
            ParkingPressurePercent = (int)Math.Min(1000, ParkingDemand * 100 / Math.Max(1, FreeParking));
            AccessCapacityPerHour = EventTrafficModel.AccessCapacity(settings);
            AccessLoadPercent = (int)Math.Min(1000, PeakVehiclesPerHour * 100 / AccessCapacityPerHour);
        }

        public EventTrafficDemand Demand { get; }

        public EventTrafficSettings Settings { get; }

        public IReadOnlyList<EventHourTrips> Hours { get; }

        public long PeakVehiclesPerHour { get; }

        public long PeakTransitPerHour { get; }

        /// <summary>Auto's die rond de locatie een plek zoeken.</summary>
        public long ParkingDemand { get; }

        public int FreeParking { get; }

        /// <summary>Auto's zonder plek; die zoeken rond of staan fout geparkeerd.</summary>
        public long ParkingOverflow { get; }

        /// <summary>Vraag als percentage van de vrije plekken (minstens 1), begrensd op 1000.</summary>
        public int ParkingPressurePercent { get; }

        public int AccessCapacityPerHour { get; }

        /// <summary>Piek als percentage van wat de toegangswegen aankunnen; boven 100 loopt het vast.</summary>
        public int AccessLoadPercent { get; }
    }

    /// <summary>
    /// Verkeer rond evenementen (E08-17): zet de verwachte bezoekers van <see cref="EventTrafficDemand"/> om in aankomsten voor en vertrek
    /// na het evenement (vaste curves), kiest de vervoerswijze (auto, OV, te voet) en rekent parkeerdruk en toegangscapaciteit uit.
    /// Alles in gehele getallen en zonder toeval, dus deterministisch.
    /// </summary>
    public static class EventTrafficModel
    {
        /// <summary>Het evenement duurt drie uur; het vertrek begint daarna.</summary>
        public const int DurationHours = 3;

        public const int PersonsPerCarTimes10 = 25;

        public const int BusSeatsPerHour = 50;

        public const int BaseAccessCapacity = 1200;

        private const int LocalTransitPercent = 20;
        private const int LocalActivePercent = 15;
        private const int OutsideTransitPercent = 35;

        // Aandeel (procent) per uur: aankomst op -3, -2, -1 uur; vertrek op +3, +4, +5 uur na het beginuur.
        private static readonly int[] ArrivalCurve = { 20, 30, 50 };
        private static readonly int[] DepartureCurve = { 50, 30, 20 };

        /// <summary>Voertuigen voor een aantal autoreizigers (naar boven afgerond).</summary>
        public static long VehiclesFor(long persons) => persons <= 0 ? 0 : ((persons * 10) + PersonsPerCarTimes10 - 1) / PersonsPerCarTimes10;

        /// <summary>Aankomstcapaciteit per uur: afgezette rijstroken verlagen die, eenrichtingsverkeer verhoogt die met een vijfde.</summary>
        public static int AccessCapacity(EventTrafficSettings settings)
        {
            var percent = 100 - (settings.ClosedLanes * 20);
            var capacity = BaseAccessCapacity * percent / 100;
            return settings.OneWay ? capacity * 120 / 100 : capacity;
        }

        public static EventTrafficReport Analyze(EventTrafficDemand demand, EventTrafficSettings settings, int freeParking)
        {
            var hours = new List<EventHourTrips>(ArrivalCurve.Length + DepartureCurve.Length);
            for (var i = 0; i < ArrivalCurve.Length; i++)
            {
                hours.Add(Hour(demand, settings, ArrivalCurve, i, i - ArrivalCurve.Length, true));
            }

            for (var i = 0; i < DepartureCurve.Length; i++)
            {
                hours.Add(Hour(demand, settings, DepartureCurve, i, DurationHours + i, false));
            }

            return new EventTrafficReport(demand, settings, hours, freeParking);
        }

        private static EventHourTrips Hour(EventTrafficDemand demand, EventTrafficSettings settings, int[] curve, int index, int offset, bool arriving)
        {
            var local = Share(demand.Local, curve, index);
            var outside = Share(demand.Outside, curve, index);
            var transit = (local * LocalTransitPercent / 100) + (outside * OutsideTransitPercent / 100);
            var active = local * LocalActivePercent / 100;
            var cars = local + outside - transit - active;
            var moved = Math.Min(cars, (long)settings.ExtraBuses * BusSeatsPerHour);
            return new EventHourTrips(offset, arriving, cars - moved, transit + moved, active);
        }

        /// <summary>Deel van het totaal in dit uur; het laatste uur krijgt de rest zodat de som klopt.</summary>
        private static long Share(long total, int[] curve, int index)
        {
            if (total <= 0)
            {
                return 0;
            }

            if (index < curve.Length - 1)
            {
                return total * curve[index] / 100;
            }

            long given = 0;
            for (var i = 0; i < index; i++)
            {
                given += total * curve[i] / 100;
            }

            return total - given;
        }
    }

    /// <summary>
    /// Bewaart de maatregelen per locatie en levert de verkeersrapporten van een dag uit een <see cref="IEventDemandSource"/>.
    /// </summary>
    public sealed class EventTrafficPlanner
    {
        private readonly SortedDictionary<int, EventTrafficSettings> _settings = new SortedDictionary<int, EventTrafficSettings>();

        public IReadOnlyDictionary<int, EventTrafficSettings> AllSettings => _settings;

        public EventTrafficSettings For(int venueId) =>
            _settings.TryGetValue(venueId, out var s) ? s : default;

        public void Set(int venueId, EventTrafficSettings settings)
        {
            _settings[venueId] = settings;
        }

        /// <summary>Rapport per evenement op deze dag; <paramref name="freeParkingAt"/> geeft de vrije plekken bij een locatie.</summary>
        public IReadOnlyList<EventTrafficReport> ReportsOn(IEventDemandSource source, GameDate date, Func<int, int> freeParkingAt)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (freeParkingAt == null)
            {
                throw new ArgumentNullException(nameof(freeParkingAt));
            }

            var reports = new List<EventTrafficReport>();
            foreach (var demand in source.DemandOn(date))
            {
                reports.Add(EventTrafficModel.Analyze(demand, For(demand.VenueId), freeParkingAt(demand.VenueId)));
            }

            return reports;
        }

        internal void Restore(IEnumerable<KeyValuePair<int, EventTrafficSettings>> items)
        {
            _settings.Clear();
            foreach (var item in items)
            {
                _settings[item.Key] = item.Value;
            }
        }
    }
}
