using System.Collections.Generic;
using Cities.Simulation.Network;
using Cities.Gameplay.Tools;
using Cities.Gameplay.Zoning;
using Cities.Presentation.UI;
using Cities.World.Grid;
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

        [Tooltip("Optioneel: voegt de zonetool toe.")]
        [SerializeField] private ZoneToolBehaviour _zones;

        private readonly List<(CityButton Button, PrefabPlacementBehaviour Placement)> _buttons = new List<(CityButton, PrefabPlacementBehaviour)>();
        private readonly List<BrushButton> _brushButtons = new List<BrushButton>();
        private readonly List<(ITool Tool, VisualElement Row)> _brushSettings = new List<(ITool, VisualElement)>();
        private Label _status;
        private Label _info;
        private VisualElement _bar;
        private Slider _targetHeight;
        private VisualElement _help;
        private readonly ToolbarModel _model = new ToolbarModel();
        private readonly Dictionary<ToolbarTab, VisualElement> _panels = new Dictionary<ToolbarTab, VisualElement>();
        private readonly Dictionary<ToolbarTab, CityButton> _tabButtons = new Dictionary<ToolbarTab, CityButton>();

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

            foreach (var tab in ToolbarModel.Tabs)
            {
                var panel = new VisualElement { name = "tool-panel-" + tab };
                panel.AddToClassList(UiClasses.Toolbar);
                panel.style.flexWrap = Wrap.Wrap;
                panel.style.justifyContent = Justify.Center;
                panel.style.maxWidth = Length.Percent(90);
                panel.style.marginBottom = 8;
                panel.style.display = DisplayStyle.None;
                _panels[tab] = panel;
                _bar.Add(panel);
            }

            foreach (var placement in _placements)
            {
                var cost = placement.Tool.Cost;
                var button = new CityButton(cost > 0 ? $"{placement.Tool.Name}\n€ {ToolbarHotkeys.ShortMoney(cost)}" : placement.Tool.Name);
                button.clicked += () => Toggle(placement);
                Panel(ToolbarCatalog.TabFor(placement.Tool.Name)).Add(button);
                _buttons.Add((button, placement));
            }

            if (_brushes != null)
            {
                AddBrushes(Panel(ToolbarTab.Terrain));
            }

            if (_roads != null)
            {
                AddRoads(Panel(ToolbarTab.Roads));
            }

            if (_zones != null)
            {
                AddZones(Panel(ToolbarTab.Zones));
            }

            var tabs = new VisualElement { name = "tool-tabs" };
            tabs.AddToClassList(UiClasses.Toolbar);
            foreach (var tab in ToolbarModel.Tabs)
            {
                if (_panels[tab].childCount == 0)
                {
                    continue;
                }

                var chosen = tab;
                var tabKey = ToolbarHotkeys.KeyOf(tab).ToString();
                var tabButton = new CityButton($"{ToolbarModel.LabelOf(tab)} ({tabKey})") { tooltip = ToolbarHotkeys.Tooltip(ToolbarModel.LabelOf(tab), tabKey, 0) };
                tabButton.clicked += () => _model.ToggleTab(chosen);
                tabs.Add(tabButton);
                _tabButtons[tab] = tabButton;
            }

            _bar.Add(tabs);
            _help = new VisualElement { name = "help" };
            _help.AddToClassList(UiClasses.Toolbar);
            _help.style.position = Position.Absolute;
            _help.style.alignSelf = Align.Center;
            _help.style.bottom = 120;
            _help.style.flexDirection = FlexDirection.Column;
            _help.style.alignItems = Align.FlexStart;
            _help.style.paddingLeft = _help.style.paddingRight = _help.style.paddingTop = _help.style.paddingBottom = 12;
            _help.style.display = DisplayStyle.None;
            foreach (var line in HelpContent.Lines())
            {
                _help.Add(new Label(line));
            }

            root.Add(_help);
            AddTooltips();
            _model.Changed += RefreshTabs;
            root.Add(_bar);
            _tools.Controller.ActiveChanged += OnActiveChanged;
        }

        private void OnDisable()
        {
            _tools.Controller.ActiveChanged -= OnActiveChanged;
            _model.Changed -= RefreshTabs;
            _bar?.RemoveFromHierarchy();
            _help?.RemoveFromHierarchy();
            _panels.Clear();
            _tabButtons.Clear();
            _buttons.Clear();
            _brushButtons.Clear();
            _brushSettings.Clear();
        }

        private static readonly System.Globalization.CultureInfo Dutch = new System.Globalization.CultureInfo("nl-NL");

        private void Start() => OnActiveChanged(_tools.Controller.Active);

        private VisualElement Panel(ToolbarTab tab) => _panels[tab];

        /// <summary>Tooltip met naam, cijfertoets en kosten op elke knop in een subpaneel.</summary>
        private void AddTooltips()
        {
            foreach (var panel in _panels.Values)
            {
                var index = 0;
                foreach (var child in panel.Children())
                {
                    if (!(child is CityButton button))
                    {
                        continue;
                    }

                    index++;
                    var cost = 0L;
                    foreach (var (placementButton, placement) in _buttons)
                    {
                        if (ReferenceEquals(placementButton, button))
                        {
                            cost = placement.Tool.Cost;
                        }
                    }

                    button.tooltip = ToolbarHotkeys.Tooltip(button.text.Split('\n')[0], index <= ToolbarHotkeys.MaxEntries ? index.ToString() : null, cost);
                }
            }
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || keyboard.ctrlKey.isPressed || keyboard.altKey.isPressed)
            {
                return;
            }

            if (keyboard.f1Key.wasPressedThisFrame)
            {
                _help.style.display = _help.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            }

            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame)
                {
                    continue;
                }

                var tab = ToolbarHotkeys.TabFor(key.keyCode);
                if (tab.HasValue && _tabButtons.ContainsKey(tab.Value))
                {
                    _model.ToggleTab(tab.Value);
                    return;
                }

                PressEntry(ToolbarHotkeys.EntryIndex(key.keyCode));
            }
        }

        /// <summary>Kiest het gereedschap met nummer <paramref name="index"/> (vanaf 0) in het open paneel.</summary>
        private void PressEntry(int index)
        {
            if (index < 0 || !_model.OpenTab.HasValue)
            {
                return;
            }

            var n = 0;
            foreach (var child in _panels[_model.OpenTab.Value].Children())
            {
                if (child is CityButton button && n++ == index)
                {
                    using (var click = NavigationSubmitEvent.GetPooled())
                    {
                        click.target = button;
                        button.SendEvent(click);
                    }

                    return;
                }
            }
        }

        /// <summary>Toont alleen het subpaneel van de open tab en markeert die tab.</summary>
        private void RefreshTabs()
        {
            foreach (var (tab, panel) in _panels)
            {
                var open = _model.OpenTab == tab;
                panel.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
                if (_tabButtons.TryGetValue(tab, out var button))
                {
                    button.Selected = open;
                }
            }
        }

        private void LateUpdate()
        {
            var afford = _tools.Controller.CanAfford;
            foreach (var (button, placement) in _buttons)
            {
                var cost = placement.Tool.Cost;
                button.EnableInClassList(UiClasses.ButtonDisabled, afford != null && cost > 0 && !afford(cost));
            }

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

            AddBrush(toolbar, "Upgraden", _roads.Upgrade, () => { }, () => true);
            AddBrush(toolbar, "Omkeren", _roads.Reverse, () => { }, () => true);
            AddBrush(toolbar, "Splitsen", _roads.Split, () => { }, () => true);
            AddBrush(toolbar, "Samenvoegen", _roads.Merge, () => { }, () => true);
            foreach (var preset in InterchangeGeometry.Presets)
            {
                var chosen = preset;
                AddBrush(toolbar, chosen.Name, _roads.Interchange, () => _roads.Interchange.Kind = chosen.Kind, () => _roads.Interchange.Kind == chosen.Kind);
            }

            AddBrush(toolbar, "Slopen", _roads.Demolish, () => { }, () => true);
            AddBrush(toolbar, "Inspecteren", _roads.Inspect, () => { }, () => true);
            AddSettings(road,
                BrushSlider("Hoekstap (°)", 0f, 45f, road.AngleStep, v => road.AngleStep = Mathf.Round(v)),
                BrushSlider("Lengtestap (m)", 0f, 50f, road.LengthStep, v => road.LengthStep = Mathf.Round(v)),
                BrushSlider("Raster (m)", 0f, 32f, road.GridStep, v => road.GridStep = Mathf.Round(v)));
        }

        private void AddZones(VisualElement toolbar)
        {
            var zone = _zones.Tool;
            AddSeparator(toolbar);
            AddBrush(toolbar, "Zone kwast", zone, () => zone.Mode = ZoneMode.Brush, () => zone.Mode == ZoneMode.Brush);
            AddBrush(toolbar, "Zone vlak", zone, () => zone.Mode = ZoneMode.Rectangle, () => zone.Mode == ZoneMode.Rectangle);
            AddBrush(toolbar, "Zone vullen", zone, () => zone.Mode = ZoneMode.Fill, () => zone.Mode == ZoneMode.Fill);
            AddBrush(toolbar, "Wonen", zone, () => Zone(zone, ZoneType.Residential), () => !zone.Erase && zone.Type == ZoneType.Residential);
            AddBrush(toolbar, "Handel", zone, () => Zone(zone, ZoneType.Commercial), () => !zone.Erase && zone.Type == ZoneType.Commercial);
            AddBrush(toolbar, "Industrie", zone, () => Zone(zone, ZoneType.Industrial), () => !zone.Erase && zone.Type == ZoneType.Industrial);
            AddBrush(toolbar, "Kantoor", zone, () => Zone(zone, ZoneType.Office), () => !zone.Erase && zone.Type == ZoneType.Office);
            AddBrush(toolbar, "Laag", zone, () => zone.Density = ZoneDensity.Low, () => zone.Density == ZoneDensity.Low);
            AddBrush(toolbar, "Hoog", zone, () => zone.Density = ZoneDensity.High, () => zone.Density == ZoneDensity.High);
            AddBrush(toolbar, "Zone wissen", zone, () => zone.Erase = true, () => zone.Erase);
            AddSettings(zone, BrushSlider("Kwast (m)", ZoneTool.MinRadius, ZoneTool.MaxRadius, zone.Radius, v => zone.Radius = v));
        }

        private static void Zone(ZoneTool zone, ZoneType type)
        {
            zone.Erase = false;
            zone.Type = type;
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
