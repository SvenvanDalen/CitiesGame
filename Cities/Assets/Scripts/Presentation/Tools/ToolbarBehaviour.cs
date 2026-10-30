using System.Collections.Generic;
using Cities.Gameplay.Tools;
using Cities.Presentation.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Cities.Presentation.Tools
{
    /// <summary>
    /// Werkbalk onderaan met een knop per plaatsingstool, plus een regel die zegt waarom de preview rood is.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class ToolbarBehaviour : MonoBehaviour
    {
        [SerializeField] private ToolsBehaviour _tools;
        [SerializeField] private PrefabPlacementBehaviour[] _placements = System.Array.Empty<PrefabPlacementBehaviour>();

        private readonly List<(CityButton Button, PrefabPlacementBehaviour Placement)> _buttons = new List<(CityButton, PrefabPlacementBehaviour)>();
        private Label _status;
        private VisualElement _bar;

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

            _bar.Add(toolbar);
            root.Add(_bar);
            _tools.Controller.ActiveChanged += OnActiveChanged;
        }

        private void OnDisable()
        {
            _tools.Controller.ActiveChanged -= OnActiveChanged;
            _bar?.RemoveFromHierarchy();
            _buttons.Clear();
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
        }
    }
}
