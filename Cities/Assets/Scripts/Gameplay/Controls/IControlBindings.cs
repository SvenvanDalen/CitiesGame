using System;
using System.Collections.Generic;

namespace Cities.Gameplay.Controls
{
    /// <summary>Wat het besturingsscherm nodig heeft om toetsen te tonen en te wijzigen, los van het Input System.</summary>
    public interface IControlBindings
    {
        IReadOnlyList<ControlRow> Rows { get; }

        /// <summary>Er wordt gewacht op een nieuwe toets.</summary>
        bool IsRebinding { get; }

        /// <summary>Minstens één slot wijkt af van de standaard.</summary>
        bool HasCustomizations { get; }

        /// <summary>Na elke wijziging, herstel of het laden van opgeslagen instellingen.</summary>
        event Action Changed;

        /// <summary>Leesbare naam van de huidige toets, bijvoorbeeld <c>W</c> of <c>Middelste muisknop</c>.</summary>
        string GetDisplayName(SlotRef slot);

        bool IsCustomized(SlotRef slot);

        /// <summary>Wacht op de volgende toets of muisknop en wijst die toe; Escape annuleert.</summary>
        void StartRebind(SlotRef slot, Action<RebindOutcome> completed);

        void CancelRebind();

        void ResetRow(ControlRow row);

        void ResetAll();
    }

    public enum RebindStatus
    {
        Completed,
        Unchanged,
        Canceled,
    }

    public readonly struct RebindOutcome
    {
        public RebindOutcome(RebindStatus status, SlotRef target, SlotRef swapped)
        {
            Status = status;
            Target = target;
            Swapped = swapped;
        }

        public RebindStatus Status { get; }

        public SlotRef Target { get; }

        /// <summary>Slot dat de oude toets kreeg omdat de nieuwe toets daar in gebruik was; anders ongeldig.</summary>
        public SlotRef Swapped { get; }
    }
}
