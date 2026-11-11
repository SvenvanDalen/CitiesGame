using System;
using System.Collections.Generic;
using Cities.Gameplay.Visuals;
using Cities.Simulation;
using Cities.Simulation.Attractions;
using Cities.Simulation.Aviation;
using Cities.Simulation.Economy;
using Cities.Simulation.Fields;
using Cities.Simulation.Freight;
using Cities.Simulation.Services;
using Unity.Mathematics;

namespace Cities.Gameplay.Expansion
{
    /// <summary>Soort plaatsbare voorziening uit de M8-uitbreidingen.</summary>
    public enum VenueKind
    {
        Zoo,
        ThemePark,
        NatureReserve,
        Faculty,
        SportsField,
        Office,
        Exchange,
        ConferenceHotel,
        Warehouse,
        Quay,
        Crane,
        FishingBoat,
        Terminal,
        Runway,
        CargoHall,
        NoiseBarrier,
        Promenade,
        WindFarm,
        ChargeStation,
        Monorail,
        CableCar,
        Airship,
        Ferry,
        Helicopter,
    }

    /// <summary>Opbrengst en kosten van één spelletdag voor alle voorzieningen.</summary>
    public readonly struct VenueDay
    {
        public VenueDay(long income, long upkeep)
        {
            Income = income;
            Upkeep = upkeep;
        }

        public long Income { get; }

        public long Upkeep { get; }

        public long Net => Income - Upkeep;
    }

    /// <summary>
    /// De plaatsbare voorzieningen van M8 (parken, campus, financieel district, magazijn, haven, luchthaven, promenade, groene stad, extra
    /// vervoer en helikopter) achter één klasse: tellingen van wat de speler plaatste, en per dag opbrengst en onderhoud uit de modellen.
    /// De opbrengsten zijn bewust bescheiden en elke voorziening kost onderhoud.
    /// </summary>
    public sealed class CityVenues
    {
        public const long UpkeepPerVenuePerDay = 60;
        public const int TransitVehiclesPerStop = 2;
        public const float TransitLineMeters = 1_500f;
        public const int TransitFare = 2;

        private static readonly (VenueKind Kind, ExtraTransitMode Mode)[] TransitModes =
        {
            (VenueKind.Monorail, ExtraTransitMode.Monorail),
            (VenueKind.CableCar, ExtraTransitMode.CableCar),
            (VenueKind.Airship, ExtraTransitMode.Airship),
            (VenueKind.Ferry, ExtraTransitMode.FerryRoute),
        };

        private readonly int[] _counts = new int[Enum.GetValues(typeof(VenueKind)).Length];
        private readonly List<ServiceHelicopter> _helicopters = new List<ServiceHelicopter>();
        private readonly PortFishing _fishing = new PortFishing();
        private Warehouse _warehouse;

        public int Count(VenueKind kind) => _counts[(int)kind];

        public int Total
        {
            get
            {
                var total = 0;
                foreach (var count in _counts)
                {
                    total += count;
                }

                return total;
            }
        }

        /// <summary>Punten die CO2 opnemen: een natuurreservaat telt vijf, een dierentuin of pretpark twee.</summary>
        public int Sinks => (Count(VenueKind.NatureReserve) * 5) + (Count(VenueKind.Zoo) * 2) + (Count(VenueKind.ThemePark) * 2);

        /// <summary>Beursbonus op kantoorbelasting (1 of 1,2).</summary>
        public float OfficeTaxFactor => Financial().OfficeTaxFactor;

        /// <summary>Extra onderzoek door faculteiten (0 tot 0,15).</summary>
        public float ResearchBonus => Campus().ResearchBonus;

        /// <summary>Extra geluk door natuurreservaten via wandelroutes.</summary>
        public float HappinessBonus => math.min(0.1f, Count(VenueKind.NatureReserve) * 0.03f);

        /// <summary>Aandeel elektrische voertuigen uit de laadpalen (aangenomen 4 km² stad, halve subsidie).</summary>
        public float ElectricShare => GreenCity.ElectricShare(Count(VenueKind.ChargeStation) / 4f, 0.5f);

        /// <summary>Uitstootfactor van het verkeer door elektrische voertuigen.</summary>
        public float TrafficEmissionFactor => GreenCity.TrafficEmissionFactor(ElectricShare);

        /// <summary>Geluidsoverlast van de luchthaven (0 tot 1) bij de vluchten van een dag.</summary>
        public float AirportNoise(int population) => Airport().Noise(Flights(Airport(), population));

        /// <summary>Voorraad vis in zee (0 tot 1).</summary>
        public float FishStock => _fishing.Stock;

        /// <summary>Wat er nu in het magazijn ligt.</summary>
        public int Stored => _warehouse?.Stored ?? 0;

        public void Add(VenueKind kind, float2 position = default)
        {
            _counts[(int)kind]++;
            if (kind == VenueKind.Helicopter)
            {
                _helicopters.Add(new ServiceHelicopter(ServiceKind.Health, position));
            }
        }

        /// <summary>Haalt één voorziening weg; doet niets als er geen is.</summary>
        public void Remove(VenueKind kind, float2 position = default)
        {
            if (_counts[(int)kind] == 0)
            {
                return;
            }

            _counts[(int)kind]--;
            if (kind == VenueKind.Helicopter && _helicopters.Count > 0)
            {
                var index = _helicopters.FindIndex(h => math.all(h.Base == position));
                _helicopters.RemoveAt(index >= 0 ? index : _helicopters.Count - 1);
            }
        }

        /// <summary>Snelste aankomsttijd van een helikopter bij een melding, of NaN als geen helikopter loont of kan opstijgen.</summary>
        public float HelicopterArrival(float2 target, bool storm)
        {
            var best = float.NaN;
            foreach (var heli in _helicopters)
            {
                heli.Grounded = storm;
                var seconds = heli.ArrivalSeconds(target);
                if (!float.IsNaN(seconds) && (float.IsNaN(best) || seconds < best))
                {
                    best = seconds;
                }
            }

            return best;
        }

        /// <summary>Eén spelletdag; het weer weegt het bezoek aan parken en de wind.</summary>
        public VenueDay Daily(int population, WeatherKind weather)
        {
            if (population < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(population), population, "Inwoners zijn niet negatief.");
            }

            var income = ParkIncome(population, weather) + CampusIncome() + FinancialIncome(population) + WarehouseIncome() + PortIncome()
                + AirportIncome(population) + PromenadeIncome() + WindIncome(weather) + TransitIncome(population);
            return new VenueDay(income, Upkeep());
        }

        /// <summary>Dagelijks onderhoud: een vast bedrag per voorziening plus de exploitatie van extra vervoer.</summary>
        public long Upkeep()
        {
            var upkeep = Total * UpkeepPerVenuePerDay;
            foreach (var (kind, mode) in TransitModes)
            {
                upkeep += Count(kind) * (ExtraTransitModes.Of(mode).UpkeepPerMonth(TransitLineMeters) / 30);
            }

            return upkeep;
        }

        private static float WeatherFactor(WeatherKind weather)
        {
            switch (weather)
            {
                case WeatherKind.Clear: return 1f;
                case WeatherKind.Cloudy: return 0.8f;
                case WeatherKind.Fog: return 0.5f;
                case WeatherKind.Rain: return 0.4f;
                default: return 0.1f;
            }
        }

        private static float WindSpeed(WeatherKind weather)
        {
            switch (weather)
            {
                case WeatherKind.Clear: return 7f;
                case WeatherKind.Cloudy: return 9f;
                case WeatherKind.Rain: return 11f;
                case WeatherKind.Fog: return 2f;
                default: return 30f;
            }
        }

        private static float Accessibility(int population) => math.saturate(0.3f + (population / 20_000f));

        private static int Flights(AirportComplex airport, int population) => Math.Min(airport.FlightsPerDay, Math.Max(0, population / 100));

        private long ParkIncome(int population, WeatherKind weather)
        {
            var access = Accessibility(population);
            var factor = WeatherFactor(weather);
            return (Count(VenueKind.Zoo) * new ParkAttraction(ParkKind.Zoo).IncomePerDay(access, factor))
                + (Count(VenueKind.ThemePark) * new ParkAttraction(ParkKind.ThemePark).IncomePerDay(access, factor));
        }

        private CampusLife Campus()
        {
            var campus = new CampusLife();
            for (var i = 0; i < Math.Min(Count(VenueKind.Faculty), CampusLife.MaxFaculties); i++)
            {
                campus.AddFaculty();
            }

            for (var i = 0; i < Count(VenueKind.SportsField); i++)
            {
                campus.AddSportTeam();
            }

            return campus;
        }

        /// <summary>Wedstrijdopbrengst per week, verdeeld over de dagen.</summary>
        private long CampusIncome() => Campus().MatchDayIncome / 7;

        private FinancialDistrict Financial() => new FinancialDistrict
        {
            OfficeBuildings = Count(VenueKind.Office),
            HasExchange = Count(VenueKind.Exchange) > 0,
            Hotels = Count(VenueKind.ConferenceHotel),
        };

        private long FinancialIncome(int population)
        {
            var district = Financial();
            var bonus = district.OfficeTaxFactor > 1f ? Count(VenueKind.Office) * 200L : 0L;
            return bonus + district.HotelIncomePerDay(population / 40, Count(VenueKind.ConferenceHotel) * 20);
        }

        private long WarehouseIncome()
        {
            var count = Count(VenueKind.Warehouse);
            if (count == 0)
            {
                _warehouse = null;
                return 0;
            }

            var capacity = count * 2_000;
            if (_warehouse == null || _warehouse.Capacity != capacity)
            {
                var kept = _warehouse?.Stored ?? 0;
                _warehouse = new Warehouse(capacity);
                _warehouse.Store(kept);
            }

            _warehouse.Store(count * 60);
            _warehouse.Take(count * 50);
            return _warehouse.Stored / 20;
        }

        private long PortIncome()
        {
            var throughput = PortFishing.Throughput(Count(VenueKind.Quay), Count(VenueKind.Crane));
            return (throughput * 4L) + (_fishing.FishDay(Count(VenueKind.FishingBoat)) * 12L);
        }

        private AirportComplex Airport() => new AirportComplex
        {
            Terminals = Count(VenueKind.Terminal),
            Runways = Count(VenueKind.Runway),
            CargoHalls = Count(VenueKind.CargoHall),
            NoiseBarriers = Count(VenueKind.NoiseBarrier),
        };

        private long AirportIncome(int population)
        {
            if (Count(VenueKind.Terminal) == 0 || Count(VenueKind.Runway) == 0)
            {
                return 0;
            }

            var airport = Airport();
            var passengers = Math.Min(airport.PassengerCapacity, population / 8);
            return (passengers * 15L) + (airport.CargoTonsPerDay * 20L);
        }

        private long PromenadeIncome()
        {
            var promenade = new Promenade(200f, 8, false);
            return Count(VenueKind.Promenade) * (promenade.PedestriansPerHour / 10L);
        }

        private long WindIncome(WeatherKind weather) =>
            (long)(GreenCity.WindPower(Count(VenueKind.WindFarm) * 3, WindSpeed(weather)) * 60f);

        private long TransitIncome(int population)
        {
            long income = 0;
            var demand = population / 20;
            foreach (var (kind, mode) in TransitModes)
            {
                var count = Count(kind);
                if (count == 0)
                {
                    continue;
                }

                var capacity = count * ExtraTransitModes.Of(mode).PassengersPerHour(TransitVehiclesPerStop, TransitLineMeters) * 8;
                income += Math.Min(capacity, demand) * (long)TransitFare;
            }

            return income;
        }
    }
}
