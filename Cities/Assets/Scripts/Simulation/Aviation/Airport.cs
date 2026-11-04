using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Cities.Simulation.Aviation
{
    /// <summary>
    /// Een luchthaven (E09-09): één baan, een terminal en gates. De baan ligt met zijn midden op <see cref="Position"/> in richting
    /// <see cref="Heading"/>. Elke gate heeft een vliegtuigsoort; een gate waarvoor de baan te kort is, blijft ongebruikt. Onveranderlijk.
    /// De capaciteiten volgen uit de baan, de gates en de terminal: het knelpunt bepaalt wat de luchthaven aankan.
    /// </summary>
    public sealed class Airport
    {
        private readonly AircraftClass[] _gates;

        public Airport(int id, string name, float2 position, float heading, float runwayLength, float terminalCapacity, IReadOnlyList<AircraftClass> gates)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Een luchthaven heeft een naam nodig.", nameof(name));
            }

            if (!AviationRules.IsRunwayOk(runwayLength))
            {
                throw new ArgumentOutOfRangeException(nameof(runwayLength), runwayLength, $"Een baan is {AviationRules.MinRunway} tot {AviationRules.MaxRunway} m lang.");
            }

            if (!(terminalCapacity > 0f) || terminalCapacity > AviationRules.MaxTerminalCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(terminalCapacity), terminalCapacity, $"De terminal verwerkt 0 tot {AviationRules.MaxTerminalCapacity} passagiers per uur.");
            }

            if (gates == null || gates.Count < 1 || gates.Count > AviationRules.MaxGates)
            {
                throw new ArgumentException($"Een luchthaven heeft 1 tot {AviationRules.MaxGates} gates nodig.", nameof(gates));
            }

            if (!math.all(math.isfinite(position)) || !math.isfinite(heading))
            {
                throw new ArgumentException("De plaats en richting van de baan moeten getallen zijn.", nameof(position));
            }

            _gates = new AircraftClass[gates.Count];
            for (var i = 0; i < _gates.Length; i++)
            {
                _gates[i] = gates[i];
            }

            Id = id;
            Name = name;
            Position = position;
            Heading = heading;
            RunwayLength = runwayLength;
            TerminalCapacity = terminalCapacity;

            var gateMoves = 0f;
            var gatePassengers = 0f;
            var occupancyWeighted = 0f;
            foreach (var gate in _gates)
            {
                if (!AviationRules.CanOperate(gate, runwayLength))
                {
                    continue;
                }

                var moves = AviationRules.GateMovementsPerHour(gate);
                gateMoves += moves;
                gatePassengers += moves * AviationRules.PassengersPerMovement(gate);
                occupancyWeighted += moves * AviationRules.RunwayOccupancy(gate);
                UsableGateCount++;
                if (gate > LoudestClass)
                {
                    LoudestClass = gate;
                }
            }

            if (UsableGateCount == 0)
            {
                throw new ArgumentException("De baan is te kort voor alle gates: geen enkel toestel kan er terecht.", nameof(gates));
            }

            GateMovementsPerHour = gateMoves;
            RunwayMovementsPerHour = 3600f / (occupancyWeighted / gateMoves);
            MovementsPerHour = math.min(gateMoves, RunwayMovementsPerHour);
            PassengersPerHour = math.min(gatePassengers * (MovementsPerHour / gateMoves), terminalCapacity);
            AveragePassengersPerMovement = gatePassengers / gateMoves;
        }

        public int Id { get; }

        public string Name { get; }

        public float2 Position { get; }

        /// <summary>Richting van de baan in radialen (0 = naar +x).</summary>
        public float Heading { get; }

        public float RunwayLength { get; }

        /// <summary>Passagiers per uur die de terminal aankan.</summary>
        public float TerminalCapacity { get; }

        public IReadOnlyList<AircraftClass> Gates => _gates;

        /// <summary>Aantal gates waar de baan lang genoeg voor is.</summary>
        public int UsableGateCount { get; }

        /// <summary>De zwaarste vliegtuigsoort die de baan en de gates toelaten; bepaalt het lawaai.</summary>
        public AircraftClass LoudestClass { get; }

        /// <summary>Bewegingen (landingen plus starts) per uur die de gates aankunnen.</summary>
        public float GateMovementsPerHour { get; }

        /// <summary>Bewegingen per uur die de baan aankan voor de aanwezige mix van toestellen.</summary>
        public float RunwayMovementsPerHour { get; }

        /// <summary>Bewegingen per uur die de luchthaven echt haalt: het kleinste van gates en baan.</summary>
        public float MovementsPerHour { get; }

        /// <summary>Passagiers per uur (aankomend plus vertrekkend) die de luchthaven echt haalt: ook beperkt door de terminal.</summary>
        public float PassengersPerHour { get; }

        /// <summary>Het gemiddeld aantal passagiers per beweging bij de aanwezige mix van toestellen.</summary>
        public float AveragePassengersPerMovement { get; }

        /// <summary>Richting van de baan als eenheidsvector.</summary>
        public float2 Direction => new float2(math.cos(Heading), math.sin(Heading));

        /// <summary>Het begin van de baan (tegen de richting in).</summary>
        public float2 RunwayStart => Position - (Direction * (RunwayLength * 0.5f));

        /// <summary>Het einde van de baan.</summary>
        public float2 RunwayEnd => Position + (Direction * (RunwayLength * 0.5f));

        /// <summary>Benutting 0 tot 1 bij een vraag van <paramref name="demand"/> passagiers per uur.</summary>
        public float Utilisation(float demand) => demand > 0f ? math.saturate(demand / PassengersPerHour) : 0f;
    }
}
