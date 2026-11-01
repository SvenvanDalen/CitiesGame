using Unity.Mathematics;

namespace Cities.Simulation.Disasters
{
    public enum DisasterPhase : byte
    {
        Warning,
        Active,
        Aftermath,
        Ended,
    }

    /// <summary>Een lopende ramp (E15-01): waarschuwing, effect, nasleep, daarna voorbij. De manager schuift hem elke dag op.</summary>
    public sealed class Disaster
    {
        internal Disaster(DisasterSpec spec, float2 position)
        {
            Spec = spec;
            Position = position;
            Settle();
        }

        public DisasterSpec Spec { get; }

        public float2 Position { get; }

        public DisasterPhase Phase { get; private set; }

        /// <summary>Dagen die in de huidige fase al voorbij zijn.</summary>
        public int DayInPhase { get; private set; }

        /// <summary>Wat deze ramp vandaag kost: schade tijdens het effect, opruimkosten in de nasleep.</summary>
        public long CostToday => Phase switch
        {
            DisasterPhase.Active => Spec.DamagePerDay,
            DisasterPhase.Aftermath => Spec.AftermathCostPerDay,
            _ => 0,
        };

        /// <summary>Eén dag verder; geeft aan of de fase veranderde.</summary>
        internal bool Advance()
        {
            if (Phase == DisasterPhase.Ended)
            {
                return false;
            }

            DayInPhase++;
            return Settle();
        }

        private int Length() => Phase switch
        {
            DisasterPhase.Warning => Spec.WarningDays,
            DisasterPhase.Active => Spec.DurationDays,
            _ => Spec.AftermathDays,
        };

        // Fasen van nul dagen worden overgeslagen.
        private bool Settle()
        {
            var changed = false;
            while (Phase != DisasterPhase.Ended && DayInPhase >= Length())
            {
                Phase++;
                DayInPhase = 0;
                changed = true;
            }

            return changed;
        }
    }
}
