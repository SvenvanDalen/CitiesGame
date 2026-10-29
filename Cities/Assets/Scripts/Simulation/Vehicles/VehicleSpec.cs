using System;
using System.Collections.Generic;
using Cities.Core.Configuration;
using UnityEngine;

namespace Cities.Simulation.Vehicles
{
    public enum VehicleCategory : byte
    {
        PassengerCar,
        Van,
        Truck,
        Bus,
        Tram,
        Service,
    }

    /// <summary>Fysieke en economische eigenschappen van een voertuigtype, in SI-eenheden waar mogelijk.</summary>
    [Serializable]
    public struct VehicleSpec
    {
        public VehicleCategory Category;

        [Tooltip("Lengte in meters, voor wachtrijen en parkeren.")]
        public float LengthMeters;

        [Tooltip("Breedte in meters.")]
        public float WidthMeters;

        [Tooltip("Topsnelheid in km/u; op de weg geldt daarnaast de snelheidslimiet.")]
        public float MaxSpeedKmh;

        [Tooltip("Gemiddelde acceleratie in m/s² vanuit stilstand.")]
        public float AccelerationMs2;

        [Tooltip("Comfortabele remvertraging in m/s².")]
        public float BrakingMs2;

        [Tooltip("Aantal reizigers inclusief bestuurder.")]
        public int PassengerCapacity;

        [Tooltip("Laadvermogen in tonnen.")]
        public float CargoCapacityTonnes;

        [Tooltip("Aanschafprijs voor de stad; 0 voor particuliere voertuigen.")]
        public long PurchaseCost;

        [Tooltip("Vaste kosten per maand voor de stad (onderhoud, personeel); 0 voor particuliere voertuigen.")]
        public long UpkeepPerMonth;

        public float MaxSpeedMetersPerSecond => MaxSpeedKmh / 3.6f;

        /// <summary>Remweg in meters vanaf topsnelheid bij comfortabel remmen: v² / 2a.</summary>
        public float BrakingDistanceFromMaxSpeed =>
            BrakingMs2 > 0f ? MaxSpeedMetersPerSecond * MaxSpeedMetersPerSecond / (2f * BrakingMs2) : float.PositiveInfinity;

        public void Validate(ICollection<string> errors)
        {
            SpecValidation.Range(errors, nameof(LengthMeters), LengthMeters, 1.5f, 50f, " m");
            SpecValidation.Range(errors, nameof(WidthMeters), WidthMeters, 0.5f, 3.5f, " m");
            SpecValidation.Range(errors, nameof(MaxSpeedKmh), MaxSpeedKmh, 5f, 300f, " km/u");
            SpecValidation.Range(errors, nameof(AccelerationMs2), AccelerationMs2, 0.1f, 10f, " m/s²");
            SpecValidation.Range(errors, nameof(BrakingMs2), BrakingMs2, 0.5f, 12f, " m/s²");
            SpecValidation.Range(errors, nameof(PassengerCapacity), PassengerCapacity, 0, 500);
            SpecValidation.Range(errors, nameof(CargoCapacityTonnes), CargoCapacityTonnes, 0f, 60f, " t");
            SpecValidation.NotNegative(errors, nameof(PurchaseCost), PurchaseCost);
            SpecValidation.NotNegative(errors, nameof(UpkeepPerMonth), UpkeepPerMonth);
            if (Category == VehicleCategory.Bus && PassengerCapacity < 10)
            {
                errors.Add("Een bus vervoert minstens 10 reizigers.");
            }

            if (Category == VehicleCategory.Truck && CargoCapacityTonnes <= 0f)
            {
                errors.Add("Een vrachtwagen heeft laadvermogen nodig.");
            }
        }
    }
}
