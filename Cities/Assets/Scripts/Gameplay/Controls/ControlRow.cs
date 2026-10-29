using System.Collections.Generic;

namespace Cities.Gameplay.Controls
{
    /// <summary>Eén regel in het besturingsscherm: een knopactie of een richting van een composite, met zijn toetsslots.</summary>
    public sealed class ControlRow
    {
        public ControlRow(string key, string label, string category, IReadOnlyList<ControlSlot> slots)
        {
            Key = key;
            Label = label;
            Category = category;
            Slots = slots;
        }

        /// <summary><c>Map/Actie</c> of <c>Map/Actie/deel</c>.</summary>
        public string Key { get; }

        public string Label { get; }

        public string Category { get; }

        /// <summary>Primaire toets eerst, daarna de alternatieven, in de volgorde van het asset.</summary>
        public IReadOnlyList<ControlSlot> Slots { get; }

        public string Map => Slots[0].Binding.Map;

        public string Action => Slots[0].Binding.Action;
    }

    public readonly struct ControlSlot
    {
        public ControlSlot(BindingInfo binding, bool rebindable)
        {
            Binding = binding;
            Rebindable = rebindable;
        }

        public BindingInfo Binding { get; }

        /// <summary>Vast slot (zoals Escape voor het menu) wordt wel getoond maar kan niet gewijzigd worden.</summary>
        public bool Rebindable { get; }
    }

    /// <summary>Verwijzing naar een slot in een regel.</summary>
    public readonly struct SlotRef
    {
        public SlotRef(ControlRow row, int slot)
        {
            Row = row;
            Slot = slot;
        }

        public ControlRow Row { get; }

        public int Slot { get; }

        public bool IsValid => Row != null;

        public ControlSlot Value => Row.Slots[Slot];
    }
}
