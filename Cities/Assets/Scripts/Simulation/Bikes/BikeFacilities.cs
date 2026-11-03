using System;
using System.Collections.Generic;
using Cities.Simulation.Network;

namespace Cities.Simulation.Bikes
{
    /// <summary>Wat een wegvak voor fietsers biedt (E09-10).</summary>
    public enum BikeFacility : byte
    {
        /// <summary>Geen eigen fietsruimte: de fietser rijdt tussen het autoverkeer.</summary>
        None = 0,

        /// <summary>Fietsstrook langs de rijbaan, zonder fysieke scheiding.</summary>
        Lane = 1,

        /// <summary>Vrijliggend fietspad zonder autoverkeer.</summary>
        Path = 2,
    }

    /// <summary>
    /// Fietsinfrastructuur (E09-10): herkent uit het dwarsprofiel of een weg een fietsstrook of fietspad heeft en geeft per soort
    /// de snelheid, het risico, de aanlegkosten en het onderhoud. Zuivere functies; de waarden zijn schattingen.
    /// </summary>
    public static class BikeFacilities
    {
        /// <summary>Snelheidsfactor op <see cref="PathNetwork.BikeSpeed"/>: tussen auto's trager, op een eigen pad sneller.</summary>
        public const float MixedSpeedFactor = 0.7f;
        public const float LaneSpeedFactor = 1f;
        public const float PathSpeedFactor = 1.1f;

        /// <summary>Kans op een ongeval per kilometer relatief aan een fietsstrook (1).</summary>
        public const float MixedRisk = 3f;
        public const float LaneRisk = 1f;
        public const float PathRisk = 0.3f;

        /// <summary>Aanleg per strekkende meter (beide richtingen) en onderhoud per meter per week.</summary>
        public const long LaneBuildCostPerMeter = 12;
        public const long PathBuildCostPerMeter = 30;
        public const long LaneUpkeepPerMeterPerWeek = 1;
        public const long PathUpkeepPerMeterPerWeek = 2;

        /// <summary>
        /// Soort fietsvoorziening van een dwarsprofiel: zonder fietslane <see cref="BikeFacility.None"/>, met fietslane én rijstroken
        /// een <see cref="BikeFacility.Lane"/>, en met alleen fietslanes (geen voertuigen) een <see cref="BikeFacility.Path"/>.
        /// </summary>
        public static BikeFacility Classify(IReadOnlyList<LaneSpec> profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var bike = false;
            var vehicles = false;
            foreach (var lane in profile)
            {
                bike |= (lane.Types & LaneTypes.Bike) != 0;
                vehicles |= (lane.Types & LaneTypes.Vehicles) != 0;
            }

            if (!bike)
            {
                return BikeFacility.None;
            }

            return vehicles ? BikeFacility.Lane : BikeFacility.Path;
        }

        public static float SpeedFactor(BikeFacility facility)
        {
            switch (facility)
            {
                case BikeFacility.Path:
                    return PathSpeedFactor;
                case BikeFacility.Lane:
                    return LaneSpeedFactor;
                default:
                    return MixedSpeedFactor;
            }
        }

        public static float Risk(BikeFacility facility)
        {
            switch (facility)
            {
                case BikeFacility.Path:
                    return PathRisk;
                case BikeFacility.Lane:
                    return LaneRisk;
                default:
                    return MixedRisk;
            }
        }

        /// <summary>Fietssnelheid in m/s op deze voorziening.</summary>
        public static float Speed(BikeFacility facility) => PathNetwork.BikeSpeed * SpeedFactor(facility);

        /// <summary>Reistijd in seconden over <paramref name="meters"/> op deze voorziening.</summary>
        public static float TravelSeconds(BikeFacility facility, float meters) => Math.Max(meters, 0f) / Speed(facility);

        public static long BuildCost(BikeFacility facility, float meters)
        {
            var perMeter = facility == BikeFacility.Path ? PathBuildCostPerMeter : facility == BikeFacility.Lane ? LaneBuildCostPerMeter : 0L;
            return (long)Math.Ceiling(Math.Max(meters, 0f) * perMeter);
        }

        public static long WeeklyUpkeep(BikeFacility facility, float meters)
        {
            var perMeter = facility == BikeFacility.Path ? PathUpkeepPerMeterPerWeek : facility == BikeFacility.Lane ? LaneUpkeepPerMeterPerWeek : 0L;
            return (long)Math.Ceiling(Math.Max(meters, 0f) * perMeter);
        }
    }

    /// <summary>
    /// Het fietsnet van de stad in cijfers (E09-10): de lengte per voorziening, hoeveel van het wegennet fietsveilig is en wat het onderhoud kost.
    /// Wordt opnieuw opgebouwd uit het wegennet, dus niet opgeslagen.
    /// </summary>
    public sealed class BikeNetworkStats
    {
        private readonly float[] _length = new float[3];

        public float TotalLength => _length[0] + _length[1] + _length[2];

        public float LengthOf(BikeFacility facility) => _length[(int)facility];

        public void Add(BikeFacility facility, float meters)
        {
            if (!(meters >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(meters), meters, "De lengte is nul of meer.");
            }

            _length[(int)facility] += meters;
        }

        /// <summary>Aandeel van het wegennet (naar lengte) met een strook of pad, 0 tot 1.</summary>
        public float Coverage => TotalLength > 0f ? (_length[1] + _length[2]) / TotalLength : 0f;

        /// <summary>Gemiddeld ongevalsrisico per kilometer fietsen over het hele net (1 is een fietsstrook).</summary>
        public float AverageRisk
        {
            get
            {
                var total = TotalLength;
                if (total <= 0f)
                {
                    return BikeFacilities.MixedRisk;
                }

                return ((_length[0] * BikeFacilities.MixedRisk) + (_length[1] * BikeFacilities.LaneRisk) + (_length[2] * BikeFacilities.PathRisk)) / total;
            }
        }

        public long WeeklyUpkeep => BikeFacilities.WeeklyUpkeep(BikeFacility.Lane, _length[1]) + BikeFacilities.WeeklyUpkeep(BikeFacility.Path, _length[2]);
    }
}
