using System;
using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.Simulation.Network
{
    /// <summary>
    /// Eigenschappen van een wegtype: de opbouw van het dwarsprofiel (zie <see cref="LaneProfiles.Road"/>), de
    /// snelheidslimiet en de kosten per meter.
    /// </summary>
    [Serializable]
    public struct RoadTypeSpec
    {
        public const int MaxCarLanes = 4;

        [Tooltip("Rijstroken per richting; bij een eenrichtingsweg het totaal. 0 voor een fiets- of voetpad.")]
        public int CarLanes;

        [Tooltip("Alle rijstroken dezelfde kant op.")]
        public bool OneWay;

        public bool BikeLanes;

        public bool Sidewalks;

        [Tooltip("Maximumsnelheid in km/u.")]
        public float SpeedLimitKmh;

        [Tooltip("Aanlegkosten per meter.")]
        public long CostPerMeter;

        [Tooltip("Onderhoud per meter per maand.")]
        public long UpkeepPerMeterPerMonth;

        public readonly float SpeedLimitMetersPerSecond => SpeedLimitKmh / 3.6f;

        /// <summary>De lanes van een weg van dit type.</summary>
        public readonly List<LaneSpec> Profile() => LaneProfiles.Road(CarLanes, BikeLanes, Sidewalks, OneWay);

        public readonly void Validate(ICollection<string> errors)
        {
            SpecValidation.Range(errors, nameof(CarLanes), CarLanes, 0, MaxCarLanes);
            SpecValidation.Range(errors, nameof(SpeedLimitKmh), SpeedLimitKmh, 5f, 130f, " km/u");
            SpecValidation.NotNegative(errors, nameof(CostPerMeter), CostPerMeter);
            SpecValidation.NotNegative(errors, nameof(UpkeepPerMeterPerMonth), UpkeepPerMeterPerMonth);
            if (CarLanes == 0 && !BikeLanes && !Sidewalks)
            {
                errors.Add("Een weg heeft minstens een rijstrook, fietspad of stoep nodig.");
            }

            if (OneWay && CarLanes == 0)
            {
                errors.Add("Een eenrichtingsweg heeft minstens één rijstrook nodig.");
            }
        }
    }
}
