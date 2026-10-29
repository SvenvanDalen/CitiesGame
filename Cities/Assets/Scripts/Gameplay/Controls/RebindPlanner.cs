using System;
using System.Collections.Generic;

namespace Cities.Gameplay.Controls
{
    /// <summary>
    /// Bepaalt welke bindingen veranderen als de speler een toets kiest. Is de toets al in gebruik, dan ruilen de twee
    /// slots van toets, zodat er nooit twee acties op dezelfde toets zitten en er geen actie onbereikbaar wordt.
    /// </summary>
    public static class RebindPlanner
    {
        /// <summary>
        /// Wijzigingen voor <paramref name="target"/> naar <paramref name="newPath"/>; leeg als er niets verandert.
        /// <paramref name="effectivePath"/> geeft het huidige pad van een binding (met overschrijving van de speler).
        /// </summary>
        public static IReadOnlyList<BindingChange> Plan(
            IReadOnlyList<ControlRow> rows,
            Func<BindingInfo, string> effectivePath,
            SlotRef target,
            string newPath)
        {
            if (rows is null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (effectivePath is null)
            {
                throw new ArgumentNullException(nameof(effectivePath));
            }

            if (!target.IsValid || !target.Value.Rebindable)
            {
                throw new ArgumentException("Het slot is niet herbindbaar.", nameof(target));
            }

            newPath ??= string.Empty;
            var binding = target.Value.Binding;
            var oldPath = effectivePath(binding) ?? string.Empty;
            if (BindingRules.SamePath(oldPath, newPath))
            {
                return Array.Empty<BindingChange>();
            }

            var changes = new List<BindingChange>(2) { new BindingChange(binding, newPath) };
            var conflict = FindConflict(rows, effectivePath, target, newPath);
            if (conflict.IsValid)
            {
                changes.Add(new BindingChange(conflict.Value.Binding, oldPath, swapped: true));
            }

            return changes;
        }

        /// <summary>Ander slot dat <paramref name="path"/> al gebruikt, of een ongeldige verwijzing.</summary>
        public static SlotRef FindConflict(
            IReadOnlyList<ControlRow> rows,
            Func<BindingInfo, string> effectivePath,
            SlotRef except,
            string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return default;
            }

            foreach (var row in rows)
            {
                for (var slot = 0; slot < row.Slots.Count; slot++)
                {
                    var isTarget = ReferenceEquals(row, except.Row) && slot == except.Slot;
                    if (!isTarget && row.Slots[slot].Rebindable && BindingRules.SamePath(effectivePath(row.Slots[slot].Binding), path))
                    {
                        return new SlotRef(row, slot);
                    }
                }
            }

            return default;
        }

        /// <summary>Regel en slot van een binding, of een ongeldige verwijzing.</summary>
        public static SlotRef Locate(IReadOnlyList<ControlRow> rows, BindingInfo binding)
        {
            foreach (var row in rows)
            {
                for (var slot = 0; slot < row.Slots.Count; slot++)
                {
                    var candidate = row.Slots[slot].Binding;
                    if (candidate.Index == binding.Index && candidate.Map == binding.Map && candidate.Action == binding.Action)
                    {
                        return new SlotRef(row, slot);
                    }
                }
            }

            return default;
        }
    }

    /// <summary>Nieuw pad voor één binding; leeg maakt het slot vrij.</summary>
    public readonly struct BindingChange
    {
        public BindingChange(BindingInfo binding, string path, bool swapped = false)
        {
            Binding = binding;
            Path = path ?? string.Empty;
            Swapped = swapped;
        }

        public BindingInfo Binding { get; }

        public string Path { get; }

        /// <summary>Deze binding kreeg de oude toets omdat de speler zijn toets aan een andere actie gaf.</summary>
        public bool Swapped { get; }

        /// <summary>Of het nieuwe pad de standaard uit het asset is; dan kan de overschrijving weg.</summary>
        public bool IsDefault => BindingRules.SamePath(Path, Binding.DefaultPath);
    }
}
