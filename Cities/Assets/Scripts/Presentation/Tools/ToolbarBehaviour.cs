using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.Presentation.UI;
using Cities.World.Terrain;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Cities.Presentation.Tools
{
    /// <summary>
    /// Werkbalk onderaan met een knop per plaatsingstool en per penseel (terrein en bomen), plus een regel die zegt waarom de
    /// preview rood is. Met een penseel actief staan daarboven de penseelgrootte en -sterkte.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ToolbarBehaviour : MonoBehaviour
    {
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private PrefabPlacementBehaviour[] _placements = System.Array.Empty<PrefabPlacementBehaviour>();

        [Tooltip("Optioneel: voegt de penselen voor terrein en bomen toe.")]
        [FormerlySerializedAs("_terraform")]
        [SerializeField] private BrushToolsBehaviour _brushes;

        [Tooltip("Optioneel: voegt de wegtool toe.")]
        [SerializeField] private RoadToolBehaviour _roads;

        private readonly List<(CityButton Button, PrefabPlacementBehaviour Placement)> _buttons = new List<(CityButton, PrefabPlacementBehaviour)>();
        private readonly List<BrushButton> _brushButtons = new List<BrushButton>();
        private readonly List<(ITool Tool, VisualElement Row)> _brushSettings = new List<(ITool, VisualElement)>();
        private Label _status;
        private Label _info;
        private VisualElement _bar;
        private Slider _targetHeight;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddToClassList(UiClasses.Root);

            _bar = new VisualElement { name = "tool-bar" };
            _bar.style.position = Position.Absolute;
            _bar.style.left = 0;
            _bar.style.right = 0;
            _bar.style.bottom = 16;
            _bar.style.alignItems = Align.Center;
            _bar.pickingMode = PickingMode.Ignore;

            _status = new Label { name = "tool-status" };
            _status.AddToClassList(UiClasses.Badge);
            _status.AddToClassList(UiClasses.BadgeDanger);
            _status.style.marginBottom = 8;
            _bar.Add(_status);

            _info = new Label { name = "tool-info" };
            _info.AddToClassList(UiClasses.Badge);
            _info.style.marginBottom = 8;
            _bar.Add(_info);

            var toolbar = new VisualElement();
            toolbar.AddToClassList(UiClasses.Toolbar);
            foreach (var placement in _placements)
            {
                var button = new CityButton(placement.Tool.Name);
                button.clicked += () => Toggle(placement);
                toolbar.Add(button);
                _buttons.Add((button, placement));
            }

            if (_brushes != null)
            {
                AddBrushes(toolbar);
            }

            if (_roads != null)
            {
                AddRoads(toolbar);
            }

            _bar.Add(toolbar);
            root.Add(_bar);
            _tools.Controller.ActiveChanged += OnActiveChanged;
        }

        private void OnDisable()
        {
            _tools.Controller.ActiveChanged -= OnActiveChanged;
            _bar?.RemoveFromHierarchy();
            _buttons.Clear();
            _brushButtons.Clear();
            _brushSettings.Clear();
        }

        private static readonly System.Globalization.CultureInfo Dutch = new System.Globalization.CultureInfo("nl-NL");

        private void Start() => OnActiveChanged(_tools.Controller.Active);

        private void LateUpdate()
        {
            var reason = _tools.Controller.Preview.InvalidReason;
            _status.text = reason ?? string.Empty;
            _status.style.visibility = reason != null ? Visibility.Visible : Visibility.Hidden;
            var preview = _tools.Controller.Preview;
            var info = preview.Cost > 0 ? $"{preview.Info} · € {preview.Cost.ToString("N0", Dutch)}" : preview.Info;
            _info.text = info ?? string.Empty;
            _info.style.visibility = info != null ? Visibility.Visible : Visibility.Hidden;
        }

        private void Toggle(PrefabPlacementBehaviour placement)
        {
            var controller = _tools.Controller;
            controller.Select(ReferenceEquals(controller.Active, placement.Tool) ? null : placement.Tool);
        }

        private void OnActiveChanged(ITool active)
        {
            foreach (var (button, placement) in _buttons)
            {
                button.Selected = active != null && ReferenceEquals(active, placement.Tool);
            }

            foreach (var brush in _brushButtons)
            {
                brush.Button.Selected = ReferenceEquals(active, brush.Tool) && brush.IsChosen();
            }

            foreach (var (tool, row) in _brushSettings)
            {
                row.style.display = ReferenceEquals(active, tool) ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_targetHeight != null)
            {
                _targetHeight.style.display = _brushes.Terraform.Mode == TerraformMode.SetHeight ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void AddBrushes(VisualElement toolbar)
        {
            var terraform = _brushes.Terraform;
            AddSeparator(toolbar);
            AddBrush(toolbar, "Omhoog", terraform, () => terraform.Mode = TerraformMode.Raise, () => terraform.Mode == TerraformMode.Raise);
            AddBrush(toolbar, "Omlaag", terraform, () => terraform.Mode = TerraformMode.Lower, () => terraform.Mode == TerraformMode.Lower);
            AddBrush(toolbar, "Vlak", terraform, () => terraform.Mode = TerraformMode.Flatten, () => terraform.Mode == TerraformMode.Flatten);
            AddBrush(toolbar, "Glad", terraform, () => terraform.Mode = TerraformMode.Smooth, () => terraform.Mode == TerraformMode.Smooth);
            AddBrush(toolbar, "Naar hoogte", terraform, () => terraform.Mode = TerraformMode.SetHeight, () => terraform.Mode == TerraformMode.SetHeight);

            var trees = _brushes.Trees;
            AddSeparator(toolbar);
            AddBrush(toolbar, "Bomen planten", trees, () => trees.Plant = true, () => trees.Plant);
            AddBrush(toolbar, "Bomen kappen", trees, () => trees.Plant = false, () => !trees.Plant);

            _targetHeight = BrushSlider("Hoogte (m)", TerrainBrush.MinHeight, 600f, terraform.TargetHeight, v => terraform.TargetHeight = v);
            var water = _brushes.WaterSources;
            if (water != null)
            {
                AddSeparator(toolbar);
                AddBrush(toolbar, "Waterbron", water, () => water.Drain = false, () => !water.Drain);
                AddBrush(toolbar, "Afvoer", water, () => water.Drain = true, () => water.Drain);
                AddSettings(water, BrushSlider("Debiet (m³/s)", WaterSourceTool.MinDischarge, WaterSourceTool.MaxDischarge, water.Discharge, v => water.Discharge = Mathf.Round(v)));
            }

            AddSettings(terraform,
                BrushSlider("Penseel (m)", TerraformTool.MinRadius, TerraformTool.MaxRadius, terraform.Radius, v => terraform.Radius = v),
                BrushSlider("Sterkte (m/s)", TerraformTool.MinStrength, TerraformTool.MaxStrength, terraform.Strength, v => terraform.Strength = v),
                _targetHeight);
            AddSettings(trees,
                BrushSlider("Penseel (m)", TreeTool.MinRadius, TreeTool.MaxRadius, trees.Radius, v => trees.Radius = v),
                BrushSlider("Sterkte (bos/s)", TreeTool.MinStrength, TreeTool.MaxStrength, trees.Strength, v => trees.Strength = v));
        }

        private void AddRoads(VisualElement toolbar)
        {
            var road = _roads.Tool;
            AddSeparator(toolbar);
            AddBrush(toolbar, "Weg recht", road, () => road.Mode = RoadDrawMode.Straight, () => road.Mode == RoadDrawMode.Straight);
            AddBrush(toolbar, "Weg bocht", road, () => road.Mode = RoadDrawMode.Curve, () => road.Mode == RoadDrawMode.Curve);
            AddBrush(toolbar, "Weg vrij", road, () => road.Mode = RoadDrawMode.Free, () => road.Mode == RoadDrawMode.Free);
            foreach (var type in _roads.Types)
            {
                var chosen = type;
                AddBrush(toolbar, chosen.DisplayName, road, () => road.Use(chosen.Id, chosen.Spec), () => road.TypeId == chosen.Id);
            }

            AddBrush(toolbar, "Slopen", _roads.Demolish, () => { }, () => true);
            AddBrush(toolbar, "Inspecteren", _roads.Inspect, () => { }, () => true);
            AddSettings(road,
                BrushSlider("Hoekstap (°)", 0f, 45f, road.AngleStep, v => road.AngleStep = Mathf.Round(v)),
                BrushSlider("Lengtestap (m)", 0f, 50f, road.LengthStep, v => road.LengthStep = Mathf.Round(v)),
                BrushSlider("Raster (m)", 0f, 32f, road.GridStep, v => road.GridStep = Mathf.Round(v)));
        }

        private static void AddSeparator(VisualElement toolbar)
        {
            var separator = new VisualElement();
            separator.AddToClassList(UiClasses.ToolbarSeparator);
            toolbar.Add(separator);
        }

        private void AddBrush(VisualElement toolbar, string label, ITool tool, System.Action choose, System.Func<bool> isChosen)
        {
            var brush = new BrushButton(new CityButton(label), tool, choose, isChosen);
            brush.Button.clicked += () => ToggleBrush(brush);
            toolbar.Add(brush.Button);
            _brushButtons.Add(brush);
        }

        private void AddSettings(ITool tool, params Slider[] sliders)
        {
            var row = new VisualElement { name = "brush-settings" };
            row.AddToClassList(UiClasses.Toolbar);
            row.style.marginBottom = 8;
            foreach (var slider in sliders)
            {
                row.Add(slider);
            }

            _bar.Insert(_bar.IndexOf(_status) + 1, row);
            _brushSettings.Add((tool, row));
        }

        private static Slider BrushSlider(string label, float min, float max, float value, System.Action<float> changed)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true };
            slider.style.width = 300;
            slider.style.marginLeft = 8;
            slider.style.marginRight = 8;
            slider.RegisterValueChangedCallback(e => changed(e.newValue));
            return slider;
        }

        private void ToggleBrush(BrushButton brush)
        {
            var controller = _tools.Controller;
            if (brush.IsChosen() && ReferenceEquals(controller.Active, brush.Tool))
            {
                controller.Deselect();
                return;
            }

            brush.Choose();
            controller.Select(brush.Tool);
            OnActiveChanged(controller.Active);
        }

        /// <summary>Knop voor een penseel in een bepaalde stand.</summary>
        private sealed class BrushButton
        {
            public BrushButton(CityButton button, ITool tool, System.Action choose, System.Func<bool> isChosen)
            {
                Button = button;
                Tool = tool;
                Choose = choose;
                IsChosen = isChosen;
            }

            public CityButton Button { get; }

            public ITool Tool { get; }

            public System.Action Choose { get; }

            public System.Func<bool> IsChosen { get; }
        }
    }
}
