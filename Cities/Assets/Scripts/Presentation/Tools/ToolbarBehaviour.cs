using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.Presentation.UI;
using Cities.World.Terrain;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.Tools
{
    /// <summary>
    /// Werkbalk onderaan met een knop per plaatsingstool en per terreinpenseel, plus een regel die zegt waarom de
    /// preview rood is. Met een penseel actief staan daarboven de penseelgrootte en -sterkte.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ToolbarBehaviour : MonoBehaviour
    {
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private PrefabPlacementBehaviour[] _placements = System.Array.Empty<PrefabPlacementBehaviour>();

        [Tooltip("Optioneel: voegt de terreinpenselen toe.")]
        [SerializeField] private TerraformToolBehaviour _terraform;

        private static readonly (TerraformMode Mode, string Label)[] BrushModes =
        {
            (TerraformMode.Raise, "Omhoog"),
            (TerraformMode.Lower, "Omlaag"),
            (TerraformMode.Flatten, "Vlak"),
            (TerraformMode.Smooth, "Glad"),
            (TerraformMode.SetHeight, "Naar hoogte"),
        };

        private readonly List<(CityButton Button, PrefabPlacementBehaviour Placement)> _buttons = new List<(CityButton, PrefabPlacementBehaviour)>();
        private readonly List<(CityButton Button, TerraformMode Mode)> _brushButtons = new List<(CityButton, TerraformMode)>();
        private Label _status;
        private VisualElement _bar;
        private VisualElement _brushSettings;
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

            var toolbar = new VisualElement();
            toolbar.AddToClassList(UiClasses.Toolbar);
            foreach (var placement in _placements)
            {
                var button = new CityButton(placement.Tool.Name);
                button.clicked += () => Toggle(placement);
                toolbar.Add(button);
                _buttons.Add((button, placement));
            }

            if (_terraform != null)
            {
                AddBrushes(toolbar);
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
        }

        private void Start() => OnActiveChanged(_tools.Controller.Active);

        private void LateUpdate()
        {
            var reason = _tools.Controller.Preview.InvalidReason;
            _status.text = reason ?? string.Empty;
            _status.style.visibility = reason != null ? Visibility.Visible : Visibility.Hidden;
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

            var brush = _terraform != null ? _terraform.Tool : null;
            if (brush == null)
            {
                return;
            }

            var brushActive = ReferenceEquals(active, brush);
            foreach (var (button, mode) in _brushButtons)
            {
                button.Selected = brushActive && brush.Mode == mode;
            }

            _brushSettings.style.display = brushActive ? DisplayStyle.Flex : DisplayStyle.None;
            _targetHeight.style.display = brush.Mode == TerraformMode.SetHeight ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void AddBrushes(VisualElement toolbar)
        {
            var separator = new VisualElement();
            separator.AddToClassList(UiClasses.ToolbarSeparator);
            toolbar.Add(separator);
            foreach (var (mode, label) in BrushModes)
            {
                var button = new CityButton(label);
                button.clicked += () => ToggleBrush(mode);
                toolbar.Add(button);
                _brushButtons.Add((button, mode));
            }

            var brush = _terraform.Tool;
            _brushSettings = new VisualElement { name = "brush-settings" };
            _brushSettings.AddToClassList(UiClasses.Toolbar);
            _brushSettings.style.marginBottom = 8;
            _brushSettings.Add(BrushSlider("Penseel (m)", TerraformTool.MinRadius, TerraformTool.MaxRadius, brush.Radius, v => brush.Radius = v));
            _brushSettings.Add(BrushSlider("Sterkte (m/s)", TerraformTool.MinStrength, TerraformTool.MaxStrength, brush.Strength, v => brush.Strength = v));
            _targetHeight = BrushSlider("Hoogte (m)", TerrainBrush.MinHeight, 600f, brush.TargetHeight, v => brush.TargetHeight = v);
            _brushSettings.Add(_targetHeight);
            _bar.Insert(_bar.IndexOf(_status) + 1, _brushSettings);
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

        private void ToggleBrush(TerraformMode mode)
        {
            var controller = _tools.Controller;
            var brush = _terraform.Tool;
            if (brush.Mode == mode && ReferenceEquals(controller.Active, brush))
            {
                controller.Deselect();
                return;
            }

            brush.Mode = mode;
            controller.Select(brush);
            OnActiveChanged(controller.Active);
        }
    }
}
