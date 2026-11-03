using System;
using System.Collections.Generic;
using Cities.Simulation.Citizens;
using Cities.Simulation.Vehicles;
using Unity.Mathematics;

namespace Cities.Gameplay.Tools
{
    /// <summary>Zoekt de dichtstbijzijnde agent (burger of voertuig) bij een punt op de grond.</summary>
    public interface IAgentLocator<T>
    {
        bool TryFindNearest(float3 point, float maxDistance, out T agent);
    }

    /// <summary><see cref="IAgentLocator{T}"/> boven een lijst die de simulatie bijhoudt; de afstand is horizontaal.</summary>
    public sealed class NearestAgentLocator<T> : IAgentLocator<T>
    {
        private readonly Func<IEnumerable<T>> _agents;
        private readonly Func<T, float3> _position;

        public NearestAgentLocator(Func<IEnumerable<T>> agents, Func<T, float3> position)
        {
            _agents = agents ?? throw new ArgumentNullException(nameof(agents));
            _position = position ?? throw new ArgumentNullException(nameof(position));
        }

        public bool TryFindNearest(float3 point, float maxDistance, out T agent)
        {
            var best = maxDistance * maxDistance;
            var found = false;
            agent = default;
            foreach (var candidate in _agents())
            {
                var delta = _position(candidate) - point;
                var distance = (delta.x * delta.x) + (delta.z * delta.z);
                if (distance <= best)
                {
                    best = distance;
                    agent = candidate;
                    found = true;
                }
            }

            return found;
        }
    }

    public readonly struct CitizenSample
    {
        public CitizenSample(float3 position, uint id, Citizen citizen, Activity activity)
        {
            Position = position;
            Id = id;
            Citizen = citizen;
            Activity = activity;
        }

        public float3 Position { get; }

        public uint Id { get; }

        public Citizen Citizen { get; }

        public Activity Activity { get; }
    }

    public readonly struct VehicleSample
    {
        public VehicleSample(float3 position, uint id, VehicleCategory category, float speed, float speedLimit, float stoppedSeconds)
        {
            Position = position;
            Id = id;
            Category = category;
            Speed = speed;
            SpeedLimit = speedLimit;
            StoppedSeconds = stoppedSeconds;
        }

        public float3 Position { get; }

        public uint Id { get; }

        public VehicleCategory Category { get; }

        /// <summary>Snelheid in m/s.</summary>
        public float Speed { get; }

        /// <summary>Limiet van de weg in m/s.</summary>
        public float SpeedLimit { get; }

        /// <summary>Hoe lang het voertuig al stilstaat zonder dat het wacht op een licht of voorrang.</summary>
        public float StoppedSeconds { get; }
    }

    /// <summary>Welke problemen uit de gegevens van een burger of voertuig volgen. Zuivere functies, dus eenvoudig te testen.</summary>
    public static class InspectionProblems
    {
        public const int UnhappyBelow = 40;
        public const int UnhealthyBelow = 40;
        public const float StandstillAfter = 10f;

        public static List<string> For(in Citizen citizen)
        {
            var problems = new List<string>();
            if (!citizen.HasHome)
            {
                problems.Add("Dakloos");
            }

            if (citizen.IsJobSeeker)
            {
                problems.Add("Werkzoekend");
            }

            if (citizen.Health < UnhealthyBelow)
            {
                problems.Add("Slechte gezondheid");
            }

            if (citizen.Happiness < UnhappyBelow)
            {
                problems.Add("Ontevreden");
            }

            return problems;
        }

        public static List<string> For(in VehicleSample vehicle)
        {
            var problems = new List<string>();
            if (vehicle.StoppedSeconds >= GridlockDetector.Timeout)
            {
                problems.Add("Vastgelopen");
            }
            else if (vehicle.StoppedSeconds >= StandstillAfter)
            {
                problems.Add("Staat stil");
            }

            return problems;
        }
    }

    /// <summary>Inspectiebron voor burgers: leeftijd, gezondheid, geluk, woning en werk, met de problemen erbij.</summary>
    public sealed class CitizenInspection : IInspectionSource
    {
        public const float Radius = 3f;

        private readonly IAgentLocator<CitizenSample> _locator;

        public CitizenInspection(IAgentLocator<CitizenSample> locator)
        {
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        }

        public bool TryInspect(float3 point, out InspectionReport report)
        {
            if (!_locator.TryFindNearest(point, Radius, out var sample))
            {
                report = default;
                return false;
            }

            var citizen = sample.Citizen;
            report = new InspectionReport(
                InspectionKind.Citizen,
                sample.Position,
                $"Burger {sample.Id}",
                new[]
                {
                    $"{citizen.Age} jaar",
                    $"gezondheid {citizen.Health}",
                    $"geluk {citizen.Happiness}",
                    citizen.HasHome ? $"woont in gebouw {citizen.Home.Index}" : "geen woning",
                    citizen.HasWork ? $"werkt in gebouw {citizen.Work.Index}" : "geen werk",
                    $"nu: {sample.Activity}",
                },
                InspectionProblems.For(citizen));
            return true;
        }
    }

    /// <summary>Inspectiebron voor voertuigen: soort, snelheid ten opzichte van de limiet en stilstand.</summary>
    public sealed class VehicleInspection : IInspectionSource
    {
        public const float Radius = 4f;

        private readonly IAgentLocator<VehicleSample> _locator;

        public VehicleInspection(IAgentLocator<VehicleSample> locator)
        {
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        }

        public bool TryInspect(float3 point, out InspectionReport report)
        {
            if (!_locator.TryFindNearest(point, Radius, out var vehicle))
            {
                report = default;
                return false;
            }

            report = new InspectionReport(
                InspectionKind.Vehicle,
                vehicle.Position,
                $"Voertuig {vehicle.Id}",
                new[]
                {
                    vehicle.Category.ToString(),
                    $"{vehicle.Speed * 3.6f:0} km/u",
                    $"limiet {vehicle.SpeedLimit * 3.6f:0} km/u",
                },
                InspectionProblems.For(vehicle));
            return true;
        }
    }
}
