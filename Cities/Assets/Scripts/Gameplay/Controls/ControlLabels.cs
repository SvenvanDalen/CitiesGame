using System.Collections.Generic;

namespace Cities.Gameplay.Controls
{
    /// <summary>Nederlandse namen en groepen voor de regels in het besturingsscherm, in de volgorde waarin ze verschijnen.</summary>
    public static class ControlLabels
    {
        public const string CategoryCamera = "Camera";
        public const string CategorySimulation = "Simulatie";
        public const string CategoryGeneral = "Algemeen";

        private static readonly ControlLabel[] Entries =
        {
            new ControlLabel("Camera/Move/up", CategoryCamera, "Vooruit"),
            new ControlLabel("Camera/Move/down", CategoryCamera, "Achteruit"),
            new ControlLabel("Camera/Move/left", CategoryCamera, "Links"),
            new ControlLabel("Camera/Move/right", CategoryCamera, "Rechts"),
            new ControlLabel("Camera/Rotate/negative", CategoryCamera, "Tegen de klok in draaien"),
            new ControlLabel("Camera/Rotate/positive", CategoryCamera, "Met de klok mee draaien"),
            new ControlLabel("Camera/Tilt/positive", CategoryCamera, "Steiler kijken"),
            new ControlLabel("Camera/Tilt/negative", CategoryCamera, "Vlakker kijken"),
            new ControlLabel("Camera/Zoom/positive", CategoryCamera, "Inzoomen"),
            new ControlLabel("Camera/Zoom/negative", CategoryCamera, "Uitzoomen"),
            new ControlLabel("Camera/PanDrag", CategoryCamera, "Slepen om te verschuiven"),
            new ControlLabel("Camera/OrbitDrag", CategoryCamera, "Slepen om te draaien"),
            new ControlLabel("Camera/Fast", CategoryCamera, "Sneller bewegen (ingedrukt)"),
            new ControlLabel("Game/Pause", CategorySimulation, "Pauze aan of uit"),
            new ControlLabel("Game/SpeedNormal", CategorySimulation, "Normale snelheid"),
            new ControlLabel("Game/SpeedFast", CategorySimulation, "Snelheid 2×"),
            new ControlLabel("Game/SpeedFastest", CategorySimulation, "Snelheid 3×"),
            new ControlLabel("Game/Menu", CategoryGeneral, "Menu openen of sluiten"),
        };

        private static readonly Dictionary<string, int> Order = BuildOrder();

        public static IReadOnlyList<ControlLabel> All => Entries;

        public static bool TryGet(string rowKey, out ControlLabel label, out int order)
        {
            if (Order.TryGetValue(rowKey, out order))
            {
                label = Entries[order];
                return true;
            }

            label = default;
            order = int.MaxValue;
            return false;
        }

        private static Dictionary<string, int> BuildOrder()
        {
            var order = new Dictionary<string, int>(System.StringComparer.Ordinal);
            for (var i = 0; i < Entries.Length; i++)
            {
                order.Add(Entries[i].Key, i);
            }

            return order;
        }
    }

    public readonly struct ControlLabel
    {
        public ControlLabel(string key, string category, string text)
        {
            Key = key;
            Category = category;
            Text = text;
        }

        public string Key { get; }

        public string Category { get; }

        public string Text { get; }
    }
}
