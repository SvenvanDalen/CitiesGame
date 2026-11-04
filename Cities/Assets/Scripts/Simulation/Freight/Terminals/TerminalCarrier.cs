namespace Cities.Simulation.Freight.Terminals
{
    public enum CarrierState : byte
    {
        /// <summary>Wacht op een vrije ligplaats of spoor.</summary>
        Queued = 0,

        /// <summary>Ligt aan de kade en wordt gelost en geladen.</summary>
        Docked = 1,

        /// <summary>Klaar en weg; wacht tot de verkeerslaag het ophaalt via <c>TakeDeparted</c>.</summary>
        Departed = 2,
    }

    /// <summary>
    /// Een schip of trein bij een terminal. Dit is de kleine eigen abstractie voor de overslag: de
    /// verkeerssystemen voor water en spoor melden aankomst met een lading en een gewenste afvoer.
    /// </summary>
    public sealed class TerminalCarrier
    {
        internal TerminalCarrier(int id, CarrierMode mode, float capacity, float load, float wanted)
        {
            Id = id;
            Mode = mode;
            Capacity = capacity;
            Load = load;
            Wanted = wanted;
        }

        public int Id { get; }

        public CarrierMode Mode { get; }

        /// <summary>Laadvermogen in ton.</summary>
        public float Capacity { get; }

        /// <summary>Ton die nu aan boord is (bij aankomst: te lossen, later ook de al geladen afvoer).</summary>
        public float Load { get; internal set; }

        /// <summary>Ton die het voertuig uit de terminal wil meenemen.</summary>
        public float Wanted { get; }

        /// <summary>Ton die al uit de opslag is geladen.</summary>
        public float Loaded { get; internal set; }

        /// <summary>Ton die al aan de terminal is overgedragen.</summary>
        public float Unloaded { get; internal set; }

        public CarrierState State { get; internal set; }

        /// <summary>Dagen in de rij.</summary>
        public float WaitedDays { get; internal set; }

        /// <summary>Dagen aan de kade.</summary>
        public float DockedDays { get; internal set; }

        /// <summary>Ton aan boord die nog gelost moet worden (de afvoer telt niet mee).</summary>
        public float ToUnload => Load - Loaded;
    }
}
