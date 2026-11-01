using System;

namespace Cities.Simulation.Growth
{
    public enum BuildingPhase
    {
        /// <summary>Het gebouw is in gebruik.</summary>
        Active = 0,

        /// <summary>Verlaten: niemand woont of werkt er meer.</summary>
        Abandoned = 1,

        /// <summary>Gesloopt; het puin ligt er nog.</summary>
        Rubble = 2,

        /// <summary>Opgeruimd: de kavel is weer bebouwbaar en het gebouw bestaat niet meer.</summary>
        Cleared = 3,
    }

    /// <summary>
    /// Verval en sloop van een gebouw (E05-10). Een gebouw dat <see cref="AbandonAfterDays"/> dagen achtereen leeg staat, raakt verlaten; blijft het
    /// <see cref="DemolishAfterDays"/> dagen verlaten, dan wordt het gesloopt en blijft er puin liggen, dat na <see cref="ClearAfterDays"/> dagen
    /// is opgeruimd. Komt er onderweg weer gebruik (iemand trekt erin), dan is het gebouw weer in gebruik. Eén dag per <see cref="Step"/>.
    /// </summary>
    public struct BuildingLifecycle
    {
        public const int AbandonAfterDays = 30;
        public const int DemolishAfterDays = 60;
        public const int ClearAfterDays = 14;

        private int _days;

        public BuildingPhase Phase { get; private set; }

        /// <summary>Of de kavel weer bebouwd mag worden.</summary>
        public bool IsBuildable => Phase == BuildingPhase.Cleared;

        /// <summary>Of er puin ligt (en dus niet gebouwd mag worden).</summary>
        public bool HasRubble => Phase == BuildingPhase.Rubble;

        /// <summary>Verwerkt één dag; <paramref name="inUse"/> is of er die dag iemand woonde of werkte.</summary>
        public void Step(bool inUse)
        {
            switch (Phase)
            {
                case BuildingPhase.Active:
                    _days = inUse ? 0 : _days + 1;
                    if (_days >= AbandonAfterDays)
                    {
                        Move(BuildingPhase.Abandoned);
                    }

                    break;
                case BuildingPhase.Abandoned:
                    if (inUse)
                    {
                        Move(BuildingPhase.Active);
                        break;
                    }

                    _days++;
                    if (_days >= DemolishAfterDays)
                    {
                        Move(BuildingPhase.Rubble);
                    }

                    break;
                case BuildingPhase.Rubble:
                    _days++;
                    if (_days >= ClearAfterDays)
                    {
                        Move(BuildingPhase.Cleared);
                    }

                    break;
                default:
                    break;
            }
        }

        private void Move(BuildingPhase phase)
        {
            Phase = phase;
            _days = 0;
        }
    }
}
