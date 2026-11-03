using System;
using Cities.Simulation.Network;
using Cities.Simulation.Vehicles;

namespace Cities.Simulation.Pedestrians
{
    public enum CrossingKind : byte
    {
        /// <summary>Zebrapad: voetgangers gaan als er tijd genoeg is, voertuigen stoppen voor wie op het pad staat.</summary>
        Zebra = 0,

        /// <summary>Lichtenregeling: voetgangers gaan als de regeling het voertuigverkeer lang genoeg rood houdt.</summary>
        Signal = 1,
    }

    /// <summary>
    /// Een oversteek over een rijbaan (E08-12). Voetgangers en fietsers starten alleen als ze over zijn voordat een voertuig er is;
    /// zolang er iemand op staat (<see cref="VehiclesMustStop"/>) sluit het voertuigverkeer het einde van de lane (<c>endBlocked</c> in
    /// <see cref="LaneTraffic.Step"/>), zodat voertuigen en overstekenden elkaar niet raken.
    /// </summary>
    public sealed class PedestrianCrossing
    {
        /// <summary>Extra seconden marge tussen het einde van de oversteek en de aankomst van een voertuig.</summary>
        public const float SafetyMargin = 2f;

        public PedestrianCrossing(CrossingKind kind, float length, SignalPlan plan = null, int vehicleGroup = 0)
        {
            if (!(length > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "De oversteek moet een positieve lengte hebben.");
            }

            Kind = kind;
            Length = length;
            Plan = plan;
            VehicleGroup = vehicleGroup;
        }

        public CrossingKind Kind { get; }

        public float Length { get; }

        public SignalPlan Plan { get; }

        /// <summary>De groep van de regeling waar het voertuigverkeer over deze oversteek bij hoort.</summary>
        public int VehicleGroup { get; }

        /// <summary>Hoeveel mensen er nu op de oversteek zijn.</summary>
        public int Occupants { get; internal set; }

        public bool VehiclesMustStop => Occupants > 0;

        public float CrossingSeconds(PathMode mode) => Length / (mode == PathMode.Walk ? PathNetwork.WalkSpeed : PathNetwork.BikeSpeed);

        /// <param name="mode">Hoe de oversteker zich verplaatst.</param>
        /// <param name="vehicleSeconds">Seconden tot het eerstvolgende voertuig bij de oversteek is (oneindig zonder voertuig).</param>
        public bool MayEnter(PathMode mode, float vehicleSeconds)
        {
            var needed = CrossingSeconds(mode);
            if (Kind == CrossingKind.Signal && Plan != null && Plan.Enabled)
            {
                return Plan.SecondsUntilOpen(VehicleGroup) >= needed;
            }

            return vehicleSeconds >= needed + SafetyMargin;
        }
    }
}
