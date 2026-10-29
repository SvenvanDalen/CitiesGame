using System.Collections.Generic;
using System.Linq;

namespace Cities.Gameplay.Controls
{
    /// <summary>Bouwt de regels van het besturingsscherm uit de bindingen van het asset.</summary>
    public static class ControlCatalog
    {
        /// <summary>
        /// Groepeert bindingen per regel (actie of composite-deel), in de volgorde van <see cref="ControlLabels"/>.
        /// Acties zonder herbindbare binding (muispositie, wiel) komen er niet in; regels zonder label komen achteraan.
        /// </summary>
        public static IReadOnlyList<ControlRow> Build(IEnumerable<BindingInfo> bindings)
        {
            var slotsByKey = new Dictionary<string, List<ControlSlot>>(System.StringComparer.Ordinal);
            var keys = new List<string>();
            foreach (var binding in bindings)
            {
                if (!IsShown(binding))
                {
                    continue;
                }

                var key = binding.RowKey;
                if (!slotsByKey.TryGetValue(key, out var slots))
                {
                    slots = new List<ControlSlot>(2);
                    slotsByKey.Add(key, slots);
                    keys.Add(key);
                }

                slots.Add(new ControlSlot(binding, BindingRules.IsRebindable(binding)));
            }

            var rows = new List<(int Order, int Seen, ControlRow Row)>(keys.Count);
            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var row = ControlLabels.TryGet(key, out var label, out var order)
                    ? new ControlRow(key, label.Text, label.Category, slotsByKey[key])
                    : new ControlRow(key, key, ControlLabels.CategoryGeneral, slotsByKey[key]);
                rows.Add((order, i, row));
            }

            rows.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Seen.CompareTo(b.Seen));
            var result = new ControlRow[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                result[i] = rows[i].Row;
            }

            return result;
        }

        /// <summary>Sleutels van getoonde regels zonder Nederlandse naam; hoort leeg te zijn.</summary>
        public static IReadOnlyList<string> FindUnlabeled(IReadOnlyList<ControlRow> rows)
        {
            return rows.Select(row => row.Key).Where(key => !ControlLabels.TryGet(key, out _, out _)).ToList();
        }

        private static bool IsShown(BindingInfo binding) =>
            binding.Kind == BindingKind.CompositePart || (binding.Kind == BindingKind.Simple && binding.ActionIsButton);
    }
}
