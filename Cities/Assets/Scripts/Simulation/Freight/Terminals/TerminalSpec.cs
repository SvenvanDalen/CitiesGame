using System;

namespace Cities.Simulation.Freight.Terminals
{
    /// <summary>Soort terminal: een haven laadt schepen, een railterminal treinen (E14-06).</summary>
    public enum TerminalKind : byte
    {
        Port = 0,
        RailTerminal = 1,
    }

    /// <summary>Wat voor voertuig lading aanvoert of afvoert bij een terminal.</summary>
    public enum CarrierMode : byte
    {
        Ship = 0,
        Train = 1,
    }

    /// <summary>De vaste capaciteiten van een terminal; alle hoeveelheden zijn in ton, snelheden in ton per dag.</summary>
    public sealed class TerminalSpec
    {
        public TerminalSpec(TerminalKind kind, int berths, float craneRate, float gateRate, float storageCapacity, float maxDockDays)
        {
            if (!Enum.IsDefined(typeof(TerminalKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            if (berths < 1 || berths > 64)
            {
                throw new ArgumentOutOfRangeException(nameof(berths), "Een terminal heeft 1 tot 64 ligplaatsen of sporen.");
            }

            RequirePositive(craneRate, nameof(craneRate));
            RequirePositive(gateRate, nameof(gateRate));
            RequirePositive(storageCapacity, nameof(storageCapacity));
            RequirePositive(maxDockDays, nameof(maxDockDays));
            Kind = kind;
            Berths = berths;
            CraneRate = craneRate;
            GateRate = gateRate;
            StorageCapacity = storageCapacity;
            MaxDockDays = maxDockDays;
        }

        public TerminalKind Kind { get; }

        /// <summary>Aantal ligplaatsen (haven) of sporen (railterminal) waar tegelijk gelost en geladen wordt.</summary>
        public int Berths { get; }

        /// <summary>Overslag per ligplaats of spoor van en naar het voertuig (kranen, portalen), ton per dag.</summary>
        public float CraneRate { get; }

        /// <summary>Overslag van en naar vrachtwagens bij de poort van de terminal, ton per dag.</summary>
        public float GateRate { get; }

        /// <summary>Opslag op het terrein, ton.</summary>
        public float StorageCapacity { get; }

        /// <summary>Na zoveel dagen aan de kade vertrekt een voertuig toch, ook als het niet klaar is.</summary>
        public float MaxDockDays { get; }

        /// <summary>De vervoerswijze die bij dit soort terminal aanlegt.</summary>
        public CarrierMode ServedMode => Kind == TerminalKind.Port ? CarrierMode.Ship : CarrierMode.Train;

        private static void RequirePositive(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Moet een positief getal zijn.");
            }
        }
    }
}
