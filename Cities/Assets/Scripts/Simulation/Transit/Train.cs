using System;

namespace Cities.Simulation.Transit
{
    /// <summary>Soort trein (E09-07): een passagierstrein stopt op perrons, een goederentrein laadt en lost op een goederenstation.</summary>
    public enum TrainKind
    {
        Passenger = 0,
        Freight = 1,
    }

    /// <summary>
    /// De regels van de trein (E09-07): snelheid en optrekken per soort, treinlengte en capaciteit, de minimale lengte van spoor en perron,
    /// het stationsinterval en de sluitregels van een overweg. De trein is een zware, lange en snelle reiziger: ver uit elkaar liggende
    /// stations en een lange waarschuwingstijd bij een overweg. shortcut: de trein rijdt op constante snelheid in <see cref="RailNetwork"/>
    /// (geen optrekken en remmen onderweg) en rijdt nog niet met eigen capaciteit in <see cref="TransitFleet"/>; <see cref="TravelTime"/> geeft de
    /// realistische rittijd voor de dienstregeling.
    /// </summary>
    public static class Train
    {
        /// <summary>Topsnelheid van een passagierstrein in m/s (160 km/u).</summary>
        public const float PassengerSpeed = 44f;

        /// <summary>Topsnelheid van een goederentrein in m/s (80 km/u).</summary>
        public const float FreightSpeed = 22f;

        /// <summary>Optrekken en remmen van een passagierstrein in m/s².</summary>
        public const float PassengerAcceleration = 0.6f;

        /// <summary>Optrekken en remmen van een zware goederentrein in m/s².</summary>
        public const float FreightAcceleration = 0.25f;

        /// <summary>Lengte van een locomotief in meter.</summary>
        public const float LocomotiveLength = 20f;

        public const float PassengerCarLength = 25f;
        public const float FreightWagonLength = 20f;

        public const int PassengerCarCapacity = 100;

        /// <summary>Lading per goederenwagon in ton.</summary>
        public const int FreightWagonCapacity = 50;

        public const int MaxCars = 20;

        /// <summary>Kortste stuk spoor tussen twee knopen in meter.</summary>
        public const float MinTrackLength = 50f;

        public const float MinPlatformLength = 50f;

        /// <summary>Minimale afstand tussen twee passagiersstations in meter.</summary>
        public const float MinStationSpacing = 1500f;

        /// <summary>Seconden dat de bomen van een overweg al dicht zijn voordat de trein er is.</summary>
        public const float CrossingWarningTime = 30f;

        /// <summary>Meter achter de trein tot de overweg weer open mag: de bomen gaan pas open als de laatste wagon er voorbij is.</summary>
        public const float CrossingClearDistance = 10f;

        /// <summary>Minimale afstand van een overweg tot het einde van een stuk spoor in meter.</summary>
        public const float CrossingMargin = 10f;

        /// <summary>Minimale afstand tussen twee overwegen op hetzelfde spoor in meter.</summary>
        public const float CrossingSpacing = 30f;

        public static float TopSpeed(TrainKind kind) => kind == TrainKind.Passenger ? PassengerSpeed : FreightSpeed;

        public static float Acceleration(TrainKind kind) => kind == TrainKind.Passenger ? PassengerAcceleration : FreightAcceleration;

        public static float CarLength(TrainKind kind) => kind == TrainKind.Passenger ? PassengerCarLength : FreightWagonLength;

        /// <summary>De lengte van de hele trein in meter: locomotief plus wagons.</summary>
        public static float Length(TrainKind kind, int cars)
        {
            CheckCars(cars);
            return LocomotiveLength + cars * CarLength(kind);
        }

        /// <summary>Passagiers (passagierstrein) of ton lading (goederentrein) van een trein.</summary>
        public static int Capacity(TrainKind kind, int cars)
        {
            CheckCars(cars);
            return cars * (kind == TrainKind.Passenger ? PassengerCarCapacity : FreightWagonCapacity);
        }

        /// <summary>Het grootste aantal wagons dat langs een perron van <paramref name="platformLength"/> meter past (locomotief inbegrepen).</summary>
        public static int MaxCarsFor(TrainKind kind, float platformLength)
        {
            var fit = (int)Math.Floor((platformLength - LocomotiveLength) / CarLength(kind));
            return Math.Max(0, Math.Min(MaxCars, fit));
        }

        /// <summary>Seconden voor één rit over <paramref name="distance"/> meter: optrekken, rijden op topsnelheid en remmen.</summary>
        public static float TravelTime(TrainKind kind, float distance)
        {
            if (!(distance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "De afstand kan niet negatief zijn.");
            }

            var top = TopSpeed(kind);
            var acceleration = Acceleration(kind);
            var ramp = top * top / acceleration;
            return distance >= ramp
                ? distance / top + top / acceleration
                : 2f * (float)Math.Sqrt(distance / acceleration);
        }

        /// <summary>
        /// Of de bomen dicht moeten zijn. <paramref name="frontToCrossing"/> is de afstand van de neus van de trein tot de overweg langs de route
        /// (negatief als de neus er al voorbij is). Dicht vanaf <see cref="CrossingWarningTime"/> voor aankomst tot de staart er voorbij is.
        /// </summary>
        public static bool ShouldClose(float frontToCrossing, float speed, float trainLength) =>
            frontToCrossing <= speed * CrossingWarningTime && frontToCrossing >= -(trainLength + CrossingClearDistance);

        /// <summary>Hoe lang een overweg dicht is voor één passerende trein in seconden.</summary>
        public static float CrossingClosedTime(TrainKind kind, int cars) =>
            CrossingWarningTime + (Length(kind, cars) + CrossingClearDistance) / TopSpeed(kind);

        /// <summary>Gooit een <see cref="ArgumentOutOfRangeException"/> als een perron korter is dan <see cref="MinPlatformLength"/>.</summary>
        public static void CheckPlatform(float platformLength)
        {
            if (!(platformLength >= MinPlatformLength))
            {
                throw new ArgumentOutOfRangeException(nameof(platformLength), platformLength, $"Een perron is minstens {MinPlatformLength} m lang.");
            }
        }

        private static void CheckCars(int cars)
        {
            if (cars < 1 || cars > MaxCars)
            {
                throw new ArgumentOutOfRangeException(nameof(cars), cars, $"Een trein heeft 1 tot {MaxCars} wagons.");
            }
        }
    }
}
