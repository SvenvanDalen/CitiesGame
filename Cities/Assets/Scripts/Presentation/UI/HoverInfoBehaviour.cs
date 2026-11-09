using Cities.Gameplay.Tools;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cities.Presentation.UI
{
    /// <summary>
    /// Infokaart bij de cursor (E27-10): wijs een gebouw, weg of burger aan en er staat een korte kaart met naam, bezetting, status en
    /// problemen. Een klik zonder actief gereedschap zet de kaart vast; ze blijft live en blijft bruikbaar tijdens het spelen tot je op
    /// leeg terrein klikt of Escape drukt.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HoverInfoBehaviour : MonoBehaviour
    {
        private const float QueryInterval = 0.1f;
        private const float CardOffset = 18f;

        private ToolsBehaviour _tools;
        private RoadToolBehaviour _roads;
        private Label _card;
        private float _nextQuery;
        private Vector2 _lastPointer;
        private Vector2 _down;
        private float _downTime;
        private float3? _pinned;

        /// <summary>Koppelt de kaart aan de gereedschappen; wordt door <see cref="HoverInfoBootstrap"/> aangeroepen.</summary>
        public void Setup(ToolsBehaviour tools, RoadToolBehaviour roads)
        {
            _tools = tools;
            _roads = roads;
        }

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddToClassList(UiClasses.Root);
            _card = new Label { name = "hover-card", pickingMode = PickingMode.Ignore };
            _card.AddToClassList(UiClasses.Tooltip);
            _card.style.position = Position.Absolute;
            _card.style.display = DisplayStyle.None;
            _card.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_card);
        }

        private void OnDisable() => _card?.RemoveFromHierarchy();

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || _tools == null || _roads == null || _roads.Inspect == null)
            {
                return;
            }

            var pointer = mouse.position.ReadValue();
            HandleClicks(mouse, pointer);
            var active = _tools.Controller.Active;
            if (active != null && !ReferenceEquals(active, _roads.Inspect))
            {
                _pinned = null;
                Hide();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                _pinned = null;
            }

            if (Time.unscaledTime < _nextQuery && (pointer - _lastPointer).sqrMagnitude < 1f)
            {
                return;
            }

            _nextQuery = Time.unscaledTime + QueryInterval;
            _lastPointer = pointer;
            var point = _pinned ?? GroundUnderPointer();
            if (point.HasValue && _roads.Inspect.Query(point.Value, out var report))
            {
                Show(report, pointer);
            }
            else
            {
                Hide();
            }
        }

        private float3? GroundUnderPointer()
        {
            var pick = _tools.Picking.Current;
            return pick.HasGround ? pick.Ground.Point : (float3?)null;
        }

        private void HandleClicks(Mouse mouse, Vector2 pointer)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                _down = pointer;
                _downTime = Time.unscaledTime;
            }

            if (!mouse.leftButton.wasReleasedThisFrame || _tools.Controller.Active != null || !ClickDetector.IsClick(_down, _downTime, pointer, Time.unscaledTime))
            {
                return;
            }

            var ground = GroundUnderPointer();
            _pinned = ground.HasValue && _roads.Inspect.Query(ground.Value, out _) ? ground : null;
        }

        private void Show(InspectionReport report, Vector2 pointer)
        {
            _card.text = HoverCardText.Format(report);
            var panelPosition = RuntimePanelUtils.ScreenToPanel(_card.panel, new Vector2(pointer.x, Screen.height - pointer.y));
            _card.style.left = panelPosition.x + CardOffset;
            _card.style.top = panelPosition.y + CardOffset;
            _card.style.display = DisplayStyle.Flex;
        }

        private void Hide() => _card.style.display = DisplayStyle.None;
    }
}
