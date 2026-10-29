namespace Cities.Gameplay.Controls
{
    /// <summary>
    /// Eén binding van een actie, los van het Input System, zodat de catalogus en de conflictregels zonder Unity te
    /// testen zijn.
    /// </summary>
    public readonly struct BindingInfo
    {
        public BindingInfo(string map, string action, int index, string part, string defaultPath, BindingKind kind, bool actionIsButton)
        {
            Map = map;
            Action = action;
            Index = index;
            Part = part ?? string.Empty;
            DefaultPath = defaultPath ?? string.Empty;
            Kind = kind;
            ActionIsButton = actionIsButton;
        }

        public string Map { get; }

        public string Action { get; }

        /// <summary>Index in de bindingen van de actie (zoals <c>InputAction.bindings</c>).</summary>
        public int Index { get; }

        /// <summary>Naam van het deel binnen een composite (<c>up</c>, <c>negative</c>, ...); leeg voor gewone bindingen.</summary>
        public string Part { get; }

        /// <summary>Pad uit het asset, zonder overschrijving door de speler; leeg als het slot standaard vrij is.</summary>
        public string DefaultPath { get; }

        public BindingKind Kind { get; }

        /// <summary>Of de actie een knop is (en geen as of positie).</summary>
        public bool ActionIsButton { get; }

        /// <summary>Sleutel van de regel in de instellingen: <c>Map/Actie</c> of <c>Map/Actie/deel</c>.</summary>
        public string RowKey => Kind == BindingKind.CompositePart ? $"{Map}/{Action}/{Part}" : $"{Map}/{Action}";
    }

    public enum BindingKind
    {
        Simple,
        Composite,
        CompositePart,
    }
}
