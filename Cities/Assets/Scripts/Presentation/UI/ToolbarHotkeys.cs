using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Sneltoetsen van de werkbalk (E27-02): één letter per categorie opent het paneel, de cijfers 1 tot en met 9 kiezen daarin het
    /// gereedschap. T blijft vrij omdat die toets draait.
    /// </summary>
    public static class ToolbarHotkeys
    {
        public const int MaxEntries = 9;

        private static readonly Dictionary<ToolbarTab, Key> TabKeys = new Dictionary<ToolbarTab, Key>
        {
            [ToolbarTab.Roads] = Key.R,
            [ToolbarTab.Zones] = Key.Z,
            [ToolbarTab.Utilities] = Key.U,
            [ToolbarTab.Services] = Key.V,
            [ToolbarTab.Transit] = Key.O,
            [ToolbarTab.Policies] = Key.P,
            [ToolbarTab.Districts] = Key.K,
            [ToolbarTab.Terrain] = Key.G,
            [ToolbarTab.Industry] = Key.I,
        };

        public static Key KeyOf(ToolbarTab tab) => TabKeys[tab];

        /// <summary>De tab bij een toets, of <c>null</c>.</summary>
        public static ToolbarTab? TabFor(Key key)
        {
            foreach (var pair in TabKeys)
            {
                if (pair.Value == key)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        /// <summary>Index (0 tot 8) bij de cijfertoets 1 tot 9 (ook numeriek toetsenbord), of -1.</summary>
        public static int EntryIndex(Key key)
        {
            if (key >= Key.Digit1 && key <= Key.Digit9)
            {
                return key - Key.Digit1;
            }

            if (key >= Key.Numpad1 && key <= Key.Numpad9)
            {
                return key - Key.Numpad1;
            }

            return -1;
        }

        /// <summary>Tooltip: naam, sneltoets en kosten (alleen als die er zijn).</summary>
        public static string Tooltip(string name, string hotkey, long cost)
        {
            var text = hotkey == null ? name : $"{name} [{hotkey}]";
            return cost > 0 ? $"{text} · € {cost.ToString("N0", new System.Globalization.CultureInfo("nl-NL"))}" : text;
        }
    }
}
