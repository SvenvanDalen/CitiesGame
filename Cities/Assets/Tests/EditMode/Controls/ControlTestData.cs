using System.Collections.Generic;
using Cities.Gameplay.Controls;

namespace Cities.Tests.EditMode.Controls
{
    /// <summary>Bindingen met dezelfde vorm als <c>CityControls.inputactions</c>, zonder Input System.</summary>
    internal static class ControlTestData
    {
        public static List<BindingInfo> Bindings() => new List<BindingInfo>
        {
            new BindingInfo("Camera", "Move", 0, null, "2DVector", BindingKind.Composite, false),
            new BindingInfo("Camera", "Move", 1, "up", "<Keyboard>/w", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Move", 2, "down", "<Keyboard>/s", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Move", 3, "left", "<Keyboard>/a", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Move", 4, "right", "<Keyboard>/d", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Move", 5, null, "2DVector", BindingKind.Composite, false),
            new BindingInfo("Camera", "Move", 6, "up", "<Keyboard>/upArrow", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Move", 7, "down", "<Keyboard>/downArrow", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Move", 8, "left", "<Keyboard>/leftArrow", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Move", 9, "right", "<Keyboard>/rightArrow", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Rotate", 0, null, "1DAxis", BindingKind.Composite, false),
            new BindingInfo("Camera", "Rotate", 1, "negative", "<Keyboard>/q", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Rotate", 2, "positive", "<Keyboard>/e", BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Rotate", 3, null, "1DAxis", BindingKind.Composite, false),
            new BindingInfo("Camera", "Rotate", 4, "negative", string.Empty, BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Rotate", 5, "positive", string.Empty, BindingKind.CompositePart, false),
            new BindingInfo("Camera", "Scroll", 0, null, "<Mouse>/scroll/y", BindingKind.Simple, false),
            new BindingInfo("Camera", "Pointer", 0, null, "<Mouse>/position", BindingKind.Simple, false),
            new BindingInfo("Camera", "OrbitDrag", 0, null, "<Mouse>/rightButton", BindingKind.Simple, true),
            new BindingInfo("Camera", "OrbitDrag", 1, null, string.Empty, BindingKind.Simple, true),
            new BindingInfo("Game", "Pause", 0, null, "<Keyboard>/space", BindingKind.Simple, true),
            new BindingInfo("Game", "Pause", 1, null, "<Keyboard>/pause", BindingKind.Simple, true),
            new BindingInfo("Game", "Menu", 0, null, "<Keyboard>/escape", BindingKind.Simple, true),
            new BindingInfo("Game", "Menu", 1, null, "<Keyboard>/f10", BindingKind.Simple, true),
        };

        public static ControlRow Row(IReadOnlyList<ControlRow> rows, string key)
        {
            foreach (var row in rows)
            {
                if (row.Key == key)
                {
                    return row;
                }
            }

            return null;
        }
    }

    /// <summary>Huidige paden per binding, zoals de overschrijvingen van de speler ze maken.</summary>
    internal sealed class PathTable
    {
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>();

        public string Get(BindingInfo binding) =>
            _overrides.TryGetValue(KeyOf(binding), out var path) ? path : binding.DefaultPath;

        public void Set(BindingInfo binding, string path) => _overrides[KeyOf(binding)] = path;

        public void Apply(IEnumerable<BindingChange> changes)
        {
            foreach (var change in changes)
            {
                Set(change.Binding, change.Path);
            }
        }

        private static string KeyOf(BindingInfo binding) => $"{binding.Map}/{binding.Action}/{binding.Index}";
    }
}
